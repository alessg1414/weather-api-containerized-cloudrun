# Weather API — Containerized Enterprise Service

> **Cloud-Ready ASP.NET Core 8 Web API for Real-Time Meteorological Analysis & Reporting**

> [!NOTE]  
> **Project Status**: This repository contains a production-ready, containerized RESTful Web API. Multi-stage Docker containerization and local orchestration are fully implemented. Automated CI/CD deployment pipelines using **GitHub Actions (OIDC)** to **AWS ECS Fargate** with a multi-container (API + SQL Server sidecar) architecture and **AWS Secrets Manager** are fully configured and active.

---

## Executive Summary

This repository presents the architecture, containerization strategy, and operational documentation for a scalable **ASP.NET Core 8 RESTful API** designed for meteorological data retrieval, audit logging, and analytical reporting. The application integrates directly with external meteorological services (**OpenWeatherMap**) and enforces zero-trust security via **JSON Web Token (JWT)** authentication and **Role-Based Access Control (RBAC)**.

Engineered with a microservice architecture in mind, the service is built using a multi-stage `Dockerfile` to optimize execution footprints and ensure cross-platform runtime parity. The application is fully prepared for containerized local development and automated deployment to serverless container execution environments via **AWS ECS Fargate**.

---

## Production Evolution & Cost Trade-offs

To keep this portfolio demo fully self-contained and highly cost-effective under AWS Free Tier limitations, the SQL Server instance runs as a sidecar container directly inside the ECS Fargate task. 

While this works seamlessly for a single-replica demo, in a true enterprise production environment, this is an anti-pattern due to the ephemeral and horizontally-scaling nature of serverless tasks. A production-ready evolution of this stack would:
1. Move the database tier completely out of the container to **Amazon RDS for SQL Server**.
2. Update **AWS Secrets Manager** to inject the external RDS connection strings dynamically at runtime.


---

## Technical Features & Core Capabilities

* **Identity & Access Management (IAM)**: Enforces stateless JWT bearer token authentication paired with role-based authorization policies (`Admin` vs. `User`).
* **Meteorological Integration**: Asynchronous integration with the OpenWeatherMap API for real-time atmospheric data processing (temperature, humidity, atmospheric pressure).
* **Audit & Compliance**: Centralized middleware tracking for API usage metrics, rate usage, and request/response audit logging.
* **Data Persistence Layer**: Built on Entity Framework Core 9 using the Code-First approach for schema migrations and relational database interactions.
* **Cloud-Native CI/CD Pipeline**: Passwordless authentication via **GitHub OIDC** for automated container builds, image pushing to **Amazon ECR**, and deployment updates to **AWS ECS Fargate**.
* **Interactive API Documentation**: OpenAPI 3.0 specification auto-generated via Swashbuckle, providing interactive testing endpoints via Swagger UI.

---

## Technology Stack

| Component | Specification / Framework |
| :--- | :--- |
| **Framework** | ASP.NET Core 8.0 (.NET 8 SDK) |
| **Database ORM** | Entity Framework Core 9.0 |
| **Relational Database** | Microsoft SQL Server (Containerized Sidecar in ECS / LocalDB) |
| **Security & Auth** | JWT (`System.IdentityModel.Tokens.Jwt`) / Role-Based Policy |
| **External API** | OpenWeatherMap REST API |
| **Containerization** | Docker Engine (Multi-stage build runtime) |
| **Cloud Hosting** | AWS ECS Fargate & Application Load Balancer (ALB) |
| **Container Registry** | Amazon ECR |
| **Secrets Management**| AWS Secrets Manager |
| **CI/CD Pipeline** | GitHub Actions (AWS OIDC Federation) |
| **API Documentation** | Swashbuckle (OpenAPI / Swagger UI) |

---

## Architecture & Deployment Strategy

The application leverages a multi-stage `Dockerfile` designed according to enterprise container optimization guidelines. Build operations occur in an isolated SDK environment, while runtime artifacts are copied to a hardened, minimal ASP.NET Core runtime image.

