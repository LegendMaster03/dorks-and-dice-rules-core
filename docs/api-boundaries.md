# API ownership after the Rules Wiki split

The split changes presentation ownership, not the Rules Core API contract. Existing
routes keep their paths, request/response contracts, authentication, provenance,
source filtering, and authorization semantics.

## 1. Stable external API

These endpoint families remain supported for Character Sheet, Block Initiative,
Hex Crawl, future game Tools, and other non-Wiki consumers:

- `/api/rules` and `/api/rules/{conceptKey}` resolved-rule/catalog reads;
- `/api/campaigns/{campaignId}/rules` resolved campaign-rule reads;
- source-accessible catalog/entity reads under `/api/sources`;
- character mechanics and character projection endpoints mapped by
  `CharacterMechanicsEndpointExtensions` and `CharacterProjectionEndpointExtensions`;
- crafting and harvesting resolution endpoints mapped by
  `CraftingRulesEndpointExtensions` and `HarvestingRulesEndpointExtensions`;
- mechanical relationship endpoints mapped by
  `MechanicalRelationshipEndpointExtensions`;
- other published resolution contracts exposed by
  `ResolvedRulesCatalogEndpointExtensions`.

`browserLink` remains part of the external response contract, but its human-facing
Tool target is `rules-wiki`.

## 2. Rules Wiki / first-party internal API

The existing endpoints used by the migrated presentation remain in Rules Core and
are reached through delegated Tool-to-Tool access. They are not redesigned by this
migration. This includes:

- global and campaign rule authoring, preview, comparison, and publication;
- Rules Lawyer/adjudication work queues and workspace scope endpoints;
- source browser, current-user source, source revision review, normalization,
  versioning, hosted-source, acquisition, and source-administration workflows;
- source-update review and campaign baseline authoring.

A later change may consolidate presentation-specific aggregates only after the split
is stable.

## 3. Administrative/service API

Administrative source import/control-plane endpoints remain in Rules Core because
source ingestion, normalization, grants, and persistence remain Rules Core domain
responsibilities. `/api` remains service metadata.

## 4. Hosting and health API

- `/health` is the process health endpoint.
- `/ready` verifies Rules Core PostgreSQL readiness.
- `/api/integration/session` exposes the target-scoped Tool Host authentication
  context to authorized backend callers.
- `/` is service metadata only and no longer advertises a frontend module.

Rules Core does not serve static UI assets after this split.
