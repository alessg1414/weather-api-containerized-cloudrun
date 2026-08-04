# Deployment Guide — AWS ECS Fargate

Technical guide for deploying **weather-api-containerized-serverless** to
AWS using ECS Fargate, with SQL Server running as a sidecar container and
CI/CD via GitHub Actions. Written so that anyone with access to an AWS
account can reproduce the full deployment from scratch.

---

## 1. Architecture

```
                         GitHub Actions (OIDC, no access keys)
                                    │
                         build + push image ──▶ Amazon ECR
                                    │
                                    ▼
                    ECS Fargate Service (weather-api-cluster)
                                    │
                    ┌───────────────┴───────────────┐
                    │      Task (1 shared ENI)        │
                    │  ┌─────────┐      ┌──────────┐ │
   Internet ───▶ ALB │  │  api    │◀────▶│   db     │ │
   (port 80)         │  │ :8080   │      │ SQL 2022 │ │
                    │  └─────────┘      └──────────┘ │
                    └────────────────────────────────┘
```

**Key design decisions:**

- **SQL Server runs as a sidecar inside the same task**, not as a managed
  RDS/Cloud SQL instance. Both containers share network and talk over
  `localhost`. This removes the cost of a managed database, at the expense
  of the data **not persisting** across task restarts — the schema and
  seed data are automatically recreated on every startup via
  `EnsureCreated()` + `HasData()` in Entity Framework Core.
- **Fargate does not scale to zero.** Unlike Cloud Run, if the service has
  `desiredCount ≥ 1`, it's billed continuously whether or not there's
  traffic. See section 9 for the on-demand start/stop strategy.
- **GitHub Actions authenticates via OIDC**, not long-lived access keys —
  the pipeline obtains temporary credentials on every run.

---

## 2. Prerequisites

- An AWS account with administrative permissions for the initial setup
  (use an IAM user for day-to-day credentials, **never the root user**).
- AWS CLI v2 installed and configured (`aws configure`), with
  `Default output format: json`.
- Docker installed and running (Docker Desktop on Windows/Mac, or the
  native daemon on Linux).
- A GitHub account with the repository already cloned locally.

Confirm your credentials are active before continuing:

```bash
aws sts get-caller-identity
```

Note the `Account` it returns — every command in this guide uses
`572059938970` and the `us-east-1` region as an example; substitute your
own if reproducing this in a different account.

---

## 3. Amazon ECR Repository

```bash
aws ecr create-repository --repository-name weather-api --region us-east-1
```

> The SQL Server image **does not need to be mirrored** into your own
> repository — ECS Fargate supports pulling directly from
> `mcr.microsoft.com`. `task-definition.json` references
> `mcr.microsoft.com/mssql/server:2022-latest` with no intermediate steps.

---

## 4. Secrets Manager

Four secrets, each with a single responsibility:

```bash
aws secretsmanager create-secret --name weather-api/mssql-sa-password \
  --secret-string "YourSecurePassword123!" --region us-east-1

aws secretsmanager create-secret --name weather-api/connection-string \
  --secret-string "Server=localhost,1433;Database=DataVisionDB;User Id=sa;Password=YourSecurePassword123!;TrustServerCertificate=true;MultipleActiveResultSets=true" \
  --region us-east-1

aws secretsmanager create-secret --name weather-api/jwt-secret \
  --secret-string "a-secret-key-at-least-32-characters-long" --region us-east-1

aws secretsmanager create-secret --name weather-api/owm-api-key \
  --secret-string "your-openweathermap-api-key" --region us-east-1
```

> **Important design constraint**: `weather-api/mssql-sa-password` and
> `weather-api/connection-string` hold the **same password in two separate
> places**. .NET does not compose a connection string from separate
> environment variables — the configuration system only overwrites whole
> keys, it doesn't concatenate fragments. This means **if you change the
> SQL Server password, you must update both secrets manually**, at the
> same time. A legitimate future improvement is to move that composition
> into `Program.cs` in code, removing the duplication at its root.

