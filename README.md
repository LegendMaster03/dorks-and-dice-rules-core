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

## Source Layer

The Source Layer persists package, work, edition, entity, immutable entity-revision, and per-user source-grant records in PostgreSQL. `ISourceImportService` accepts 5e.tools-shaped JSON, preserves complete entity objects as JSONB, computes stable SHA-256 content fingerprints, and creates a new revision only when source content changes.

The source read API is access-aware:

- `GET /api/sources`
- `GET /api/sources/entities/{entityId}`

Anonymous/direct requests see only public packages. Hosted requests redeem the main site's one-time Tool authentication ticket and use the resulting stable user ID to include private packages for which Rules Core stores an explicit `user_source_grant`. Missing and inaccessible private entities both return not-found behavior.

Grant mutation and source import remain internal application services for now; there is no unauthenticated write endpoint.

See `docs/source-layer.md` for the persistence, fingerprinting, provenance, and access boundaries.

## Global Rules Layer

Stable `rule_concept` records provide source-independent rule identities. A concept can bind one or more source-specific entities, while append-only global decisions pin an exact immutable source revision. Reimporting changed source material therefore does not silently alter an already-adjudicated rule.

Global decisions support exact `select-source`, simple `json-merge-patch`, and array-aware `json-rule-patch` behavior. Structured rule patches can combine ordinary object merge/delete semantics with ordered `append`, `remove`, `replace-by-key`, `insert-before`, and `insert-after` operations against JSON Pointer-addressed arrays. Selectors must resolve uniquely for destructive or positional operations, so ambiguous edits fail instead of silently changing the wrong list item. The Source Layer is never modified.

Rules Lawyers can preview a candidate decision before saving it. `POST /api/global/rules/concepts/{conceptId}/preview` returns the source/base document, candidate document, normalized patch provenance, and a deterministic structural diff without creating a decision or ruleset revision. Preview requires normal Rules Lawyer change authority and independently enforces restricted-source grants because it returns source-backed content.

Rules Lawyers can then create concepts, bind source entities, set decisions, and publish immutable global ruleset revisions through authenticated Tool Host requests. Global mutation requires both the `dorks-and-dice` site mode and the effective `Rules Lawyer` role.

`GET /api/rules/{conceptKey}` resolves the concept from the latest published ruleset and returns exact decision/source provenance plus patch provenance. The source-backed document is returned only when the caller independently satisfies Source Layer access control. Rules Lawyer authority does not grant restricted source access, and a source grant does not grant Rules Lawyer authority.

Publishing the same effective decisions twice is idempotent. Source changes become visible in the resolved ruleset only after a Rules Lawyer deliberately creates a decision against the new source revision and publishes a new ruleset revision.

## Campaign Rules Layer

Campaigns deliberately select a published global ruleset revision rather than floating with `latest`. Selecting a newer baseline does not affect the campaign's active rules until its DM publishes a new immutable campaign ruleset revision.

Campaign decisions are append-only and support exact source overrides, explicit return to the selected global baseline, simple merge patches, and array-aware structured patches. Campaign patches compose after any global patch, while `select-source` deliberately bypasses the baseline implementation and pins a different bound source revision.

A campaign DM can preview an override through `POST /api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview`. The preview compares the selected global baseline to the proposed campaign result and does not create a campaign decision or publication. It requires DM authority plus any independent source grants needed to display restricted source-backed content.

Campaign reads require authenticated membership in the requested campaign. Campaign mutation additionally requires the campaign-scoped `DM` role. Source licensing remains independent: campaign membership or DM authority does not grant access to restricted source content, and a source grant does not grant campaign authority.

Current campaign endpoints:

- `PUT /api/campaigns/{campaignId}/rules/baseline`
- `POST /api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview`
- `PUT /api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision`
- `POST /api/campaigns/{campaignId}/rules/publish`
- `GET /api/campaigns/{campaignId}/rules/{conceptKey}`

See `docs/rules-layer.md` for global and campaign concept identity, patch semantics, preview/diff, publication, migration, authority, and resolution behavior.

## Deployment

Pushes to `main` trigger `.github/workflows/deploy.yml`, which follows the same self-hosted deployment pattern as the main Dorks & Dice site. The workflow runs the test suite against disposable PostgreSQL 18, builds and smoke-tests `dorks-and-dice-rules-core:latest`, then recreates the production container through Docker Compose.

The production workflow expects:

- a self-hosted GitHub Actions runner with the `dorks-and-dice-rules-core` label;
- `/mnt/HDDs/www/dorks-and-dice-rules-core/.env` containing `ConnectionStrings__RulesCore`;
- the external Docker network `dorks-and-dice-backend`;
- the production PostgreSQL database referenced by the deployment-only connection string.

The production database and credentials are not created, modified, or stored by the workflow.

## Tool hosting

The Dorks & Dice Tool Host supports both embedded modules and proxied applications. Rules Core currently exposes:

- `/health` - liveness endpoint used by the host/deployment.
- `/ready` - database-aware readiness endpoint.
- `/app.js` - minimal ES module for the existing Embedded Module integration.
- `/` and `/api` - standalone service metadata.
- `/api/integration/session` - authenticated backend view of the redeemed Tool Host context.

The host strips browser Cookie, Authorization, forwarding, and reserved Tool-auth headers before proxying. For authenticated backend requests it injects a short-lived one-time ticket. Rules Core redeems that ticket against the main site's fixed `rules-core` introspection endpoint before accepting the supplied identity, global roles, or campaign memberships.

Rules Core does not use Blazor Server or a separate production Identity store. Dorks & Dice remains the identity and change-authority source; Rules Core separately owns restricted source-content grants.

See `docs/architecture.md` and `docs/tool-hosting.md` for the current boundaries.
