# Pillars 1 and 3 — Weapon proficiency and mastery grant audit

**Date:** 2026-11-10  
**Native ruleset:** `first-party-001`  
**Working branch:** `feature/character-origins-equipment-contract-20261110`  
**Status:** Canonical source-grant reconciliation against the merged equipment proficiency / Weapon Mastery contract. **This is not a certification that mastery combat effects, item variants, or the Character selection executor are implemented.**

## Established shared contract

- A **weapon category** is a type of equipment, with canonical types (`equipment.weapon.*`) and explicitly defined family groups (`equipment.group.weapon.family.*`). Simple/Martial and Melee/Ranged are independent classification groupings. A named **weapon type** does not imply family proficiency.
- `equipmentProficiencyGrants` grants weapon/equipment **proficiency**, either through a canonical fixed type/group target or an authorized choice. A group named as a **choice pool** identifies which individual types are eligible; it does not award the group itself.
- `weaponMasteryGrants` separately supplies **Mastery entitlements**. A mastery choice requires proficiency in the selected weapon; no proficiency source automatically grants Weapon Mastery. The common training mask is **0** (neither), **1** (proficiency only), **2** (mastery entitlement without proficiency, inoperative), **3** (proficiency and usable Mastery).
- A weapon or its magical variant normally resolves to the same canonical base type for qualification purposes when the item is explicitly linked. Possession never grants proficiency. A class source, Origin source, feat or feature must still be acquired and authorized.

## Audit of currently authored native sources

| Source | Native proficiency / mastery status | Disposition |
| --- | --- | --- |
| Barbarian | Simple + Martial weapon **group** proficiency; separate class-feature Mastery choice for 2 proficient melee weapon types at level 1, 3 at level 4, 4 at level 10 | Already canonical; no broad automatic Mastery grant |
| Bard | Simple weapon group and seven specified individual weapon-type proficiencies; retained historical choice of one Martial weapon | **Updated:** one counted `equipment-type` choice from the Martial group, in addition to the already-authored fixed grants. A redundant selection does not award a second proficiency or a replacement. No Mastery |
| Cleric | Simple weapon group proficiency | Already canonical; no native Mastery grant |
| Druid | Eleven specified individual weapon-type grants | Already canonical; do **not** replace the list with all Simple/Martial or a broader family scope. No native Mastery grant |
| Rogue | Simple weapon group, additional specific type grants and the expressly authored Martial Finesse-or-Light group; separate class-feature Mastery choice of two **proficient** weapon types | Already canonical. Mastery choice does not imply all martial weapons are proficient |
| Sorcerer | Simple weapon group proficiency | Already canonical; no native Mastery grant |
| Warlock | Simple weapon group proficiency | Already canonical. Pact of the Blade's proficiency **while bonded** is conditional and must **not** be converted into an unconditional permanent group/type grant. The Pact explicitly does not confer automatic Mastery |
| Wizard | Simple weapon group proficiency | Already canonical; no native Mastery grant |
| High Elf Upbringing | Fixed Longsword, Shortsword, Shortbow and Longbow base weapon-type proficiency | Already authored under the merged catalog; no family or Mastery grant |
| Wood Elf Upbringing | Fixed Shortbow and Longbow base weapon-type proficiency | Already authored under the merged catalog; no family or Mastery grant |
| Gladiator Background | One mundane, arena-appropriate **specific** weapon proficiency with matching ordinary starting weapon | **Updated:** `equipmentProficiencyGrants.choices` selects one individual canonical base weapon type from `equipment.group.weapon.all`. The group is only the **candidate pool**, not proficiency with all weapons. No Mastery |

