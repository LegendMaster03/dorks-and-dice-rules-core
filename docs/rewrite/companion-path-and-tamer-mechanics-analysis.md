# Shared companion paths and Tamer-inspired mechanics — source review

**2026-10-23 — Status:** The owner **approved mutually exclusive base-class companion paths**; all **Tamer-inspired gameplay additions remain proposals awaiting separate review**. No executable companion/familiar code has changed. Do not confuse this approval with authorization to import a proprietary Tamer class or its mechanics verbatim.

## Approved class-access structure

- **Sorcerer / Wizard:** A class-granted companion choice between **bonded/persistent familiar** and **ephemeral/conjured familiar**. Familiar-specific progression is still open, with the owner having a Tamer-inspired idea to evaluate.
- **Druid:** A choice between the **historical persistent Animal Companion**, which is **not technically a familiar**, and the modern temporary **Wild Companion** familiar. This **supersedes** the earlier approval for both to coexist automatically. Existing Druid native class data/tests have **not yet been migrated**; the original level-1 versus level-2 grants need explicit transition/timing treatment.
- Class features, spells, feats or campaign rules could explicitly give a character another creature; the shared **default class path** does not itself grant more. Learning `Find Familiar` is not equivalent to selecting an additional companion path.

## External reference inspected

The owner uploaded **Tamer Class Lite** (24-page free-to-access PDF, Loot Tavern / Heliana's Guide to Monster Hunting) and its `module.json` Foundry VTT manifest (public module v1.0.1). The attached manifest **only lists remote Foundry compendium pack paths**, including Tamer base class, player/familiar features, creatures and spells; **pack data was not provided by this manifest**. The reference PDF contains original publisher-identified material, and its page 24 specifies that the publisher's original classes, player options, characters and spells are **not open content**. Do not upload/source-copy these proprietary entries into the native SRD or public Dorks & Dice rules repository without appropriate permission; use high-level systems analysis and original mechanics design.

Source-verified **high-level distinctions** (summaries only; not native rules):

| Dimension | Tamer Lite reference | Shared-system observation |
| --- | --- | --- |
| Bonded **roster** | Grows from 1 companion at character level 1 to **6** at level 19; milestones at 1, 3, 7, 11, 15 and 19 | Roster capacity and on-field count are different mechanics |
| **Simultaneous deployment** | Normally only **one active** companion; level-20 feature briefly permits **three** together | Active cap must be separate from owned roster capacity; high-cap exception should remain specialist |
| Storage / replacement | Individual companion storage vessels, recall, deployment and release, with separate rules for equipment and effects | A generalized bond-storage interface is possible; source-specific vessel language is not a universal SRD rule |
| Advancement | Per-companion training on levels and retroactive catch-up, with specialist improvements | Generic per-companion progression could be extensible but Tamer's rate and trees are **class-specific**, not automatic for other classes |
| Eligibility | Creature selection limited by creature type, size and CR, increasing with Tamer level | Bound each class's companions to separate source/DM eligibility profiles; no universal free creature capture |
| Combat economy | Companions use shared initiative but actions need Tamer commands; Multiattack and summoning-combat benefits unlock as class features | Integrate with approved controlled-creature initiative; retain class-specific command permissions |
| Creature safety and balance | Source addresses defeated companions, rest, special recharge/legendary abilities and encounter effects | Shared subsystems need explicit treatment, not blind reuse of monster stat blocks |
| Supporting class powers | Specialist companion healing, spell linkage, modified behavior, location switching and high-level multi-companion support | These are hallmarks of the Tamer class and should not be given to every class as generic companion infrastructure |

PDF evidence locations: Tamer class/levels **pages 5–6**; per-companion training **page 7**; roster size, eligibility and taming **pages 8–9**; limitations on special monster powers **page 10**; companion-related class abilities **pages 11–12**; high-level simultaneous deployment **page 13**. The reference's familiar upgrade example is on **pages 15–18**.

## Owner-defined companion power philosophy — 2026-10-23

**Owner direction, not yet a numeric feature specification:** A **bonded familiar** or **persistent animal companion** should feel like an **extension of an otherwise capable player character**. The bonded creature should enhance its owner's own class identity, personal actions, magic, perception, movement, defenses or tactical choices. A character retaining most of their effectiveness without the creature should become more flexible, expressive and capable when paired with it. Preserve the companion's own identity and continuity without making it a second independently class-equivalent hero.

**Distinct future specialist archetype, not being implemented:** If a Tamer class is ever added, its fictional and mechanical center of gravity is the **team**: the trainer contributes leadership and tactical coordination, while several complementary creatures collectively supply much of the combat capability. The owner's Ash-and-Pokémon analogy is illustrative of team dependency, not an adoption of any Pokémon or proprietary Tamer content. **No Tamer class implementation is in scope.**

**Advancement consequence:** The progression of ordinary bonded creatures should prioritize **owner–companion synergy**, reliable execution of their intended roles and relevant survivability. Source-backed familiar and animal-companion improvements can be retained after reconciliation, but a generic per-level independent combat upgrade or the Tamer's broad monster-training tree must **not** automatically be assigned to all bonded creatures. Differentiate **Sorcerer/Wizard familiars** (extending arcane senses, magic delivery, communication and protection) from **Druid animal companions** (extending natural awareness, physical positioning, defense, movement and coordinated combat). The Druid animal can participate in combat; the design rule is about its *role relative to the Druid*, not an automatic no-attack restriction.

**Shared engine versus class budget:** Rules Core should model class-based grants, available companion actions, bond-derived cooperation, creature identity and stat progressions independently. A shared companion engine is not a reason for each class to obtain the same companion offensive scaling or command privileges. The adopted **exclusive companion-path selection** still stands; exact milestone powers, HP/proficiency formulas, special abilities and training choices require later review, and have **not** been approved by this philosophy statement.

## Proposed generic companion framework — NOT approved

**Model separate dimensions rather than a single "familiar" flag:**

1. **Grant and identity:** source class/feature, selected **path** (persistent/ephemeral), individual creature identity, eligibility and ownership.
2. **Capacity:** `bondedRosterMaximum` versus `simultaneouslyDeployedMaximum`. A character with one active slot need not have only one eligible bonded creature. Whether **non-Tamers** may maintain one *total bonded* companion or one *active* companion from a small roster is **not yet decided**.
3. **Lifecycle:** eligible acquisition, initial binding, persistent identity, summon/recall action or rest requirements, storage, release, incapacitation, defeat, restoration or replacement. Ephemeral spirits and living companions may implement different lifecycle handlers.
4. **Advancement:** a class/source-specific progression applied to each persistent companion, without letting ordinary Sorcerer/Wizard/Druid companions receive the Tamer's per-level training catalog or high-CR capture privilege by default.
5. **Combat:** shared controlled-creature initiative; source-/class-specific command costs, available actions, restrictions and triggered effects. The engine does not grant automatic attacks or multiple extra turns just for deploying a creature.
6. **Exclusivity:** one selected base-class companion path; additional companions or broader roster/deployment allowances require explicit authorized feats/features/class privileges, notably any future optional proprietary Tamer integration.
7. **Identity integrity:** model *Druid Animal Companion*, *Sorcerer/Wizard Familiar* and *Tamer bond/monster companion* as related but distinct grant types. A class's unique features modify the same underlying owner-and-companion records without silently treating all of them as `Find Familiar` spirits.

**Initial balance recommendation (pending owner decision):** Ordinary companion-eligible base classes normally maintain **one bonded companion and one deployed companion** from their chosen path. The companion can benefit from shared lifecycle/identity/storage utilities, but **not** the Tamer's expanding roster, general monster-capture system, every-level companion training tree, or multi-companion combat capstone. The Tamer, if licensed/authorized for future inclusion, can expand roster capacity and gain specialized command, training and deployment allowances. If the owner's goal is to allow ordinary classes a *small reserve roster* while limiting them to one active companion, treat that as a separate, explicit class/feat/campaign option rather than hiding it in the definition of bonded.

**Open implementation and design boundaries:** how long-lived physical companions interact with demiplane-like storage; whether bonded and conjured paths use the same recovery costs; what happens on path retraining; the Druid's level-1/level-2 timing difference; how to preserve Tamer's companion specializations; and licensing/permission for any distributed implementation of the non-SRD class. These do not revoke the approved path-choice structure.

**Note:** This document deliberately describes mechanics at a high level, does not reproduce publisher class stat blocks or feature wording, and does not authorize adding the provided PDF or proprietary Foundry compendium files to the project repository.
