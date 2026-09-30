# Rules Core architecture

## Purpose

Rules Core is a separately deployable Dorks & Dice Tool. It owns source ingestion and storage, canonical source recognition, Rules Layer decisions, source-access grants, ruleset revisions, and rule resolution. The main Dorks & Dice site owns account identity, global site roles, mode-scoped account roles, campaign membership and authority, site-mode resolution, and Tool registration/routing.

The central architectural boundary is that **source material is preserved independently from canonical recognition and independently from Rules Layer adjudication**.

## Rules Wiki integration boundary

Rules Wiki is not a consumer of Rules Core's public API.

The standing integration rule is:

> **Every Rules Wiki -> Rules Core request uses the private first-party Tool-to-Tool API. Rules Wiki must not call Rules Core's stable external consumer API.**

The normal hosted path is:

`browser -> Rules Wiki -> Site Tool-to-Tool delegation -> Rules Core internal API`

Rules Wiki has this unique internal access because it is the first-party human presentation and authoring application for Rules Core. Public Rules Core APIs exist for other Dorks & Dice Tools and independent consumers. A Rules Wiki requirement is therefore never, by itself, justification for adding a capability to the public API.

Wiki-specific reference catalogs, source/history detail, semantic comparison support, class-family relationships, presentation projections, Rules Lawyer workflows, source administration, and other human-facing support contracts remain internal Tool-to-Tool contracts. If Rules Wiki needs semantic information that it can not derive safely, Rules Core should add or extend the internal contract. Promotion to the public API requires a separately reviewed independent non-Wiki consumer need.

The internal boundary does not bypass domain authorization. Rules Core still enforces the delegated user's source grants, campaign membership, Rules Lawyer authority, campaign-DM authority, publication state, and all other applicable rules. The Site delegation capability establishes the allowed Tool-to-Tool caller; it does not replace end-user authorization.

Rules Core may reuse the same application/domain services behind internal and external HTTP contracts. Network/API compatibility is still separate: Rules Wiki depends only on the internal contract, never on the public route simply because an equivalent operation exists there.

## Content pipeline

```text
physical source bytes
        |
        v
format adapter
        |
        v
normalized source records + publication evidence
        |
        v
immutable Source Layer persistence
(SourcePackage / SourceRepresentation / SourceEntity / SourceEntityRevision)
        |
        v
canonical recognition
(CanonicalPublication / CanonicalEntity / CanonicalSourceOccurrence)
        |
        v
Rules Layer adjudication
        |
        v
published Dorks & Dice ruleset revision
        |
        v
campaign Rules Layer overrides
        |
        v
resolved rules API
```

Adapter output is a persistence boundary, not a universal source-document format. Each adapter may preserve its native source in a representation-appropriate `RawJson` projection while the original artifact bytes remain immutable in the global content-addressed `SourceContentBlob` store. Private `SourceRepresentation` rows retain package membership and provenance and reference that blob by SHA-256. `SemanticJson` is optional comparison evidence and never replaces the stored source body.

Current persistent adapters include native 5e.tools-shaped JSON, PCGen 3.x PCC/LST data, and text-readable PDFs. New formats enter through the same adapter-neutral contract rather than adding another source hierarchy.

## Source Layer

The persisted Source Layer is intentionally small:

- `SourcePackage` is the distribution and access boundary. It carries provider, license, visibility, and user grants.
- `SourceRepresentation` is one immutable physical artifact/version, including original bytes, format, origin identity, URI/media metadata, content hash, and optional predecessor representation.
- `SourceEntity` is one stable source-native identity within a package and format. Its `NativeKey` and `NativeIdentityJson` preserve source-specific identity rather than a Dorks & Dice ruling.
- `SourceEntityRevision` is an immutable observed body for that source entity and points to the physical representation that supplied it.

There is no persisted `SourceWork` or `SourceEdition` parent in the current model. Older `WorkKey`, `EditionKey`, and related request fields remain only on compatibility import surfaces and bootstrap catalog inputs; they must not be treated as durable database identity.

