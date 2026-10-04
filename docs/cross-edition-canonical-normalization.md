# Cross-edition canonical normalization

Phase 4.1B establishes a source-neutral mechanical contract without discarding source-native
evidence. `SourceEntityRevision.RawJson` remains immutable. Source-specific structures such as
`_rulesCore.threeX.fields` and PCGen unmapped segments remain available for provenance, audit,
unsupported mechanics, and future normalization work.

The canonical contract is a semantic superset. A field is absent when the mechanic does not apply
or Rules Core can not normalize it defensibly. Absence does not imply a 5e default.

## Ownership and canonical paths

The current canonical mechanical locations are:

| Family | Canonical structure | Notes |
| --- | --- | --- |
| class / prestige class | `_rulesCore.character` | `hitDie`, BAB/save progressions, skill points, class skills, spellcasting profile, prerequisites, advancement features |
| skill / competency | `_rulesCore.competency` | Governing ability, rank/class-skill capability, training state, family/specialty and reviewed cross-edition competency identity |
| feat | `_rulesCore.character.feat` plus `_rulesCore.character.prerequisites` / `prerequisiteText` | Structured prerequisites are retained when defensible; source text is used when safe decomposition is not available |
| spell | `_rulesCore.spell` | Scalar level, per-list/class access, school/subschool/descriptors, casting time, range, components, duration, concentration, ritual, target/area/effect, saving throw and spell resistance |
| item / equipment / magic item | `_rulesCore.item` | Type/category, rarity/attunement when applicable, canonical copper value, pounds weight, charges, enhancement bonus and special properties |
| race / species | `_rulesCore.species` | Size, speed, creature type, ability adjustments, languages and parent/copy relationship when defensible |
| background | `_rulesCore.character.background` | Existing structured ability/proficiency/feat data is preserved without fabricating older-edition equivalents |
| optional feature / feature | `_rulesCore.character.feature` | Existing structured type/prerequisite data is preserved |
| 3.x combat primitives | `_rulesCore.combat` | Total/touch/flat-footed AC, BAB, grapple, damage reduction, spell resistance and miss chance when structured evidence exists |

Top-level 5e.tools-derived fields remain intact for native compatibility and source-family fidelity.
They are no longer the only machine-readable location for mechanics normalized into one of the
structures above.

`_rulesCore.context.canonicalSchemaVersion` identifies the translation projection version. It is
non-rule-bearing interpretation metadata, so ordinary mechanical comparison strips it with the rest
of `_rulesCore.context` rather than changing semantic identity merely because the projection version
was incremented.

## Spell-list and generated lookup ownership

Published 5e.tools site spell records do not consistently carry intrinsic class-list membership.
The generated `gendata-spell-source-lookup.json` index is therefore ingested as package-owned
`spellSourceLookup` companion evidence.

The physical generated lookup remains the owner of that companion content. During batch
normalization, matching lookup evidence is exposed transiently to the target spell representation
through `NormalizedSourceRepresentation.NormalizationCompanionEvidence`; it is not copied into the
target representation's persisted `CompanionContents`. Canonical spell list rows record
`evidence: "generated-spell-source-lookup"` when this evidence supplied the grant.

During normalization replay, persisted lookup evidence is rehydrated only from the same source
package as the target source entity. This prevents a separately licensed package from being baked
into another package's derived `ContentJson` while still allowing existing imported spells to gain
the same canonical list semantics as fresh batch imports.

Wiki and other consumers must read `_rulesCore.spell.lists`; they must not query and merge generated
lookup evidence on every browse request.

## Replay

`SourceNormalizationVersion.Current` is bumped when this contract changes. Existing revisions are
eligible for the normal replay pipeline and canonical reconciliation. Raw source JSON, native
fingerprints, native revision numbers, batching, parallel workers, and `SKIP LOCKED` work claiming
remain unchanged.

A replay may change `ContentJson` and the semantic fingerprint when newly understood mechanics
become rule-bearing. Translation-version metadata alone is not rule-bearing. Canonical
reconciliation remains responsible for preserving reviewed source identity/history rather than
creating a source revision.

## Conservative normalization rules

- Do not manufacture modern rarity, attunement, proficiency, or similar mechanics for editions
  which do not use them.
- Do not convert BAB/save progressions into proficiency bonus.
- Do not collapse ranked 3.x skills into the Dorks & Dice competency model when they are distinct
  facets.
- Do not force class-dependent 3.x spell levels into one scalar level.
- Preserve source text when an expression can not be safely decomposed.
- Leave unknown mechanics in source-specific evidence rather than guessing.
