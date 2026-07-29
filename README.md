# Weather API — Containerized Enterprise Service

> **Cloud-Ready ASP.NET Core 8 Web API for Real-Time Meteorological Analysis & Reporting**

> [!NOTE]  
> **Academic & Project Status**: This repository contains a production-ready, containerized RESTful Web API. Please note that while multi-stage Docker containerization and local orchestration are fully implemented, automated deployment pipelines to **Google Cloud Run** and integration with **Google Cloud SQL** are currently pending implementation as a future deployment milestone.

---

## Executive Summary

This repository presents the architecture, containerization strategy, and operational documentation for a scalable **ASP.NET Core 8 RESTful API** designed for meteorological data retrieval, audit logging, and analytical reporting. The application integrates directly with external meteorological services (**OpenWeatherMap**) and enforces zero-trust security via **JSON Web Token (JWT)** authentication and **Role-Based Access Control (RBAC)**.

Engineered with a microservice architecture in mind, the service is built using a multi-stage `Dockerfile` to optimize execution footprints and ensure cross-platform runtime parity. The application is fully prepared for containerized local development and future deployment to serverless container execution environments such as **Google Cloud Run**.

---

## Technical Features & Core Capabilities

* **Identity & Access Management (IAM)**: Enforces stateless JWT bearer token authentication paired with role-based authorization policies (`Admin` vs. `User`).
* **Meteorological Integration**: Asynchronous integration with the OpenWeatherMap API for real-time atmospheric data processing (temperature, humidity, atmospheric pressure).
* **Audit & Compliance**: Centralized middleware tracking for API usage metrics, rate usage, and request/response audit logging.
* **Data Persistence Layer**: Built on Entity Framework Core 9 using the Code-First approach for schema migrations and relational database interactions.
* **Interactive API Documentation**: OpenAPI 3.0 specification auto-generated via Swashbuckle, providing interactive testing endpoints via Swagger UI.

---

## Technology Stack

| Component | Specification / Framework |
| :--- | :--- |
| **Framework** | ASP.NET Core 8.0 (.NET 8 SDK) |
| **Database ORM** | Entity Framework Core 9.0 |
| **Relational Database** | Microsoft SQL Server / Azure SQL / LocalDB |
| **Security & Auth** | JWT (`System.IdentityModel.Tokens.Jwt`) / Role-Based Policy |
| **External API** | OpenWeatherMap REST API |
| **Containerization** | Docker Engine (Multi-stage build runtime) |
| **API Documentation** | Swashbuckle (OpenAPI / Swagger UI) |

---

## Architecture & Containerization Strategy

The application leverages a multi-stage `Dockerfile` designed according to enterprise container optimization guidelines. Build operations occur in an isolated SDK environment, while runtime artifacts are copied to a hardened, minimal ASP.NET Core runtime image.


```

+-------------------------------------------------------------------+
|                        Container Build Process                    |
|                                                                   |
|   +-----------------------+           +-----------------------+   |
|   | Stage 1: Build & Test |           | Stage 2: Runtime      |   |
|   | - [mcr.microsoft.com/](https://www.google.com/search?q=https%3A%2F%2Fmcr.microsoft.com%2F)  |           | - [mcr.microsoft.com/](https://www.google.com/search?q=https%3A%2F%2Fmcr.microsoft.com%2F)  |   |
|   |   dotnet/sdk:8.0      |== Copy ==>|   dotnet/aspnet:8.0   |   |
|   | - EF Core Migrations  |  Binaries | - Light, hardened OS  |   |
|   | - Source Compilation  |           | - Port 80/8080 Target |   |
|   +-----------------------+           +-----------------------+   |
+-------------------------------------------------------------------+

```

### Key Architectural Standards
1. **Separation of Concerns**: Business logic, atmospheric data processing, and persistence layers are segregated across clean service layers.
2. **Environment-Driven Configuration**: Application configurations fall back to system environment variables, making the container completely agnostic to host platforms.
3. **Stateless Operations**: Session data and authentication tokens are validated statelessly, ensuring horizontal scalability across container replicas.

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
   git clone [https://github.com/alessg1414/weather-api-containerized-cloudrun.git](https://github.com/alessg1414/weather-api-containerized-cloudrun.git)
   cd weather-api-containerized-cloudrun

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

## Local Container Deployment (Docker)

To validate container behavior prior to cloud deployment, build and execute the runtime image locally:

1. **Build the container image**:
```bash
docker build -t weather-api:v1.0.0 .

```


2. **Execute the containerized service**:
```bash
docker run -d \
  -p 8080:80 \
  -e "ConnectionStrings__DefaultConnection=Server=YOUR_HOST;Database=WeatherApiDb;User Id=sa;Password=YOUR_PASSWORD;" \
  -e "JwtSettings__SecretKey=YOUR_CRYPTOGRAPHICALLY_SECURE_SECRET_KEY" \
  -e "OpenWeatherMap__ApiKey=YOUR_OPENWEATHERMAP_API_KEY" \
  --name weather-api-instance \
  weather-api:v1.0.0

```


3. Access `http://localhost:8080` to verify container startup and endpoint availability.

---

## Default Development Accounts

During local initialization, seed data provisions two default service accounts for authentication validation:

| Username | Password | Role | Permissions Scope |
| --- | --- | --- | --- |
| `admin` | `admin123` | **Admin** | Full system administration, audit log access, user management |
| `user` | `user123` | **User** | Read-only weather querying and analytical report generation |

> **Security Warning**: Standard default credentials must be removed or overridden prior to staging in non-development environments.

---

## Project Roadmap & Pending Milestone

* [x] **Phase 1**: ASP.NET Core 8 Web API Implementation & JWT Security Architecture
* [x] **Phase 2**: Relational Schema Migration & OpenWeatherMap Data Aggregation
* [x] **Phase 3**: Multi-Stage Dockerfile Optimization & Local Container Validation
* [ ] **Phase 4 (Pending)**: Integration with **Google Cloud Secret Manager** and **Cloud SQL (SQL Server)**
* [ ] **Phase 5 (Pending)**: Automated Build & Deployment Pipeline via **Google Cloud Build** to **Google Cloud Run**

---

## License & Attribution

* **License**: Open-source under the terms of the [MIT License](https://www.google.com/search?q=LICENSE).
* **External Services**:
* [OpenWeatherMap API](https://openweathermap.org/) — Meteorological Data Provider
* [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) — OpenAPI Specification Tools
* [Entity Framework Core](https://github.com/dotnet/efcore) — Object-Relational Mapping



```

```
