# Built-in source and rules baseline

Rules Core initializes a deterministic global baseline after the database schema is ready. Source registration, source acquisition, and Rules Layer adjudication remain separate so provenance is not collapsed into house rules.

## Startup behavior

`RulesCore:BootstrapBaseline` defaults to `true`.

On startup, the baseline bootstrapper:

1. ensures the built-in source package/work/release identities exist;
2. records corpus-membership authority references for the 3e, 3.5e, 5.1, and 5.2.1 SRDs;
3. registers four public persistent hosted-source definitions used to acquire parseable SRD representations;
4. imports the local immutable Dorks & Dice baseline-rule source document;
5. on a fresh Rules Layer only, creates the settled baseline concepts/decisions and publishes the first global ruleset.

Startup does **not** fetch any remote SRD corpus. Remote URLs are persistent acquisition definitions; a Rules Lawyer can preview and refresh them through the Hosted Sources workflow. Runtime rule reads therefore never require the representation host or authority archive to be online.

A conflicting built-in package/work/release identity stops startup instead of silently changing provenance. Existing hosted-source definition revisions are not replaced by bootstrap, so a Rules Lawyer can deliberately disable or revise a built-in acquisition definition.

## SRD authority and parseable representations

Rules Core separates **corpus membership authority** from the **representation parsed for import**.

### 3e

- Membership authority: archived SRD 3.0 distribution index at `https://web.archive.org/web/20080209011829/http://www.opengamingfoundation.org/srd.html`.
- Parseable representation: `https://www.dragon.ee/30srd/`.
- Hosted format: `legacy-srd-text` using `html-index` expansion.
- Normalized source label: `SRD3`.

The Dragon.ee mirror is used because the original 3.0 distribution is no longer a convenient live structured source. It does not replace the archived distribution as the statement of what belongs to the SRD.

### 3.5e

- Membership authority: archived official Wizards Revised 3.5 SRD ZIP at `https://web.archive.org/web/20160328013113/http://www.wizards.com/d20/files/v35/SRD.zip`.
- Parseable representation: `https://github.com/olimot/srd-v3.5-md`.
- Hosted format: `legacy-srd-text` using explicit `github-tree` roots for `basic-rules-and-legal`, `divine`, `epic`, `magic-items`, `monsters`, `psionics`, and `spells`.
- Normalized source label: `SRD35`.

`SRD3` and `SRD35` are Rules Core normalization labels. They are not represented as historical Wizards source codes.

The legacy adapter converts HTML/Markdown headings and stat-block structures into the same canonical entity documents accepted by `ISourceImportService`. It assigns deterministic normalized identities and preserves the original document URI, heading, and body. Recognized entity families include classes, prestige classes, NPC classes, races, skills, feats, spells, monsters, and magic items where the source structure provides a reliable distinction. Remaining material is retained as generic `rule` entities rather than discarded.

### 5e and 5.5e

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

- `builtin-wotc-srd-3e`
- `builtin-wotc-srd-3-5e`
- `builtin-wotc-srd-5-1`
- `builtin-wotc-srd-5-2-1`

The 3e and 3.5e definitions are public definitions under canonical package `wotc-srd-ogl` with `OGL-1.0a` provenance. The 5.1 and 5.2.1 definitions are public definitions under `wotc-srd-cc` with `CC-BY-4.0` provenance.

All four definitions are acquisition metadata only until explicitly previewed/refreshed. Bootstrap does not hydrate their remote content.

## Built-in source families

### D&D 3e and 3.5e SRDs

Rules Core registers and can acquire the public Wizards of the Coast SRDs under package `wotc-srd-ogl`:

- `srd-3e` / 3e / normalized source `SRD3`;
- `srd-3-5e` / 3.5e / normalized source `SRD35`.

Both are normalized into ordinary immutable Source Layer entities, so the Rules Layer does not need a separate legacy-edition resolver.

### D&D 5e and 5.5e SRDs

Rules Core registers the Wizards SRDs under package `wotc-srd-cc`:

- `srd-5-1` / release `5.1` / game edition `5e`;
- `srd-5-2-1` / release `5.2.1` / game edition `5.5e`.

### Loot Tavern

The source registry separates Loot Tavern into two package families:

- `loot-tavern-free` — public/free releases;
- `loot-tavern-licensed` — restricted user-owned/Patreon/paid releases.

No specific Loot Tavern work is fabricated during bootstrap. A release is attached to the appropriate package only after its own availability and redistribution/direct-link terms are known.

## Cross-edition resolution

Importing multiple editions does not create automatic edition precedence. Same-name entities remain distinct source entities with their own package/work/release provenance until the Rules Layer binds them to a concept and a Rules Lawyer adjudicates the selection or consolidation.

The integration suite now exercises this directly with `Power Attack`:

1. a 3e `Power Attack` source entity and a 3.5e `Power Attack` source entity are imported under their respective SRD works;
2. both are bound to one rule concept;
3. the Rules Lawyer explicitly chooses the 3.5e revision and publishes it;
4. anonymous resolution returns the 3.5e document with `SRD35` and `srd-3-5e` provenance;
5. the Rules Lawyer explicitly switches the decision to the 3e revision and republishes;
6. anonymous resolution then returns the 3e document with `SRD3` and `srd-3e` provenance.

This validates the resolution pipeline without prematurely encoding a general rule such as “newest edition wins.” Future additive/consolidated behavior remains an explicit Rules Lawyer decision.

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

Global public sources and published global rules do not require authentication. The source and rules APIs resolve with a nullable user ID; public packages are returned when that ID is null. Authentication/grants are required only for restricted packages or campaign-specific state.

The regression suite explicitly verifies that an unauthenticated client can list the public source packages and resolve published global rules while restricted packages remain hidden.

## Validation policy

Normal historical integration tests run with `RulesCore__BootstrapBaseline=false` so their isolated assumptions remain stable. Dedicated tests verify source identities, all four authority references, all four persistent SRD definitions, manual 5e/5.5e membership constraints, legacy HTML/Markdown normalization, 3e/3.5e hosted refresh behavior, explicit cross-edition adjudication, initial global publication, repeat-bootstrap behavior, preservation of Rules Lawyer edits, and anonymous public access.
