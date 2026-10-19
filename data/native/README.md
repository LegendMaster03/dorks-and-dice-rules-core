# Native Dorks & Dice rule definitions

Use **one JSON per substantial native entity** for larger categories such as classes, species, and creatures. Use **one consolidated file per small, contained category**, such as `skills.json` and `competencies.json`. Do not split a skill into a mirrored competency entry; a composed/parent skill is still a skill. There is no generic `fluff-*.json` companion-file convention. Mechanical and descriptive sections can coexist in a single document; independently licensed or restricted source text must stay separately governed.

Directory names express navigation and mechanical categories, **not authoritative identity**. Every entity must have a stable ID and explicit relationships/lineage as appropriate. Changes to filenames or folder structure must not change those identities.

- `classes/base/`, `classes/prestige/`, `classes/npc/`, and `classes/sidekick/` represent distinct classes.
- `classes/subclasses/<parent-class>/` stores one subclass file per entity. The parent class link must also exist explicitly in its data.
- `species/base/` contains species; `species/subspecies/<parent-species>/` contains independently identified subspecies with explicit parents.
- Ordinary and epic class advancement are defined **with the corresponding class**, not as `classProgression` or `prestigeClassProgression` entities. Genuine distinct epic prestige classes remain prestige classes.

New categories can be added when approved subsystems require them. Placeholder directories do not constitute approved schema or gameplay definitions.

## Native skills versus competencies

`skills.json` is the Dorks & Dice Skills list. It includes the established broader/narrower Skill relationships (Perception/Listen/Spot, Stealth/Hide/Move Silently, etc.), independently usable 3.x-adopted skills, and specialized Perform/Profession skills. Skills have no historical edition aliases, imported source metadata, or need to resolve the original SRD.

`competencies.json` combines crafting skills and tool proficiencies into learned capabilities. A competency may have a crafting facet, a tool-proficiency facet, or both, and only reviewed shared training identities combine them. Skill ranks and tool proficiency calculations are distinct mechanical facets, not interchangeable bonuses. Craft specialization belongs here, rather than being spread between generic Skill entries and duplicate Competency rows.

Edition-specific names, provenance and external content mappings belong in `data/conversions/` or external reference documents, never in the core Skills catalog.

## Native conditions

`conditions.json` stores the compact native condition catalog. Exhaustion has a six-level cumulative rule approved from the tiered SRD 5.1 mechanic. References to the alternative source variant and the Owner's reconciliation decision live outside native rules in `data/reference/srd/reconciliation-decisions.json`.

## Complete staged SRD native coverage

The baseline now includes **2,522 staged logical entities**, including
already accepted native Skills/Conditions, and 2,225 per-entity JSON
documents. Small categories remain consolidated. The unreviewed files
contain `candidateDefinitions` with `baselineStatus:
"pending-native-reconciliation"`; they are **not active native rules**.
Both source variants are preserved where appropriate without introducing
parallel runtime edition engines. Original source provenance and candidate
mapping stay in `data/reference/srd/`. See
`docs/rewrite/native-srd-baseline-complete.md` for a coverage accounting.

Legal Information sections are retained in the original SRD books, not
in gameplay rule sections. Descriptive `fluff` has been attached to
the relevant staged entity instead of another physical file.

## Approved-policy reconciliation progress

The first approved five-policy normalization pass retained **467 single-source compatible catalog definitions**, reconciled **34 semantically equivalent definitions**, and added **30 compatible Weapon Mastery properties** to eligible equipment definitions. Together with **19** earlier identical Item Type collapses and **24** previously approved entities, **574 of 2,522** staged logical entries now have a single retained/approved representation. **1,358** entries still have multiple unresolved candidates and **590** have single candidates not yet cleared for native retention.

The new retained records use `baselineStatus: "retained-native-definition"` and a single `definition` object. This denotes selected catalog content, **not** an executable mechanics schema. The `definition` may retain 5etools markup and require conversion into final typed mechanics. Its presence does not automatically grant a feat, item mastery, class feature, or any other optional ability to a character.

Approved native Skills and Conditions are not displaced or overridden by staging. No unresolved candidate is made authoritative merely because it appears first in the source pack.

The machine-readable accounting is `docs/rewrite/native-reconciliation-statistics.json`. See `docs/rewrite/native-baseline-consistency-gate.md` for the remaining acceptance gates.
