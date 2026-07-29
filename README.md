# Weather API — Containerized & Cloud Run-ready

A containerized ASP.NET Core 8 REST API for real-time weather data analysis and reporting. This project provides JWT-based authentication, role-based authorization, weather data retrieval from OpenWeatherMap, API usage auditing, and is configured to be built into a container image and deployed to Google Cloud Run.

## Key changes in this repository

- Multi-stage Dockerfile included (SDK build stage + lightweight ASP.NET runtime stage).
- Container image build and deployment instructions for Google Cloud Run.
- Guidance for managing secrets and database connectivity for cloud deployments.

## 📦 Installation (Local development)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for local development)
- Docker (to build and run images locally)
- (Optional) SQL Server for local development (LocalDB or full instance)
- An [OpenWeatherMap API key](https://openweathermap.org/api)

### Setup

```bash
git clone https://github.com/alessg1414/weather-api-containerized-cloudrun.git
cd weather-api-containerized-cloudrun
```

Configure `appsettings.json` (or use environment variables — recommended for container/cloud):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WeatherApiDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-at-least-32-characters-long",
    "Issuer": "WeatherApi",
    "Audience": "WeatherClients",
    "ExpiryInHours": "24"
  },
  "OpenWeatherMap": {
    "ApiKey": "your-openweathermap-api-key",
    "BaseUrl": "https://api.openweathermap.org/data/2.5/"
  }
}
```

Apply EF migrations and run locally:

```bash
dotnet ef database update
dotnet run
```

The API will be available at `https://localhost:5001` (or at the port configured). The Swagger UI is available at `/` when running locally.

## 🐳 Build and run with Docker (local)

Build the container image locally (the project contains a multi-stage Dockerfile optimized for small runtime image):

```bash
docker build -t weather-api:local .
```

Run the container and map port 8080 (Cloud Run uses port 8080 by convention — container listens on port 80 by default in ASP.NET images):

```bash
docker run -p 8080:80 \
  -e "ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...;" \
  -e "JwtSettings__SecretKey=your-secret-key" \
  -e "OpenWeatherMap__ApiKey=your-openweathermap-key" \
  weather-api:local
```

Then open http://localhost:8080 for Swagger and API endpoints.

## 🚀 Deploy to Google Cloud Run

This section shows a simple flow to build, push, and deploy the container image to Cloud Run using Google Cloud Build and the gcloud CLI.

### Prerequisites (GCP)

- A Google Cloud project (PROJECT_ID)
- gcloud CLI installed and authenticated (gcloud auth login)
- Enable required APIs:
  - Cloud Run API (run.googleapis.com)
  - Cloud Build API (cloudbuild.googleapis.com)
  - Artifact Registry or Container Registry (artifactregistry.googleapis.com / containerregistry.googleapis.com)
  - (Optional) Cloud SQL Admin API (cloudsqladmin.googleapis.com) if using Cloud SQL for SQL Server

### Build and push image

You can use Cloud Build to build and push the image to Artifact Registry or Container Registry. Example using Container Registry:

```bash
gcloud config set project PROJECT_ID
gcloud builds submit --tag gcr.io/PROJECT_ID/weather-api:latest .
```

Or build locally and push to Artifact Registry / Container Registry if you prefer:

```bash
docker build -t gcr.io/PROJECT_ID/weather-api:latest .
docker push gcr.io/PROJECT_ID/weather-api:latest
```

### Secrets and environment configuration

For production deployments you should avoid baking secrets into the image or storing them in source control. Recommended options:

- Use Secret Manager and reference secrets from Cloud Run.
- Set environment variables at deployment time (not checked into code).

Example (recommended) — create secrets in Secret Manager and grant the Cloud Run runtime service account access, then add secrets when deploying.

### Deploy to Cloud Run (basic)

```bash
gcloud run deploy weather-api \
  --image gcr.io/PROJECT_ID/weather-api:latest \
  --platform managed \
  --region REGION \
  --allow-unauthenticated \
  --set-env-vars "JwtSettings__SecretKey=YOUR_SECRET,OpenWeatherMap__ApiKey=YOUR_KEY"
```

Note: For production, prefer using Secret Manager and Cloud Run's `--add-secrets` (or use the console) to inject secrets instead of `--set-env-vars`.

### Connecting to a managed database (Cloud SQL for SQL Server)

If you use Cloud SQL for SQL Server, you can connect a Cloud Run service to it using:

1. Create a Cloud SQL instance (SQL Server) and note the instance connection name.
2. Grant the Cloud Run service account the `Cloud SQL Client` role.
3. Deploy Cloud Run adding the Cloud SQL instance:

```bash
gcloud run deploy weather-api \
  --image gcr.io/PROJECT_ID/weather-api:latest \
  --region REGION \
  --platform managed \
  --add-cloudsql-instances INSTANCE_CONNECTION_NAME \
  --set-env-vars "ConnectionStrings__DefaultConnection=Server=tcp:YOUR_CLOUDSQL_PRIVATE_IP,1433;Database=DB;User Id=USER;Password=PASS;"
```

Alternatively, use the Cloud SQL Auth proxy approach or private IP depending on networking preference. Ensure the connection string used by your app matches the SQL Server connection format.

## 🔐 Recommended environment variables / secrets

- ConnectionStrings__DefaultConnection
- JwtSettings__SecretKey
- JwtSettings__Issuer (optional)
- JwtSettings__Audience (optional)
- OpenWeatherMap__ApiKey

Store sensitive values in Secret Manager and inject them into Cloud Run at deploy time.

## 🛠 Usage (same as local)

The database is seeded with two default users for convenience (if you keep seed data in production be mindful of credentials):

| Username | Password | Role  |
|----------|----------|-------|
| admin    | admin123 | Admin |
| user     | user123  | User  |

1. Open the service URL provided by Cloud Run in the browser
2. Use `POST /api/auth/login` to get a JWT token
3. Click **Authorize** in Swagger and enter `Bearer {token}`
4. Query weather data, generate reports, or manage users

## ✨ Features

- JWT authentication and role-based access control (Admin / User)
- Real-time weather data via OpenWeatherMap
- Report generation (temperature, humidity, pressure)
- API usage auditing and statistics
- Swagger UI (interactive documentation)

## 🧰 Tech Stack

- ASP.NET Core 8 (.NET 8)
- SQL Server (LocalDB or managed SQL Server in the cloud)
- Entity Framework Core 9
- JWT authentication (System.IdentityModel.Tokens.Jwt)
- OpenWeatherMap external API
- Swashbuckle (Swagger / OpenAPI)

## 📡 API Endpoints (summary)

See the API for full details; Swagger documents all endpoints once running.

## 📄 License

MIT

## 🙌 Credits

- [OpenWeatherMap](https://openweathermap.org/) for weather data
- [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) for Swagger integration
- [Entity Framework Core](https://github.com/dotnet/efcore) for data access

---

If you'd like, I can also:
- Add a short Cloud Build config (cloudbuild.yaml) to automate image builds and pushes.
- Create a sample `gcloud` deploy script that uses Secret Manager and Cloud SQL properly.
- Update the Dockerfile or add instructions specific to Artifact Registry instead of Container Registry.
