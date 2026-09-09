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
- `GET /api/sources/entities`
- `GET /api/sources/entities/{entityId}`

Anonymous/direct requests see only public packages. Hosted requests redeem the main site's one-time Tool authentication ticket and use the resulting stable user ID to include private packages for which Rules Core stores an explicit `user_source_grant`. Missing and inaccessible private entities both return not-found behavior.

Source ingestion is exposed through `POST /api/source-admin/import` only to effective `Dev` users while the site is in `dorks-and-dice` mode. Importing a restricted package does not automatically grant the importing Dev access to that package. The Dev may explicitly grant or revoke only the current authenticated account through the Source Administration package catalog; the target user ID is always derived from the redeemed Tool Host identity. Arbitrary other-user grant mutation and acquisition tracking are not exposed yet.

See `docs/source-layer.md` and `docs/source-administration.md` for persistence, fingerprinting, provenance, access boundaries, Dev-only ingestion, and explicit current-account source grants.

## Global Rules Layer

Stable `rule_concept` records provide source-independent rule identities. A concept can bind one or more source-specific entities, while append-only global decisions pin an exact immutable source revision. Reimporting changed source material therefore does not silently alter an already-adjudicated rule.

Global decisions support exact `select-source`, simple `json-merge-patch`, and array-aware `json-rule-patch` behavior. Structured rule patches can combine ordinary object merge/delete semantics with ordered `append`, `remove`, `replace-by-key`, `insert-before`, and `insert-after` operations against JSON Pointer-addressed arrays. Selectors must resolve uniquely for destructive or positional operations, so ambiguous edits fail instead of silently changing the wrong list item. The Source Layer is never modified.

Rules Lawyers can preview a candidate decision before saving it. `POST /api/global/rules/concepts/{conceptId}/preview` returns the source/base document, candidate document, normalized patch provenance, and a deterministic structural diff without creating a decision or ruleset revision. Preview requires normal Rules Lawyer change authority and independently enforces restricted-source grants because it returns source-backed content.

The hosted authoring API exposes the state needed to drive the Rules Lawyer UI without introducing mutable server-side drafts. `GET /api/global/rules/authoring` summarizes concepts, the latest publication, and pending saved decisions. `GET /api/global/rules/authoring/concepts/{conceptId}` returns bindings, exact accessible source revisions, the latest saved decision, and publication state. Restricted bindings remain visible only as Rules Layer identities/counts until the current user independently possesses the corresponding source grant.

Rules Lawyers can create concepts, search source entities they may access, bind those sources, set decisions, and publish immutable global ruleset revisions through the hosted authoring UI. Global mutation requires both the `dorks-and-dice` site mode and the effective `Rules Lawyer` role.

To reduce repetitive cross-edition setup, `GET /api/global/rules/normalization/candidates` proposes deterministic concept keys for accessible unbound source entities. A Rules Lawyer reviews each proposal and may accept it with `POST /api/global/rules/normalization/entities/{sourceEntityId}/accept`. Acceptance creates or reuses a stable concept and binds the source entity, but never creates a rule decision or publishes anything. Name/type matching is therefore an authoring suggestion rather than an automatic semantic merge. Restricted source metadata remains filtered by independent source grants.

`GET /api/rules/{conceptKey}` resolves the concept from the latest published ruleset and returns exact decision/source provenance plus patch provenance. The source-backed document is returned only when the caller independently satisfies Source Layer access control. Rules Lawyer authority does not grant restricted source access, and a source grant does not grant Rules Lawyer authority.

Publishing the same effective decisions twice is idempotent. Source changes become visible in the resolved ruleset only after a Rules Lawyer deliberately creates a decision against the new source revision and publishes a new ruleset revision.

See `docs/source-normalization.md` for reviewed normalization semantics and `docs/authoring-workflow.md` for the browse -> preview -> save -> publish workflow and its authorization/source-access boundaries.

## Resolved Rules Browser

Published rules are also available through access-aware catalogs. `GET /api/rules` lists the latest global publication filtered to source packages the current identity may access. `GET /api/campaigns/{campaignId}/rules` does the same for the latest published campaign ruleset and requires campaign membership. Catalog responses include rule identity and source provenance but not source-backed rule documents; full documents continue to use the existing per-concept resolved endpoints and their independent source-access checks.

