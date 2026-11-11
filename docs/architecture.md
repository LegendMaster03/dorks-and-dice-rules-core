# Rules Core architecture — current native service

Rules Core is the headless .NET 10 native rules-content and mechanics service for Dorks & Dice. Rules Wiki is the separate human-facing first-party consumer. The earlier PostgreSQL-backed Source Layer / Rules Layer service and its `/api/rules` endpoints are retired; their documentation is not an implementation contract for this host.

## Content authority

- The configured first-party package is `data/rulesets/first-party-001/`; its `native/` subtree alone supplies runtime rules. A coherent approved rule is promoted into `native/`; an incomplete proposal stays outside it.
- `working/`, `data/reference/`, and `data/conversions/` are staging, evidence, and conversion inputs, **not** runtime rule sources or fallbacks. Source access, license attribution, and any future import/adaptation workflow must respect these boundaries.
- `RulesCore.Content` validates the manifest and operative JSON, enforces stable identities, and publishes an immutable, fingerprinted catalog snapshot. The host reports process liveness via `GET /health` and snapshot readiness via `GET /ready`.
- `RulesCore.Mechanics` evaluates implemented native rules and returns explicit unresolved/unsupported outcomes rather than substituting an edition-specific or legacy rule. A JSON rule definition does not imply every associated mechanic is executable.

## HTTP surfaces

`RulesCore:ApiSurface` accepts exactly `PublicOnly` or `PrivateOnly`. They are separate host ingresses; neither exposes the other's private reference routes. All authenticated endpoints use Site-issued, target-scoped credentials.

| Ingress | Route family | Use |
| --- | --- | --- |
| PublicOnly | `GET /api/rulesets` and child read routes | Anonymous native catalog **only when explicitly authorized for public distribution**; otherwise fail closed |
| PublicOnly | `GET /api/consumer/v1/rulesets` and child read routes | Native catalog for Site-authenticated consumers |
| PublicOnly | `/api/mechanics/v1` | Ticket-authenticated native calculations for ordinary Tool consumers |
| PrivateOnly | `GET /api/private/rulesets` and child read routes | Private, first-party Rules Wiki reference access |
| PrivateOnly | `/api/private/mechanics/v1` | Authenticated native mechanics, with private catalog search and batch identity resolution |

See [authenticated catalog](native-authenticated-consumer-catalog.md) and [native mechanics API](native-mechanics-api.md) for current contract details. Do not use the historical `/api/rules`, `/api/global/rules`, or source-ingestion endpoint documentation to implement a new client.

## Responsibilities and deployment

Rules Core owns native rule identities, validated content, qualification/eligibility checks, and implemented deterministic mechanics. Calling Tools own user-specific character/encounter/exploration state and selection workflows. Rules Wiki owns the human-facing presentation and uses the separately authorized private tunnel rather than the ordinary consumer surface.

The split ingress and network isolation are described in [private tunnel deployment](private-tool-tunnel-deployment.md). Deployment is **manual**, with ruleset fingerprint and CI checks described in the [repository README](../README.md). Neither a document edit nor a merged content change is deployment authorization. The ruleset remains under active development; proposals and unresolved functionality must not be presented as operational capabilities.
