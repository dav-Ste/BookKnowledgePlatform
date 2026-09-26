# ADR 0001: Provider-neutral infrastructure

## Status

Accepted

## Context

BookKnowledgePlatform is intended to be both:

- an open-source project that developers can run entirely locally; and
- an enterprise application capable of deployment into environments such as Azure.

The application must therefore not make cloud infrastructure a prerequisite for development.

In particular:

- local SQL Server should be provided by .NET Aspire/container infrastructure;
- local secrets should be capable of being provided by HashiCorp Vault;
- local identity should be backed by an on-premises SQL Server database;
- enterprise deployments should be capable of using Azure SQL, Azure Key Vault and Microsoft Entra ID;
- application and domain code must not depend directly on a particular infrastructure provider.

## Decision

Infrastructure providers are selected through configuration.

Provider choices are implementation details of the infrastructure/composition layer.

The application/domain layers depend on abstractions rather than Azure-specific or local-development-specific implementations.

The initial provider model is:

| Capability | Local | Enterprise |
|---|---|---|
| Identity | ASP.NET Identity + OpenIddict | Microsoft Entra ID |
| Database | SQL Server container | Azure SQL |
| Secrets | Development/Vault | Azure Key Vault |
| Object storage | MinIO | Azure Blob Storage |
| Vector store | Qdrant | Qdrant/managed equivalent |
| Messaging | RabbitMQ | RabbitMQ/Azure equivalent |
| AI | Ollama/configurable provider | Azure OpenAI/configurable provider |

## Consequences

A developer must be able to clone the repository and run the complete platform without an Azure subscription.

Cloud-specific SDKs must remain outside core application/domain projects.

Production configuration must not require code changes.

Provider selection must be tested independently of the provider implementation.

## Authentication

Authentication is also provider-neutral.

The application authorization model is expressed in terms of claims, roles and permissions.

The authentication authority may be:

- the locally hosted BookKnowledge Identity service; or
- Microsoft Entra ID.

Downstream APIs validate standard OAuth 2.0/OIDC bearer access tokens and do not depend on how the user authenticated.
