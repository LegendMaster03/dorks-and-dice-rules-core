# Pillar 2 — characteristic inheritance, expression, and quantitative balance

**Date:** 2026-11-14  
**Status:** **Design candidate preserved for continued investigation.** This is not an approved biological allocation rule, trait cost schedule, native ruleset change, JSON schema, or implementation authorization.  
**Scope:** Dorks & Dice Rules Core Character Origins, especially Pillar 2 (Origination, Heritage & Traits).  
**Design provenance:** Collaborative comparison and experiments drawing on *An Elf and an Orc Had a Little Baby*, *Daggerheart*, and *RimWorld: Biotech*, applied to the existing Dorks & Dice characteristic model.

> **Handoff summary:** Retain one universal Origin construction/reconstruction engine. Treat conventional Species/Ancestries as editable characteristic presets, not immutable power benchmarks. Biological reproduction is the first procedure being designed, not the universal model of origination. Combine reusable characteristic definitions and related selection groups with transmission, expression/suppression, and **quantitative evaluation of complete character power**. More than two contributors and previously mixed contributors must work. No fixed two-parent/two-feature rule, point limit, valuation formula, latent-gene policy, or new Species balance change has been approved. The experimental seven/eight-point budgets and nine-Species indices below are **unvalidated probes only**. The next task is power-measurement calibration and mechanical refinement, not redoing the architectural investigation.

## 1. Purpose and design status

The goal is not merely to permit mixed-species character creation. The system should **quantify the mechanical power of each Species/Ancestry and its constituent characteristics, use that information to rebalance existing Species, and evaluate conventional, mixed, custom, and homebrew configurations consistently**.

Two ideas must remain distinct:

1. **Established architectural principles and requirements.** These govern the existing Origin direction and should be preserved.
2. **The combined inheritance/allocation model.** This is the leading *candidate* explored in design conversations. Its structure is promising and worth preserving; its numerical and gameplay details remain under investigation.

Do not infer approval of a rule from the existence of an illustrative character build, an experimental value, or an analogous mechanic in another game.

### Established requirements

- One shared Origin engine performs **deconstruct → combine → reconcile ↔ manifest → reconstruct**. Reconciliation and manifestation can iterate; the three player-facing workflow stages are deconstruct/assemble, reconcile/manifest, reconstruct.
- Standard, mixed, partially/fully custom, and homebrew configurations use the same characteristic vocabulary and mechanical interpretation. A conventional Species/Ancestry is an author-defined **preset**, not a separate privileged evaluator.
- **No mandatory primary Species** and no requirement for exactly two biological parents. Biological procedures may involve multiple contributors; contributors can themselves have mixed Origins.
- Origination procedures determine eligible contributor roles and manifestation rules. Biological reproduction is one procedure. Construct assembly, spawning, vegetative reproduction, magical creation, transformations, and other procedures may use different resources and methods while producing the same kind of reconstructed Origin. A construct's power source may determine available capacity; do not impose biological metabolism on it.
- A contributor supplies *eligible source evidence*, not all of its Species benefits automatically. Previously mixed contributors have verified current characteristic configurations and provenance; historical genealogy alone is not a source of unlimited grants.
- One canonical mechanic/trait definition, its occurrence in a Species/Ancestry, a contributor offer, and an effective manifested grant are distinct identities. Equal names do not prove equivalent mechanics. Duplicate evidence must not manufacture duplicate benefits or pools; legitimately distinct pools remain distinct.
- Preserve complete multi-effect abilities, resource relationships, advancement, restrictions, and linked choices. Decompose internally only where independently meaningful variation, composition, or relationship semantics require it. Do not treat every effect field as a separately inheritable trait.
- Upbringing and Background are distinct acquired-characteristic responsibilities, not automatically biological inheritances. Pillar 4 Supernatural Nature and transformations remain distinct responsibilities.
- Species/Ancestry definitions are **not final**. Changes to their traits, strength, progressions, dependencies, and disadvantages are legitimate outcomes of this design work. Balance the native Species and the allocation model *together*.
- Table/DM rule customization remains possible with explicit authority and scope; an override is not silently promoted to a first-party default.

### Repository-specific representation at this date