The other currently selected-native Species/Ancestries, Backgrounds, and Upbringings do not require a newly inferred weapon grant. The retained nonweapon qualifications (Thieves' Tools, Gaming Sets, instruments, shields, armor) remain distinct; they do not gain Weapon Mastery. Generic class `weaponProficiencies` display/source fields are **not** executable qualification authority; the typed grant blocks are.

## Scope and remaining independent work

1. **Gladiator source choice:** The proficiency benefit and starting item now explicitly reference the same source-owned `equipmentTypeChoiceId: "arena-weapon"`. The starting-item entry also declares `itemRequirements` for matching base type, mundanity, low cost, and arena appropriateness. This corrects the **authored linkage**, not the runtime: the generic equipment resolver cannot prove the chosen item meets those source-specific conditions. A future Origin selection authority must validate the whole pair before granting the item and proficiency. The historical examples **Net** and **Trident** have not yet been reconciled as native types, so this selection does not invent them.
2. **Warlock Blade:** Weapon proficiency exists only for the currently bonded/conjured Pact weapon, including its applicable item identity. A permanent generic melee-weapon grant would be mechanically incorrect. A conditional equipment entitlement or another correctly authorized interaction is still required for machine execution.
3. **Source promotion boundary:** Fighter, Paladin and Ranger have retained candidate Weapon Mastery features under `working/`, but not an approved currently native base-class/feature package. Do **not** auto-promote their whole class or invent additional grants merely to fill out the current Mastery list.
4. **Mastery properties are not mastery entitlements:** The merged common evaluator can validate that a selected weapon's Mastery flag is usable, but does not itself define or execute the item's actual **Cleave, Graze, Nick, Push, Sap, Slow, Topple or Vex** combat effect. The working weapon/item sources retain some mappings, but the native proficiency catalog does not presently link each of its 25 weapon types to an authoritative Mastery property, and the eight property rules remain working content. Do not present the combined training flag as completed combat resolution. Some native weapon types also lack a straightforward selected working item definition; do not invent those missing property assignments.
5. **Item identity and equipment scope:** The native type catalog is intentionally representative, not a complete weapon inventory. New base types should be reconciled with a proper family and Simple/Martial and Melee/Ranged memberships. This is separate from owned-item instances and magic-item templates.

## Validation boundary

The content review checked the **25** existing native weapon types, their **21** weapon groups/classification identities, the **eight** native base classes, **two** native Mastery feature sources and **three** current Origin weapon-grant sources. All **179** manually evaluated static contract/reference assertions passed following these changes. The added regression tests check authored grant targets, choice identity/cardinality, the three orthogonal weapon classification dimensions, and prerequisite-gated Mastery without freezing catalog sizes.

### Executed branch validation (PR #138, November 10 US Eastern / November 11 UTC)

GitHub Actions ran against the Character Origins branch after the audit:

- **.NET Release build:** passed with zero warnings and zero errors.
- **Mechanics / Host tests:** **99/99** and **111/111** passed.
- **Dedicated Pillars 1/3 tests:** **39/39** Character Origins, **7/7** Goliath Upbringing, and **6/6** native weapon training tests passed (**52/52** total).
- **Rewrite structure validation:** passed.
- **Full Python suite:** **375 tests**, **two remaining failures**. One is the unrelated native Creature-type inheritance-versus-working-evidence mismatch; the other is the wider native/working comparison reporting multiple class, Competency and Weapon Mastery content differences already outside this Origin-specific validation pass. CI stops before later Docker and end-to-end smoke stages. Those stages are **not certified** by this run.

The earlier Bard retained-source comparison failure was corrected by verifying its new executable Martial-weapon choice link without changing the historical working copy. Four pinned SRD `subspecies.*` source identities (Hill Dwarf, High Elf, Rock Gnome and Lightfoot Halfling) now explicitly map to their already approved `ancestry.*` native IDs in the source coverage manifest. Original SRD identities, kinds and pinned source counts remain unchanged; the baseline and split validators check the mapped targets. This is **migration bookkeeping**, not a new Species or Ancestry gameplay rule.

**Status:** The authored weapon proficiency and Mastery grants have executable static test coverage, but actual Character source authorization, item-instance matching, conditional Pact entitlements and mastery combat effects remain unimplemented. PR #138 is draft only; no Character Origins changes have been merged into `main`.
