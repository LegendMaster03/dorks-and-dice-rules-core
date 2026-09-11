# Built-in source and rules baseline

Rules Core initializes a deterministic global baseline after the database schema is ready. Source registration, source acquisition, normalization, and Rules Layer adjudication remain separate so provenance is not collapsed into house rules.

## Startup behavior

`RulesCore:BootstrapBaseline` defaults to `true`.

On startup, after the core schema initializer has created the Source Layer tables, the baseline bootstrapper:

1. ensures the built-in source package/work/release identities exist;
2. records corpus-membership authority references for the 3e, 3.5e, 5.1, and 5.2.1 SRDs;
3. registers four public persistent hosted-source definitions used to acquire parseable SRD representations;
4. imports the local immutable Dorks & Dice baseline-rule source document;
5. on a fresh Rules Layer only, creates the settled baseline concepts/decisions and publishes the first global ruleset.

The `source_edition_authority_reference` table is owned by the core Source Layer schema initializer. Bootstrap only inserts the built-in authority records; it does not create Source Layer tables.

Startup does **not** fetch any remote SRD corpus. Remote URLs are persistent acquisition definitions; a Rules Lawyer can preview and refresh them through the Hosted Sources workflow. Runtime rule reads therefore never require the representation host or authority archive to be online.

A conflicting built-in package/work/release identity stops startup instead of silently changing provenance. Existing hosted-source definition revisions are not replaced by bootstrap, so a Rules Lawyer can deliberately disable or revise a built-in acquisition definition.

## SRD authority and parseable representations

Rules Core separates **corpus membership authority** from the **representation parsed for import**.

### 3e

- Membership authority: archived SRD 3.0 distribution index at `https://web.archive.org/web/20080209011829/http://www.opengamingfoundation.org/srd.html`.
- Parseable representation: `https://www.dragon.ee/30srd/`.
- Hosted format: `legacy-srd-text` using `html-index` expansion.
- Normalized source label: `SRD3`.

The Dragon.ee mirror is used because the original 3.0 distribution is no longer a convenient live structured source. It does not replace the archived distribution as the statement of what belongs to the SRD. Because the mirror is mutable rather than Git-backed, Rules Core also carries a manually reviewed **139-document manifest** for the built-in 3e representation. Preview/refresh must resolve exactly that reviewed document set; missing or unexpected HTML documents fail acquisition so mirror drift can not silently change corpus membership. Updating the manifest therefore requires an explicit source review. Every successfully imported document is still fingerprinted into immutable source revisions.

### 3.5e

- Membership authority: archived official Wizards Revised 3.5 SRD ZIP at `https://web.archive.org/web/20160328013113/http://www.wizards.com/d20/files/v35/SRD.zip`.
- Parseable representation: `olimot/srd-v3.5-md` at reviewed commit `c7f30a0ce11a579f75456746f278a4c75f67b4c1`.
- Hosted format: `legacy-srd-text` using explicit `github-tree` roots for `basic-rules-and-legal`, `divine`, `epic`, `magic-items`, `monsters`, `psionics`, and `spells`.
- Normalized source label: `SRD35`.

`SRD3` and `SRD35` are Rules Core normalization labels. They are not represented as historical Wizards source codes.

The legacy adapter converts HTML/Markdown into the same canonical entity documents accepted by `ISourceImportService`. It assigns deterministic normalized identities and preserves the original document URI, heading, and body. Classification is evidence-based rather than path-only: spell and power signatures, feat benefits, monster stat-block fields, item price summaries, class/race identity, and domain/ability structure prevent section headings from becoming false entities. Recognized families include classes, prestige classes, NPC classes, races, skills, feats, spells, psionic powers, divine domains, salient divine abilities, monsters, and magic items. Remaining material is retained as generic `rule` entities rather than discarded.

Obvious presentation-only differences are normalized for advisory concept matching. Examples include `Dwarves` -> `Dwarf` and `Heal (Wis)` -> `Heal`; the original source heading remains preserved in the revision.

