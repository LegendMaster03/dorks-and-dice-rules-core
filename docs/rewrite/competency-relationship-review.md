# Competency relationships — structural consistency review

**Scope:** Native `first-party-001` Competencies; review baseline for a later ranked proficiency design.
**Status:** Review findings and candidate relationships. This document grants no new proficiency, numerical rank, prerequisite, tool entitlement, or crafting procedure.

## Current accepted native baseline

- The native catalog has **30** competencies. `competency.smithing` is the single native Smithing competency with Smith's Tools as associated equipment. The prior native Blacksmithing identity and independent Armorsmithing/Weaponsmithing entries have been retired.
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
| Tinkering; Locksmithing; Trapmaking | Tinkering can involve mechanisms, but a lock specialist or trap maker need not practice every form of tinkering; traps need not be mechanical | Preserve independent identities; candidate **related** links, not a parent relationship | Are there particular mechanical tasks for which experience transfers? |
| Carpentry; Woodcarving; Bowmaking | Shared work with wood, but carpentry, carving, and bowmaking have materially different outputs and techniques | Preserve independent identities | Would a **Woodworking** taxonomy family help without automatically granting proficiency? |
| Carpentry; Shipmaking | Construction of a ship may need carpentry but also other materials and ship systems | Preserve independent identities | Should ship construction require one or more qualified specialties rather than inherit carpentry ranks? |
| Alchemy; Herbalism; Poisoning | Different applications overlap in substances and methods; neither herbalism nor poisoning is universally a subdiscipline of alchemy | Preserve independent identities | Can recipes establish scoped prerequisites or substitutions? |
| Leatherworking; Cobbling | Footwear can be leatherwork, but cobbling need not cover all leatherwork, and footwear can use nonleather materials | Preserve independent identities | Is task-scoped overlap useful for repairs or manufacture? |
| Cooking; Brewing | Both concern consumable production, but neither is necessarily a specialization of the other | Preserve independent identities | Would a culinary taxonomy provide navigation without rank transfer? |
| Painting; Calligraphy; Bookbinding | Shared media and materials do not imply interchangeable techniques | Preserve independent identities | Should a particular craft project consume several competencies? |
| Gemcutting; Smithing | Some jewelry or setting work uses both, but gem cutting is not inherently smithing | Preserve independent identities | Should multi-disciplinary crafting procedures explicitly combine competencies? |

The purpose of this review is to avoid inventing new general-purpose umbrella competencies, recategorizing existing source records, or silently equating use of the same physical tools with shared training.

**Use Rope review:** `skill.use-rope` remains a native Skill. Moving it to a competency or removing it entirely is under consideration, not approved. Ordinary rope tasks and specialized ropework should be evaluated separately before changing the catalog.\n\n## Follow-up decisions needed before ranked progression

1. **Progression model:** whether ranks measure check bonuses, qualification thresholds, access to techniques, or a combination; whether all competencies use the same rank schedule.
2. **Conditional item-specific modifiers:** determine whether features may grant bonuses for making particular item types without creating new competencies or automatic proficiency transfers.
3. **Equipment requirements:** distinguish the competency from possession of Smith's Tools, Alchemist's Supplies, or other required equipment. A tool is not a second character advancement track unless explicitly approved.
4. **Duplicate grants and origins:** how repeated competency training from Background, Upbringing, Class, Species, or feats interacts with rank allocation.
5. **Crafting and harvesting:** preserve Loot Tavern Assessment/Carving creature-type checks, required tool qualifications, qualified guidance and helper proficiency contributions; do not infer proficiency from a related competency.
6. **Compatibility:** native `competency.blacksmithing`, `competency.armorsmithing`, and `competency.weaponsmithing` have been replaced or retired. Preserved `working/` and source representations remain historical evidence. An explicit future compatibility mapping can be reviewed if a consumer still emits those old identifiers; the runtime must not load `working/` as a fallback.

## Implementation boundaries

Only the JSON draft has been revised. Existing C# training presentation and mechanics consumers still implement the older contract; they must not be treated as an authoritative interpretation of the provisional competency profile.

The current structural profile expresses proficiency plus level within one native competency. Numerical progression and governing Ability behavior are still undecided; these should not be inferred from historical source facets. Crafting item categories do not imply separate competency identities. Independent specializations, if any are proposed later, require their own mechanical review.