A valid source import does not depend on prior canonical recognition. When canonical reconciliation encounters a recognized identity conflict, Rules Core preserves the already-valid package, representation, entity, and revision, rolls back only the affected canonical-reconciliation group, and returns an explicit reconciliation issue. Database failures, malformed input, and immutable native-identity violations still fail the import.

## Canonical recognition

Canonical records are shared recognition metadata above the access-scoped Source Layer:

- `CanonicalPublication` identifies a real publication independently of package or file format.
- `CanonicalEntity` identifies a rule-bearing entity across representations when there is sufficient evidence of identity.
- `CanonicalSourceOccurrence` records an entity occurrence within a canonical publication.
- canonical aliases and relationships record strong source-lineage evidence and explicit revision/reprint/rename/variant history.

Canonical IDs, fingerprints, aliases, and relationship records do not contain a globally readable substitute for restricted source text and do not grant access to any `SourcePackage`.

Exact semantic evidence may associate equivalent representations automatically. Different formats may encode the same mechanics differently, so a bootstrap-confirmed trusted source-lineage alias may also associate different semantic projections with one canonical entity. Mechanical changes remain distinct canonical entities with explicit relationships rather than being collapsed by name.

## Source and access boundaries

Open/distributable sources may be bundled or imported where their licenses permit it. Restricted material enters at a user's direction through supported imports/connections and remains package-scoped.

Core invariant:

> Recognition may be shared. Permission must not be shared.

Current-user source packages are shared by logical source origin rather than by account. Identical uploads therefore converge by content identity, and users importing the same Web source URL point at the same package, representations, entities, and revisions while retaining independent registrations and grants. Byte-identical artifacts from different logical origins still share one content-addressed `SourceContentBlob` without merging those origins. Runtime access remains grant-scoped; sharing stored source data does not grant another account access to it.

## Rules Layers

1. **Source Layer** — immutable representations of what each source says.
2. **Canonical recognition** — shared evidence that source-specific records represent the same publication/entity or have an explicit historical relationship.
3. **Global Rules Layer** — Dorks & Dice decisions curated by users with the Dorks-mode `Rules Lawyer` authority.
4. **Campaign Rules Layer** — campaign-specific decisions/overrides controlled by the campaign's authorized DM/editor.
5. **Resolved Rules** — generated effective rules for a user/campaign/source-access context.

A `RuleConcept` is deliberately separate from canonical source identity. Canonical recognition answers what source objects are; the Rules Layer answers how Dorks & Dice uses them. A Rules Lawyer may bind multiple canonical/source implementations to one concept, select one, consolidate several, or record an explicit override without rewriting source history.

## Authorization axes

Rules Core must answer two independent questions for every relevant operation:

- **May this identity make this change?** Dorks & Dice mode-scoped Rules Lawyer authority or campaign-scoped authority comes from Dorks & Dice.
- **May this identity access this source content?** Source grants are enforced by Rules Core against `SourcePackage`.

UI visibility is not an authorization boundary. API and runtime resolution paths enforce both requirements independently.

For internal Rules Wiki requests, Rules Core additionally verifies that the request arrived through the authorized first-party Tool-to-Tool boundary. That caller restriction is independent from, and additive to, the delegated user's domain authorization.

## Database and runtime storage

Rules Core uses external PostgreSQL. Original artifact bytes are stored once in `source_content_blob`, keyed by SHA-256. `source_representation` retains package-scoped provenance, format, origin, URI/media metadata, byte length, and the content digest that references the shared blob. Other relational tables store package access, source-native identities and revisions, canonical recognition metadata, Rules Layer decisions, and published rulesets.

Deduplication is deliberately below the authorization boundary. A shared current-user package may have grants for several accounts, while each account retains its own registration metadata such as the submitted Web URL and refresh state. Different origins that happen to contain identical bytes share only the content blob. Import responses do not disclose which other accounts, if any, already reference the same package or blob.

## Bootstrap and maintenance

