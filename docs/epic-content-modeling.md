# Epic content modeling

Rules Core treats **epic** as a cross-cutting rules tier, not as a replacement entity family.
A feat remains a feat, a prestige class remains a prestige class, and so on. Source-specific
terms and tags remain available in immutable source data while the normalized mechanical model
uses one canonical vocabulary.

## Canonical terminology

- 3.x **Epic Feat** -> `feat`, epic tier, canonical term `Epic Feat`.
- 5.5e **Epic Boon Feat** / 5e.tools category `EB` -> `feat`, epic tier, canonical term `Epic Feat`.
- The original 5e.tools `EB` category remains in `RawJson`; normalized `ContentJson` exposes
  `category: "Epic"` and canonical epic metadata only.

Source-specific terms such as `EB` and `Epic Boon Feat` deliberately do not enter mechanical
metadata. Keeping them in `RawJson` preserves provenance without changing semantic fingerprints or
preventing otherwise equivalent epic mechanics from reconciling across source formats.

Normalizing the category does not assert that similarly named feats from different editions are
revisions of one another. Canonical history reconciliation still requires its normal evidence.

## Class progression versus separate classes

3.x SRD continuation documents such as **Epic Barbarian** are additional progression for an
existing class. They must not appear as another selectable base class. Rules Core therefore uses
the generic `classProgression` entity type for these records and records the continuation target in
`_rulesCore.epic.continuationOf`.

The same rule applies to continuation documents such as **Epic Arcane Archer**. They use
`prestigeClassProgression` rather than becoming another prestige class.

This is distinct from true epic prestige classes such as **Epic Infiltrator** or **Legendary
Dreadnought**. Those remain `prestigeClass` concepts with epic-tier metadata.

The reviewed legacy SRD makes the distinction mechanically observable: continuation sections use
"Skill Points at Each Additional Level," while actual classes define their own levels. The
normalizer uses that evidence rather than assuming every name beginning with `Epic` is a
continuation.

## PCGen

PCGen already models ordinary base-class epic advancement as `.MOD` operations against the base
class, for example `CLASS:Barbarian.MOD`. Those operations are not a second base class.

The reviewed 3.5 RSRD epic prestige-class file also contains true epic prestige classes whose PCGen
`TYPE` is `Epic.PC` rather than `Prestige.Epic.PC`. Rules Core normalizes those reviewed records to
`prestigeClass` so they do not leak into the base-class catalog.

Full interpretation and attachment of deferred PCGen `.MOD` progression operations is separate
from classification containment and remains part of the broader epic advancement work.

## Mechanical metadata

Normalized epic content uses `_rulesCore.epic` in `ContentJson`:

```json
{
  "tier": "epic",
  "kind": "feat | progression | prestige-class | content",
  "canonicalTerm": "Epic Feat",
  "continuationOf": {
    "entityType": "class",
    "name": "Barbarian"
  },
  "startsAfterClassLevel": 20
}
```

Only applicable fields are emitted. No universal minimum character level is stored here because
3.x Epic Feats and 5.5e Epic Boon Feats do not share the same acquisition level.

## Existing imported data

Epic normalization is source-normalization version 2. The maintenance replay updates derived
`ContentJson`, canonical source association, and reviewed derived `SourceEntity` type/name
corrections while preserving native keys, `RawJson`, native fingerprints, source representation
bytes, and revision numbers.

## Scope boundary

This model is intended to make imported epic content structurally correct and unambiguous before
complete epic character advancement is implemented. It does not by itself implement all 3.x
post-20 attack/save progression, spellcasting advancement, feat cadence, or Character Sheet UI for
level 21+ play.
