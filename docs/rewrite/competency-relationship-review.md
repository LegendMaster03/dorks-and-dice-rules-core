# Competency relationships — structural consistency review

**Scope:** Native `first-party-001` Competencies; review baseline for a later ranked proficiency design.
**Status:** Review findings and candidate relationships. This document grants no new proficiency, numerical rank, prerequisite, tool entitlement, or crafting procedure.

## Current accepted native baseline

- The native catalog has **29** competencies. `competency.smithing` is the single native Smithing competency with Smith's Tools as associated equipment. The prior native Blacksmithing identity and independent Armorsmithing/Weaponsmithing entries have been retired.
- Crafting competencies generally describe a learned method or discipline. Weapon and armor manufacture are item categories whose procedures may involve Smithing or other relevant competencies; the items do not create an additional competency level. Conditional item-specific modifiers remain possible but unassigned.
- Every native competency has a single provisional `profile` containing proficiency and a level. The historical Craft/tool facets remain in `working/` and reference material, not in the native definition. A level scale or evaluation formula has **not** been selected.
- **Forgery:** the former historical `skill.forgery` is represented by `competency.forgery` in the native ruleset. `working/skills.json` retains the source entry for conversion; no second native skill or independently tracked training is created.
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
| Tinkering; Locksmithing; Trapmaking | Tinkering and Locksmithing are plausible independent methods; native Trapmaking still exists, but traps span mechanical, structural, improvised, and magical construction | Retain existing JSON unchanged while reviewing Trapmaking as a possible **retired output-category** identity; do not transfer its proficiency to other competencies automatically | Would trap engineering expertise be better represented by conditional features than a dedicated Trapmaking level? |
| Carpentry; Woodcarving; historical Bowmaking | Carpentry and Woodcarving are distinct retained methods. Bowmaking is retired from the native catalog; historical Craft (bowmaking) remains source evidence | Preserve Carpentry and Woodcarving; evaluate bow manufacture by the applicable method and recipe | How should imported Bowmaking ranks or a bowyer feature be represented without automatic level conversion? |
| Carpentry; Shipmaking | Construction of a ship may need carpentry but also other materials and ship systems | Preserve independent identities | Should ship construction require one or more qualified specialties rather than inherit carpentry ranks? |
| Alchemy; Herbalism; Poisoning | Different applications overlap in substances and methods; neither herbalism nor poisoning is universally a subdiscipline of alchemy | Preserve independent identities | Can recipes establish scoped prerequisites or substitutions? |
| Leatherworking; Cobbling | Footwear can be leatherwork, but cobbling need not cover all leatherwork, and footwear can use nonleather materials | Preserve independent identities | Is task-scoped overlap useful for repairs or manufacture? |
| Cooking; Brewing | Both concern consumable production, but neither is necessarily a specialization of the other | Preserve independent identities | Would a culinary taxonomy provide navigation without rank transfer? |
| Painting; Calligraphy; Bookbinding | Shared media and materials do not imply interchangeable techniques | Preserve independent identities | Should a particular craft project consume several competencies? |
| Gemcutting; Smithing | Some jewelry or setting work uses both, but gem cutting is not inherently smithing | Preserve independent identities | Should multi-disciplinary crafting procedures explicitly combine competencies? |

The purpose of this review is to avoid inventing new general-purpose umbrella competencies, recategorizing existing source records, or silently equating use of the same physical tools with shared training.

**Use Rope review:** `skill.use-rope` remains a native Skill. Moving it to a competency or removing it entirely is under consideration, not approved. Ordinary rope tasks and specialized ropework should be evaluated separately before changing the catalog.

## Manufacturing method versus output review — Bowmaking approved; other candidates pending

**Status:** Bowmaking retirement approved and reflected in the 29-entry native catalog. Shipmaking and Trapmaking remain under review; Cobbling remains retained.

### Classification criterion

