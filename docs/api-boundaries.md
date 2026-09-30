# API ownership after the Rules Wiki split

The Rules Wiki split establishes a strict API boundary between Rules Core's stable
external consumer API and the private first-party API used by Rules Wiki.

The central invariant is:

> **Rules Wiki does not consume Rules Core's public/external API. Every Rules Wiki -> Rules Core request uses the delegated first-party internal Tool-to-Tool API.**

This is a standing architectural rule, not a preference for new work. The public API
exists for other Dorks & Dice Tools and independent Rules Core consumers. Rules Wiki
has a unique delegated Tool-to-Tool relationship with Rules Core and therefore has no
reason to depend on the public consumer surface.

A capability needed only by Rules Wiki must not be added to the external API merely
because it needs an HTTP contract. It belongs to the internal Rules Wiki API. An
internal handler may reuse the same Rules Core application/domain services as an
external endpoint, but the network contract exposed to Rules Wiki remains internal.
Promotion of an internal capability to the external API requires a separately reviewed,
independent non-Wiki consumer need.

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
published ruleset expected by game Tools. Rules Wiki must not use it for browsing,
detail, comparison, fallback, source-history, or any other Wiki workflow even when the
same information could technically be obtained from it.

`browserLink` remains part of the external response contract, but its human-facing
Tool target is `rules-wiki`.

## 2. Rules Wiki first-party internal API

Rules Core owns the semantic read/write models used by Rules Wiki, but Rules Wiki
reaches them only through delegated Tool-to-Tool access. These contracts are private
first-party integration surfaces between `rules-wiki` and `rules-core`; they are not
part of Rules Core's stable external compatibility promise.

The browser never calls Rules Core directly. The normal path is:

`browser -> Rules Wiki -> Site Tool-to-Tool delegation -> Rules Core internal API`

The delegation capability remains server-side. Rules Core still enforces the delegated
user's source grants, campaign membership, Rules Lawyer authority, campaign-DM
authority, publication state, and every other domain authorization requirement. Internal
means restricted to the first-party Tool relationship; it does not mean authorization-
free.

The existing Wiki reference read model includes contracts for:

- browsing the global accessible reference catalog;
- reading accessible source/history detail for one logical reference;
- campaign-scoped reference browsing and detail;
- read-only semantic comparison;
- class-family and other presentation-support relationships;
- presentation projections that are semantically unsafe for Rules Wiki to derive.

Historically some of these routes use `/api/wiki/...` names. Route spelling alone does
not make a contract external. Wiki-specific routes must be protected and treated as
internal Tool-to-Tool contracts. New work must not make them generally consumable in
order to satisfy a Rules Wiki requirement.

Rules Wiki authoring and maintenance capabilities are part of the same internal
boundary, including:

- global and campaign rule authoring, preview, comparison, and publication;
- Rules Lawyer/adjudication work queues and workspace scope endpoints;
- source browser, current-user source, source revision review, normalization,
  versioning, hosted-source, acquisition, and source-administration workflows;
- source-update review and campaign baseline authoring.

There is no architectural distinction where Wiki read APIs are public but Wiki mutation
APIs are internal. **All Rules Wiki -> Rules Core APIs are internal.**

### Reference-read semantics

Rules Core remains authoritative for source grants, canonical identity, publication and
edition metadata, source occurrence history, concept relationships, effective global and
campaign resolution, unresolved fallback selection, server-authoritative facets, and
semantic comparison. Rules Wiki renders those semantics; it does not reconstruct them
from raw source records.

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
is not accessible to the delegated user identity must not leak through reference
results, search, facets, counts, history, comparison, fallback selection, or canonical
grouping.

Reading accessible source variations is not a Rules Lawyer operation. Rules Lawyer or
campaign authority is still required by the mutation, adjudication, and publication
services. Read-only comparison reuses Rules Core's semantic comparison implementation
while preserving source-access checks.

The effective/default reference variation is a scope projection, not a second source
history. A published Rules Layer decision on the authoritative history anchor selects
the exact effective source variation when one exists. If no applicable decision exists,
Rules Core deterministically selects the newest accessible applicable variation as an
unresolved fallback without creating or persisting a rule decision.

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

## 3. Browser-facing Rules Wiki API versus Rules Core internal API

Rules Wiki may expose its own browser-facing `/api/*` routes as part of the Rules Wiki
application contract. Those routes terminate at Rules Wiki. The thin Rules Wiki backend
adapter then calls Rules Core through the internal Tool-to-Tool boundary.

A browser-facing Rules Wiki route and a Rules Core internal route do not need to share
the same path or DTO. Rules Wiki may preserve browser compatibility while the internal
contract evolves with the first-party integration.

Rules Wiki must not implement its backend adapter as a dependency on Rules Core's
stable external API. If an external and internal operation share semantics, Rules Core
should reuse application/domain services behind both boundaries rather than requiring
Rules Wiki to call the public endpoint.

## 4. Administrative/service API

Administrative source import/control-plane capabilities remain Rules Core domain
responsibilities. Whether a particular administrative operation is available to Rules
Wiki is determined through the internal Tool-to-Tool contract and normal authority
checks, not by broadening the external API.

`/api` remains service metadata.

## 5. Hosting and health API

- `/health` is the process health endpoint.
- `/ready` verifies Rules Core PostgreSQL readiness.
- `/api/integration/session` exposes the target-scoped Tool Host authentication
  context to authorized backend callers.
- `/` is service metadata only and does not advertise a frontend module.

Rules Core does not serve Rules Wiki static UI assets.
