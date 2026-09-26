# Enterprise Readiness Checklist

## Architecture
- [x] Clean Architecture project boundaries
- [x] Domain/application/infrastructure/API separation
- [x] Bounded-context structure
- [x] Event contracts project
- [ ] Outbox pattern
- [ ] Idempotent event consumers
- [ ] Versioned event contracts

## Security
- [x] Gateway boundary
- [x] Rate limiting baseline
- [x] ProblemDetails
- [ ] OpenID Connect / OAuth implementation
- [ ] RBAC/policy authorization
- [ ] Secrets in Vault
- [ ] Azure Key Vault provider
- [ ] Security headers
- [ ] CORS policy
- [ ] Request size limits
- [ ] Prompt-injection tests
- [ ] Audit logging

## Data
- [x] SQL Server resource
- [ ] EF Core DbContexts
- [ ] EF migrations
- [ ] Repository implementations
- [ ] Transaction/outbox implementation
- [ ] Azure SQL configuration
- [ ] Blob storage provider

## AI/RAG
- [x] Vector database resource
- [ ] PDF extraction
- [ ] Chunking
- [ ] Embeddings
- [ ] Hybrid search
- [ ] RAG orchestration
- [ ] Azure OpenAI provider
- [ ] Ollama provider
- [ ] Grounding/citation enforcement
- [ ] Evaluation dataset

## Reliability
- [x] OpenTelemetry baseline
- [x] Health endpoints
- [ ] Liveness/readiness checks per dependency
- [ ] Resilience policies per downstream dependency
- [ ] Retry/dead-letter strategy
- [ ] Backup/restore plan

## Testing
- [x] Unit test projects
- [x] Architecture tests
- [ ] Integration tests with Testcontainers
- [ ] API contract tests
- [ ] Security tests
- [ ] RAG evaluation tests
- [ ] Playwright E2E tests
- [ ] Load tests
- [ ] CI coverage gates

## Operations
- [ ] Structured audit events
- [ ] Dashboards
- [ ] Alerts
- [ ] CI/CD
- [ ] Container scanning
- [ ] Dependency scanning
- [ ] Infrastructure-as-code
- [ ] Disaster recovery documentation