Crafting competencies describe an independently meaningful craft, technique, or material-working discipline, **not merely a category of finished goods**. The narrower output of a discipline is not itself grounds for removing it: expertise in specialized production can be a real learned method. Item recipes identify the applicable tool, method, material and abilities; a named item category does not automatically define a character advancement track. Nonmanufacturing competencies such as Navigation are outside this particular criterion.

**Source examined:** *Heliana's Guide to Monster Hunting*, Second Edition v2.02, Chapter 5, “Tools and Their Products,” printed page 107. Its tool table assigns wooden bows and similar equipment to Carpenter's Tools or Woodcarver's Tools, some mechanical weapons to Tinker's Tools, leather goods to Leatherworker's Tools, and footwear to Cobbler's Tools. It does not require separately advancing Bowmaking, Shipmaking, or Trapmaking proficiencies. Tool applicability remains subject to the relevant manufacturing procedure and GM judgment. These are source observations, not new native rules.

### Priority candidates

| Competency | Assessment | Proposed next step |
| --- | --- | --- |
| **Bowmaking** | Product-focused historical Craft specialty; Loot Tavern assigns ordinary wooden bow manufacture to appropriate woodworking tools. | **Approved retirement** from native competencies. Select a crafting method such as Carpentry or Woodcarving by activity and recipe; retain historical identity in `working/`. Bow-specific benefits remain possible but are not defined. No automatic rank conversion. |
| **Shipmaking** | A complex final project requiring several possible crafts, materials, and design expertise, not one universal manufacturing method. | **Candidate project-based treatment**; determine whether specialized naval design remains separately trained before any retirement |
| **Cobbling** | Recognizable footwear construction, fitting, and repair craft. Loot Tavern expressly associates footwear with Cobbler's Tools. | **Retain provisionally**; no basis yet to collapse into Leatherworking |
| **Trapmaking** | Traps span different materials and methods. 3.5e has specific Craft (trapmaking) rules for mechanical traps, while 5e Thieves' Tools and Loot Tavern's Trapper features cover placing or constructing traps without a second general Trapmaking proficiency. | **Recommend retirement as a universal native manufacturing competency, subject to approval.** Preserve historical construction rules as source evidence and allow explicit conditional trap-engineering expertise. Do not conflate construction with setting, detection, or disarming. |

### Trapmaking — cross-system analysis

**Disposition: proposed for approval, not implemented.** `competency.trapmaking` remains in the 29-entry native catalog. The following evidence informs a possible future retirement.

