# Rules Core API boundaries

Rules Core has two deliberately separate HTTP surfaces:

1. a stable public consumer API for ordinary Dorks & Dice Tools and independent Rules Core consumers; and
2. a private first-party API reachable only through explicitly configured source-to-target private Tool tunnels.

The central invariants are:

> **Rules Wiki does not consume Rules Core's public API. Rules Wiki reaches Rules Core directly through its private deployment tunnel.**

> **Ordinary Tool-to-Tool delegation does not grant access to Rules Core private APIs.**

> **Rules Wiki is a UI application, not an API gateway. Rules Wiki does not proxy Rules Core APIs or expose a general API of its own.**

These are standing architectural rules. A capability needed only by Rules Wiki must not be added to the public Rules Core API merely because Rules Wiki needs an HTTP contract. Promotion of a private capability to the public API requires a separately reviewed consumer need outside the private integration.

## 1. Stable public consumer API

The public API exists for Character Sheet, Block Initiative, Hex Crawl, future game Tools, and other consumers that do not have a private integration with Rules Core.

Published consumer endpoint families include resolved rule/catalog reads and game-mechanics resolution contracts such as:

- `/api/rules` and `/api/rules/{conceptKey}`;
- `/api/campaigns/{campaignId}/rules` and resolved campaign-rule reads;
- the published source/catalog reads under `/api/sources`;
- character mechanics and character projection contracts;
- crafting and harvesting resolution contracts; and
- travel/environment resolution contracts.

`/api/rules` is intentionally a resolved consumer contract. It exposes the effective published ruleset expected by game Tools. Rules Wiki must not use it for browsing, detail, comparison, fallback, source history, authoring, or other Wiki workflows even when equivalent information could technically be reconstructed from it.

The transport boundary is private-by-default. Existing public route patterns are explicitly listed by `RulesCoreApiBoundary`, and future public routes may be deliberately marked with `PublicRulesCoreApi`. A newly mapped `/api` route is therefore private unless its public status is an explicit code change.

This protects the stable public contract from accidental expansion to satisfy first-party UI needs.

## 2. Private first-party API

Rules Core owns the semantic read/write models used by Rules Wiki. Those contracts are private Rules Core interfaces and are not part of the stable public compatibility promise.

The deployment path is:

```text
browser -> Rules Wiki
             |
             | direct private source-to-target tunnel
             v
          Rules Core private API
```

Site remains the identity and authorization control plane, not the HTTP data plane for the Rules Wiki -> Rules Core request. A Rules Wiki backend request obtains a short-lived Rules Core target-scoped authentication ticket through the Site control plane, then sends the actual request directly to Rules Core over the configured private deployment path. Rules Core redeems the ticket through Site introspection and receives trusted private-tunnel source provenance.

A private relationship is configured independently for each source/target pair. If another Tool needs a private Rules Core integration, that Tool receives its own explicitly configured private tunnel. It does not inherit private access because it can use normal Tool delegation or because it can consume the public Rules Core API.

At the transport boundary:

- a public Rules Core endpoint is reachable according to its normal endpoint authorization rules;
- a private endpoint without Tool authentication returns `401`;
- a private endpoint with ordinary Tool authentication or ordinary Tool-to-Tool delegation returns `403`;
- a private endpoint requires a target-scoped context carrying Site-issued private-tunnel provenance; and
- domain authorization still applies after the transport boundary is crossed.

Private does not mean authorization-free. Rules Core continues to enforce source grants, campaign membership, Rules Lawyer authority, campaign-DM authority, publication state, and every other domain authorization requirement.

Historically some private read routes use `/api/wiki/...` names. Route spelling does not make a contract public. Those are Rules Core private API routes for the Rules Wiki integration and must remain behind the private transport boundary.

The existing Wiki reference read model includes contracts for:

- browsing the global accessible reference catalog;
- reading accessible source/history detail for one logical reference;
- campaign-scoped reference browsing and detail;
- read-only semantic comparison;
- class-family and other presentation-support relationships; and
- presentation projections that are semantically unsafe for Rules Wiki to derive.

Rules Wiki authoring and maintenance capabilities are part of the same private boundary, including:

- global and campaign rule authoring, preview, comparison, and publication;
- Rules Lawyer/adjudication work queues and workspace scope operations;
- source browser, current-user source, source revision review, normalization, versioning, hosted-source, acquisition, and source-administration workflows;
- source-update review and campaign baseline authoring.

There is no architectural distinction where Wiki read APIs are public but Wiki mutation APIs are private. **All Rules Wiki -> Rules Core APIs are private.**

### Reference-read semantics

Rules Core remains authoritative for source grants, canonical identity, publication and edition metadata, source occurrence history, concept relationships, effective global and campaign resolution, unresolved fallback selection, server-authoritative facets, and semantic comparison. Rules Wiki renders those semantics; it does not reconstruct them from public consumer records.

