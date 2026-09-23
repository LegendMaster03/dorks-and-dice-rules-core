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

Two users may independently import representations of the same publication and reuse the same canonical publication/entity identities while retaining separate packages, representation/provenance rows, source entities, revisions, and grants. Byte-identical physical artifacts share one content-addressed `SourceContentBlob`; blob reuse does not merge packages, grants, source URLs, filenames, or import history. Runtime substitution may use an accessible representation of the same canonical entity, but it must never expose another user's inaccessible representation.

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

Content-addressed reuse is deliberately below the authorization boundary: deduplicating bytes never deduplicates grants or exposes another package's representation/provenance. Import responses do not disclose whether another account already caused a blob to exist.

## Bootstrap and maintenance

Baseline bootstrap hydrates reviewed public SRD snapshots and the Dorks & Dice house-rule baseline without requiring a live upstream host at runtime. It also performs a narrowly scoped Rules Layer synchronization for skill/tool competencies found in the exact checked-in reviewed SRD representations. That synchronization still passes through normalized canonical identity, Rule Concepts, canonical bindings, Global Rule decisions, and immutable ruleset publication; the Character mechanics consumer does not read unpublished Source Layer skills directly. Arbitrary imports and hosted-source refreshes do not receive this bootstrap publication treatment. Hosted-source definitions and authority references remain maintenance/acquisition metadata; they do not replace immutable Source Layer representations or canonical identity.

The 3e/3.5e developer reconciliation workflow uses the same persistent adapters and canonical stores as normal imports. Its reusable output is canonical identity knowledge, not a global cache of restricted source bodies.

## Frontend boundary

The frontend is intentionally downstream of these contracts. Application-owned DOM uses the explicit render lifecycle documented in `frontend-render-lifecycle.md`. Backend source, canonical, and Rules Layer contracts should stabilize before the deferred UI pass is updated to expose new reconciliation and source-model behavior.