Baseline bootstrap hydrates reviewed public SRD snapshots and the Dorks & Dice house-rule baseline without requiring a live upstream host at runtime. It also performs a narrowly scoped Rules Layer synchronization for skill/tool competencies found in the exact checked-in reviewed SRD representations. That synchronization still passes through normalized canonical identity, Rule Concepts, canonical bindings, Global Rule decisions, and immutable ruleset publication; the Character mechanics consumer does not read unpublished Source Layer skills directly. Arbitrary imports and hosted-source refreshes do not receive this bootstrap publication treatment. Hosted-source definitions and authority references remain maintenance/acquisition metadata; they do not replace immutable Source Layer representations or canonical identity.

The 3e/3.5e developer reconciliation workflow uses the same persistent adapters and canonical stores as normal imports. Its reusable output is canonical identity knowledge, not a global cache of restricted source bodies.

## Implementation module boundaries

Rules Core keeps externally consumed contracts stable while organizing implementations by responsibility.

- Web composition is registered through focused service groups for persistence, sources, rules, Character mechanics, and bootstrap rather than one expanding `Program.cs` registration block.
- Character-facing services are coordinators. Catalog construction, evaluation, source/provenance loading, support parsing, resolved-rules reading, and projection families live in focused modules behind the same public application contracts.
- Character projection separates abilities/proficiency, weapons, standard Armor Class, 3.x combat, initiative/saves, competencies, spellcasting, health, prerequisites, legacy placeholders, and source-rule projection modules. Shared state remains in the projection context instead of being duplicated across resolvers.
- Adjudication keeps workflow/state transitions separate from persistence and evidence/query construction.
- Source registration/import orchestration remains separate from remote HTTP/GitHub acquisition. Hosted and current-user remote resolvers share one remote-URI safety policy without sharing authorization state.
- Source format adapters remain independent implementations behind the adapter registry.

A long file is not split solely by size. Cohesive translation pipelines, import transactions, schema initialization, and shared projection state may remain substantial when dividing them would scatter one responsibility.

## Frontend boundary

The human frontend is owned by Rules Wiki. Rules Core remains headless. Presentation uses the explicit render lifecycle documented in the Rules Wiki repository and does not make Rules Core a browser application again.

Rules Core supplies authoritative semantics through the private Rules Wiki Tool-to-Tool contracts and supplies stable external consumer contracts to other Tools. Those are separate compatibility surfaces even when they reuse the same underlying domain services.

## Rules Wiki reference read model

Rules Wiki needs a complete human-reference view that spans accessible source history even when some source concepts have not entered the Rules Layer. That read model sits downstream of Source Layer access and canonical recognition but does not become another adjudication layer.

The first-party reference read model is an **internal Rules Wiki Tool-to-Tool contract**, not part of Rules Core's public consumer API. It groups accessible canonical histories for browsing, history, facets, search, category membership, read-only semantic comparison, class-family relationships, and other presentation support. Existing `/api/wiki/...` route names are historical implementation details and do not make those contracts external.

`revision` and `rename` relationships can form one evolving logical reference; `variant` and `reprint` remain distinct. Each variation retains its exact source revision, canonical publication, edition, package, and source provenance while exposing one canonical mechanical category.

Canonical source-only histories use stable `canonical:{canonicalEntityId}` identities. Accessible source occurrences whose canonical reconciliation is intentionally unresolved and whose `canonical_entity_id` remains null stay browseable as isolated provisional histories using deterministic `occurrence:{canonicalSourceOccurrenceId}` identities. They are never grouped by loose name matching, and reading either kind of source-only identity does not create a `RuleConcept` or Rules Layer decision.

Terminology aliases that describe the same mechanical category normalize completely for Wiki presentation: Race/Species becomes `species`, and Subrace/Subspecies becomes `subspecies`. Immutable raw source data still preserves its original source terminology. Mechanically distinct categories remain distinct, so a 3.5e `prestigeClass` variation is not rewritten as a 5e `subclass` variation merely because both belong to one logical history.

A revision/rename history has one authoritative Rules Layer anchor. When legacy data contains multiple RuleConcepts bound within the same history component, Rules Core prefers a RuleConcept bound to a root canonical entity in the directed history graph. Stable concept-key ordering is used only as a deterministic compatibility fallback when a unique rooted binding is unavailable. Decision creation timestamps do not select which RuleConcept represents the history. Published global or campaign decisions select the effective source variation for the authoritative concept; they do not establish history ownership.

