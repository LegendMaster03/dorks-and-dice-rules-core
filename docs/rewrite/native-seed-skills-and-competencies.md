# Native Skills and Competencies — corrected baseline

**Status:** Provisional native competency baseline. Numerical progression remains undecided; obsolete intermediate definitions are removed when reconciled, while original external sources remain available for research.

## Native data organization

- **`data/rulesets/first-party-001/native/skills.json`** defines individual Skills, derived composite Skills, and specialized Perform/Profession entries.
- **`data/rulesets/first-party-001/native/competencies.json`** defines native Competencies as **single learned capabilities**, each with its own proficiency and level. A competency does not contain a historical Craft skill and a separate tool proficiency.
- **`data/rulesets/first-party-001/working/competencies.json`** contains only intermediate definitions still needed for ongoing design. It is not a permanent archive or a required representation of every retired identity.
- **`data/conversions/skill-name-mappings.json`** retains earlier skill-name translations for source conversion, not native mechanical definitions.

The native JSON describes the Dorks & Dice ruleset rather than imposing the source edition's internal distinction between skills and tool proficiencies.

## Skills

The 87 selected native Skills retain the existing composite and family structures. The former `skill.forgery` is represented solely by the native `competency.forgery`; no duplicate historical intermediate Skill is required. The existing Skills data includes standalone skills, independently addressable children, computed parent Skills, and Perform/Profession skill families.

A Skill has proficiency and level in character state. Its normal Ability-score interactions and any explicit composite calculations belong to the Skill mechanics. Those calculations are not automatically imposed on Competencies.

## Competencies

The native catalog contains **27** competency identities. The approved `competency.smithing` replaces the earlier provisional Blacksmithing identity. Armorsmithing and Weaponsmithing have been retired as native competencies because weapon and armor production can use multiple methods and materials. Each definition has one `profile` with:

- **`proficiency`:** binary proficiency as part of the competency itself, not a separately tracked tool proficiency;
- **`level`:** an independently represented advancement dimension for the same competency;
- **`levelScale`:** undecided pending the numerical progression review;
- **`abilityResolution`:** activity-dependent rather than inherited from historical Craft rules.

The profile expresses the agreed shape of the capability; it does **not** set a bonus, a tier threshold, an advancement cost, or a proficiency/level dependency.

`associatedTools` lists applicable equipment identities such as Alchemist's Supplies, Carpenter's Tools, Smith's Tools, and Forgery Kit. These names do not create additional training entries or establish possession of an item. Competencies that do not currently have a named tool association are still full competencies.

For example:

- **Alchemy:** one competency, with Alchemist's Supplies as associated equipment.
- **Carpentry:** one competency, with Carpenter's Tools as associated equipment.
- **Bow manufacture:** may use Carpentry, Woodcarving, or another appropriate method competency depending on the activity and recipe. Bowmaking is not a separate native competency or level. The superseded intermediate Bowmaking definition has been removed.
- **Trap construction:** uses the applicable method, materials, equipment, spell or feature for the particular trap; no universal `competency.trapmaking` advancement track remains. Detection, disarming, placement and construction remain distinct activities. The superseded intermediate Trapmaking definition has been removed.
- **Ship construction:** uses applicable manufacturing methods and vessel-specific rules when they are reconciled. Shipmaking is not a standalone native proficiency/level. The superseded intermediate Shipmaking definition has been removed; *VoidSea* construction, repair, and progression remain deferred to a naval-system review.
- **Smithing:** one competency, associated with Smith's Tools. Metal weapons and armor can use Smithing where appropriate, while items made using other materials or techniques use their applicable competencies. No universal weapon- or armor-specific advancement track is introduced.
- **Forgery:** one competency, with Forgery Kit as associated equipment. There is no separate native `skill.forgery` advancement identity or an obligation to retain its intermediate definition.

### Item categories and conditional benefits

Native crafting competencies identify learned methods and disciplines, rather than always representing a category of manufactured product. Bowmaking, Trapmaking, and Shipmaking have been retired as separate native output-category identities. `Smithing` is a single competency; making weapons or armor does not introduce separate Armorsmithing or Weaponsmithing levels. Specific recipes and applicable rules determine which competencies, tools, and abilities an activity uses.

Item-specific benefits can later be expressed through applicable modifiers or features. **No such benefits, level formula, or multi-competency procedure is assigned by this identity reconciliation.** The existing general parent/child structure remains available for genuinely independent specialized capabilities when justified, but this draft does not use it for armor and weapons.

## Historical source information

The core Skills and Competencies catalogs contain no `srd`, `srd52`, previous-edition flags, old source keys, or historical name aliases. Earlier names such as Bluff and Sense Motive are represented **only** in future import-name mapping data. Native Skill names are the chosen Dorks & Dice names.

CC BY 4.0 attributions are retained in `data/reference/srd/manifest.json` because the initial 18 descriptions came from SRD-flagged structured records. Legal provenance documentation does not create a gameplay dependency on an original ruleset. Official-SRD coverage checking is still required before treating excerpts as verified distributable text.

## Checks and remaining work

While Skills and Competencies remain under active design, no test should freeze their provisional count, field layout, or historical working-catalog representation. JSON parsing and focused structural review can still be performed without imposing obsolete catalog requirements. Integration testing before deployment remains separate; executable progression mechanics are not yet implemented.
