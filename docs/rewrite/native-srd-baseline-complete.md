# Native SRD baseline: staged coverage complete, reconciliation deferred

**Status:** Complete *native staging* of the pinned, unofficial SRD-only
structured JSON pack, **not** a complete reconciled/executable Dorks & Dice
ruleset. Original SRD 5.1 and 5.2.1 source content remains in
`data/reference/srd/structured/`; its fidelity against the official SRD
PDFs has not been independently verified.

The baseline stages **2,522 logical native-coverage entries** from source
material, of which **24 already have approved native definitions** (18
Skills and six resolved Conditions). **2,225 entries** have independent
primary native JSON files. The rest of the staged entities are in compact
catalogs under `data/native/`, including Actions, Languages, Item Types,
Weapon Masteries, General Rules, and nine unresolved Conditions.

## Organization

- `classes/base/<class>.json`
- `classes/subclasses/<parent-class>/<subclass>.json`
- `species/base/<species>.json`
- `species/subspecies/<parent-species>/<subspecies>.json`
- `bestiary/<monster>.json`
- `spells/<spell>.json`
- `items/equipment/`, `items/magic/`, and other item categories
- `feats/`, `backgrounds/`, `features/classes/`,
  `features/subclasses/`, `features/optional/` and supporting rule
  categories
- `rules/<rulebook-section>.json` stages the full gameplay chapter
  content from both SRDs, except **Legal Information**, which correctly
  remains outside gameplay definitions in the original reference books.

Small categories remain consolidated, notably `skills.json`,
`competencies.json`, `conditions.json`, `actions.json`,
`languages.json`, `general-rules.json`, and similarly sized catalogs.

Descriptive or lore entries are embedded with the appropriate entity's
candidate content, not in a `fluff-*.json` companion file.

## How to interpret a staged file

A staged entry has `baselineStatus:
"pending-native-reconciliation"` and one or more complete
`candidateDefinitions`. They are **inert draft candidates** containing
structured rule data from the imported SRDs, not selectable gameplay modes
or competing active rules. Where the editions disagree, candidates remain
distinct rather than one being chosen implicitly. Even a single or
identical candidate has **not automatically received gameplay approval**.
The candidate keys `candidate-a` and `candidate-b` are only temporary,
source-neutral identifiers; the source-to-candidate mapping is in
`data/reference/srd/native-coverage-manifest.json`, not runtime
mechanics.

These candidate bodies still contain some unconverted 5etools inline
markup and nested publication references. They are *staged source-derived
data*, not final sanitized native definitions. The later review will
replace them with authoritative Dorks & Dice definitions after agreed
reconciliation. The baseline work does not add permanent importer
infrastructure or a source-version rule selector.

**Previously approved native rules are preserved**, including:
- 88 named native Skills, with 18 SRD 5e skills already included;
- 33 crafting/tool Competencies, independently defined from ordinary Skills;
- Six native Conditions: Deafened, Exhaustion, Frightened, Grappled,
  Poisoned and Restrained. Nine remaining Conditions are candidates.

## Completeness controls and remaining caveats

The pinned pack contains 90 JSON files (about 8.16 MB). Permanent tests
verify their SHA-256 fingerprints and verify all staged-entity coverage,
identifier uniqueness, native parent folders, retained lore, and no
changes to previously approved Conditions.

The pack also contains Foundry-enhancement artifacts, UI indexes, and
generated lookups. These have **not** been promoted into native gameplay
rules; they remain fully preserved in `data/reference/srd/structured/`.
Full first-party license parity for every element remains unverified.

The large source-difference inventory is still in
`data/reference/srd/reconciliation-inventory.json`. It is *not*
a ruling. No fresh substantive reconciliation was performed beyond the
explicitly approved choices already in the branch. The next phase is the
planned **second-pass rules review**, starting with actual semantic
differences and applying existing Dorks & Dice decisions first.

No executable engine, database schema, new deployment, access change,
or merge to `main` was made.
