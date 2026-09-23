# Competency reconciliation audit

This document classifies the reviewed 3e/3.5e competency corpus for the cross-edition Character contract. It is an implementation/audit map, not a claim that similarly named rules are interchangeable.

## Classification meanings

1. **Direct normalized identity** — the source name normalizes to the same competency identity without a separate relationship.
2. **Renamed later skill** — a reviewed direct skill-to-skill identity under a later name.
3. **Composite contributor** — the historical competency remains independent and contributes to a later umbrella competency through Rules Layer composition.
4. **Specialized competency with multiple facets** — one learned discipline has separate mechanical facets, with shared training identity but facet-specific numeric mechanics.
5. **Scoped or related competency relationship** — concepts overlap but are not the same learned competency for unrestricted proficiency.
6. **Transformed rules subsystem** — the historical function belongs to another normalized subsystem rather than a modern skill row.
7. **Preserved standalone historical competency** — no lossless reviewed convergence is currently established.
8. **Unresolved / Rules Lawyer review** — plausible transformations exist, but current evidence/modeling does not justify selecting one.

`Craft`, `Perform`, and `Profession` additionally use **family taxonomy**: the parent organizes independently addressable specialties and is not a shared rank pool. `Knowledge` deliberately does not use visible family taxonomy.

## Reviewed corpus

| Historical competency | Classification | Current reconciliation |
| --- | --- | --- |
| Alchemy (3e) | 4 | Skill facet of learned competency **Alchemy**; related later facet is Alchemist's Supplies. Ranks/class-skill mechanics stay on the skill facet. |
| Animal Empathy | 8 | Preserve independently until the relationship to later animal-handling/class-feature mechanics is reviewed without information loss. |
| Appraise | 7 | Preserved standalone. |
| Balance | 3 | Contributor to Acrobatics with Tumble; remains independently ranked. |
| Bluff | 2 | Directly normalizes to Deception. |
| Climb | 3 | Contributor to Athletics with Jump and Swim. |
| Concentration | 7 | Preserved standalone; modern concentration/save mechanics are not treated as identical. |
| Craft | Family taxonomy | Parent/category. Specialties remain independent. Only reviewed specialty/tool identities are linked. |
| Decipher Script | 7 | Preserved standalone. |
| Diplomacy | 2 | Directly normalizes to Persuasion. |
| Disable Device | 5 | Scoped relationship to Thieves' Tools with scope `disable-device`; does not grant unrestricted Thieves' Tools proficiency. |
| Disguise | 5 | Related to Disguise Kit, but does not share unrestricted training identity. The historical skill covers broader disguise use than tool proficiency alone. |
| Escape Artist | 7 | Preserved standalone. |
| Forgery | 4 | Skill facet of learned competency **Forgery**; later facet is Forgery Kit. |
| Gather Information | 7 | Preserved standalone. |
| Handle Animal | 2 | Directly normalizes to Animal Handling. |
| Heal | 2 | Directly normalizes to Medicine. |
| Hide | 3 | Contributor to Stealth with Move Silently. |
| Innuendo | 7 | Preserved standalone. |
| Intimidate | 2 | Directly normalizes to Intimidation. |
| Intuit Direction | 7 | Preserved standalone. |
| Jump | 3 | Contributor to Athletics with Climb and Swim. |
| Knowledge (X) | 1 | Direct normalized identity `X`; no visible Knowledge family. |
| Listen | 3 | Contributor to Perception with Spot. |
| Move Silently | 3 | Contributor to Stealth with Hide. |
| Open Lock | 5 | Scoped relationship to Thieves' Tools with scope `open-lock`; no unrestricted tool proficiency. |
| Perform | Family taxonomy | Independent specialties. Not collapsed into Performance. Instrument facets require specific source-supported correspondence. |
| Pick Pocket (3e) | 2 | Directly normalizes to Sleight of Hand for 3e sources. |
| Profession | Family taxonomy | Independent specialties; no generic modern tool equivalence is inferred from occupation names. |
| Read Lips | 7 | Preserved standalone. |
| Ride | 7 | Preserved standalone. |
| Scry | 7 | Preserved standalone. |
| Search | 7 | Preserved standalone. |
| Sense Motive | 2 | Directly normalizes to Insight. |
| Speak Language | 6 | Transformed into the language-proficiency subsystem. Historical source/rank evidence is preserved for compatibility, but Speak Language is excluded from the ordinary universal competency/skill catalog and receives no fabricated governing Ability. |
| Spellcraft | 7 | Preserved standalone. |
| Spot | 3 | Contributor to Perception with Listen. |
| Swim | 3 | Contributor to Athletics with Climb and Jump. |
| Tumble | 3 | Contributor to Acrobatics with Balance. |
| Use Magic Device | 7 | Preserved standalone. |
| Use Rope | 7 | Preserved standalone. |
| Wilderness Lore (3e) | 2 | Directly normalizes to Survival for 3e sources. |

## 3.5e psionic competencies

