# Source Layer

The Source Layer is the immutable record of what imported source material says before any Dorks & Dice adjudication, merging, or campaign override is applied.

## Persistence model

The first vertical slice stores five relational identities in PostgreSQL:

1. `source_package` - an acquired/imported package and its distribution metadata.
2. `source_work` - a work contained by the package.
3. `source_edition` - an edition/release of that work.
4. `source_entity` - a source-specific entity identity within an edition.
5. `source_entity_revision` - immutable JSON revisions of that entity.

Package, work, and edition keys are normalized at the application boundary. Source entity identity is based on the 5e.tools array/property name plus source code, entity name, and an upstream `uniqueId`/`id` when one is present. The database ID is the local persistent identifier; the natural key and revision fingerprint provide stable content identity.

## Lossless 5e.tools ingestion

`ISourceImportService` accepts a 5e.tools-shaped JSON document. Top-level entity arrays are imported without projecting the entity into a fixed application schema. The complete entity object is stored as PostgreSQL `jsonb`, so fields unknown to Rules Core survive ingestion and can be returned later.

Each entity also receives a SHA-256 fingerprint computed from a canonical JSON representation. Object property ordering does not affect the fingerprint. Array ordering remains significant. Reimporting semantically identical JSON is idempotent; changed content creates the next immutable revision instead of updating an existing revision.

Source package/work/edition registration metadata is immutable after first registration under a given key. A subsequent import using the same key with conflicting registration metadata is rejected rather than rewriting source provenance.

## Read API boundary

The initial read endpoints are:

- `GET /api/sources` - lists public source packages.
- `GET /api/sources/entities/{entityId}` - returns the latest revision of a public source entity with package/work/edition provenance and the preserved source document.

Only packages explicitly marked public are exposed by these endpoints. Private/restricted packages return the same not-found behavior as a missing entity. This is deliberately narrower than the final authorization model.

There is no unauthenticated source-import HTTP endpoint. Imports currently enter through the application service so a future mutation endpoint can be placed behind the Dorks & Dice Tool Host identity contract and Rules Core source-access checks rather than creating a temporary insecure write surface.

## Authorization still to add

`IsPublic` is only the open-content boundary for this first slice. Restricted content will use separate acquisition and per-user source-grant records. Those grants must remain independent of Rules Lawyer or campaign editing authority:

- Dorks & Dice determines whether an identity may adjudicate/change rules.
- Rules Core determines whether that identity may read a restricted source.

A later slice will add those grants and authenticated Tool Host introspection before restricted source documents are exposed through the API.
