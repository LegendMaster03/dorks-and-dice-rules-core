# Built-in source and rules baseline

Rules Core initializes a deterministic global baseline after the database schema is ready. Source registration, source acquisition, and Rules Layer adjudication remain separate so provenance is not collapsed into house rules.

## Startup behavior

`RulesCore:BootstrapBaseline` defaults to `true`.

On startup, the baseline bootstrapper:

1. ensures the built-in source package/work/release identities exist;
2. records the official PDF authority for SRD 5.1 and SRD 5.2.1;
3. registers the two public persistent hosted-source definitions used to acquire structured SRD data;
4. imports the local immutable Dorks & Dice baseline-rule source document;
5. on a fresh Rules Layer only, creates the settled baseline concepts/decisions and publishes the first global ruleset.

Startup does **not** fetch the remote SRD JSON or PDF. The remote URLs are persistent acquisition definitions; a Rules Lawyer can preview and refresh them through the Hosted Sources workflow. Runtime rule reads therefore never require the remote host to be online.

A conflicting built-in package/work/release identity stops startup instead of silently changing provenance. Existing hosted-source definition revisions are not replaced by bootstrap, so a Rules Lawyer can deliberately disable or revise a built-in acquisition definition.

## PDF authority and structured representations

For 5e and 5.5e, the official SRD PDF is the corpus-membership authority. Structured JSON is only the field-level representation used by the importer.

Rules Core persists these authority references:

- SRD 5.1: `https://media.wizards.com/2023/downloads/dnd/SRD_CC_v5.1.pdf`
- SRD 5.2.1: `https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf`

The eventual automatic PDF parser can replace the manual review step without changing the source architecture. Until then, `OfficialSrdMembershipCatalog` records the manually reviewed membership of aggregate sections that are particularly vulnerable to cross-version contamination.

### Manually reviewed SRD 5.1 membership

The official PDF confirms:

- Background: Acolyte.
- Feat: Grappler.
- Races: Dragonborn, Dwarf, Elf, Gnome, Half-Elf, Half-Orc, Halfling, Human, Tiefling.

Subrace data remains part of the corresponding SRD source data and continues to be selected by its SRD source code.

### Manually reviewed SRD 5.2.1 membership

The official PDF confirms:

- Backgrounds: Acolyte, Criminal, Sage, Soldier.
- Species: Dragonborn, Dwarf, Elf, Gnome, Goliath, Halfling, Human, Orc, Tiefling.
- Origin feats: Alert, Magic Initiate, Savage Attacker, Skilled.
- General feats: Ability Score Improvement, Grappler.
- Fighting Style feats: Archery, Defense, Great Weapon Fighting, Two-Weapon Fighting.
- Epic Boon feats: Boon of Combat Prowess, Boon of Dimensional Travel, Boon of Fate, Boon of Irresistible Offense, Boon of Spell Recall, Boon of the Night Spirit, Boon of Truesight.

The shared structured feat file contains two records named `Magic Initiate` with source `SRD52`. The official PDF contains the 2024 Origin Feat. Rules Core therefore admits the `basicRules2024` representation and rejects the malformed legacy-looking duplicate. Page number is not added to durable identity.

Other source families use edition-specific structured files where available, especially `spells-srd51.json`, `spells-srd52.json`, `bestiary-srd51.json`, and `bestiary-srd52.json`. Mixed aggregate files continue to be partitioned by source code, with the manual membership catalog applied for the reviewed background/race/species/feat families.

## Persistent hosted SRD definitions

Fresh installations register:

- `builtin-wotc-srd-5-1`
- `builtin-wotc-srd-5-2-1`

Both are public definitions under the canonical `wotc-srd-cc` package. Their physical JSON URLs point at the public `CoolFireGiant/hewnhero-srd` representation, while the logical provider remains Wizards of the Coast and the official PDFs remain the membership authority.

The resource sets intentionally use explicit files/indexes rather than a whole-repository crawl. They cover actions, backgrounds, classes, conditions/diseases, feats, equipment/items, languages, magic-item variants, objects, optional features, races/species, senses, skills, tables, traps/hazards, variant rules, vehicles, spells, and monsters. SRD 5.1 also includes the SRD deity data. SRD 5.2.1 does not assume a standalone deity catalog that is not represented as such in the official document.

## Built-in source families

### D&D 3e and 3.5e SRDs

Rules Core registers the public Wizards of the Coast SRD identities under package `wotc-srd-ogl` with `OGL-1.0a` provenance:

- `srd-3e` / 3e;
- `srd-3-5e` / 3.5e.

No live mirror is guessed. Their source documents/adapters will be attached after deliberate source selection and validation.

### D&D 5e and 5.5e SRDs

Rules Core registers the Wizards SRDs under package `wotc-srd-cc` with `CC-BY-4.0` provenance:

- `srd-5-1` / release `5.1` / game edition `5e`;
- `srd-5-2-1` / release `5.2.1` / game edition `5.5e`.

### Loot Tavern

The source registry separates Loot Tavern into two package families:

- `loot-tavern-free` — public/free releases;
- `loot-tavern-licensed` — restricted user-owned/Patreon/paid releases.

No specific Loot Tavern work is fabricated during bootstrap. A release is attached to the appropriate package only after its own availability and redistribution/direct-link terms are known.

## Dorks & Dice adjudicated baseline

The deterministic public package `dorks-and-dice-baseline` contains the settled house rules that can be represented without resolving unfinished progression design:

- **Healing Potion Use** — bonus action uses rolled healing; using the action grants maximum possible healing.
- **Spell Preparation** — the normal prepared-spell restriction is removed.
- **Spellcasting Resource Choice** — casters may choose spell slots or spell points.
- **Controlled Creature Initiative** — a controlled creature acts on its controller's initiative.
- **Free Flavor Feats** — purely flavor feats do not consume a mechanical feat choice.
- **Cross-Edition Additive Compatibility** — 3e, 3.5e, 5e, and 5.5e are merged additively; omission from a newer edition does not remove a compatible older option; explicit conflicts require adjudication; source provenance is preserved.

On a fresh Rules Layer these six source entities are bound to stable `house.*` concepts and published as the first global ruleset revision.

Unresolved class/prestige progression, feat cadence, BAB/skill-rank/save prerequisite conversion, prestige spellcasting progression, and similar open design questions remain unseeded. Detailed runtime initiative-block execution remains the Initiative Tracker's responsibility.

## Anonymous global access

Global public sources and published global rules do not require authentication. The source and rules APIs already resolve with a nullable user ID; public packages are returned when that ID is null. Authentication/grants are required only for restricted packages or campaign-specific state.

The regression suite explicitly verifies that an unauthenticated client can:

- list `wotc-srd-ogl`, `wotc-srd-cc`, `loot-tavern-free`, and `dorks-and-dice-baseline`;
- not see `loot-tavern-licensed`;
- resolve `house.healing-potion-use`;
- browse the published global rules catalog.

## Validation policy

Normal historical integration tests run with `RulesCore__BootstrapBaseline=false` so their isolated assumptions remain stable. Dedicated baseline tests invoke bootstrap directly and verify source identities, PDF authority references, persistent hosted definitions, manual membership constraints, initial global publication, repeat-bootstrap behavior, preservation of Rules Lawyer edits, and anonymous public access.