### 5e and 5.5e

For 5e and 5.5e, the official SRD PDF is the corpus-membership authority. Structured JSON is only the field-level representation used by the importer. The built-in JSON representation is pinned to reviewed `CoolFireGiant/hewnhero-srd` commit `d06d1dadee357857767b1e4da985df6609509bcf`.

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

Git-backed representation URLs are pinned to reviewed commits. Updating those mirrors requires a deliberate hosted-source definition revision. The mutable 3e mirror is instead constrained by the reviewed 139-document manifest described above. Bootstrap never overwrites a later Rules Lawyer revision.

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

## Cross-edition normalization and resolution

Importing multiple editions does not create automatic edition precedence. Same-name entities remain distinct source entities with their own package/work/release provenance until the Rules Layer binds them to a concept and a Rules Lawyer adjudicates the selection or consolidation.

Normalization is advisory. When two safely normalized source entities produce the same concept key, accepting the suggestions can bind both sources to one concept, but it creates no global decision and publishes nothing. This is covered with a 3e `Dwarf` / 3.5e `Dwarves` integration test.

The resolver integration suite exercises explicit adjudication with `Power Attack`:

1. a 3e `Power Attack` source entity and a 3.5e `Power Attack` source entity are imported under their respective SRD works;
2. both are bound to one rule concept;
3. the Rules Lawyer explicitly chooses the 3.5e revision and publishes it;
4. anonymous resolution returns the 3.5e document with `SRD35` and `srd-3-5e` provenance;
5. the Rules Lawyer explicitly switches the decision to the 3e revision and republishes;
6. anonymous resolution then returns the 3e document with `SRD3` and `srd-3e` provenance.

The same suite also exercises explicit additive consolidation. A 3.5e revision can be selected as the base while the reviewed 3e revision is recorded as an `incorporated` contribution and a structured/merge patch records the adjudicated hybrid result. Published `ResolvedRuleView` responses expose those contributing revisions with their package/work/release/source-code provenance and contribution notes, rather than losing provenance after publication.

Contribution access is part of rule access. A consumer can resolve a published rule only when the selected base source **and every contribution actually applied by that global decision** are accessible to that consumer. A public base therefore does not leak a restricted incorporated source. Campaign rules that inherit or patch the global decision retain the same contribution requirements; a campaign `select-source` replacement that does not use the global contribution does not inherit that unused restriction.

This validates the resolution and consolidation pipeline without encoding a general rule such as “newest edition wins.” Cross-edition inclusion remains an explicit Rules Lawyer decision.

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

Global public sources and published global rules do not require authentication. The source and rules APIs resolve with a nullable user ID; public packages are returned when that ID is null. Authentication/grants are required for restricted packages or campaign-specific state.

For consolidated rules, public visibility is evaluated across the entire applied provenance set, not only the base source revision. If any incorporated/referenced contribution is restricted, anonymous resolution is denied and authenticated consumers must hold the appropriate grant. This prevents a public patched document from becoming an accidental redistribution path for restricted source material.

The regression suite explicitly verifies that an unauthenticated client can list the public source packages and resolve published global rules while restricted packages and restricted-contribution consolidations remain hidden.

## Validation policy

Normal historical integration tests run with `RulesCore__BootstrapBaseline=false` so their isolated assumptions remain stable. Dedicated tests verify source identities, Source Layer ownership of authority-reference schema, all four authority references, all four persistent SRD definitions, the reviewed 139-document 3e manifest, manual 5e/5.5e membership constraints, legacy HTML/Markdown normalization, false-positive resistance on real legacy corpus structures, 3e/3.5e hosted refresh behavior, advisory cross-edition normalization, explicit cross-edition adjudication and consolidation, resolved contribution provenance, contribution-aware restricted-source access, initial global publication, repeat-bootstrap behavior, preservation of Rules Lawyer edits, and anonymous public access.
