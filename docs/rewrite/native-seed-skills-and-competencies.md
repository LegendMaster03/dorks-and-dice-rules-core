# Native Skills and Competencies — corrected baseline

**Status:** Provisional native competency baseline. Numerical progression remains undecided; obsolete intermediate definitions are removed when reconciled, while original external sources remain available for research.

## Native data organization

- **`data/rulesets/first-party-001/native/skills.json`** defines individual Skills, derived composite Skills, and specialized Perform/Profession entries.
- **`data/rulesets/first-party-001/native/competencies.json`** defines native Competencies as **single learned capabilities**, each with its own proficiency and level. A competency does not contain a historical Craft skill and a separate tool proficiency.
- **`data/rulesets/first-party-001/working/competencies.json`** contains only intermediate definitions still needed for ongoing design. It is not a permanent archive or a required representation of every retired identity.
- **`data/conversions/skill-name-mappings.json`** retains earlier skill-name translations for source conversion, not native mechanical definitions.

The native JSON describes the Dorks & Dice ruleset rather than imposing the source edition's internal distinction between skills and tool proficiencies.

## Skills

The 81 selected native Skills retain the existing composite and family structures. The former `skill.forgery` is represented solely by the native `competency.forgery`; no duplicate historical intermediate Skill is required. The existing Skills data includes standalone skills, independently addressable children, computed parent Skills, and Perform/Profession skill families.

A Skill has proficiency and level in character state. Its normal Ability-score interactions and any explicit composite calculations belong to the Skill mechanics. Those calculations are not automatically imposed on Competencies.

## Competencies

The native catalog contains **26** competency identities. The approved `competency.smithing` replaces the earlier provisional Blacksmithing identity. Armorsmithing and Weaponsmithing have been retired as native competencies because weapon and armor production can use multiple methods and materials. Each definition has one `profile` with:

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
- **Open Lock:** a Skill for opening locks without their intended keys, using suitable equipment. It does not confer Locksmithing or any other manufacturing competency.
- **Trap disarming:** an activity using the relevant Skill, Competency, equipment, spell, or feature appropriate to the trap. Neither Trap Disarming nor a general Disable Device Skill constitutes a separate native advancement track.
- **Thieves' Tools:** remain equipment, not a leveled competency. Their proficiency can be granted as a narrow, binary equipment qualification under the shared weapon/armor/shield/tool model. Possession, equipment proficiency, and the Open Lock Skill remain distinct; their check interaction has not yet been selected.

### Crafting-related Profession Skills consolidated

**Brewing, Cooking, and Herbalism** each serve as the single native learned-capability identity for the overlapping 3.x profession-based craft and the 5.x associated tools:

- `skill.profession-brewer` is retired in favor of `competency.brewing` and Brewer's Supplies as applicable equipment.
- `skill.profession-cook` is retired in favor of `competency.cooking` and Cook's Utensils.
- `skill.profession-herbalist` is retired in favor of `competency.herbalism` and the Herbalism Kit.

The corresponding occupation is not an additional advancement track. Activities that are not the craft itself may use other appropriate Skills and rules. Other Profession specialties are not retired solely because their names resemble a craft: Apothecary, Tanner, and Scribe require specific activity mapping before a further decision, and the remaining Profession family must be reviewed on its merits.

These consolidations do not set competency level formulas, convert historical ranks, grant or require equipment proficiency automatically, or stack proficiency bonuses.

### Item categories and conditional benefits

Native crafting competencies identify learned methods and disciplines, rather than always representing a category of manufactured product. Bowmaking, Trapmaking, and Shipmaking have been retired as separate native output-category identities. `Smithing` is a single competency; making weapons or armor does not introduce separate Armorsmithing or Weaponsmithing levels. Specific recipes and applicable rules determine which competencies, tools, and abilities an activity uses.

Item-specific benefits can later be expressed through applicable modifiers or features. **No such benefits, level formula, or multi-competency procedure is assigned by this identity reconciliation.** The existing general parent/child structure remains available for genuinely independent specialized capabilities when justified, but this draft does not use it for armor and weapons.

## Shared equipment proficiency — approved structure

The established weapon and armor proficiency concept extends to a **single equipment proficiency qualification model** spanning weapon, armor, shield, and tool categories. An equipment proficiency is a scoped qualification granted by an applicable class, background, feature, or other approved rule; it is **not a second independently leveled Skill or Competency**. It is distinct from possessing equipment and from eligibility to attempt an activity. The mechanical consequence can differ by equipment category.

This is deliberately narrower than treating every named tool as an advancing Competency. Equipment proficiency is available where explicitly granted and relevant; the existence of `associatedTools` on a Competency does **not** itself create or require separate tool proficiency. Smithing and Smith's Tools must not automatically become two advancement requirements. Thieves' Tools proficiency can coexist with the Open Lock Skill without merging their identities or creating a Trap Disarming track.

The native class data currently expresses weapon and armor grants, but the complete common equipment-proficiency representation and evaluator **have not been implemented or approved at the field/formula level**. Exact grant encoding, matching scope, prerequisites, effects on Skill/Competency checks, duplicate grants, and 5.x source-specific interactions remain for the general equipment/check reconciliation. Do not infer that all equipment categories share the same mechanical effect.

## Historical source information

The core Skills and Competencies catalogs contain no `srd`, `srd52`, previous-edition flags, old source keys, or historical name aliases. Earlier names such as Bluff and Sense Motive are represented **only** in future import-name mapping data. Native Skill names are the chosen Dorks & Dice names.

CC BY 4.0 attributions are retained in `data/reference/srd/manifest.json` because the initial 18 descriptions came from SRD-flagged structured records. Legal provenance documentation does not create a gameplay dependency on an original ruleset. Official-SRD coverage checking is still required before treating excerpts as verified distributable text.

## Checks and remaining work

While Skills and Competencies remain under active design, no test should freeze their provisional count, field layout, or historical working-catalog representation. JSON parsing and focused structural review can still be performed without imposing obsolete catalog requirements. Integration testing before deployment remains separate; executable progression mechanics are not yet implemented.
