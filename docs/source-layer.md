# Source Layer

The Source Layer is the immutable record of what imported source material says before any Dorks & Dice adjudication, merging, or campaign override is applied.

## Persistence model

The current vertical slice stores five source identities plus independent user grants in PostgreSQL:

1. `source_package` - an acquired/imported package and its distribution metadata.
2. `source_work` - a work contained by the package.
3. `source_edition` - an edition/release of that work.
4. `source_entity` - a source-specific entity identity within an edition.
5. `source_entity_revision` - immutable JSON revisions of that entity.
6. `user_source_grant` - a stable Dorks & Dice user ID's permission to access a restricted package.

Package, work, and edition keys are normalized at the application boundary. Source entity identity is based on the 5e.tools array/property name plus source code, entity name, and an upstream `uniqueId`/`id` when one is present. The database ID is the local persistent identifier; the natural key and revision fingerprint provide stable content identity.

Source grants deliberately do not contain global or campaign roles. They answer only whether a specific site identity may access a specific source package.

## Lossless 5e.tools ingestion

`ISourceImportService` accepts a 5e.tools-shaped JSON document. Top-level entity arrays are imported without projecting the entity into a fixed application schema. The complete entity object is stored as PostgreSQL `jsonb`, so fields unknown to Rules Core survive ingestion and can be returned later.

Each entity also receives a SHA-256 fingerprint computed from a canonical JSON representation. Object property ordering does not affect the fingerprint. Array ordering remains significant. Reimporting semantically identical JSON is idempotent; changed content creates the next immutable revision instead of updating an existing revision.

Source package/work/edition registration metadata is immutable after first registration under a given key. A subsequent import using the same key with conflicting registration metadata is rejected rather than rewriting source provenance.

## Read API boundary

The read endpoints are:

- `GET /api/sources` - lists all packages accessible in the current request context.
- `GET /api/sources/entities/{entityId}` - returns the latest accessible source entity revision with package/work/edition provenance and the preserved source document.

Anonymous/direct requests can access only packages explicitly marked public. When a request arrives through the authenticated Dorks & Dice Tool gateway, Rules Core redeems the host-issued ticket to obtain the stable user ID and includes private packages having a matching `user_source_grant`.

A private entity without a grant returns the same not-found result as a missing entity. This prevents the source API from becoming an oracle for restricted package existence.

## Source grants

`ISourceGrantService` provides idempotent grant, revoke, and grant-check operations inside the application boundary. A grant is keyed by `(user_id, source_package_id)` and is deleted when revoked. Package deletion cascades to its grants.

Grant management is not exposed as a public HTTP mutation endpoint yet. The eventual write workflow must establish why the user is entitled to the source and then create the grant without conflating that decision with Rules Layer edit authority.

The two authorization axes remain independent:

- Dorks & Dice determines whether an identity may adjudicate/change global or campaign rules.
- Rules Core determines whether that identity may read a restricted source.

A user can therefore be a Rules Lawyer without access to a private source, or have access to that source without being allowed to adjudicate global rules.

## Import boundary

There is still no unauthenticated source-import HTTP endpoint. Imports enter through the application service until the acquisition/import workflow is designed around authenticated Tool Host identity and explicit source entitlement. This avoids creating a temporary write surface that would later need to be removed.