Canonical source-only histories use stable `canonical:{canonicalEntityId}` reference identities. Accessible occurrences that are still unresolved by canonical reconciliation remain browseable as isolated provisional histories using deterministic `occurrence:{canonicalSourceOccurrenceId}` identities. They are not grouped through loose name matching. Reading either identity does not create a RuleConcept, Rules Layer decision, or publication.

Authoritative `revision` and `rename` relationships form the evolving logical history used for Rules Layer participation. `variant` and `reprint` do not automatically make another canonical entity interchangeable for effective selection. If legacy data has multiple RuleConcepts bound inside one revision/rename component, the history representative is selected semantically: prefer a concept bound to a root canonical entity in the directed history graph, then use stable concept-key ordering only as a deterministic compatibility fallback when a unique rooted binding is unavailable. Decision creation timestamps do not choose the history representative.

Source grants remain the content-read authorization boundary. A source package that is not accessible to the delegated user identity must not leak through reference results, search, facets, counts, history, comparison, fallback selection, or canonical grouping.

Reading accessible source variations is not a Rules Lawyer operation. Rules Lawyer or campaign authority is still required by mutation, adjudication, and publication services. Read-only comparison reuses Rules Core's semantic comparison implementation while preserving source-access checks.

The effective/default reference variation is a scope projection, not a second source history. A published Rules Layer decision on the authoritative history anchor selects the exact effective source variation when one exists. If no applicable decision exists, Rules Core deterministically selects the newest accessible applicable variation as an unresolved fallback without creating or persisting a rule decision.

Catalog rows distinguish two variation roles. `EffectiveVariation` is the variation selected by the current global/campaign/default rules scope. `BrowseVariation` is the variation whose source, package, edition, and type-specific browser metadata populate the current catalog row. In `categoryMode=effective`, they normally coincide. In `categoryMode=any`, the browse projection comes from a historical variation matching the requested browse category and applicable source/package/edition filters while `EffectiveCategory` continues to report the effective rules type. Full mechanical JSON is hydrated only for the variation projected into each requested catalog row; detail and history hydrate the accessible variations in that one logical reference.

Terminology normalization remains distinct from mechanical-category evolution. `race`/`species` normalize to `species`, and `subrace`/`subspecies` normalize to `subspecies`. By contrast, a `prestigeClass` -> `subclass` transition remains a real change of mechanical category even when authoritative revision/rename history proves that the variations are successive forms of one evolving logical concept.

## 3. Rules Wiki is not an API gateway

Rules Wiki is the UI frontend for Rules Core, but it is not a transparent or path-for-path proxy for Rules Core.

The browser interacts with the Rules Wiki web application. Rules Wiki server-side code uses focused internal clients to call the Rules Core private API over its private tunnel. Rules Wiki must not implement `/api/{**path}` forwarding, mirror the Rules Core route tree, or relay arbitrary Rules Core requests on behalf of the browser.

If browser-side interaction requires a server action in Rules Wiki, that action remains part of the Rules Wiki application implementation rather than creating a general Rules Wiki API surface. The Rules Wiki backend chooses the specific Rules Core operation it needs and performs it through the private client.

If a public and private operation share semantics, Rules Core should reuse application/domain services behind both boundaries rather than requiring Rules Wiki to call the public endpoint.

## 4. Site control plane and private-tunnel provenance

Site remains authoritative for Tool identity and target-scoped user context.

Normal Tool delegation and private tunnels are separate relationships:

- normal delegation uses the registered Tool delegation allowlist and may use the Site delegation gateway;
- private tunnel authorization is deployment configuration for one explicit source/target pair;
- normal delegation provenance (`DelegatedFromToolKey`) does not satisfy the Rules Core private API boundary; and
- private access requires `PrivateTunnelSourceToolKey` in the target-scoped context issued by Site.

Rules Core trusts the private-tunnel provenance only because it comes from redemption of a short-lived target-scoped Site ticket. A caller can not grant itself private access by supplying an HTTP header or source Tool name directly.

The private network path and the target-scoped identity check are complementary controls. Network reachability alone is not private authorization, and a valid identity credential does not replace deployment isolation.

## 5. Administrative/service API

Administrative source import and control-plane capabilities remain Rules Core domain responsibilities. Whether a particular administrative operation is available to a first-party Tool is determined through its private integration and normal authority checks, not by broadening the public API.

`/api` remains service metadata.

## 6. Hosting and health API

- `/health` is the process health endpoint.
- `/ready` verifies Rules Core PostgreSQL readiness.
- `/api/integration/session` exposes the target-scoped Tool Host authentication context to authorized backend callers.
- `/` is service metadata only and does not advertise a frontend module.

Rules Core does not serve Rules Wiki static UI assets.
