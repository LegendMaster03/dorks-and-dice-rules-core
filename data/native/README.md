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