The embedded `Rules Browser` is available to authenticated Dorks & Dice users even when they have no Rules Lawyer or DM authority. Players can browse a campaign they belong to, while Rules Lawyers and DMs retain the separate authoring views. Unpublished global decisions, campaign baseline selections, and campaign overrides are not exposed through this read-only browser.

See `docs/rules-browser.md` for catalog behavior, campaign membership rules, source-access filtering, and the current generic resolved-document renderer.

## Campaign Rules Layer

Campaigns deliberately select a published global ruleset revision rather than floating with `latest`. Selecting a newer baseline does not affect the campaign's active rules until its DM publishes a new immutable campaign ruleset revision.

Campaign decisions are append-only and support exact source overrides, explicit return to the selected global baseline, simple merge patches, and array-aware structured patches. Campaign patches compose after any global patch, while `select-source` deliberately bypasses the baseline implementation and pins a different bound source revision.

A campaign DM can preview an override through `POST /api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview`. The preview compares the selected global baseline to the proposed campaign result and does not create a campaign decision or publication. It requires DM authority plus any independent source grants needed to display restricted source-backed content.

Campaign authoring is exposed without mutable server-side drafts. `GET /api/campaigns/{campaignId}/rules/authoring` reports the selected baseline, latest campaign publication, current-baseline concepts, pending campaign-decision count, and whether publication is required because the baseline or a campaign decision changed. `GET /api/campaigns/{campaignId}/rules/authoring/concepts/{conceptId}` returns the exact baseline global decision, latest/published campaign decision state, bindings, and source revisions the DM may independently access. Historical campaign decisions for concepts outside the current selected baseline remain preserved but do not make the current campaign appear pending.

DMs can discover published global baselines with `GET /api/campaigns/{campaignId}/rules/baselines` and preview a migration with `GET /api/campaigns/{campaignId}/rules/baselines/{rulesetRevisionId}/preview`. The migration preview compares Rules Layer structure and campaign-decision activation without returning source-backed documents. The hosted UI then lets the DM deliberately select a candidate baseline; that selection remains pending until campaign publication.

Campaign reads require authenticated membership in the requested campaign. Campaign mutation, authoring, baseline discovery, and migration preview additionally require the campaign-scoped `DM` role. Source licensing remains independent: campaign membership or DM authority does not grant access to restricted source content, and a source grant does not grant campaign authority.

Current campaign endpoints:

- `GET /api/campaigns/{campaignId}/rules`
- `GET /api/campaigns/{campaignId}/rules/authoring`
- `GET /api/campaigns/{campaignId}/rules/authoring/concepts/{conceptId}`
- `GET /api/campaigns/{campaignId}/rules/baselines`
- `GET /api/campaigns/{campaignId}/rules/baselines/{rulesetRevisionId}/preview`
- `PUT /api/campaigns/{campaignId}/rules/baseline`
- `POST /api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview`
- `PUT /api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision`
- `POST /api/campaigns/{campaignId}/rules/publish`
- `GET /api/campaigns/{campaignId}/rules/{conceptKey}`

See `docs/rules-layer.md` for global and campaign concept identity, patch semantics, preview/diff, publication, migration, authority, and resolution behavior. See `docs/authoring-workflow.md` for both global Rules Lawyer and campaign DM authoring state machines.

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
- `/app.js` - ES module entry point for the existing Embedded Module integration.
- `/` and `/api` - standalone service metadata.
- `/api/integration/session` - authenticated backend view of the redeemed Tool Host context.

The host strips browser Cookie, Authorization, forwarding, and reserved Tool-auth headers before proxying. For authenticated backend requests it injects a short-lived one-time ticket. Rules Core redeems that ticket against the main site's fixed `rules-core` introspection endpoint before accepting the supplied identity, global roles, or campaign memberships.

Rules Core does not use Blazor Server or a separate production Identity store. Dorks & Dice remains the identity and change-authority source; Rules Core separately owns restricted source-content grants.

See `docs/architecture.md` and `docs/tool-hosting.md` for the current boundaries.