```text
+-------------------------------------------------------------------+
|                     Container Build Process                       |
|                                                                   |
|   +-----------------------+           +-----------------------+   |
|   | Stage 1: Build & Test |           | Stage 2: Runtime      |   |
|   | - [mcr.microsoft.com/](https://mcr.microsoft.com/)  |           | - [mcr.microsoft.com/](https://mcr.microsoft.com/)  |   |
|   |   dotnet/sdk:8.0      |== Copy ==>|   dotnet/aspnet:8.0   |   |
|   | - EF Core Migrations  |  Binaries | - Light, hardened OS  |   |
|   | - Source Compilation  |           | - Port 8080 Target    |   |
|   +-----------------------+           +-----------------------+   |
+-------------------------------------------------------------------+

```

### AWS Deployment Architecture

* **Orchestration**: AWS ECS Fargate running a multi-container task (API + SQL Server sidecar sharing network namespace via `localhost`).
* **Ingress**: Application Load Balancer (ALB) routing public traffic to port `8080`.
* **CI/CD Security**: GitHub Actions authenticates to AWS using OIDC (OpenID Connect) eliminating static Access Keys.
* **Secrets**: Database passwords, JWT secret keys, and API keys are dynamically retrieved from AWS Secrets Manager at task execution.

---

## Local Development & Installation Guide

### Prerequisites

* **.NET 8.0 SDK** (for native binary execution)
* **Docker Engine / Docker Desktop** (for container execution)
* **Microsoft SQL Server** (LocalDB, standalone instance, or containerized instance)
* **OpenWeatherMap API Key** (v2.5 REST endpoint access)

### Setup & Configuration

1. Clone the repository:
```bash
git clone [https://github.com/alessg1414/weather-api-containerized-serverless.git](https://github.com/alessg1414/weather-api-containerized-serverless.git)
cd weather-api-containerized-serverless

```


2. Configure environment settings in `appsettings.json` or export environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WeatherApiDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "SecretKey": "YOUR_CRYPTOGRAPHICALLY_SECURE_SECRET_KEY_MIN_32_BYTES",
    "Issuer": "WeatherApi",
    "Audience": "WeatherClients",
    "ExpiryInHours": "24"
  },
  "OpenWeatherMap": {
    "ApiKey": "YOUR_OPENWEATHERMAP_API_KEY",
    "BaseUrl": "[https://api.openweathermap.org/data/2.5/](https://api.openweathermap.org/data/2.5/)"
  }
}

```

3. Execute database migrations and initialize the service:

```bash
dotnet ef database update
dotnet run

```

*The API will bind to `https://localhost:5001`. Interactive Swagger documentation will be available at root `/`.*

---

## Local Container Deployment (Docker Compose)

To validate multi-container behavior locally prior to cloud deployment, execute the local orchestration stack:

1. **Start local containers**:

```bash
docker-compose up -d --build

```

2. Access `http://localhost:8080/swagger` to verify container startup and endpoint availability.

---

## Default Development Accounts

During local initialization, seed data provisions two default service accounts for authentication validation:

| Username | Password | Role | Permissions Scope |
| --- | --- | --- | --- |
| `admin` | `admin123` | **Admin** | Full system administration, audit log access, user management |
| `user` | `user123` | **User** | Read-only weather querying and analytical report generation |

> **Security Warning**: Standard default credentials must be removed or overridden prior to staging in non-development environments.

---

## Project Roadmap & Deployment Milestones

* [x] **Phase 1**: ASP.NET Core 8 Web API Implementation & JWT Security Architecture
* [x] **Phase 2**: Relational Schema Migration & OpenWeatherMap Data Aggregation
* [x] **Phase 3**: Multi-Stage Dockerfile Optimization & Multi-Container Docker Compose Validation
* [x] **Phase 4**: Integration with **AWS Secrets Manager** and **Amazon ECR**
* [x] **Phase 5**: Automated CI/CD Deployment Pipeline via **GitHub Actions (OIDC)** to **AWS ECS Fargate** behind an Application Load Balancer

---

## License & Attribution

* **License**: Open-source under the terms of the [MIT License](https://www.google.com/search?q=LICENSE).
* **External Services**:
* [OpenWeatherMap API](https://openweathermap.org/) — Meteorological Data Provider
* [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) — OpenAPI Specification Tools
* [Entity Framework Core](https://github.com/dotnet/efcore) — Object-Relational Mapping



```

```
