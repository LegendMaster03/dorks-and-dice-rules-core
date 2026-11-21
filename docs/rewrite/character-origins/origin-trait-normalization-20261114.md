# Native inherent trait normalization — 2026-11-14

## Scope and authority

Selected-native Species/Ancestry content now stores every named inherent trait as an independently addressable `origin-trait` entity initially under `native/traits/` (relocated without mechanical changes to `native/origination/biological/genes/` on November 20, 2026). This is a representation change, not an approval of new gameplay rules, trait equivalences, inherited benefits, allocation budgets, or homebrew import formats. The later source-neutral catalog stores 87 canonical genes as individual `genes/trait.<mechanical-id>.json` files. Two mechanically equivalent pairs were consolidated; canonical IDs and references were updated together without changing the authored mechanical effects.

The model distinguishes (1) one canonical trait definition and its complete mechanical payload, (2) a position-specific reference in a Species or Ancestry record, (3) a contributor's offered candidate, and (4) a manifested, authorized grant. A trait may contain multiple effects without making each effect a separately inheritable component.

Each reference is `{"traitId": "trait.…"}`, and the definition is an entity `{"id": "trait.…", "kind": "origin-trait", "name": "...", "trait": {...}}`. Identifiers are stable independent of their physical files. The local `trait.id` remains part of the legacy source projection. Selected local labels and IDs were renamed where they explicitly identified the source Species rather than the mechanic.

## Migration behavior

- **Nine Species and 34 Ancestries** were audited. Five currently unselectable Gem Dragonborn leaf records had no traits to move. **38 source records** containing traits were changed.
- **95 authored trait occurrences** were moved to **89 canonical definitions**. Four occurrences share one strictly identical `trait.amphibious` definition; four share one strictly identical `trait.low-light-vision`. Distinct `low-light-vision` in Tiefling, Orc/Goliath Powerful Build, and Red/Copper Natural Climber remain different definitions where authored behavior differs.
- Full trait payloads, choice fields, `originBindings`, dependent spellcasting, and source-relative Dragonborn parameters are retained, without editing their inner mechanics.
- `NativeCatalogLoader` validates reference existence and category, and produces the exact previous embedded source `traits` view for catalog and mechanics consumers. It simultaneously registers `origin-trait` entities for canonical lookup and maps each source ID plus authored trait index to its definition ID.
- `NativeOriginSourceDeconstructor` retains source occurrence and contributor evidence but uses the canonical definition ID in new candidates. The reconciler already reports shared definitions at different occurrences as unresolved pending explicit policy; this refactor adds no automatic stacking.
- The Python Pillar 2 research inventory and selected historical reconciliation tests use read-only projections of canonical references. Their outputs remain illustrative evidence, not new rules.

## Compatibility caveats

Public catalog searches include additional `origin-trait` entities and a changed fingerprint. Source catalog details still expose expanded embedded traits, preserving current consumer parsing, but source content files on disk are now references. Consumers reading raw files must resolve `traitId` rather than assuming each array member carries mechanics. Profile objects persisted with previous source-qualified `MechanicalDefinitionId` strings will differ from newly generated canonical-ID candidates; implement explicit version-aware migration or reconstruction against the correct catalog before treating them as equivalent. Do not infer authorization or attempt silent migration.

Intrinsic creature type, size, movement values, spellcasting choice, Species/Ancestry selection and parent hierarchy remain unchanged. No homebrew packages or transformation behaviors are introduced.

## Verification

Review the added C# loader tests for shared definition identity, runtime projection, dependent bindings, and missing/wrong-category failures. Run `dotnet restore RulesCore.slnx`, `dotnet build RulesCore.slnx -c Release`, `dotnet test RulesCore.slnx -c Release`, `python3 -m unittest discover -s tests -p 'test_*.py' -v`, `python3 tools/audit_native_baseline.py --native data/rulesets/first-party-001/native --strict`, and `python3 tools/validate_ruleset_split.py` before merge or deployment. The branch remains for review until these gates complete.

## Source-neutral ID migration — 2026-11-21

The 89-definition normalization above is preserved as a historical step. The
active biological catalog now has 87 genes and uses mechanical IDs rather than
Species/Ancestry-qualified IDs. Natural Climber and Low-Light Vision are
consolidated because their mechanical payloads agree after disregarding
non-mechanical wording; Darkness and Powerful Build variants remain separate
because their mechanics differ. Every Species/Ancestry `traitId` and active
source-bound `mechanicalDefinitionId` reference is updated.

Persisted Origin data must be migrated explicitly using
`data/migrations/biological-gene-ids-20261121.json`. The separate
`tools/migrate_biological_gene_ids.py` tool changes recognized gene-ID
references only, preserving source provenance and history IDs. Existing
serialized profiles are not silently normalized by catalog lookup.
