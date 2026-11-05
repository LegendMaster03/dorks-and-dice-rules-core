# Competency relationships — structural consistency review

**Scope:** Native `first-party-001` Competencies; review baseline for a later ranked proficiency design.
**Status:** Review findings and candidate relationships. This document grants no new proficiency, numerical rank, prerequisite, tool entitlement, or crafting procedure.

## Current accepted native baseline

- The native catalog has **32** competencies. `competency.smithing` was folded into `competency.blacksmithing`; Smith's Tools now belongs to Blacksmithing's tool-proficiency facet.
- Blacksmithing is a general trainable competency, and Armorsmithing and Weaponsmithing are its independently identifiable **specializations**. Parent/child references are reciprocal.
- Every native competency carries the same provisional `trainingProfile` structure. The existing `facets` retain applicable ranked-craft, tool, or skill capabilities. A common numerical advancement system has **not** been selected.
- `Craft` as a historical specialization family remains distinct from the native Blacksmithing specialization hierarchy. Neither a taxonomic family nor a specialization link implies arithmetic value composition.
- Native Skills such as Athletics/Climb/Jump/Swim use their own **computed composite** relationship and are not precedents for automatically averaging competency values.

### Relationship vocabulary

| Relationship | Intended interpretation | Automatic numeric consequence in this baseline |
| --- | --- | --- |
| Shared competency identity / multiple facets | Two established applications of the same learned discipline, such as Blacksmithing and Smith's Tools | None; each applicable facet keeps its existing calculation |
| Parent / specialization | A broad learned discipline with separately recognizable specializations, such as Blacksmithing -> Armorsmithing | None; ranks, qualification, and proficiency transfer are undecided |
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
| Gemcutting; Blacksmithing | Some jewelry or setting work uses both, but gem cutting is not inherently blacksmithing | Preserve independent identities | Should multi-disciplinary crafting procedures explicitly combine competencies? |

The purpose of this review is to avoid inventing new general-purpose umbrella competencies, recategorizing existing source records, or silently equating use of the same physical tools with shared training.

## Follow-up decisions needed before ranked progression

1. **Progression model:** whether ranks measure check bonuses, qualification thresholds, access to techniques, or a combination; whether all competencies use the same rank schedule.
2. **Specialization behavior:** whether a parent has independently purchased ranks; whether child ranks grant any general competence; whether parent ranks qualify a character for introductory specialized tasks.
3. **Equipment requirements:** distinguish the competency from possession of Smith's Tools, Alchemist's Supplies, or other required equipment. A tool is not a second character advancement track unless explicitly approved.
4. **Duplicate grants and origins:** how repeated competency training from Background, Upbringing, Class, Species, or feats interacts with rank allocation.
5. **Crafting and harvesting:** preserve Loot Tavern Assessment/Carving creature-type checks, required tool qualifications, qualified guidance and helper proficiency contributions; do not infer proficiency from a related competency.
6. **Compatibility:** native `competency.smithing` has been retired, but preserved `working/` and source representations remain historical evidence. If an imported source or external tool still emits the old identifier, an explicit compatibility mapping can be reviewed; the runtime must not load `working/` as a fallback.

## Implementation boundaries

The native training presentation exposes `SpecializationIds` and `ParentCompetencyId` separately from `ComponentIds` and `Composition`. Consumers should not derive one from the other.

The current structural profile indicates that numeric progression is undecided, not that an identical rank formula or governing Ability has already been established for every competency. New parent-child proposals must be reviewed as mechanical relationships rather than generated from naming similarity.
