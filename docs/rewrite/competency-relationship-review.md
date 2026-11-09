# Competency relationships — structural consistency review

**Scope:** Native `first-party-001` Competencies; review baseline for a later ranked proficiency design.
**Status:** Review findings and candidate relationships. This document grants no new proficiency, numerical rank, prerequisite, tool entitlement, or crafting procedure.

## Current accepted native baseline

- The native catalog has **26** competencies. `competency.smithing` is the single native Smithing competency with Smith's Tools as associated equipment. The prior native Blacksmithing identity and independent Armorsmithing/Weaponsmithing entries have been retired.
- Crafting competencies generally describe a learned method or discipline. Weapon and armor manufacture are item categories whose procedures may involve Smithing or other relevant competencies; the items do not create an additional competency level. Conditional item-specific modifiers remain possible but unassigned.
- Every native competency has a single provisional `profile` containing proficiency and a level. Superseded intermediate Craft/tool facets are not retained as separate rules; the original source publications remain available for consultation. A level scale or evaluation formula has **not** been selected.
- **Forgery:** the former historical `skill.forgery` is represented by `competency.forgery` in the native ruleset. No separate historical intermediate Skill or independently tracked training identity is required.
- Historical `Craft` classification remains reference evidence. The general `childCompetencies` / `parentCompetency` vocabulary is available in parallel to Skills, but **Smithing has no native child competencies**. No numerical composition or inheritance has been approved.
- Native Skills such as Athletics/Climb/Jump/Swim use their own **computed composite** relationship and are not precedents for automatically averaging competency values.

### Relationship vocabulary

| Relationship | Intended interpretation | Automatic numeric consequence in this baseline |
| --- | --- | --- |
| One native competency / associated tools | A single learned capability, such as Smithing, identifies relevant tools without a second training profile | None; equipment use and possession remain separate questions |
| Parent / specialization | Optional structure for independently meaningful specializations, not automatically used for item output categories | None; no native Smithing child relationship remains |
| Related competencies | Overlap or transferable knowledge without evidence of a strict parent-child relationship | None; optional scopes can be proposed later |
| Family / taxonomy | Navigation and display grouping of otherwise independent specialties | None |
| Computed composite Skill | Explicit numeric parent formula from constituent skill values | Only when a separate reviewed rule defines the formula |

## Relationship candidates: no additional hierarchy approved

| Candidate | Observation | Present disposition | Potential future question |
| --- | --- | --- | --- |
| Tinkering; Locksmithing; historical Trapmaking | Tinkering and Locksmithing are independent native methods. Trapmaking was retired from the native catalog because traps span mechanical, structural, improvised, and magical construction. | Preserve applicable native competencies; original edition mechanics may be consulted without retaining obsolete intermediate definitions or transferring training | Whether trap design deserves scoped conditional features remains open |
| Carpentry; Woodcarving; historical Bowmaking | Carpentry and Woodcarving are distinct retained methods. Bowmaking is retired from the native catalog; its original-edition mechanics can still be consulted in source publications | Preserve Carpentry and Woodcarving; evaluate bow manufacture by the applicable method and recipe | How should imported Bowmaking ranks or a bowyer feature be represented without automatic level conversion? |
| Carpentry; historical Shipmaking | Shipbuilding involves materials, planning, and coordinated trades. The obsolete intermediate Shipmaking record has been removed; native Shipmaking is retired; *VoidSea* associates component repairs with material-appropriate tools | **Approved retirement** of the native item-output identity; retain naval design and ship construction as separate future subsystem work | Does ship design justify scoped expertise or another learned capability later? |
| Alchemy; Herbalism; Poisoning | Different applications overlap in substances and methods; neither herbalism nor poisoning is universally a subdiscipline of alchemy | Preserve independent identities | Can recipes establish scoped prerequisites or substitutions? |
| Leatherworking; Cobbling | Footwear can be leatherwork, but cobbling need not cover all leatherwork, and footwear can use nonleather materials | Preserve independent identities | Is task-scoped overlap useful for repairs or manufacture? |
| Cooking; Brewing | Both concern consumable production, but neither is necessarily a specialization of the other | Preserve independent identities | Would a culinary taxonomy provide navigation without rank transfer? |
| Painting; Calligraphy; Bookbinding | Shared media and materials do not imply interchangeable techniques | Preserve independent identities | Should a particular craft project consume several competencies? |
| Gemcutting; Smithing | Some jewelry or setting work uses both, but gem cutting is not inherently smithing | Preserve independent identities | Should multi-disciplinary crafting procedures explicitly combine competencies? |