**Password complexity requirements**: at least 8 characters, including
uppercase, lowercase, a number, and a symbol — SQL Server refuses to start
if this isn't met, with a login error for `sa` that doesn't mention the
real cause.

### Getting the full ARNs (with suffix)

Every secret gets a random 6-character suffix in its real ARN, which
**does not match** what you typed when creating it. This suffix is
required in `task-definition.json` — an ARN without it can fail resolution
inconsistently depending on the exact call context.

```bash
aws secretsmanager describe-secret --secret-id weather-api/mssql-sa-password --query ARN --output text
aws secretsmanager describe-secret --secret-id weather-api/connection-string --query ARN --output text
aws secretsmanager describe-secret --secret-id weather-api/jwt-secret --query ARN --output text
aws secretsmanager describe-secret --secret-id weather-api/owm-api-key --query ARN --output text
```

Save all 4 full ARNs — they're used in step 6.

---

## 5. IAM Roles

### 5.1 Execution Role

Used by ECS to start the containers: pulling images, reading secrets,
writing logs.

```bash
aws iam create-role --role-name weather-api-ecs-execution-role \
  --assume-role-policy-document file://.aws/ecs-tasks-trust-policy.json

aws iam put-role-policy --role-name weather-api-ecs-execution-role \
  --policy-name weather-api-execution-policy \
  --policy-document file://.aws/ecs-execution-role-policy.json
```

The policy must include **all three logs permissions**, not just two —
`logs:CreateLogGroup` is required because the task definition uses
`awslogs-create-group: true`:

```json
"Action": ["logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents"]
```

And the secrets permission must use a trailing wildcard on each ARN
(without the specific suffix), to cover the secret regardless of its exact
suffix:

```json
"Resource": [
  "arn:aws:secretsmanager:us-east-1:<ACCOUNT_ID>:secret:weather-api/mssql-sa-password*",
  "arn:aws:secretsmanager:us-east-1:<ACCOUNT_ID>:secret:weather-api/connection-string*",
  "arn:aws:secretsmanager:us-east-1:<ACCOUNT_ID>:secret:weather-api/jwt-secret*",
  "arn:aws:secretsmanager:us-east-1:<ACCOUNT_ID>:secret:weather-api/owm-api-key*"
]
```

### 5.2 Task Role

Permissions the *application code* would have at runtime. This API never
calls any AWS service from its code, so the role is created without any
additional policy:

```bash
aws iam create-role --role-name weather-api-ecs-task-role \
  --assume-role-policy-document file://.aws/ecs-tasks-trust-policy.json
```

### 5.3 GitHub Actions Role (OIDC)

Lets the CI/CD pipeline obtain temporary credentials without storing
access keys.

```bash
# Once per AWS account — if it already exists, this step can be skipped
aws iam create-open-id-connect-provider \
  --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com \
  --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1

aws iam create-role --role-name weather-api-github-actions-role \
  --assume-role-policy-document file://.aws/github-oidc-trust-policy.json

aws iam put-role-policy --role-name weather-api-github-actions-role \
  --policy-name weather-api-deploy-policy \
  --policy-document file://.aws/github-actions-deploy-policy.json
```

The `sub` inside `github-oidc-trust-policy.json` must point exactly to the
repository and branch that will trigger the pipeline:

```
repo:<github-username>/weather-api-containerized-serverless:ref:refs/heads/main
```

Copy this role's ARN — it's used in step 8:

```bash
aws iam get-role --role-name weather-api-github-actions-role --query "Role.Arn" --output text
```

---

## 6. Networking: VPC, Security Group, Load Balancer

Uses the account's default VPC.

```bash
VPC_ID=$(aws ec2 describe-vpcs --filters Name=isDefault,Values=true --query "Vpcs[0].VpcId" --output text)
SUBNET_IDS=$(aws ec2 describe-subnets --filters Name=vpc-id,Values=$VPC_ID --query "Subnets[].SubnetId" --output text)

aws ec2 create-security-group --group-name weather-api-sg \
  --description "weather-api ECS tasks" --vpc-id $VPC_ID

SG_ID=$(aws ec2 describe-security-groups --filters Name=group-name,Values=weather-api-sg --query "SecurityGroups[0].GroupId" --output text)
```

