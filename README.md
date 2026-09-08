# Dorks & Dice Rules Core

Rules Core is the independent rules aggregation, normalization, adjudication, and API service for Dorks & Dice.

## Initial stack

- .NET 10 / ASP.NET Core
- PostgreSQL through Entity Framework Core and Npgsql
- 5e.tools-compatible rule documents with Rules Core extensions where required
- Docker
- xUnit integration and boundary tests

The PostgreSQL database is deployment-owned and runs independently of the Rules Core application container. Runtime source payloads, user-provided material, caches, database files, credentials, and access-controlled content are never committed to this repository.

## Projects

- `RulesCore.Domain` - domain concepts and invariants.
- `RulesCore.Application` - use cases and application contracts.
- `RulesCore.Infrastructure` - PostgreSQL, source ingestion, storage, and external integrations.
- `RulesCore.Web` - HTTP API, health endpoints, standalone host, and Dorks & Dice frontend module.
- `RulesCore.Tests` - domain/application boundary tests.
- `RulesCore.IntegrationTests` - HTTP and hosting-contract tests.

## Development

```bash
dotnet restore dorks-and-dice-rules-core.slnx
dotnet test dorks-and-dice-rules-core.slnx --configuration Release
dotnet run --project src/RulesCore.Web
```

Set `ConnectionStrings__RulesCore` to a PostgreSQL connection string before database-backed work. The service can start without a database during early standalone development; `/ready` reports unavailable until PostgreSQL is configured and reachable.

## Tool hosting

The existing Dorks & Dice Tool Host supports both embedded modules and proxied applications. Rules Core currently exposes:

- `/health` - liveness endpoint used by the host/deployment.
- `/ready` - database-aware readiness endpoint.
- `/app.js` - minimal ES module for the existing Embedded Module integration.
- `/` and `/api` - standalone service metadata.

The host strips browser Cookie and Authorization headers before proxying and does not support WebSockets or tool-owned cookie sessions. For that reason the initial scaffold deliberately does not use Blazor Server or a separate Identity store. Dorks & Dice remains the production identity/authorization authority; the authenticated write contract will be added explicitly rather than inferred from proxy headers.

See `docs/architecture.md` and `docs/tool-hosting.md` for the current boundaries.
