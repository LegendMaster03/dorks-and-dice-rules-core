# Native Skills and Competencies — corrected baseline

**Status:** Reconciled provisional native competency data baseline. The numerical rank/proficiency advancement system is still undecided; historical `working/` records remain reference evidence.

## Native data organization

- **`data/native/skills.json`** contains ordinary skills, independently usable narrower skills and derived parent skills, plus the previously adopted standalone skills and specialized Perform/Profession skills. There is no Skills directory of per-entry files.
- **`data/native/competencies.json`** contains actual competencies: the combination of crafting skills and tool proficiencies into unified trainable capabilities. A competency can have only a crafting facet, only a tool facet, or both. Two different numeric profiles must remain distinct even where their training identity is reviewed as shared.
- **`data/conversions/skill-name-mappings.json`** holds prior-edition skill-name mappings for future imported content, not native rule semantics. The optional review audit in `data/reference/` documents why the initial list includes the concepts it does, without placing import history in Skills.

The native JSON is the Dorks & Dice rules source of truth, not a projection of historical-edition records.

## Skills

The initial 18 standard SRD skill names and short descriptions are present as native Skills. Additional previously adopted 3.x skills include Appraise, Balance, Climb, Concentration, Decipher Script, Disable Device, Disguise, Escape Artist, Gather Information, Hide, Listen, Move Silently, Open Lock, Ride, Search, Spellcraft, Spot, Swim, Tumble, Use Magic Device, Use Rope, and reviewed psionic capabilities. Some skill numerical mechanics still require native design review; they have not been silently inferred from SRD prose.

All three members of **Perception ← Listen + Spot** are skills. Likewise, Stealth, Acrobatics and Athletics are **skills**, not competencies; their component Skills retain independent numerical state. The reviewed arithmetic mean, truncation toward zero, and component-before-parent modifier order are recorded on each parent Skill.

Crafting-related capabilities are not duplicated as unrelated Skill and Competency rows. Craft specialization is modeled under the Competency catalog; Perform and Profession specialties remain skill-presented, with individually addressable children. Speak Language belongs to the separate language-proficiency subsystem, not to Skills or Competencies.

## Competencies

The native Competencies catalog contains 32 identities following the consolidation of Smithing into Blacksmithing. It retains all 20 reviewed Craft specialties and their existing implementation facets. For example:

- **Alchemy:** crafting-skill facet **and** Alchemist's Supplies tool-proficiency facet under one competency identity.
- **Carpentry:** crafting-skill facet **and** Carpenter's Tools facet under one identity.
- **Blacksmithing:** one competency identity with the historical ranked Craft facet and the **Smith's Tools** tool-proficiency facet. The former standalone native Smithing identity is no longer selectable. Armorsmithing and Weaponsmithing are recorded as specialized sub-competencies of Blacksmithing, not as automatic rank grants.
- **Forgery:** a tool competency with an explicit link to the separately adopted Forgery skill; this linkage is not a generalized claim that skills and competencies are interchangeable.

Every native competency uses a common `trainingProfile` structure: training belongs to the competency identity; governing Ability is resolved by the applicable activity or existing facet; and the future numeric progression remains pending review. Existing crafting/tool/skill facets remain authoritative for their currently defined mechanics, rather than being silently converted to an unapproved universal rank formula.

`parentCompetency` / `childCompetencies` record specialization links without assigning a derived arithmetic mean, rank inheritance, or automatic proficiency. That distinguishes sub-competencies from established derived composite Skills. Other plausible competency relationships remain candidates for separate review.

Only explicit reviewed relationships unify training. A crafting rank does not turn into a tool-proficiency bonus or vice versa. Standalone tools do not invent Craft (X) source rules.

## Historical source information

The core Skills and Competencies catalogs contain no `srd`, `srd52`, previous-edition flags, old source keys, or historical name aliases. Earlier names such as Bluff and Sense Motive are represented **only** in future import-name mapping data. Native Skill names are the chosen Dorks & Dice names.

CC BY 4.0 attributions are retained in `data/reference/srd/manifest.json` because the initial 18 descriptions came from SRD-flagged structured records. Legal provenance documentation does not create a gameplay dependency on an original ruleset. Official-SRD coverage checking is still required before treating excerpts as verified distributable text.

## Checks and remaining work

Structural tests cover the reviewed skill corpus, 18 SRD skills, Skill-to-Skill composites, the merged Blacksmithing identity, specialization links, the common competency training profile, existing competency facets and the strict boundary between core rules and import mappings. This is **not** confirmation that all native numeric Skill mechanics or all crafting/tool interactions are implemented. Those require approved schemas and gameplay-engine work.
