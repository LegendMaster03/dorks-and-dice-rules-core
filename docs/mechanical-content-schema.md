# Rules Core mechanical content schema

Rules Core deliberately separates **source/provenance representation** from **mechanical game-data representation**.

The Source Layer is format-neutral. It preserves physical bytes, source-native identity, source-native revisions, access grants, publication evidence, canonical occurrences, and reconciliation metadata.

The mechanical content contract is not format-neutral. It uses the actual 5e.tools entity model as the reference/default schema and adds explicit Rules Core extensions only when another edition or source contains mechanics that can not be represented faithfully by the applicable 5e.tools family.

The intended flow is:

`source bytes -> source-native record -> translation -> 5e.tools-derived Rules Core content -> canonical/rules consumption`

`SourceEntityRevision.RawJson` remains the immutable source-native record. `SourceEntityRevision.ContentJson` is the translated mechanical body. Native revision fingerprints are calculated from `RawJson`, never from translation output.

## Upstream 5e.tools review

The reference review was performed against the upstream 5e.tools corpus in `5etools-mirror-3/5etools-src` and the schema-template/schema tooling in `TheGiddyLimit/5etools-utils`. Representative structures were checked for:

- creatures/bestiary;
- spells;
- items/equipment;
- feats;
- classes and subclasses;
- races/species and subraces;
- backgrounds;
- conditions, statuses, and diseases;
- actions;
- optional features;
- books and adventures;
- source/publication metadata and `_meta`;
- fluff/content companions;
- `_copy` and copy-modification semantics;
- `_versions`/version and reprint conventions;
- tagged renderer text and references;
- polymorphic fields and family-specific alternatives.

The upstream model is a collection of rich, family-specific schemas. It is not one universal entity envelope. Common conventions exist, but a field present in one family must not be assumed to exist in every family.

Important upstream conventions include:

- `name` and `source` on ordinary mechanical entities;
- `page` where the source supplies a page locator;
- nested family-specific mechanical fields rather than flattened key/value projections;
- renderer-tagged text such as `{@spell ...}`, `{@item ...}`, `{@condition ...}`, dice/damage tags, and other reference syntax;
- `_copy` as a first-class inheritance/copy mechanism with structured modification operations;
- `_versions` and reprint/reference fields for versioned or reissued content where the family permits them;
- corpus `id` and `parentSource` on publication/corpus structures where applicable;
- `edition` only in schema families that define it;
- `_meta.sources` and related metadata for homebrew/source declarations;
- `uniqueId` as a source/editor identity convention where used, especially editable homebrew workflows, rather than as a universal entity identifier;
- fluff as companion content structures instead of requiring every mechanical entity to inline all descriptive material.

Rules Core preserves unknown 5e.tools fields rather than projecting native entities through a narrow DTO. This is required both for current rich structures and for forward compatibility with upstream fields that Rules Core does not yet understand.

## Native 5e.tools content

For an ordinary native 5e.tools mechanical entity:

- `RawJson` contains the complete native entity object accepted by the adapter;
- `ContentJson` is effectively the same complete entity object;
- unknown fields survive unchanged;
- `source`, `page`, `_copy`, nested mechanics, tagged text, family-specific fields, and any applicable identity/version fields remain intact;
- Rules Core does not flatten the entity into a generic `{name, entityType, segments}` structure.

Translation may add a Rules Core extension only when Rules Core itself needs additional information that is not expressible in the native family. It must not overwrite or reinterpret an upstream field with a different meaning.

## PCGen and 3e/3.5e translation

PCGen remains source-native evidence. PCC/LST parsing preserves campaign metadata, publication context, source names, publisher/date evidence, game modes, file references, native keys, operations, raw tag/value segments, and unsupported fragments.

Recognized PCGen mechanical records are translated into the applicable 5e.tools-derived family shape. Current mappings intentionally cover only defensible equivalences. Examples include:

- PCGen spell level, school, descriptions, simple verbal/somatic components, and instantaneous duration into spell-family fields;
- feat description and repeatability into feat-family fields;
- equipment weight/cost/description into item-family fields;
- race size, speed, and direct ability bonuses into race-family fields;
- creature size/type, abilities, AC, HP/hit dice, speed, CR, and description into bestiary-family fields when the source record supplies those values in a form that can be represented faithfully.

A 3.x rule is not converted into a false 5e mechanic merely to fill a familiar field. Unmapped edition-specific mechanics are retained under the explicit `_rulesCore` extension namespace. For PCGen records the current extension shape is:

```json
{
  "_rulesCore": {
    "edition": "3.5e",
    "pcgen": {
      "unmappedSegments": [
        { "tag": "PREMULT", "value": "..." }
      ]
    }
  }
}
```

The extension is subordinate to the complete `RawJson`; it is not a replacement 3.x ontology. Unsupported source fragments and operations remain separate source evidence until a translator can construct a legitimate mechanical entity.

## PDF boundary

A PDF representation is valid Source Layer material even when no mechanical entity can be extracted confidently. Page-level text fragments therefore normally have `ContentJson = null` and retain only native/source evidence.

A PDF fragment becomes a monster, spell, item, feat, or other normalized mechanical entity only when an extractor has enough information to construct a legitimate family-shaped object. Rules Core must not fabricate a 5e.tools entity simply because a page contains text that resembles one.

## Canonical identity is separate

Mechanical translation does not determine source access or collapse source-native identity.

A PCGen spell and a 5e.tools spell can have:

- different packages;
- different native keys;
- different raw revisions;
- different grants;
- different translated content details;

while canonical reconciliation still determines whether they represent the same canonical entity or related revision/reprint/rename/variant entities.

Canonical metadata never grants access to a private source representation. Rules Layer bindings remain canonical-aware and access-aware.

## Mechanical versus native consumers

Code must choose the representation based on intent.

Mechanical consumers normally use `SourceEntityRevision.GetMechanicalContentJson()`, which returns translated `ContentJson` when present and falls back to `RawJson` for legacy/native records that have no translation. This includes:

- semantic comparison and compatibility;
- automatic cross-edition resolution;
- rule preview;
- rule patching and overrides;
- resolved rules;
- campaign baselines;
- source-revision mechanical review;
- source-browser mechanical entity views.

Native/provenance consumers continue to use `RawJson`. Examples include native revision fingerprints and source-version relationship evidence that depends on explicit source-native references.

## Revision semantics

`RawJson` defines the native revision. A change in translated output alone must not manufacture a new source-native revision.

When an unchanged native record is reprocessed by an improved translator, Rules Core may refresh `ContentJson` on that existing revision. If the native source body changes, a new native revision is created using the canonicalized raw-body SHA-256 fingerprint.

This distinction allows translation logic to improve without falsifying source history.

## Seeding boundary

5e/5.5e and 3e/3.5e bootstrap workflows use source adapters and the same translation/canonical layers as ordinary imports. Rules Core does not require or maintain a global mirror of the complete 5e.tools or PCGen corpus.

A one-time developer seeding/import capability may establish source access and canonical identity knowledge. Persistent global responsibility remains source identity/provenance, access, translation, canonical relationships, and Rules Layer consumption. CI uses deterministic local fixtures and never depends on live upstream repositories.
