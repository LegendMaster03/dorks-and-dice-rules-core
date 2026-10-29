# Phase 3A — Native private reference contract

Status: local implementation candidate, not integrated or deployed. The release remains blocked until both repository builds, test suites, and private ingress checks pass in CI and a real Site ticket is redeemed against the new Core.

## Authority and identity

Only `data/rulesets/first-party-001/native/` supplies runtime definitions through the immutable `NativeCatalogSnapshot` published by `NativeCatalogStore`. The first-party ruleset ID is an internal stable identifier, not a public game title. No `working/`, `reference/`, `conversions/`, PostgreSQL, revision, import, authoring, or legacy Wiki content is consulted by these handlers.

The `PrivateOnly` host maps **GET only** under `/api/private/rulesets`. The `PublicOnly` host does not map these operations. Other `/api` paths return 404. The private ingress must be attached to only the `rules-wiki`/`rules-core` pair network and restricted Site control-plane network; `RulesCore:ApiSurface=PrivateOnly` is not itself a substitute for network isolation.

### Authentication

Every private request must carry exactly one `X-Dorks-Tool-Auth-Ticket` header and exactly one `X-Dorks-Tool-Auth-Introspection-Path` header. The latter must be exactly `/tool-host/registrations/rules-core/api/introspect`. The Core posts `Authorization: Bearer <ticket>` to that fixed endpoint using `ToolHost:BaseUrl` (server-only). A successful Site response must contain `contractVersion=1`, `toolKey=rules-core`, a stable user ID, `siteMode=dorks-and-dice`, and `privateTunnelSourceToolKey=rules-wiki` **exactly**. Ordinary delegation, other site modes, and a header claiming the source do not grant permission. Site itself decides which registered users and tools are allowed to receive and redeem tickets; the initial native reference contract is a read-only first-party library, not source-grant or authoring permission.

Missing/invalid/revoked tickets: 401. Valid target tickets with wrong private source or site mode: 403. Missing Site configuration or unavailable catalog: 503. Site connection/invalid upstream status: 502. Timeout: 504. No ticket, Site address, or introspection path is sent to the browser or included in errors. `/health` and `/ready` remain independent of authentication.

### JSON response contract

- `GET /api/private/rulesets` → `{ "rulesets": [{ "id": "first-party-001", "entityCount": 35, "fingerprint": "<snapshot>", "capabilities": { "read": true, "write": false } }] }`
- `GET /api/private/rulesets/{rulesetId}/rules?q=&category=&limit=&offset=` → `{ "rulesetId": "first-party-001", "fingerprint": "<snapshot>", "total": 35, "offset": 0, "limit": 50, "categoryFacets": [{ "kind": "condition", "count": 29 }, ...], "rules": [{ "id": "condition.blinded", "name": "Blinded", "kind": "condition" }, ...] }`. Full category facet counts describe the published native snapshot; `total` respects query/category filters. Results are ordered by stable native ID. Case-insensitive substring search covers native IDs and names.
- `GET /api/private/rulesets/{rulesetId}/rules/{ruleId}` → `{ "rulesetId": "first-party-001", "fingerprint": "<snapshot>", "rule": { "id": "condition.blinded", "name": "Blinded", "kind": "condition", "definition": { ... } } }`. `definition` is the approved native JSON object, without source metadata or legacy projections.

Pagination defaults to 50, limit must be 1–100, offset must be 0–1,000,000, and query length is at most 200 characters. Duplicate/unsupported filters and malformed numeric inputs yield 400. Unknown categories return an empty search result rather than inventing content. Unknown rulesets/IDs yield 404. An unavailable snapshot yields 503. Per-request snapshot capture ensures fingerprint and records come from one immutable version. No request-driven filesystem reads, catalog reloads, or index rebuilds occur.

## Boundary verification before release

Use actual Site-issued tickets over the pair network to confirm successful `rules-wiki` private reads and failures for a wrong source, ordinary delegated target tickets, wrong target, wrong introspection path, revoked tickets, invalid modes and public ingress. Confirm the public ingress is not attached to the private pair network. Release only after documented Core/Wiki commit pairing and verified rollback order. Phase 3B must add a separate public consumer surface; it must not reuse the private Wiki endpoint as a public API.