| Source | Source-supported mechanics | Implication for native design |
| --- | --- | --- |
| **D&D 3.5e** | The mechanical-trap construction rules use dedicated **Craft (trapmaking)** checks; base DC varies with trap challenge rating and additional trigger/reset features. Search and Disable Device are separate activities. [3.5e SRD trap rules](https://www.d20srd.org/srd/traps.htm) | Preserve the historical construction specialty and its mechanical formulas as source evidence. Do not assume its ranks convert one-to-one into native proficiency or level. |
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
| Disarm or safely bypass a trap | Thieves' Tools / applicable Disable Device mechanics; specialized abilities when provided | Do not make routine detection or disarming Rogue-exclusive; the approved Rogue Trapfinding/Trap Sense decision is preserved |
| Create or manipulate a magical trap | Its spell, magical crafting procedure or class feature, with Arcana where specified | Ordinary material-working proficiency does not grant spellcasting or class-granted trap access |

#### Compatibility proposal

If the retirement is approved, **remove `competency.trapmaking` only from native character advancement**, retaining historical `working/` records. Recipes and trap procedures should identify the applicable materials, methods, tools, and class or spell requirements. A character specializing in trigger design or trap engineering could receive an *explicit, scoped* bonus from a feature or proficiency use; no generic trap-bonus mechanism is adopted here.

This differs from a total removal of trap-creation expertise. The source evidence supports distinct *procedures* and sometimes sophisticated design knowledge, but not the necessity of a separately leveled, all-purpose Trapmaking proficiency in every system. Historical trap DCs and Loot Tavern's class-derived trap DCs must **not** be flattened into one formula.

**Unresolved:** Whether trap-design expertise deserves a general optional feature, whether a broader mechanism-design competency is warranted in the future, and what conversion policy to use for historical Craft (trapmaking) ranks. None of these constitutes approval for a new native competency, feat, DC, or conversion.

### Wider consistency scan

| Current competencies | Preliminary treatment |
| --- | --- |
| Basketweaving; Bookbinding | Retain provisionally as identifiable production techniques, although their outputs are specialized |
| Carpentry; Woodcarving | Retain as separately meaningful methods despite overlapping source item eligibility |
| Locksmithing | Retain pending distinction among lock construction, Tinkering, Open Lock, and Thieves' Tools |
| Gemcutting; Sculpting | Retain as fabrication methods, not universal finished-item categories |
| Disguise Kit; Thieves' Tools | Tool-named identities warrant nomenclature/scope review, not automatic removal or loss of tool-proficiency compatibility |
| Navigation | Retain as nonmanufacturing competency |
| Alchemy; Brewing; Calligraphy; Cartography; Cooking; Forgery; Glassblowing; Herbalism; Leatherworking; Painting; Poisoning; Pottery; Smithing; Stonemasonry; Tinkering; Weaving | No change indicated by this method-versus-output review |

### Boundaries and follow-up

- Each native competency still has **one proficiency state and one independently advancing level**. This review grants no level-dependent bonus, prerequisite, or proficiency-transfer rule.
- Item-type-specific benefits may be represented by **conditional modifiers** without introducing new competencies, but no new modifier has been approved.
- Multi-craft projects do not imply rank averaging, shared proficiency, automatic prerequisites, or a particular number of checks.
- Historical 3.x Craft specialties remain in `working/` as reference evidence, including retired Bowmaking. Retirement does not establish a blanket conversion from Craft (bowmaking) to Carpentry or Woodcarving.

**Next review:** Decide whether to approve the proposed retirement of native Trapmaking; then review Shipmaking and broader tool-name/skill overlap. Cobbling remains independent unless new evidence demonstrates redundant mechanics.

## Follow-up decisions needed before ranked progression

1. **Progression model:** whether ranks measure check bonuses, qualification thresholds, access to techniques, or a combination; whether all competencies use the same rank schedule.
2. **Conditional item-specific modifiers:** determine whether features may grant bonuses for making particular item types without creating new competencies or automatic proficiency transfers.
3. **Equipment requirements:** distinguish the competency from possession of Smith's Tools, Alchemist's Supplies, or other required equipment. A tool is not a second character advancement track unless explicitly approved.
4. **Duplicate grants and origins:** how repeated competency training from Background, Upbringing, Class, Species, or feats interacts with rank allocation.
5. **Crafting and harvesting:** preserve Loot Tavern Assessment/Carving creature-type checks, required tool qualifications, qualified guidance and helper proficiency contributions; do not infer proficiency from a related competency.
6. **Compatibility:** native `competency.blacksmithing`, `competency.armorsmithing`, and `competency.weaponsmithing` have been replaced or retired. Preserved `working/` and source representations remain historical evidence. An explicit future compatibility mapping can be reviewed if a consumer still emits those old identifiers; the runtime must not load `working/` as a fallback.

## Implementation boundaries

Only the JSON draft has been revised. Existing C# training presentation and mechanics consumers still implement the older contract; they must not be treated as an authoritative interpretation of the provisional competency profile.

The current structural profile expresses proficiency plus level within one native competency. Numerical progression and governing Ability behavior are still undecided; these should not be inferred from historical source facets. Crafting item categories do not imply separate competency identities. Independent specializations, if any are proposed later, require their own mechanical review.