The 2026-11-14 normalization work records canonical `origin-trait` definitions in `data/rulesets/first-party-001/native/traits/`, referenced by `traitId` from Species/Ancestry records. The loader expands them for existing consumers. This recent storage change **does not** introduce new inheritance grants or trait equivalences. Intrinsic properties (creature type, size, movement, senses, ancestry choices, etc.) still require general-purpose composition logic.

Relevant source documents:

- [Universal Origin model and sequencing](pillar2-universal-origin-model-first-20261111.md)
- [Characteristic decomposition and standard reconstruction](pillar2-characteristic-decomposition-proposal-20261111.md)
- [Characteristic facets and identity](pillar2-characteristic-facets-20261111.md)
- [Characteristic overlap/compatibility review](pillar2-characteristic-identity-compatibility-audit-20261111.md)
- [Heritage manifestation scope](pillar2-heritage-manifestation-scope-20261111.md)
- [Biological construction vertical slice](pillar2-phase3-biological-vertical-slice-20261112.md)
- [Origin trait normalization](origin-trait-normalization-20261114.md)
- [Quantitative Species and Ancestry evaluation, pinned to this branch's original revision](pillar2-species-power-evaluation.md)

## 2. How the combined idea emerged

### Initial failure: local recombination is not general mixed-Species allocation

Related Ancestries share characteristics that can be configured or recombined. For example, mixed Red/Bronze Dragonborn can combine eligible damage types with eligible *whole* ordinary Breath Weapon geometries. High/Wood Elf spell progressions offer another compositional test, although independent stage selection is not approved.

Across unrelated Species there may be no homologous ability. Human Adaptable Learning, High Elf magic, and Red Dragonborn Breath Weapon are not three variants of the same feature. Characteristic compatibility alone does not limit their accumulated power.

### Comparative inspiration

| Source | Useful principle | Limitation not carried over uncritically |
| --- | --- | --- |
| *An Elf and an Orc Had a Little Baby* | Independently representable parentage characteristics, dependencies, trait valuation, and customizable mixes | Fixed 16-point parentage, inconsistent contextual trait costs, difficulties with expensive flight and unanticipated homebrew, two-parent framing |
| *Daggerheart* | Small numbers of meaningful choices and coherent conventional ancestry feature groups | Literal two-feature positions are not equivalent across the existing Dorks & Dice Species and do not represent unrestricted multi-contributor inheritance |
| *RimWorld: Biotech* | Germline transmission, inherited vs. expressed genes, suppression, conflicting genes, physiological tradeoffs, separate engineering complexity and intentional genetic optimization | Random or two-parent reproductive details, food as an assumed universally meaningful penalty, and literal metabolism or gene costs are not directly transferable to a TTRPG |

The productive combination is **reusable mechanical characteristics + structured, coherent player decisions + bounded hereditary transmission/expression + whole-configuration power quantification**.

None of these inspirations has been adopted wholesale.

## 3. Characteristic representation and grouping

A conventional Species is an authored composition of intrinsic properties, canonical traits, configured parameters, dependencies, effects, and advancement relationships. Ancestry contributions can add, replace, or configure those elements where an explicit rule permits that behavior.

Useful *analytical responsibilities* include anatomy/physical form, physiology, senses, movement, innate capabilities and magic, elemental affinity, linked choices, resource management, and progression. **These are not an approved exclusive taxonomy or mandatory allocation slots.** A characteristic can cross several responsibilities.

Distinguish:

- **Definition:** What a complete capability does, independently of who offers it.
- **Source occurrence:** Which Species/Ancestry records offer that capability.
- **Configured instance:** Which supported parameters, effects, restrictions, resource pool, progression, and choices are bound.
- **Contributor offer:** How an actual contributor makes a configured instance eligible.
- **Manifested grant:** What this offspring actually possesses and can use.
- **Hereditary state (candidate proposal):** What was transmitted, including any expressly supported latent/suppressed alternative.
- **Power evaluation:** How the whole resulting configuration performs in defined gameplay contexts.

Daggerheart-inspired **selection groups** are a user-experience tool: a few choices may select a complete coherent progression or configure multiple related effects. They need not be identical to either the unit of biological transmission or the unit of balance evaluation.

Examples:

- **Red/Bronze Dragonborn:** One ordinary Breath Weapon remains one attack with its authored action, saving throw, damage progression, and use pool. Independently eligible damage type and whole area can be recombined. Line is 30 by 5 feet; cone is 15 feet. Fire/lightning and line/cone yield four approved potential configurations. Neither the two area dimensions nor damage dice/resources become separate grants. Chromatic Warding, Metallic Breath, resistance, and leaf adaptations do **not** automatically transmit with that decision.
- **High Elf:** A coherent spell progression contains a level-1 cantrip entitlement, Detect Magic at level 3, Misty Step at level 5, and a source-bound casting ability. Whether a High/Wood mixed offspring can select spell stages from both courses is a *test*, not an established right.
- **Stone Goliath:** Stone Strike consumes uses of Stone's Endurance; it does not create another independent pool. Stone Throw has its own pool. Level-4 and level-5 unlocks must be preserved and evaluated.
- **Human:** Skillful, Versatile, and Adaptable Learning are cumulative; the last provides four extra skill ranks at level 1 and one per later level, subject to native limits.
- **Orc/Goliath Powerful Build:** The shared name and carrying-capacity component do not make the full traits identical. Orc also covers pushing/dragging/lifting; Goliath additionally helps end Grappled.

The same mechanism must import unfamiliar/homebrew content without silently discarding unknown effects or pretending that similar prose establishes canonical identity. Novel generalized mechanics may require authoring or review; new combinations of existing mechanics should generally be data-driven.

## 4. Biological inheritance and expression — candidate procedure

This procedure applies specifically to biological origination. It does **not** create a second universal Origin engine.

**Conceptual steps (not yet approved rules):**

1. **Gather eligible contributors.** Roles and eligibility are defined by the biological origination procedure, not by a hard-coded pair of Species or parents. A mixed contributor offers its actual eligible configured characteristics.
2. **Establish transmission candidates.** Preserve canonical definition identity, full parameters, relationships, source occurrences, and contributor provenance. Historical grandparents do not automatically reopen every ancestral trait.
3. **Determine transmitted configuration.** Biological recombination selects eligible hereditary material using a procedure-specific policy. This is distinct from automatically granting every offered trait. The exact selection rule (deliberate, weighted, random, or mixed), inheritance quantity, and whether latent inherited information may recur remain **open**.
4. **Resolve expression.** Explicitly permitted variants, competing values, dependencies, suppression, and upgrades determine actual expressed capabilities. If two powerful capabilities are independent and compatible, gene conflict alone will not prevent both from expressing.
5. **Evaluate the whole Origin.** Include foundational creature/size/movement/senses consequences, resources, progression, drawbacks, duplicate/overlapping effects, and synergies. Apply whatever inheritance-capacity/balance policy is ultimately approved.
6. **Reconstruct the current Origin.** Save expressed traits and choices, direct source/provenance, and only whatever latent heredity an approved procedure actually supports. Distinguish inherited, not transmitted, and merely historical information.

Do not conflate three outcomes:

| State | Meaning |
| --- | --- |
| Not transmitted | Not part of this offspring's hereditary configuration |
| Transmitted but suppressed | Present but not expressed; possible future inheritance **only if explicitly supported** |
| Transmitted and expressed | Present in the effective Origin with its full behavior and relevant progression |

Latent information is **not** already a universally available feature of the Rules Core implementation. Current evidence for previously mixed contributors prioritizes verified manifested characteristics and does not infer arbitrary latent grants. Unbounded free latent storage would allow generations to accumulate hidden benefits and undermine ordinary inheritance constraints.

### Multiple contributors and mixed descendants

The number of contributors must not *automatically* increase inheritance capacity. Three sources can broaden available choices without adding three whole packages. A mixed Red–Bronze parent expressing a lightning-line Breath Weapon can offer that configured ability to a later offspring; the fact that its own ancestors offered Fire and Lightning does not automatically make both grandparent packages available again.

### Natural inheritance versus deliberate modification

RimWorld-inspired ordinary inheritance and intentional alteration can have different access/selection rules. Biological engineering, transformations, or another origin procedure might legitimately permit otherwise unavailable alterations, stronger optimization, or new characteristic sources. Those require their own costs and authority. Do not assume genetically modified characters are always stronger, or that ordinary reproduction cannot produce powerful combinations.

### Illustrative state shape, not a schema

```text
CharacteristicInstance:
  canonicalDefinitionId
  completeConfiguredBehaviorAndParameters
  sourceOccurrencesAndContributorEvidence
  dependencyAndCompositionRelationships
  progressionAndResourceLinks
  hereditaryStatus: not-transmitted | transmitted
  expressionStatus: expressed | suppressed | unresolved
  customAuthorityAndAdjudication, where applicable
```

These are conceptual responsibilities, not endorsed field names, persistence decisions, or a modification to the current Origin API.

## 5. Tradeoffs, linked adaptations, and recombination

A drawback can be the biological consequence of an adaptation, rather than a freely selectable negative trait used to purchase unrelated positive traits.

**Drow is the first existing test case.** Drow has 120-foot Darkvision and separately defined Sunlight Sensitivity. In direct, unprotected sunlight, Sunlight Sensitivity applies **Dazzled**; under the native `condition.dazzled` definition, that means a −1 attack penalty and −1 Search/Spot penalty, not blindness. Shade or effective tinted eyewear can prevent or end exposure. Thus the drawback is real but often mitigable. Its existence does **not** prove that superior Darkvision has become power-neutral.

A **proposed linked adaptation** would treat Drow dark-adapted vision and its sunlight consequence as normally transmitted/expressed together. **The current content does not encode that inheritance link or declare it mandatory.**

Separate questions:

- Is this relationship normally linked, inseparable, conditionally separable, or freely recombinable?
- Can a descendant express 120-foot Darkvision without Sunlight Sensitivity through an authorized recombination or modification rule?
- If so, how much does removing the drawback change the resulting configuration's measured power?
- What does a different source's independently acquired Darkvision imply? It must not automatically inherit a Drow drawback.
- How should an explicit upgrade work? Stone Goliath Cavernous Sight adds 60 feet to *existing* Darkvision at level 4; Drow 120 feet plus that feature could yield 180 feet **if both grants are authorized and their actual composition rules apply**.

A useful relation vocabulary for investigation:

| Relationship | Example | What must be preserved |
| --- | --- | --- |
| Linked adaptation | Drow vision + Sunlight Sensitivity | Authored benefit/drawback relationship, possibly separable under an explicit rule |
| Dependency | Stone Strike uses Stone's Endurance | Required base capability and shared resource |
| Upgrade | Cavernous Sight adds to Darkvision | An authored modifier rather than automatic duplicate or maximum |
| Alternative configuration | Drow vs. Dwarf darkvision profiles | Eligible values and a decision about which one manifests |
| Overlap | Orc vs. Goliath Powerful Build | Shared component once, distinct effects preserved |
| Independent characteristic | Human Adaptable Learning + Breath Weapon | No invented biological conflict; overall allocation still matters |
| Suppression | Competing inherited vision variant | Only retains heredity if the selected procedure permits it |

A generic negative-cost refund model encourages irrelevant disadvantages; linked adaptations and **marginal contribution to complete character power** should be tested instead. Neither rule has yet been approved.

## 6. Quantification: fundamental design objective

**Power measurement is not an optional audit bolted onto an inheritance system. It is one of the system's intended purposes.** Measure conventional Species/Ancestry configurations *and* use the results to rebalance their traits as well as mixed configurations.

Distinguish three quantities:

1. **Effective mechanical power:** Character capabilities available at a level and in a gameplay context.
2. **Inheritance/allocation burden:** A proposed limit or construction cost used by a particular origination procedure. It may be informed by power without being identical to it.
3. **Hereditary information/complexity and physiological consequences:** Potential transmission complexity and real limitations such as environmental adaptation. These are not interchangeable with mechanical power.

### Proposed measurement framework, not a final formula

Evaluate a complete effective configuration as a vector across relevant contexts and level milestones, rather than only a raw sum of trait entries:

| Dimension | Examples of measurable mechanics |
| --- | --- |
| Offense/control | Expected damage, accuracy, save DCs, control strength, target count, action use, frequency |
| Defense | HP, resistance, save bonuses, avoidance, emergency survival, independent resource pools |
| Mobility/exploration | Movement modes and speed, unrestricted vs. time-limited flight, teleportation, sensory modes |
| Utility/versatility | Feats, proficiencies, skill-rank entitlements, spell options, noncombat relevance |
| Progression | Character-level unlocks, continuous scaling, fixed/dynamic use pools |
| Tradeoffs | Scope, severity, frequency, avoidability and campaign relevance of disadvantages |
| Interactions | Dependencies, overlap, stacking rules, synergistic and redundant combinations |

The assessment should include **levels 1, 3, 4, 5, 11, and 17**, with other milestones when a trait requires them. Use scenarios with declared assumptions (combat length, short/long rests, environmental conditions, equipment, class archetypes). Compare incremental power added to an existing configuration, not simply the sum of standalone prices.

An eventual scalar *comparative index* may be derived from the vector for player-facing allocation or design review. Its normalization, weights, confidence/uncertainty, handling of game-changing utilities, and acceptable range are **unresolved**.

A number of individually acceptable traits can be excessive together; the effective configuration must be assessed as a whole. Conversely, duplicated fire resistance or carrying-size equivalence must not count twice merely because two contributors offered it.

### Calibration must change both sides of the equation

The current nine Species are **editable experimental inputs**, not already-balanced standards. Evaluate their complete preset profiles through the same engine as mixed characters. If a Species scores much higher or lower, investigate whether (a) valuation is wrong, (b) the Species needs a mechanical change, (c) allocation policy needs changing, or (d) there is an interaction not modeled. Do not arbitrarily boost a universal limit to preserve the most expensive incumbent or add unrelated weak features to fill a target score.

A conventional Species must remain mechanically recognizable, and straightforward direct selection must remain available, but its present ability list and progression are **not immutable**.

### Homebrew assessment

Homebrew uses the same measurement vocabulary and generic mechanics. A completely new effect requires authoring a reusable definition and possibly adjudication, not a pairwise cost for each Species combination. Unrecognized effects must remain explicitly unresolved; they must not default to zero power or silently become selectable.

## 7. Experimental results to preserve — **not approved prices**

### Initial fixed-limit feasibility probes

A seven-point example permitted a complete Human package at a provisional burden of 6, High Elf at 5, and Red Dragonborn at 6. It allowed Human/Elf and Elf/Dragonborn combinations without merely granting the union. A synthetic unrestricted-flight burden of 4 left room for a secondary ability. These assumptions were arbitrary and did **not** resolve foundational traits or prove parity.

A later eight-point probe illustrated multiple contributors and cross-Species choices. Flight valued at 4, 5, or 6 changes which secondary abilities fit; the result is too sensitive to guessed prices to establish a rule.

**No numerical inheritance capacity has been approved. A bound expressed as trait counts or package positions likewise has not been approved.**

### Nine-Species provisional scoring probe

A subsequent **partial, subjective comparison** of native configurations produced these illustrative indices:

| Conventional configuration | Experimental index |
| --- | ---: |
| Human | 8 |
| Hill Dwarf | 9 |
| High Elf | 8 |
| Forest Gnome | 6 |
| Stone Goliath | 12 |
| Lightfoot Halfling | 7 |
| Orc | 9 |
| Infernal Tiefling | 8 |
| Red Dragonborn | 8 |

These were **not comprehensive measured power scores**: creature-type/size implications, synergies and campaign context were incompletely accounted for; some assignments were arbitrary, including a −1 adjustment for 25-foot walking speed. Changing a few subjective Goliath trait assessments can lower its total substantially without any gameplay change.

Consequently an eight-point rule fitted six of nine representative profiles, nine points fitted eight, and twelve fitted all nine **under that limited rubric only**. That failure is evidence against adopting a number prematurely, not proof that Goliath must be weakened or Forest Gnome strengthened. Quantification and Species revision should proceed together.

### Canonical stress cases

| Test | Expected structural behavior, not automatic balance approval |
| --- | --- |
| High Elf + Wood Elf | One shared Elf foundation; source-bound spell choices and progressions preserved. A mixed stage-by-stage course remains experimental. |
| Red + Bronze Dragonborn | Fire/lightning × complete line/cone creates four permitted ordinary Breath Weapon parameter combinations; one attack and use pool; no automatic resistance/warding/Metallic Breath union. |
| Human + High Elf | Candidate trait pool from both; avoid full unrestricted cumulative grant. Measure Adaptable Learning across levels and a complete spell progression. |
| High Elf + Red Dragonborn | Magic and Breath Weapon can be compatible but powerful; no fabricated incompatibility merely to balance them. Complete physical Origin still required. |
| Human + High Elf + Dragonborn | Multiple biologically contributing sources broaden eligibility without automatically multiplying allocation capacity or inherited grants. |
| Mixed Dragonborn parent + additional contributors | Actual configured lightning-line Breath Weapon is a possible source; ancestral history does not automatically reopen every parental variant. |
| Drow + Dwarf | 120-foot Darkvision plus sunlight drawback versus 60-foot Darkvision, alternative expression, optional future severance with revaluation. |
| Drow + Stone Goliath | Native Cavernous Sight upgrade and effects at level 4; check whether 180-foot outcome is authorized and appropriately assessed. |
| Orc + Stone Goliath | Partial overlap in Powerful Build; shared components not doubled; genuine distinct effects and independent pools retained. |
| Dwarf + Rock Gnome | 60-foot activated Stonebound vs. 15-foot continuous Earthbound Tremorsense; cannot collapse by shared name alone. |
| Flight-capable custom ancestry | Distinguish unrestricted flight from Gem Dragonborn's short, once-per-rest flight; signature trait must not arbitrarily become its only useful inheritance, but cannot be treated as free. |
| Construct assembled from blueprint, components, power source | Must reuse definitions and evaluation pipeline, **not** default to biological contributors, inheritance or food-based metabolism. |

### Important limitations demonstrated by the tests

- Pure conflict resolution does not prevent unrelated powerful capabilities from accumulating.
- Independent per-trait transmission can allow uncontrolled unions; multiple contributors can improve optimization even when a fixed total limit exists.
- A fixed package count is vulnerable to arbitrary packaging and unequal feature strength.
- A raw additive budget is vulnerable to valuation errors, synergies and negative-trait exploits.
- Automatically preserving all unselected characteristics as suppressed heredity creates unbounded latent inheritance.
- Assigning a high burden to flight can recreate the problem where flight consumes an entire inheritance budget; lower burdens require evidence.
- Conventional preset scores are both **targets of study and objects of revision**, not ground truth.

### Recorded Dwarf–Rock Gnome Tremorsense decision — 2026-11-14

**Owner-selected resolution for this mixed-inheritance test case:** Consolidate the two offered Tremorsense capabilities into **one effective Stonebound Tremorsense** using the Dwarf's complete authored constraints:

| Property | Selected effective behavior |
| --- | --- |
| Range | 60 feet |
| Activation | Bonus Action |
| Duration | 10 minutes per activation |
| Uses | Proficiency Bonus per Long Rest |
| Environment | Character must remain in contact with **stone** |
| Rock Gnome 15-foot passive mode | **Not retained** as a simultaneous or separately usable capability |

This is mechanically equivalent to the existing Dwarf Stonebound Tremorsense, not a newly enhanced hybrid. It intentionally sacrifices the Rock Gnome's passive Earthbound Tremorsense and its earth-or-stone substrate coverage. Therefore, a quantitative comparison should count **one** effective activated Stonebound capability, not a 60-foot active sense **plus** a 15-foot passive sense. In scenarios with unexpected nearby contacts on earth alone, this mixed resolution may perform *worse* than standalone Rock Gnome. Its power is not simply the maximum of both parents.

**Scope:** This records a specific selected test-case consolidation to use in evaluation and subsequent rule design. It does **not** approve a general "take the larger range and stricter activation" rule, rewrite the native Dwarf or Rock Gnome presets, authorize unrelated biological transmissions, determine numerical inheritance burden, or request a production implementation. Apply the shared Origin model's explicit authorization and source provenance rules to any eventual live manifestation.

**Research evidence:** [WorkChat quantitative Species/Ancestry evaluation](pillar2-species-power-evaluation.md) modeled the *unresolved* original Dwarf/Rock Gnome union with both passive and activated modes. Its mixed-character frontier results should not be mistaken for the **later owner-selected consolidated result**. Preserve the original research rather than retroactively changing its assumptions.

## 8. Open questions — explicitly unapproved

1. **Power metric:** What measurable rubric, context profiles, normalizations and validation/playtest methods produce reproducible enough comparative power assessments?
2. **Allocation:** Is a biological inheritance constraint a numerical burden, a transmission/structural constraint, a hybrid, or an optional table policy? How does this differ from quantified mechanical power?
3. **Capacity:** If a budget exists, how is it derived, what must it include (including foundational traits), and how do incomplete/unknown abilities behave?
4. **Transmission agency:** Deterministic player construction, weighted inheritance, randomness, and/or an optional procedure-specific choice?
5. **Latent heredity:** Is any suppressed hereditary information transmissible; if so, how much, for how long, at what constraint, and with what provenance?
6. **Multi-contributor biology:** How are hereditary contributions obtained when the reproductive procedure uses more than two contributors, or donors with distinct roles?
7. **Adaptation links:** When are a benefit/drawback or a developmental series inseparable, linked by default, or legitimately recombinable?
8. **Intrinsic composition:** How do conflicting creature types, size, speeds, natural movement, needs, and senses resolve in mixed Species?
9. **Growth and resources:** Are future unlocks entirely committed at character creation? Which progressions share pools; how does cross-trait synergy affect valuation?
10. **Species changes:** Which first-party Species/Ancestry traits and progressions deserve modification once full measurements exist?
11. **Homebrew:** What evidence and adjudication is required for new effects, unknown interactions, or uncalibrated imported content?
12. **Balance disclosure:** What should be shown to players (numbers, relative bands, warnings, narrative tradeoffs) versus retained as developer/DM diagnostics?

These questions are next-stage work. They do not block preserving the current candidate design.

> **Research implementation update (2026-11-14):** An initial [read-only quantitative evaluation prototype](pillar2-power-evaluation-prototype.md) now reconstructs canonical native presets and calculates selected, scenario-dependent endpoints and resource frontiers. It is not a complete power metric, inheritance cost model, or normative Species revision. The [WorkChat research report](pillar2-species-power-evaluation.md) remains the comparison baseline. Full native-catalog CI is pending.

## 9. Recommended next experiment and acceptance criteria

**Next objective:** build a reproducible *power-evaluation prototype* against all nine native Species and representative Ancestries, without approving a biological allocation cost from arbitrary values.

Suggested sequence:

1. Inventory all canonical trait definitions, complete operational behavior, explicit relations, intrinsic characteristics, and future unlocks. Detect unmodeled semantic gaps rather than treating them as zero-value.
2. Define declared playtest contexts and a comparative measurement rubric. Include advancement, defense, mobility, utility, resource usage, and class synergies.
3. Measure complete conventional configurations and quantify uncertainty; investigate outliers rather than presuming existing balance.
4. Measure selected mixed compositions with overlapping, independent and synergistic traits; compare marginal to standalone value.
5. Revise the proposed rubric **and, when justified, existing Species/Ancestry mechanics**. Repeat until a reasonable conventional/mixed comparison is possible.
6. Only then test one or more biological allocation policies against the same measurements, including flight and multigenerational inheritance. Keep biology-specific tradeoffs separate from universal power measurement.
7. Run the same semantic evaluator against synthetic homebrew and a nonbiological construct example to detect hidden Species-specific assumptions.

Acceptance criteria for the *design*:

- Every standard preset can be reconstructed without losses through the shared Origin characteristic machinery; unresolved source semantics remain visible.
- A conventional Species is available as a valid intentional selection without requiring the player to manually decompose it.
- Red/Bronze Breath Weapon semantics, Elven source-bound choices, actual resource sharing, intrinsic effects, and progression survive.
- Multiple contributors and mixed descendants work without a two-parent/two-package limitation and without genealogy automatically granting historical abilities.
- Same capability, parameter variant, marginal upgrade, linked drawback, and independent pool are not conflated.
- At levels 1/3/4/5/11/17, evaluations distinguish current effects from committed future progression.
- Homebrew unknowns are flagged, not assumed to be free or mechanically equivalent.
- The system produces explainable comparative results for standard and mixed configurations, supports meaningful iteration on the **Species themselves**, and does not claim perfect balance from a scalar score alone.
- Nonbiological origination can adopt the universal evaluation without pretending its construction capacity is biological metabolism.
- No experiment or authoring proposal silently changes approved first-party rules or Table/DM authority boundaries.

## 10. Documentation and implementation boundary

**This file preserves the candidate so later work can continue without restarting the comparative investigation.** Existing approved Origin decisions remain governed by their source specifications. This document authorizes no changes to native `species`/`traits` content, C# runtime, API, Character Sheet, default inheritance permissions, feat/competency mechanics, or existing transformations.

When future decisions are approved, update this document's status *and* the relevant normative rule contracts, keeping the prior experimental evidence labeled rather than retrospectively presenting assumptions as facts.
