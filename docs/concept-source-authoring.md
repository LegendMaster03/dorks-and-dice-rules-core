# Concept and source binding authoring

The global Rules Lawyer UI can create stable Rules Layer concepts and attach existing Source Layer entities without editing source content.

## Concept creation

A concept has immutable metadata:

- stable key, normalized to lowercase;
- entity type, normalized to lowercase;
- display name.

The UI submits this metadata through `POST /api/global/rules/concepts`. The endpoint continues to require Dorks & Dice mode plus effective `Rules Lawyer` authority. Repeating an identical concept request is idempotent; conflicting immutable metadata for an existing key is rejected.

## Accessible source discovery

`GET /api/sources/entities` provides a metadata-only search over source entities the current caller may access. Supported query parameters are:

- `entityType` for an exact case-insensitive source entity type filter;
- `q` for a case-insensitive search across entity name, source code, package, work, and edition display names;
- `limit`, clamped to 1-200 results.

Search results include provenance and latest-revision metadata but never include the source JSON document. Public entities are visible without a source grant. Private entities appear only when the redeemed Tool Host user has an explicit Rules Core source grant. This keeps source access independent from Rules Lawyer authority.

## Source binding

A Rules Lawyer can bind a discovered source entity to a concept with `POST /api/global/rules/concepts/{conceptId}/bindings`. The binding records only Rules Layer identity and provenance; it does not mutate the source entity or any source revision.

The UI defaults source discovery to the concept entity type but allows that filter to be changed or cleared. This is intentional: a normalized concept may occasionally need to map source material whose upstream entity taxonomy differs between systems or editions.

Once a source entity is bound, its accessible immutable revisions become available to the existing decision preview/save workflow. Restricted bindings remain hidden at the source-content layer when the current user lacks the corresponding source grant.

## Source ingestion remains separate

This authoring slice does not make Rules Lawyers Source Layer administrators. Source ingestion and source-grant mutation remain separate operations. A Rules Lawyer can create concepts and bind source entities that are already accessible, but Rules Lawyer authority by itself does not grant, import, alter, or expose restricted source content.
