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

The retained source-pack inventory contains **2,522 logical entities**. After applying the five owner-approved general rules and selecting the newer role-specific monster defaults, **1,073 entries** still have multiple candidates and **590** have an unreviewed single candidate. The original 2,479 unresolved candidate records have decreased to **1,663**.

The first pass retained 467 uniquely represented catalog entries, consolidated 34 equivalent definitions, and combined 30 eligible Weapon Mastery additions. The subsequent monster pass selected **285** default monster stat blocks, mapped **23** existing single-source old monster identities as aliases, and retained **84** independently compatible older traits. The aliases were already counted in the retained source-only entries and do not produce a second reduction in unresolved candidate counts.

Selected monster records use a single `definition` plus `baselineStatus: "retained-native-definition"`. Identity aliases use `aliasOf` and have no competing native stat block. `monster-families.json` defines named Goblin/Hobgoblin/Bugbear variants. The original older abilities remain available in the reference source pack, with review work indexed by `data/reference/srd/monster-legacy-variant-review.json`.

These definitions still require conversion of imported markup and validation under fully typed native mechanics. No retained source-only entry or older-ability review entry automatically grants a feature to an ineligible character. See `docs/rewrite/native-reconciliation-statistics.json` and `docs/rewrite/native-baseline-consistency-gate.md` for current counts and remaining approval gates.

### Monster ability reconciliation

Monster content is now keyed to 5.5e's role-specific stat blocks. The initial 84 compatible older traits were followed by **141 additional independent older abilities** and **33 missing spells** restored from SRD 5.1; the old attack suites were not concatenated into the new action lists. The **249 legacy review entries** record the entire audit, and just **6 open feature questions** remain: Assassinate, Lion/Panther/Saber-Toothed Tiger/Weretiger Pounce, and Priest Divine Eminence. Those questions concern mechanic conversion within existing creatures, not whether any newer-only creature should be imported.

The current native creature definitions retain source markup pending the shared native mechanics/schema conversion. Their restored capabilities may warrant encounter-balance testing before release. The active audit is in `data/reference/srd/monster-legacy-variant-review.json` and the latest totals are in `docs/rewrite/native-reconciliation-statistics.json`.
