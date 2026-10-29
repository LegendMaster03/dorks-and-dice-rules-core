# Native baseline data strategy

**Current approach, 2026-10-18:** Dorks & Dice native JSON files are its rules source of truth. We can **copy and adapt selected, verified SRD-licensed structured data directly** from 5etools and apply our reviewed modifications in those files. A bespoke importer for seeding the initial native rules is unnecessary. The old source-first system is not being rebuilt.

The native rules draw on three inputs:

1. **CC BY 4.0 SRD 5.1 and SRD 5.2.1 material** presented as structured 5etools JSON. Copy only relevant `srd` / `srd52` candidates, verify licensing and official-SRD coverage, and retain required attribution. The full 5etools catalog is not itself all licensed under the SRD CC grant.
2. **Reviewed mechanics and decisions in the old Rules Core on `main`**, recovered as design evidence rather than code or a compulsory source-layer database.
3. **Current design decisions in `docs/rewrite/`**, including later house rules, unified competencies, criticals, Emphasis, Touch AC, class progression and optional complexity.

Native entities follow our agreed category folders, with one primary JSON file per substantial entity or a consolidated JSON catalog for compact groups such as skills/competencies; their stable IDs and meaningful relationships do not depend on file location. Mechanical data and descriptions can share a single file. Source-lineage metadata is valuable for **future imports, edition comparisons, and external content**, but an authoritative native roll/check does not have to select an SRD document or source edition at runtime.

See [the first native skills and competencies seed](native-seed-skills-and-competencies.md) for the initial slice and its scope limitations.

**Still required before gameplay implementation:** review the native schema, coverage of approved decisions, composition/conflict semantics, and regression acceptance cases. The seed is data-only and provisional. No runtime language, persistent database, importer, deploy, access change, or merge is introduced by this step.

### Native core versus imported-source context

There is no source-edition selection in core Skill evaluation. Edition identifiers, alternate source names, source revisions and source-to-native translation tables belong outside native skills, in `data/conversions/` when needed for user imports. In particular, a composite Skill is not a Competency, and core Skills should not carry old-edition labels as metadata.

Competencies combine crafting skills and tool proficiencies under coherent learned identities with explicitly distinct mechanical facets. This is not a second catalog of all normal Skills.
