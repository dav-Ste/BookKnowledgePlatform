# BookKnowledgePlatform

.NET 10 enterprise-oriented starting point for a book catalogue and AI/RAG content-search platform.

## Current skeleton

- YARP API gateway
- Separate Identity, Books and Content Search APIs
- Two Blazor frontend placeholders
- Indexing worker placeholder
- .NET Aspire AppHost
- SQL Server
- RabbitMQ
- Qdrant
- MinIO fallback Docker Compose
- OpenTelemetry/health-check service defaults
- ASP.NET Core rate limiting
- ProblemDetails
- Unit and architecture test projects
- Provider-oriented configuration boundaries

## Prerequisites

- .NET 10 SDK
- Docker Desktop
- Aspire tooling

Current Aspire documentation requires the .NET 10 SDK for C# AppHosts and recommends Docker Desktop for local container resources.

## Run with Aspire

From the repository root:

    dotnet restore
    dotnet build
    dotnet run --project src/AppHost/BookKnowledge.AppHost

Open the Aspire dashboard URL shown by the CLI.

## Run infrastructure only

    docker compose -f docker-compose.infrastructure.yml up -d

This is useful when running individual APIs from Visual Studio/Rider/VS Code.

## Important

This is deliberately a skeleton, not a completed production application. Authentication, EF Core repositories, migrations, PDF extraction, embeddings, RAG, Azure provider implementations, Vault integration and full integration/E2E tests are the next vertical slices.