The purpose of this review is to avoid inventing new general-purpose umbrella competencies, recategorizing existing source records, or silently equating use of the same physical tools with shared training.

**Use Rope decision:** `skill.use-rope` has been retired as a native Skill. Ordinary rope work uses applicable activities and equipment; specialized rigging remains a future question rather than an automatically added Competency.

## Manufacturing method versus output review — Bowmaking, Trapmaking, and Shipmaking approved

**Status:** Bowmaking, Trapmaking, and Shipmaking retirements approved and reflected in the 26-entry native catalog. Detailed naval construction and component mechanics remain deferred; Cobbling remains retained.

### Classification criterion

Crafting competencies describe an independently meaningful craft, technique, or material-working discipline, **not merely a category of finished goods**. The narrower output of a discipline is not itself grounds for removing it: expertise in specialized production can be a real learned method. Item recipes identify the applicable tool, method, material and abilities; a named item category does not automatically define a character advancement track. Nonmanufacturing competencies such as Navigation are outside this particular criterion.

**Source examined:** *Heliana's Guide to Monster Hunting*, Second Edition v2.02, Chapter 5, “Tools and Their Products,” printed page 107. Its tool table assigns wooden bows and similar equipment to Carpenter's Tools or Woodcarver's Tools, some mechanical weapons to Tinker's Tools, leather goods to Leatherworker's Tools, and footwear to Cobbler's Tools. It does not require separately advancing Bowmaking, Shipmaking, or Trapmaking proficiencies. Tool applicability remains subject to the relevant manufacturing procedure and GM judgment. These are source observations, not new native rules.

### Priority candidates

| Competency | Assessment | Proposed next step |
| --- | --- | --- |
| **Bowmaking** | Product-focused historical Craft specialty; Loot Tavern assigns ordinary wooden bow manufacture to appropriate woodworking tools. | **Approved retirement** from native competencies. Select a crafting method such as Carpentry or Woodcarving by activity and recipe; no intermediate Bowmaking identity is required. Bow-specific benefits remain possible but are not defined. No automatic rank conversion. |
| **Shipmaking** | A vessel is a multi-material project rather than a single fabrication method. Historical 3.5e Craft (shipmaking) remains source evidence, while *VoidSea* has material-appropriate component repair tools and naval perks without separate Shipmaking proficiency. | **Approved retirement** as an independent native competency. Applicable crafting disciplines remain available; ship design, naval construction, and specialized features are separate future reconciliation questions. |
| **Cobbling** | Recognizable footwear construction, fitting, and repair craft. Loot Tavern expressly associates footwear with Cobbler's Tools. | **Retain provisionally**; no basis yet to collapse into Leatherworking |
| **Trapmaking** | Traps span different materials and methods. 3.5e has specific Craft (trapmaking) rules for mechanical traps, while 5e Thieves' Tools and Loot Tavern's Trapper features cover placing or constructing traps without a second general Trapmaking proficiency. | **Approved retirement** as a universal native manufacturing competency. Consult source construction rules as needed and allow separately defined, conditional trap-engineering benefits when approved. Construction, placement, detection and disarming remain distinct activities. |

### Trapmaking — cross-system analysis

**Disposition: approved and reflected in native JSON.** `competency.trapmaking` is no longer in the 26-entry native catalog. The following source comparisons support the retirement without requiring an obsolete intermediate record.

