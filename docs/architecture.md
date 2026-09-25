# Rules Core architecture

## Purpose

Rules Core is a separately deployable Dorks & Dice Tool. It owns source ingestion and storage, canonical source recognition, Rules Layer decisions, source-access grants, ruleset revisions, and rule resolution. The main Dorks & Dice site owns account identity, global site roles, campaign membership and authority, site-mode resolution, and Tool registration/routing.

The central architectural boundary is that **source material is preserved independently from canonical recognition and independently from Rules Layer adjudication**.

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

- **May this identity make this change?** Global Rules Lawyer authority or campaign-scoped authority comes from Dorks & Dice.
- **May this identity access this source content?** Source grants are enforced by Rules Core against `SourcePackage`.

UI visibility is not an authorization boundary. API and runtime resolution paths enforce both requirements independently.

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

The frontend is intentionally downstream of backend contracts and uses the explicit render lifecycle documented in `frontend-render-lifecycle.md`. The shared UI vocabulary now includes compact page leads, section headings, toolbars, filter/action bars, fields, and list/detail workspaces.

The Rules Library follows the same coordinator/module pattern as the backend: browser state/loading, index rendering, detail/version comparison, and routing are separate modules. Rule rendering exposes a small registry facade over shared rendering support and specialized entity renderers. Maintenance and source-management views use the same compact primitives without forcing every workflow into the Rules Library's list/detail interaction model.

## Runtime consumer API boundary

Rules Core is the authoritative runtime rule-resolution boundary. The Source Layer preserves immutable source truth and provenance. The Rules Layer owns adjudication, published global rulings, and campaign overrides. Rules Core combines those inputs into the effective rule consumed by game tools.

Normal game tools consume effective results only. They do not receive competing source/profile implementations and do not select an edition, source revision, competency profile, or facet as a second rules engine. Provenance on an effective result is still valid and should identify the selected source/ruling without exposing alternatives as consumer choices.

Rules Lawyer and authorized DM/admin surfaces are intentionally richer. They may inspect source revisions, competing implementations, profiles, facets, semantic differences, and adjudication state because those details are inputs to a Rules Core decision rather than runtime choices for a game tool.

When no formal effective ruling exists, Rules Core selects one accessible, non-ignored source revision deterministically and returns it as a temporary effective result with `resolution.state = "unresolved-fallback"`, `isFallback = true`, and `requiresAdjudication = true`. The fallback is not persisted as a Rules Lawyer decision. A later published ruling replaces it automatically on the next request.

Consumers must not persist Rules Core rulings as their own authoritative rule cache. Ordinary HTTP/runtime caching may still be used where safe, but the next Rules Core request is authoritative.

### API classification

Normal tool-consumer surfaces include:

- `GET /api/rules` and `GET /api/rules/{conceptKey}`;
- campaign equivalents under `/api/campaigns/{campaignId}/rules`;
- `POST /api/rules/character-mechanics/resolve` and its campaign equivalent;
- `GET /api/rules/mechanics` and generic mechanic evaluation routes only through their effective-only compatibility DTO/validation boundary;
- Character support and recovery semantic operations.

Rules Lawyer/admin surfaces include:

- `GET /api/rules/{conceptKey}/versions`;
- `GET /api/admin/rules/mechanics`;
- `GET /api/campaigns/{campaignId}/admin/rules/mechanics`;
- source comparison, authoring, adjudication, normalization, and publication workflows already protected by their existing authority checks.

Internal services retain rich source/profile/provenance contracts. Hiding alternatives from normal consumers does not delete or collapse that information inside Rules Core.