**Two ingress rules are required — not one.** It's easy to create only the
first and end up with an `unhealthy` Target Group with no obvious cause:

```bash
# 1. Internal traffic between the ALB and the tasks (within the same SG)
aws ec2 authorize-security-group-ingress --group-id $SG_ID \
  --protocol tcp --port 8080 --source-group $SG_ID

# 2. Public inbound traffic to the ALB — without this, the browser never
#    even reaches the Load Balancer
aws ec2 authorize-security-group-ingress --group-id $SG_ID \
  --protocol tcp --port 80 --cidr 0.0.0.0/0
```

### Load Balancer and Target Group

```bash
aws elbv2 create-load-balancer --name weather-api-alb \
  --subnets $SUBNET_IDS --security-groups $SG_ID --type application

ALB_ARN=$(aws elbv2 describe-load-balancers --names weather-api-alb --query "LoadBalancers[0].LoadBalancerArn" --output text)

aws elbv2 create-target-group --name weather-api-tg \
  --protocol HTTP --port 8080 --vpc-id $VPC_ID --target-type ip \
  --health-check-path //swagger/v1/swagger.json

TG_ARN=$(aws elbv2 describe-target-groups --names weather-api-tg --query "TargetGroups[0].TargetGroupArn" --output text)

aws elbv2 create-listener --load-balancer-arn $ALB_ARN \
  --protocol HTTP --port 80 \
  --default-actions Type=forward,TargetGroupArn=$TG_ARN
```

> **Note for Git Bash on Windows**: MSYS reinterprets any argument that
> starts with `/` as a Windows path. The double slash (`//swagger/...`)
> avoids that conversion. Not needed on PowerShell or WSL.

---

## 7. Cluster and Task Definition

```bash
aws ecs create-cluster --cluster-name weather-api-cluster
```

Edit `.aws/task-definition.json`, replacing `<ACCOUNT_ID>` with your real
account ID across the 4 secret paths and the 2 role ARNs (or use the full
ARNs with suffix obtained in step 4). Then:

```bash
aws ecs register-task-definition --cli-input-json file://.aws/task-definition.json
```

**Task definition fields that are not optional:**

| Field | Why |
|---|---|
| `dependsOn: [{"containerName": "db", "condition": "HEALTHY"}]` on `api` | Without this, `api` tries to connect before SQL Server is ready to accept connections |
| `healthCheck` on `db` using `sqlcmd` | This is what ECS uses to know when `db` is actually `HEALTHY`, not just "the process started" |
| `MSSQL_PID: Developer` | Without this, SQL Server falls back to Evaluation edition, which stops working after 180 days |
| `awslogs-create-group: true` | Lets ECS auto-create the log group — requires the matching IAM permission (section 5.1) |

---

## 8. ECS Service

```bash
aws ecs create-service \
  --cluster weather-api-cluster \
  --service-name weather-api-service \
  --task-definition weather-api \
  --desired-count 1 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[$(echo $SUBNET_IDS | tr ' ' ',')],securityGroups=[$SG_ID],assignPublicIp=ENABLED}" \
  --load-balancers "targetGroupArn=$TG_ARN,containerName=api,containerPort=8080"
```

`assignPublicIp=ENABLED` is required even though user traffic goes through
the ALB: the default VPC has no NAT Gateway, and the task needs its own
outbound internet access to pull images and read secrets.

---

## 9. CI/CD with GitHub Actions

In the repository's **Settings → Secrets and variables → Actions**, create:

| Secret | Value |
|---|---|
| `AWS_GHA_ROLE_ARN` | ARN of the role obtained in step 5.3 |

The workflow (`.github/workflows/deploy.yml`) triggers on every push to
`main`, or manually via `workflow_dispatch`. It builds the image, pushes
to ECR, and updates the ECS service — without touching the `db` container,
which keeps its fixed tag (`2022-latest`).

