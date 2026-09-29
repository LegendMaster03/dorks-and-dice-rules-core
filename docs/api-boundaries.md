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

`/api/rules` is intentionally a resolved consumer contract. It exposes the effective
published ruleset expected by game Tools and must not be repurposed as the complete
Rules Wiki reference catalog.

`browserLink` remains part of the external response contract, but its human-facing
Tool target is `rules-wiki`.

## 2. Rules Wiki / first-party reference API

Rules Core owns the first-party read model used by Rules Wiki:

- `GET /api/wiki/references` browses global accessible reference concepts;
- `GET /api/wiki/references/{referenceIdentity}` returns accessible source/history
  detail for one logical reference concept;
- `GET /api/campaigns/{campaignId}/wiki/references` browses the same reference
  catalog using the selected campaign rules scope;
- `GET /api/campaigns/{campaignId}/wiki/references/{referenceIdentity}` returns
  campaign-scoped effective/default state without changing source history;
- `POST /api/wiki/references/comparison` compares two accessible variations of the
  same logical reference concept without granting adjudication authority.

These endpoints are presentation-oriented first-party contracts, but Rules Core
remains authoritative for source grants, canonical identity, publication and edition
metadata, source occurrence history, concept relationships, effective global and
campaign resolution, unresolved fallback selection, and server-authoritative facets.
Rules Wiki renders those semantics; it does not reconstruct them from raw source
records.

Source grants remain the content-read authorization boundary. A source package that
is not accessible to the request identity must not leak through reference results,
search, facets, counts, history, comparison, fallback selection, or canonical
grouping. Anonymous requests see only public packages.

Reading accessible source variations is not a Rules Lawyer operation. Rules Lawyer
or campaign authority is still required by the existing mutation, adjudication, and
publication endpoints. The read-only Wiki comparison endpoint reuses Rules Core's
semantic comparison implementation while preserving source-access checks.

The effective/default reference variation is a scope projection, not a second source
history. A published Rules Layer decision selects the effective variation when one
exists. If no applicable decision exists, Rules Core deterministically selects the
newest accessible applicable variation as an unresolved fallback without creating or
persisting a rule decision.

## 3. Rules Wiki authoring / first-party internal API

The existing endpoints used by the migrated presentation remain in Rules Core and
are reached through delegated Tool-to-Tool access. This includes:

- global and campaign rule authoring, preview, comparison, and publication;
- Rules Lawyer/adjudication work queues and workspace scope endpoints;
- source browser, current-user source, source revision review, normalization,
  versioning, hosted-source, acquisition, and source-administration workflows;
- source-update review and campaign baseline authoring.

The reference API does not replace those mutation workflows. It separates ordinary
reference reading from adjudication authority.

## 4. Administrative/service API

Administrative source import/control-plane endpoints remain in Rules Core because
source ingestion, normalization, grants, and persistence remain Rules Core domain
responsibilities. `/api` remains service metadata.

## 5. Hosting and health API

- `/health` is the process health endpoint.
- `/ready` verifies Rules Core PostgreSQL readiness.
- `/api/integration/session` exposes the target-scoped Tool Host authentication
  context to authorized backend callers.
- `/` is service metadata only and no longer advertises a frontend module.

Rules Core does not serve static UI assets after this split.
