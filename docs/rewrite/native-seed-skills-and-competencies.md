# Native Skills and Competencies — corrected baseline

**Status:** Reconciled provisional native competency data baseline. The numerical rank/proficiency advancement system is still undecided; historical `working/` records remain reference evidence.

## Native data organization

- **`data/rulesets/first-party-001/native/skills.json`** defines individual Skills, derived composite Skills, and specialized Perform/Profession entries.
- **`data/rulesets/first-party-001/native/competencies.json`** defines native Competencies as **single learned capabilities**, each with its own proficiency and level. A competency does not contain a historical Craft skill and a separate tool proficiency.
- **`data/rulesets/first-party-001/working/competencies.json`** and source reconciliation material retain the historical Craft/tool/skill representations. These are reference evidence, not native runtime facets.
- **`data/conversions/skill-name-mappings.json`** retains earlier skill-name translations for source conversion, not native mechanical definitions.

The native JSON describes the Dorks & Dice ruleset rather than imposing the source edition's internal distinction between skills and tool proficiencies.

## Skills

The 87 selected native Skills retain the existing composite and family structures. The former `skill.forgery` is represented by the single native `competency.forgery`, while its historical skill definition remains under `working/` for source conversion. The existing Skills data includes standalone skills, independently addressable children, computed parent Skills, and Perform/Profession skill families.

A Skill has proficiency and level in character state. Its normal Ability-score interactions and any explicit composite calculations belong to the Skill mechanics. Those calculations are not automatically imposed on Competencies.

## Competencies

The native catalog contains **32** competency identities after merging the former Smithing tool identity into Blacksmithing. Each definition has one `profile` with:

- **`proficiency`:** binary proficiency as part of the competency itself, not a separately tracked tool proficiency;
- **`level`:** an independently represented advancement dimension for the same competency;
- **`levelScale`:** undecided pending the numerical progression review;
- **`abilityResolution`:** activity-dependent rather than inherited from historical Craft rules.

The profile expresses the agreed shape of the capability; it does **not** set a bonus, a tier threshold, an advancement cost, or a proficiency/level dependency.

`associatedTools` lists applicable equipment identities such as Alchemist's Supplies, Carpenter's Tools, Smith's Tools, and Forgery Kit. These names do not create additional training entries or establish possession of an item. Competencies that do not currently have a named tool association are still full competencies.

For example:

- **Alchemy:** one competency, with Alchemist's Supplies as associated equipment.
- **Carpentry:** one competency, with Carpenter's Tools as associated equipment.
- **Blacksmithing:** one competency, with Smith's Tools as associated equipment. Its children are Armorsmithing and Weaponsmithing. Both children independently identify Smith's Tools as applicable equipment; this does not grant either child proficiency or establish inherited levels.
- **Forgery:** one competency, with Forgery Kit as associated equipment. There is no separate native `skill.forgery` advancement identity; historical Forgery skill mechanics remain source-conversion evidence.

### Specialization structure

`childCompetencies` and `parentCompetency` follow the existing Skills parent/child reference pattern. The specialization does **not** constitute a different kind of capability or a container of nested skill/tool implementations. Each child is itself a native competency with the same proficiency/level profile.

As with Skills, the hierarchy and any resulting numerical composition are separate design questions: no automatic arithmetic mean, rank inheritance, parent proficiency grant, or child proficiency grant is inferred from the reference alone. A later explicit rule can establish applicable mechanics.

## Historical source information

The core Skills and Competencies catalogs contain no `srd`, `srd52`, previous-edition flags, old source keys, or historical name aliases. Earlier names such as Bluff and Sense Motive are represented **only** in future import-name mapping data. Native Skill names are the chosen Dorks & Dice names.

CC BY 4.0 attributions are retained in `data/reference/srd/manifest.json` because the initial 18 descriptions came from SRD-flagged structured records. Legal provenance documentation does not create a gameplay dependency on an original ruleset. Official-SRD coverage checking is still required before treating excerpts as verified distributable text.

## Checks and remaining work

During competency design, no automated test locks down the competency profile's field layout, count, numerical progression, or specialization formula. The lightweight JSON Action verifies parsing only. Integration testing on `main` remains a separate deployment prerequisite and may need to be reconciled with the approved schema before native competencies are deployed. Basic competency identity and relationship consistency have been reviewed manually on this branch; that is not confirmation that executable mechanics are implemented.