### Manual build and push (if the pipeline is unavailable)

```bash
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com

docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/weather-api:latest .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/weather-api:latest

aws ecs update-service --cluster weather-api-cluster \
  --service weather-api-service --force-new-deployment
```

---

## 10. Verification

```bash
aws elbv2 describe-target-health --target-group-arn $TG_ARN \
  --query "TargetHealthDescriptions[].{Target:Target.Id,Health:TargetHealth.State}"
```

Once `healthy` is confirmed:

```bash
aws elbv2 describe-load-balancers --names weather-api-alb --query "LoadBalancers[0].DNSName" --output text
```

Open that URL with `http://` (no TLS is configured on the ALB). Swagger UI
should load at the root. Test `POST /api/Auth/login` with
`admin` / `admin123` to confirm the full flow (ALB → API → SQL Server → JWT).

---

## 11. Costs and shutdown

Fargate **does not scale to zero** — if `desiredCount ≥ 1`, it's billed
continuously. To avoid leaving anything running between demos:

```bash
# Shut down (nothing is deleted, only compute is stopped)
aws ecs update-service --cluster weather-api-cluster \
  --service weather-api-service --desired-count 0

# Start it back up when needed
aws ecs update-service --cluster weather-api-cluster \
  --service weather-api-service --desired-count 1
```

Starting from `desiredCount 0` takes 1-2 minutes (pulling both images +
SQL Server initialization). The rest of the infrastructure (ECR, IAM,
Secrets Manager, ALB) doesn't generate meaningful cost while idle.

To tear everything down completely:

```bash
aws ecs delete-service --cluster weather-api-cluster --service weather-api-service --force
aws elbv2 delete-load-balancer --load-balancer-arn $ALB_ARN
aws elbv2 delete-target-group --target-group-arn $TG_ARN
aws ecs delete-cluster --cluster weather-api-cluster
```

---

## 12. Troubleshooting — issues already identified in this project

| Symptom | Root cause | Diagnosis |
|---|---|---|
| `AccessDeniedException: secretsmanager:GetSecretValue` | Secret ARN missing its suffix, or missing the `*` wildcard in the policy | `aws iam simulate-principal-policy` with the exact ARN from the error message |
| `AccessDeniedException: logs:CreateLogGroup` | That permission is missing from the execution role | Check `aws iam get-role-policy` against the list of 3 required logs actions |
| `Login failed for user 'sa'` | Password doesn't meet complexity requirements, or is out of sync between `mssql-sa-password` and `connection-string` | `aws logs tail /ecs/weather-api` — SQL Server's message distinguishes "invalid password" from "password mismatch" |
| Target Group `unhealthy` while the task is `RUNNING` | Missing the port 80 ingress rule from `0.0.0.0/0` on the Security Group | `aws ec2 describe-security-groups` — confirm both rules from section 6 |
| A fix seems not to apply after a `force-new-deployment` | Stacked, unresolved deployments — the service is still serving from an old revision while the new one stabilizes | `aws ecs describe-services --query "services[0].deployments"` — check `rolloutState` and `taskDefinition` for each one, don't assume the most recent is already active |
| Infrastructure-level login succeeds but the application rejects it (`"Incorrect password"`) | The image in ECR predates a code fix (hashing, business logic) | Compare the date of the last `docker push` against `git log` for the relevant file |

---

## 13. Infrastructure file structure

```
.aws/
├── task-definition.json           # Definition of both containers (api + db)
├── ecs-tasks-trust-policy.json    # Shared trust policy (execution + task role)
├── ecs-execution-role-policy.json # Permissions: ECR pull, logs, secrets
├── github-oidc-trust-policy.json  # GitHub Actions role trust policy
└── github-actions-deploy-policy.json # Pipeline permissions: ECR push, ECS deploy

.github/workflows/
└── deploy.yml                      # Build + push + deploy on every push to main
```