| Source | Source-supported mechanics | Implication for native design |
| --- | --- | --- |
| **D&D 3.5e** | The mechanical-trap construction rules use dedicated **Craft (trapmaking)** checks; base DC varies with trap challenge rating and additional trigger/reset features. Search and Disable Device are separate activities. [3.5e SRD trap rules](https://www.d20srd.org/srd/traps.htm) | Original-edition construction rules remain consultable; no intermediate historical competency entry is required. Do not assume its ranks convert one-to-one into native proficiency or level. |
| **D&D 5e** | *Xanathar's Guide to Everything*, “Thieves' Tools” (p. 84), provides a **Set a Trap** use based on the tool check and materials on hand, without separate Trapmaking training. The same tool supports disarming traps. | Ordinary improvised traps must not be gated on acquiring a second universal Trapmaking track. |
| **D&D 5.5e (2024)** | The 2024 basic tool rules use tool proficiency for applicable checks and Advantage when the applicable skill is also proficient. [2024 Basic Rules: Equipment](https://www.dndbeyond.com/sources/dnd/br-2024/equipment) | Preserve the normal tool/skill interaction and avoid introducing a redundant always-on Trapmaking check bonus. The basic tool rule does not itself define every trap construction procedure. |
| **Loot Tavern** | *Heliana's Guide to Monster Hunting*, Second Edition v2.02, Ranger **Trapper** (printed pp. 189–193): Trapper's Tools grants thieves' tools proficiency and doubles its contribution to disarm/manipulate checks; **Set Trap** grants physical trap creation with materials and time and separate magical trap creation dependent on feature progression, rest and spells. Deployment, concealment and disarming have their own procedures. | The subclass features must remain mechanically usable **without a separate native Trapmaking proficiency requirement**. Their trap options and save DCs are features, not universal advancement levels. |

#### Activity separation

| Trap-related activity | Candidate native resolution path | Boundary |
| --- | --- | --- |
| Design or assembly of mechanical components | Applicable manufacturing method, such as Tinkering, Smithing, Carpentry or Woodcarving | A trap design can need multiple materials; do not infer a universal combined check or equal competence |
| Improvised field snares | Applicable tools, materials and fieldcraft checks; or a feature that specifies a procedure | A rope-based snare does not establish a separate native Use Rope or Trapmaking progression |
| Add poison or an alchemical component | Applicable Poisoning or Alchemy competency where the procedure calls for it | Component manufacture and trap assembly are distinct |
| Set, arm, deploy or conceal a trap | The rule governing that trap, applicable Thieves' Tools check, skill or class feature | Loot Tavern's Set Trap effects, timing and DCs are not automatically replaced by normal manufacturing checks |
| Detect or investigate a trap | Applicable Perception, Investigation, Search or specialized feature | Do not grant detection merely through craftsmanship; existing Rogue Trapfinding remains meaningful |
| Disarm or safely bypass a trap | Relevant Skill, Competency, equipment, spell, or feature depending on the trap | Do not invent a universal Trap Disarming track or make routine attempts Rogue-exclusive; approved Rogue Trapfinding/Trap Sense remains meaningful |
| Create or manipulate a magical trap | Its spell, magical crafting procedure or class feature, with Arcana where specified | Ordinary material-working proficiency does not grant spellcasting or class-granted trap access |

#### Compatibility decision and unresolved mechanics

**`competency.trapmaking` has been retired from native character advancement**, and its obsolete intermediate record was removed. Recipes and trap procedures may identify applicable materials, methods, tools, and class or spell requirements. A character specializing in trigger design or trap engineering could later receive an *explicit, scoped* bonus from a feature or proficiency use; no generic trap-bonus mechanism has been adopted.

This differs from a total removal of trap-creation expertise. The source evidence supports distinct *procedures* and sometimes sophisticated design knowledge, but not the necessity of a separately leveled, all-purpose Trapmaking proficiency in every system. Historical trap DCs and Loot Tavern's class-derived trap DCs must **not** be flattened into one formula.

**Unresolved:** Whether trap-design expertise deserves a general optional feature, whether a broader mechanism-design competency is warranted in the future, and whether any future import use case actually requires translating earlier Craft (trapmaking) ranks. Retirement does not constitute approval for a new native competency, feat, DC, or conversion.

### Shipmaking — cross-system analysis

**Disposition: approved retirement, reflected in native JSON.** `competency.shipmaking` is absent from the 26-entry native catalog. Detailed ship construction and its interface with crafting are outside the scope of this reconciliation.

| Source | Source-supported mechanics | Consequence for native design |
| --- | --- | --- |
| **D&D 3.5e** | [Craft (Int)](https://dndtools.net/skills/craft/) explicitly lists **shipmaking** among separately ranked Craft specialties; it does not make its rank identical to Carpentry. [Profession (Wis)](https://www.35srd.com/players/skills/skilldescriptions/profession/) addresses broader vocations rather than the manufacture of items. | Original-edition Craft (shipmaking) can be consulted in its source publication; no intermediate entry is required. Do not convert its ranks automatically to Carpentry, another competency, or a shipwright feat. |
| **D&D 5e** | The **Shipwright** background in *Ghosts of Saltmarsh* (p. 33) grants Carpenter's Tools and Vehicles (water) proficiencies. Its hull repair feature uses Carpenter's Tools and wood rather than a separate Shipmaking proficiency. [Shipwright background summary](https://dnd-5e.fandom.com/wiki/Shipwright) | Hull repair has a direct tool-based precedent. Water-vehicle proficiency is distinct from building or designing a vessel; a background feature does not establish a universal shipbuilding skill. |
| **D&D 5.5e / 2024** | [Equipment / Tool Proficiency](https://www.dndbeyond.com/sources/dnd/br-2024/equipment) governs tool checks and applicable skill interaction. [Official 2024 crafting overview](https://www.dndbeyond.com/posts/1836-help-your-players-get-crafty-with-the-2024-dungeon) ties item crafting to appropriate tools, materials and time. | Use the existing native competency/tool relationship where a particular ship component or repair uses an applicable method. These general rules are **not** a ready-made multi-crew shipbuilding procedure. |
| **Loot Tavern — Heliana's Guide** | *Heliana's Guide to Monster Hunting*, Second Edition v2.02, Chapter 5, printed pp. 105–110: ordinary item Manufacturing uses appropriate tools, materials, checks and work time; “Tools and Their Products” (p. 107) gives method/material associations. This chapter is not the complete account of Loot Tavern's ship systems. | Relevant general crafting context only; do not infer the absence of a naval construction system from this one book. |
| **Loot Tavern — VoidSea** | *VoidSea — Beta 0.1* (user-provided [PDF](https://drive.google.com/file/d/1MAakMPUBAuO-qa3bja1811or_dEsiYgN/view)), **Introduction** (p. 7) uses standard 5e and treats *Heliana's Guide* as an optional richer crafting reference. **Ship Components** (pp. 17–20) specifies modular component types and a distinct **repair** procedure using tools appropriate to the material (e.g., Smith's or Tinker's Tools for metal; Carpenter's or Woodcarver's Tools for wood). Component grades and qualities affect statistics. **Bosun Perks** (pp. 30–32) enable specialized component crafting, upgrades and repairs. | The inspected beta has **no separate Shipmaking proficiency or comprehensive ship-construction procedure**. Its ship-specific repair, component quality and naval progression rules coexist with existing tool-based crafting methods; they are not a separate universal ship-crafting skill. A later naval-system pass can reconcile those mechanics without requiring a new competency in this pass. |

#### Shipbuilding is several distinct activities

| Activity | Candidate existing native capability | Boundary |
| --- | --- | --- |
| **Plan a ship** (hull form, stability, load, structure, systems) | Naval design/engineering knowledge could be represented by an explicitly scoped check or future feature; no automatic assignment is made | **Navigation**, **Cartography**, and **Profession (Sailor)** do not automatically grant naval architecture expertise |
| **Fabricate a hull or internal wooden structure** | Carpentry, Woodcarving, or other appropriate method based on the project | A skilled carpenter is not automatically a qualified ship designer; a shipwright may still need specialized planning |
| **Make fittings, rigging, sails, mechanisms** | Smithing, Weaving, Leatherworking, Tinkering, or other methods as applicable | Different materials and vessel types can change which crafts contribute |
| **Assemble and supervise a complete vessel** | A future multi-craft project procedure, with roles and coordination defined explicitly | Do not infer average competency levels, a single universal check, mandatory crew proficiency, or an arbitrary build duration |
| **Repair a vessel** | Appropriate method and tool; historical Shipwright background provides a scoped hull-repair benefit | General repair and the special background feature are not automatically identical |
| **Navigate or operate a vessel** | Navigation and the relevant Skills, tools, and vehicle-training permissions | Route finding, piloting, and vessel construction are separate capabilities; native Profession (Boater/Sailor) does not determine construction level |

#### Approved classification and scope boundary

**Retire `competency.shipmaking` from the native character advancement catalog.** *VoidSea* reinforces method/material-based component work and does not require an independently trained Shipmaking proficiency. Its modular component, repair, and upgrade rules do not define a new universal crafting-track identity. Detailed shipbuilding remains out of scope.

**Do not automatically introduce “Naval Architecture.”** Its possible role, any conditions for shipwright specialization, and any relation to Carpentry or other competencies require a later naval-system reconciliation. A simple raft and a complex seagoing warship need not share the same construction or design requirements.

No shipwright-specific bonus, ship-design proficiency, tool mapping, recipe, check/DC, rank conversion, or additional Skill is approved here. The obsolete intermediate Shipmaking record was removed. This pass should not implement or harmonize ship construction, component crafting/quality tiers, repair, navigation, or naval combat.

**Open questions for the future naval review:** how to represent ship design expertise, whether component/ship work relies on one or several crafting methods, how specialized naval features interface with competency levels, and how historical Shipmaking ranks convert without falsely granting broad carpentry expertise.

### Wider consistency scan

| Current competencies | Preliminary treatment |
| --- | --- |
| Basketweaving; Bookbinding | Retain provisionally as identifiable production techniques, although their outputs are specialized |
| Carpentry; Woodcarving | Retain as separately meaningful methods despite overlapping source item eligibility |
| Locksmithing | Retain manufacturing identity distinct from Open Lock Skill and Tinkering; exact task overlaps remain for the mechanical review |
| Gemcutting; Sculpting | Retain as fabrication methods, not universal finished-item categories |
| Disguise; Open Lock; Thieves' Tools | Disguise is a Competency; Open Lock is a Skill; Thieves' Tools are equipment, with 5.x tool-grant effects to be reconciled separately |
| Navigation | Retain as nonmanufacturing competency |
| Alchemy; Brewing; Calligraphy; Cartography; Cooking; Forgery; Glassblowing; Herbalism; Leatherworking; Painting; Poisoning; Pottery; Smithing; Stonemasonry; Tinkering; Weaving | No change indicated by this method-versus-output review |

### Boundaries and follow-up

- Each native competency still has **one proficiency state and one independently advancing level**. This review grants no level-dependent bonus, prerequisite, or proficiency-transfer rule.
- Item-type-specific benefits may be represented by **conditional modifiers** without introducing new competencies, but no new modifier has been approved.
- Multi-craft projects do not imply rank averaging, shared proficiency, automatic prerequisites, or a particular number of checks.
- Superseded intermediate 3.x Craft specialty entries have been removed where reconciliation is complete. Source publications can still be consulted, but retirement does not establish a blanket conversion of prior Craft ranks.

**Next review:** Proceed with tool-name versus method-name identities and remaining Skill/Competency overlaps. Naval construction remains deferred, and Cobbling remains independent unless evidence demonstrates redundant mechanics.

## Skill and equipment-named competency overlap — approved identity reconciliation

**Status:** The native draft contains **26 Competencies and 84 Skills**. Disguise is one Competency, Open Lock is a Skill, and Use Rope and the general Disable Device Skill have been retired. Lockpicking, Trap Disarming, and the earlier Security Bypass were provisional Competencies; all have been removed. **Thieves' Tools remain equipment that can have a separately granted, binary equipment proficiency**, rather than a native independently advancing Competency.

### Disguise

**Approved:** `competency.disguise` represents the preparation and maintenance of disguises, with **Disguise Kit** as associated equipment. `skill.disguise` and `competency.disguise-kit` are retired. Social impersonation may also involve Deception or another applicable Skill; the general rules must later specify any interaction without automatic double-counting.

### Open Lock and Thieves' Tools

**Approved:** `skill.open-lock` is an ordinary Skill governing attempts to open locks without their intended keys. Its success or failure is resolved through the applicable Skill check and lock-specific rules. **Thieves' Tools** are equipment. Proficiency with them is a narrow equipment qualification, not a second native independently leveled competency. The manufacturing competency **Locksmithing** remains distinct: a character skilled at making or repairing locks is not automatically skilled at picking them.

**Scope:** Picking a lock is distinct from making, repairing, or destroying it. **Thieves' Tools proficiency** remains available as a separately granted qualification in the shared weapon/armor/shield/tool equipment-proficiency framework. Possession of the tools, proficiency with them, and the Open Lock Skill are distinct. An equipment grant does not automatically grant a Skill, and the Skill does not automatically grant equipment proficiency. Their exact mechanical interaction remains to be reconciled.

**5.x compatibility boundary:** The 2014 tool rules allow proficiency to contribute to lock opening and trap disarming; the 2024 tool rules also recognize a tool/Skill proficiency interaction. [2014 Basic Rules](https://www.dndbeyond.com/sources/dnd/basic-rules-2014/equipment) and [2024 Basic Rules](https://www.dndbeyond.com/sources/dnd/br-2024/equipment). The identity reconciliation preserves the capability to represent these grants without adopting possession-only access, automatic Skill proficiency, an extra level track, or a new universal numeric formula.

### Disarming traps as an activity, not a competency

**Approved:** Disarming or safely bypassing a trap uses the **relevant Skill, Competency, equipment, spell, or feature**, as appropriate to that trap. There is **no universal `competency.trap-disarming`** or `skill.disable-device` advancement track. Trap disarming is a goal that can require materially different techniques depending on the triggering mechanism, hazard, and construction.

| Activity | Approved classification or unresolved mechanics |
| --- | --- |
| Pick a physical lock without a key | `skill.open-lock` and applicable tools/lock procedure |
| Detect or identify a trap | Search, Perception, Investigation, or applicable feature; detecting does not automatically disarm |
| Neutralize a mechanical tripwire or pressure plate | Relevant technique, Skill/Competency, equipment, and the trap's procedure |
| Neutralize a poison, structural, or other unusual trap | Appropriate method, equipment, or feature; no universal Trap Disarming level |
| Sabotage or repair a general mechanism | Tinkering, Locksmithing, or another applicable method or feature, not a general Disable Device Skill |
| Construct, place, or conceal a trap | Crafting methods, materials, equipment, or the relevant class/spell feature |
| Disable a magical trap | Whatever spells, magic-related capabilities, or explicit feature permissions apply to that effect |

**Rogue feature boundary:** The approved [Rogue Band 1 reconciliation](rogue-band-1-reconciliation.md) retains Trapfinding and Trap Sense. Those features can grant additional trap-handling capabilities without making routine attempts Rogue-exclusive. The 3.5e Disable Device DC+10 benchmark remains source reference only; the native check and bypass procedures are not yet approved.

### Use Rope

**Approved retirement:** `skill.use-rope` remains removed, with no general Ropework competency. Ordinary rope tasks use applicable Skills and equipment procedures. Specialized rigging could be reviewed separately where it represents an independently learned technique.

### Remaining overlaps

- **Profession (Brewer/Cook/Herbalist/Apothecary)** versus Brewing, Cooking, Herbalism, and Alchemy: decide whether employment/professional knowledge and production skills warrant separate progression.
- **Performance** (`skill.performance`) versus the **Perform** family: determine whether there are duplicate training identities.
- **Locksmithing/Tinkering** and **Disguise/Deception**: resolve overlap by activity and the general Skill/Competency mechanics rather than by sharing a tool name.
- **Shared equipment proficiency**: weapon, armor, shield, and tool qualifications use a common structural grant/eligibility framework. Thieves' Tools proficiency is a narrow qualification, not a leveled Competency; exact data representation and check effects remain open. Do not duplicate Smithing or other Competencies just because they name associated tools.

**Approved equipment-proficiency direction:** Keep binary, non-leveling equipment qualifications within a common weapon/armor/shield/tool framework. Grant them explicitly and apply them only within the relevant equipment scope, without automatically duplicating existing Competency training. **Unresolved:** check effects, grant schema, 5.x tool/Skill interaction, stacking, prerequisites, DCs, and progression formulas. No executable equipment proficiency change is implemented.

## Follow-up decisions needed before ranked progression

1. **Progression model:** whether ranks measure check bonuses, qualification thresholds, access to techniques, or a combination; whether all competencies use the same rank schedule.
2. **Conditional item-specific modifiers:** determine whether features may grant bonuses for making particular item types without creating new competencies or automatic proficiency transfers.
3. **Equipment requirements:** distinguish the competency from possession of Smith's Tools, Alchemist's Supplies, or other required equipment. A tool is not a second character advancement track unless explicitly approved.
4. **Duplicate grants and origins:** how repeated competency training from Background, Upbringing, Class, Species, or feats interacts with rank allocation.
5. **Crafting and harvesting:** preserve Loot Tavern Assessment/Carving creature-type checks, required tool qualifications, qualified guidance and helper proficiency contributions; do not infer proficiency from a related competency.
6. **Compatibility:** native `competency.blacksmithing`, `competency.armorsmithing`, and `competency.weaponsmithing` have been replaced or retired. Superseded working entries are removed. Compatibility mappings are not required for native gameplay; if an actual importer or consumer later needs a mapping, it must be justified explicitly, and the runtime must not load `working/` as a fallback.

## Implementation boundaries

Only the JSON draft has been revised. Existing C# training presentation and mechanics consumers still implement the older contract; they must not be treated as an authoritative interpretation of the provisional competency profile.

The current structural profile expresses proficiency plus level within one native competency. Numerical progression and governing Ability behavior are still undecided; these should not be inferred from historical source facets. Crafting item categories do not imply separate competency identities. Independent specializations, if any are proposed later, require their own mechanical review.
