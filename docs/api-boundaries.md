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

Canonical source-only histories use stable `canonical:{canonicalEntityId}` reference
identities. Accessible occurrences that are still unresolved by canonical reconciliation
remain browseable as isolated provisional histories using deterministic
`occurrence:{canonicalSourceOccurrenceId}` identities. They are not grouped through
loose name matching. Reading either identity does not create a RuleConcept, Rules
Layer decision, or publication.

Authoritative `revision` and `rename` relationships form the evolving logical history
used for Rules Layer participation. `variant` and `reprint` do not automatically make
another canonical entity interchangeable for effective selection. If legacy data has
multiple RuleConcepts bound inside one revision/rename component, the history
representative is selected semantically: prefer a concept bound to a root canonical
entity in the directed history graph, then use stable concept-key ordering only as a
deterministic compatibility fallback when a unique rooted binding is unavailable.
Decision creation timestamps do not choose the history representative.

Source grants remain the content-read authorization boundary. A source package that
is not accessible to the request identity must not leak through reference results,
search, facets, counts, history, comparison, fallback selection, or canonical
grouping. Anonymous requests see only public packages.

Reading accessible source variations is not a Rules Lawyer operation. Rules Lawyer
or campaign authority is still required by the existing mutation, adjudication, and
publication endpoints. The read-only Wiki comparison endpoint reuses Rules Core's
semantic comparison implementation while preserving source-access checks.

The effective/default reference variation is a scope projection, not a second source
history. A published Rules Layer decision on the authoritative history anchor selects
the exact effective source variation when one exists. If no applicable decision
exists, Rules Core deterministically selects the newest accessible applicable variation
as an unresolved fallback without creating or persisting a rule decision.

Catalog rows distinguish two variation roles. `EffectiveVariation` is the variation
selected by the current global/campaign/default rules scope. `BrowseVariation` is the
variation whose source, package, edition, and type-specific browser metadata populate
the current catalog row. In `categoryMode=effective`, they normally coincide. In
`categoryMode=any`, the browse projection comes from a historical variation matching
the requested browse category and applicable source/package/edition filters while
`EffectiveCategory` continues to report the effective rules type. Full mechanical JSON
is hydrated only for the variation projected into each requested catalog row; detail
and history hydrate the accessible variations in that one logical reference.

Terminology normalization remains distinct from mechanical-category evolution.
`race`/`species` normalize to `species`, and `subrace`/`subspecies` normalize to
`subspecies`. By contrast, a `prestigeClass` -> `subclass` transition remains a real
change of mechanical category even when authoritative revision/rename history proves
that the variations are successive forms of one evolving logical concept.

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