If no published selection applies, the Wiki read model chooses a deterministic newest accessible fallback for presentation and labels it `unresolved-fallback`. The fallback does not create a `RuleConcept`, decision, or publication and does not turn source-only material into an effective consumer rule.

Catalog construction applies authorization and logical grouping over lightweight metadata. Full mechanical documents are loaded only for the variation projected into each requested catalog row; detail/history loads the documents for that one accessible logical history. `EffectiveVariation` is the source variation selected by the current global/campaign/default rules scope. `BrowseVariation` is the variation used to populate the current catalog row. In `categoryMode=effective`, they normally coincide. In `categoryMode=any`, source, package, edition, type-specific browser fields, and row metadata come from a historical variation that matched the requested browse category and applicable filters, while `EffectiveCategory` continues to describe the effective rules type. Campaign `overridesOnly` filtering is evaluated in the same authoritative read model rather than reconstructing facets with per-reference detail requests.

The source grant boundary applies before grouping and before any facet/count/detail/comparison result is produced. Canonical identity may be shared globally, but inaccessible source material can not leak through reference membership, counts, history, fallback selection, or comparison.

This read model is intentionally separate from `/api/rules`. Game Tools may consume the effective public consumer API. Rules Wiki does not consume `/api/rules` or any other public consumer endpoint; it uses its internal read model instead.

## Runtime consumer API boundary

Rules Core is the authoritative runtime rule-resolution boundary. The Source Layer preserves immutable source truth and provenance. The Rules Layer owns adjudication, published global rulings, and campaign overrides. Rules Core combines those inputs into the effective rule consumed by game tools.

Normal game tools consume effective results only. They do not receive competing source/profile implementations and do not select an edition, source revision, competency profile, or facet as a second rules engine. Provenance on an effective result is still valid and should identify the selected source/ruling without exposing alternatives as consumer choices.

Rules Wiki is explicitly **not** one of these public API consumers. It uses the private first-party Tool-to-Tool API because it must inspect and administer richer Rules Core state for human presentation.

Rules Lawyer and authorized DM/admin surfaces are intentionally richer. They may inspect source revisions, competing implementations, profiles, facets, semantic differences, and adjudication state because those details are inputs to a Rules Core decision rather than runtime choices for a game tool. Rules Wiki reaches these surfaces through the internal API only.

When no formal effective ruling exists, Rules Core selects one accessible, non-ignored source revision deterministically and returns it as a temporary effective result with `resolution.state = "unresolved-fallback"`, `isFallback = true`, and `requiresAdjudication = true`. The fallback is not persisted as a Rules Lawyer decision. A later published ruling replaces it automatically on the next request.

Consumers must not persist Rules Core rulings as their own authoritative rule cache. Ordinary HTTP/runtime caching may still be used where safe, but the next Rules Core request is authoritative.

### API classification

Normal external tool-consumer surfaces include:

- `GET /api/rules` and `GET /api/rules/{conceptKey}`;
- campaign equivalents under `/api/campaigns/{campaignId}/rules`;
- `POST /api/rules/character-mechanics/resolve` and its campaign equivalent;
- `GET /api/rules/mechanics` and generic mechanic evaluation routes only through their effective-only compatibility DTO/validation boundary;
- Character support and recovery semantic operations;
- travel/environment effective catalog and resolution routes under `/api/rules/travel-environment` and their campaign equivalents. Travel definitions are structured in Rules Core; expedition state remains downstream.

These routes are not Rules Wiki dependencies.

Rules Wiki internal surfaces include all reference browsing, source/history inspection, version comparison, authoring, adjudication, normalization, source administration, and Wiki-specific presentation projections. They must require the first-party Tool-to-Tool boundary in addition to their normal user/source/campaign authority checks.

Internal services retain rich source/profile/provenance contracts. Hiding alternatives from normal consumers does not delete or collapse that information inside Rules Core.

See `docs/api-boundaries.md` for the normative external-versus-internal API rule. See `travel-environment-mechanics.md` for the travel/environment consumer contract, reviewed source projections, and Hex Crawl integration boundary.
