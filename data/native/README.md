# Native Dorks & Dice rule definitions

Store **one primary JSON file per independently authored native rule entity**. There is no universal `races.json`, `feats.json`, or `fluff-*.json` companion-file convention. Mechanical and descriptive sections can coexist in a single document; independently licensed or restricted source text must stay separately governed.

Directory names express navigation and mechanical categories, **not authoritative identity**. Every entity must have a stable ID and explicit relationships/lineage as appropriate. Changes to filenames or folder structure must not change those identities.

- `classes/base/`, `classes/prestige/`, `classes/npc/`, and `classes/sidekick/` represent distinct classes.
- `classes/subclasses/<parent-class>/` stores one subclass file per entity. The parent class link must also exist explicitly in its data.
- `species/base/` contains species; `species/subspecies/<parent-species>/` contains independently identified subspecies with explicit parents.
- Ordinary and epic class advancement are defined **with the corresponding class**, not as `classProgression` or `prestigeClassProgression` entities. Genuine distinct epic prestige classes remain prestige classes.

New categories can be added when approved subsystems require them. Placeholder directories do not constitute approved schema or gameplay definitions.