| Historical competency | Classification | Current reconciliation |
| --- | --- | --- |
| Autohypnosis | 7 | Preserved standalone. |
| Knowledge (Psionics) | 1 | Directly normalizes to Psionics under the Knowledge (X) rule. |
| Psicraft | 7 | Preserved standalone. |
| Use Psionic Device | 7 | Preserved standalone. |

## Specialized families

The Character contract has a reviewed universal family catalog independent of whether a particular serialized SRD snapshot emitted every specialty row. The parent family is taxonomy only; children remain independent learned competencies. The Rules layer owns the family defaults: Craft children are Intelligence-based ranked skills, Perform children are Charisma-based ranked skills, and Profession children are Wisdom-based trained-only ranked skills; all retain class-skill and training state and do not apply Armor Check Penalty by default. Missing source-shaped specialty rows are materialized as Rules-layer mechanics without fabricated SourceEntity provenance.

### Craft

Reviewed baseline children are:

`Alchemy`, `Armorsmithing`, `Basketweaving`, `Blacksmithing`, `Bookbinding`, `Bowmaking`, `Calligraphy`, `Carpentry`, `Cobbling`, `Gemcutting`, `Leatherworking`, `Locksmithing`, `Painting`, `Pottery`, `Sculpting`, `Shipmaking`, `Stonemasonry`, `Trapmaking`, `Weaponsmithing`, and `Weaving`.

Alchemy is one universal learned competency:

```text
competency.alchemy
  3e implementation/source alias: Alchemy
  3.5e implementation/source alias: Craft (alchemy)
  later implementation/source alias: Alchemist's Supplies
```

Only Alchemy has this reviewed Craft/tool collapse. No other Craft specialty is mapped to a modern tool solely from name similarity.

### Perform

Reviewed baseline children are:

`Act`, `Comedy`, `Dance`, `Keyboard Instruments`, `Oratory`, `Percussion Instruments`, `Sing`, `String Instruments`, and `Wind Instruments`.

These are the independently ranked 3.5 categories. 3e performance forms belong to the older Perform rule implementation and are not promoted into additional independently ranked universal children merely because they were named forms. Instrument categories do not imply one particular later instrument-tool proficiency.

### Profession

Reviewed baseline children are:

`Apothecary`, `Boater`, `Bookkeeper`, `Brewer`, `Cook`, `Driver`, `Farmer`, `Fisher`, `Guide`, `Herbalist`, `Herder`, `Hunter`, `Innkeeper`, `Lumberjack`, `Miller`, `Miner`, `Porter`, `Rancher`, `Sailor`, `Scribe`, `Siege Engineer`, `Stablehand`, `Tanner`, `Teamster`, and `Woodcutter`.

Profession specialties remain independent and are not mapped to tools by occupational-name similarity.

### Knowledge exception

Knowledge is intentionally normalized differently:

```text
Knowledge (arcana)     -> Arcana
Knowledge (history)    -> History
Knowledge (nature)     -> Nature
Knowledge (religion)   -> Religion
Knowledge (Psionics)   -> Psionics
Knowledge (the planes) -> The planes
```

The source-native Knowledge name and older-edition profile/provenance remain available, but the Character-facing competency identity is the specialty itself.

## Shared identity versus numeric mechanics

A shared learned-competency identity shares only conceptual training/proficiency state. It never converts numeric systems.

For example, six ranks in `Craft (alchemy)` remain six ranks on that profile. They do not become a +6 Alchemist's Supplies bonus. A later proficiency bonus does not become 3.x ranks. Class-skill state, governing Ability, trained-only rules, and Armor Check Penalty applicability remain attached to the profile that defines them.

## Compatibility rule

Shared learned-competency identity is deliberately separate from canonical Source/Rules Layer identity. Cross-type facets retain their original skill/tool source entities and Rule Concepts. This avoids orphaning existing Character references and avoids forcing one Rule Concept entity type to represent incompatible mechanical contracts.

Installations that already persisted the earlier direct cross-type normalization are migrated in place when the same reviewed native record is imported again. The existing SourceEntity ID and native revision identity are retained, and existing Rule Concept/binding IDs and keys are not rewritten. The corrected skill facet is attached through canonical revision lineage, so an existing Character reference such as `competency.tool.alchemists-supplies` continues to resolve while exposing the new shared `Alchemy` training identity and the correct ranked-skill facet mechanics. Fresh imports use the corrected skill identity directly. The same compatibility rule applies to the earlier Forgery -> Forgery Kit direct normalization.

The Character mechanics catalog now promotes that learned identity into a first-class universal `competencies` surface. One semantic entry exposes the stable semantic key, family, training-state key, reviewed aliases, implementation mechanic keys, profiles/facets, relationships, and provenance. Source-shaped `mechanics` remain available for evaluation and backward compatibility, so Character-owned ranks and other facet-specific numeric state stay keyed to the applicable implementation/profile rather than being converted into the universal training state.

This is an in-place migration: existing SourceEntity and Rule Concept IDs/keys are retained. Compatibility keys allow pre-normalization Character references to resolve during migration. The older source evidence remains immutable.
