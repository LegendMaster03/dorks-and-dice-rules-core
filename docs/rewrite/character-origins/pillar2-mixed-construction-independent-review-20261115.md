# Pillar 2 — independent review and mixed-construction completion candidate

**Date:** 2026-11-15  
**Review baseline:** PR #157 at de751fb95801729aa3aeadfa04558995cbc1bfd2  
**Status:** Research candidate, **not an adopted first-party rule or production implementation**.  
**Scope:** Mixed Species/Ancestry only. No classes or class features are evaluated or modified.

This review begins with the existing design rather than replacing it. It separates structural validity, authorization, construction capacity and measured mechanical power. **The quantitative evidence available as of this review does not establish conventional/mixed parity.** A reproducible conditional construction algorithm is proposed below, with first-party calibration and specific authoring choices expressly unresolved. Approval of the algorithm would not, by itself, approve numeric costs or grant any mixed character.

## 1. Independent repository audit

The working branch adds three research/design documents, one diagnostic scenario fixture, a Python Species/Ancestry evaluator and its tests, plus a small link in the universal Origin specification. Commit de751fb reverted prior class-related additions. No native Species, Ancestry, origin-trait, C# constructor or authoritative ruleset definition is changed by PR #157.

Both workflows on the reviewed head passed: Validate Rules Core Rewrite Structure (JSON validation) and Rules Core Phase 3B Public Native API CI (build, C# tests and existing Python validation, among other checks). These checks establish repository/test health, **not** the accuracy of an inheritance cost schedule or gameplay balance. The research-only evaluator's detailed native integration claims and default scenario assumptions remain a separate matter from gameplay verification.

Existing implementation:

- NativeOriginSourceDeconstructor resolves source chains, contributor occurrences and current manifested-profile grants; historical source IDs do not automatically become offers.
- NativeOriginConstructionEngine accepts direct defaults under defined conditions; mixed/custom candidates require verified decisions instead of gaining unrestricted native grants.
- NativeOriginReconciler consolidates identical occurrences, but shared definition IDs from distinct occurrences, competing intrinsic slots and unsupported relation dispositions still require rules or scoped rulings.
- NativeOriginConstraintEvaluator executes only supported catalog-backed or authenticated constraints; unsupported relationship kinds remain unresolved.
- NativeOriginApprovedMixedIntrinsicProjector interprets a deliberately limited, completely DM-authorized mixed profile (creature type, size, walking speed); it does not establish general mixed-Species entitlement or execute the rest of the Origin mechanics.
- The current catalog stores canonical origin-trait definitions referenced by Species/Ancestry records, but canonicalization **does not** imply hereditary eligibility, equal cost, stacking rights or a default mixed profile.

The existing [inheritance/balance design](pillar2-characteristic-inheritance-and-balance-design.md), [universal model](pillar2-universal-origin-model-first-20261111.md), [biological slice](pillar2-phase3-biological-vertical-slice-20261112.md) and [research evaluator](pillar2-power-evaluation-prototype.md) form the foundation. The [quantitative inventory](pillar2-species-power-evaluation.md) is useful evidence, but was authored against an earlier pinned catalog revision. Current-catalog replay is required before treating an inventory difference as a present native mechanic.

### Findings by decision status

**Preserve as established:** one universal Origin engine; direct presets and mixed/custom profiles share characteristic identities; no mandatory primary Species, two-parent limit or requirement to inherit every donor grant; donor eligibility is not a grant; manifested mixed contributors do not reopen unmanifested ancestry history; exact provenance; complete traits/progression/choices; Upbringing and Background outside biological grants; scoped DM authority; intrinsic values and partial overlaps do not automatically stack. Red/Bronze ordinary Breath Weapon permits independent damage type and **whole area** parameter selection, while retaining one action, progression and pool. The Dwarf/Rock Gnome **selected test-case outcome** is one Dwarf Stonebound Tremorsense, without the Rock Gnome passive mode. Neither decision is a general balance authorization.

**Retain as candidates, not established:** deterministic player-directed biological transmission; no default latent state; coupled Drow darkvision/sunlight adaptation; allocation burden tied to calibrated mechanical power; conservative unknown-effect gating; level-indexed capacity; the precise reconciliation defaults proposed below.

**Not established:** an eight-point or other numeric limit; final Species or trait prices; parity among the nine conventional Species; universal trait equivalences; a general first-party mixed-construction recipe; parentage weighting or randomness; automatic independent spell-course accumulation; double-valued resource pools; dormant hereditary storage; universal separation of advantageous traits from their costs; a native catalog that already covers every inherited intrinsic or downstream mechanical effect.

**Observed tension:** the original Dwarf/Rock research union produced three qualifying contacts, whereas the later selected Stonebound-only result produces two in that fixture. Both numbers are correct for *different* manifestations and must not be compared as if they describe the same character. Scenario coverage and class-independent measured endpoints cannot turn unevaluated feats, spell effects, social utility, creature-type targeting or trait synergies into a defensible zero price. Source identity and mechanical identity must not be conflated: a shared concept name is weaker evidence than a complete authored behavior.

## 2. Candidate first-party construction procedure

This section is an **implementation-ready conditional algorithm**, not an approved native selection policy. The engine should represent rejected and unresolved alternatives without granting them.

### 2.1 Deconstruct and establish eligibility

1. Select the authored origination method. For the initial biological procedure, validate its contributor roles and count using method data, not an assumed pair of Species. Any number of eligible contributors can contribute evidence.
2. For each donor, read only its verified **currently manifested, heritable inherent characteristics** and their complete authored progression, parameters, source occurrence and choice evidence. A currently granted feature that is learned, acquired, temporary, not inherent or not designated heritable by the selected procedure is not silently eligible.
3. Never promote a historical Species, a grandparent's dormant alternative, an unselected Ancestor option, an independent feat, or an Upbringing into an offer merely because it appears in a contributor's history.
4. If the catalog or procedure cannot determine whether an actual manifested feature is heritable, mark that candidate **unresolved**. Catalog membership alone is not enough.
5. Keep the source occurrence, canonical mechanic, configuration parameters, donor ID and identity of any shared resource or linked adaptation distinct. Two donors offering one identical occurrence provide two evidence paths, not two copies of a grant.

A mixed contributor's expressed lightning-line Breath Weapon offers that effective configuration. Its prior Red/Bronze history does not, without an explicit latent-transmission rule, restore Fire or Cone as separate values. The whole authored childhood-to-adult level progression transmits as one entitlement; the donor's current level, spent charges and level-earned character resources do not.

### 2.2 Transmission, expression and selection groups

6. The **candidate default** is deliberate selection at creation, subject to a single biological procedure's eligibility and capacity rules. Random inheritance could be an alternative procedure mode later, but must produce the same validated transmitted set. Neither third contributor nor repeat donors increase capacity.
7. A transmitted characteristic is one whole mechanic or an explicitly authored recombinable parameterized instance. A selection-group choice may set multiple parameters/effects together; grouping is a user-interface decision, not automatically one free balance unit.
8. An author may declare individual parameters recombinable. A recombinant instance may draw an eligible field value from a different compatible offer **only** where the canonical trait permits that field; the complete original action, progression, save, limitation and resource semantics remain. Composite fields such as a complete line/cone area are atomic. A source supplying one parameter is not automatically supplying every related adaptation.
9. Resolve transmitted versus expressed states explicitly. **Provisional conservative policy:** no default latent hereditary inventory. Unselected offered values are *not transmitted*. Transmitted selections normally express if valid, and otherwise remain blocked/unresolved rather than silently carried forward as dormant. A future bounded latent rule requires its own evidence, capacity and generational rules; no prior profile may be reinterpreted retroactively as latent storage.
10. Effective grants must retain full provenance, verified choices and original source relationships. Future level unlocks and pool progressions are committed at creation and evaluated at every required breakpoint. They do not become additional free selections when unlocked.

### 2.3 Intrinsic foundation and conflict resolution

11. The final profile must explicitly resolve **exactly one** usable creature type, size and ordinary walking-speed value. Multiple offered values for a scalar slot are alternatives, not additive values; equal values from distinct sources still need identity or an authored resolution. This is a *configuration choice*, not designation of a primary Species. The resulting type has its actual targeting consequences, not a fictional average of parent types.
12. Distinct movement modes, senses, anatomy and physiological needs remain independent authored characteristics; they can coexist when valid, but are not silently inherited because another property was selected. An upgrade adds to an effective property only if the source defines that relationship; otherwise competing values require selection. The direct Wood Elf speed replacement precedent does not establish a generic mixed-source maximum-speed rule.
13. An expressed ability that requires another ability, anatomical structure, form, resource or choice may activate only when the full prerequisite closure is valid. Reject the dependent expression if closure fails. Expressed tradeoffs linked by an authored adaptation are not arbitrary negative-point credits.
14. Same exact authored occurrence and configuration: collapse duplicate evidence into one grant. Same canonical definition from different occurrences: require a verified stacking/independence/consolidation/replacement policy. Same name with different effects: preserve the distinctions; never equate by name. Explicitly overlapping subeffects apply once if nonstacking; genuinely different subeffects remain when their grants are authorized. No arbitrary conflict winner based on insertion order.
15. A resource is owned by the authored effective pool, not by the number of offers or distinct activation modes. Stone Strike spends Stone's Endurance charges; Stone Throw has its separate authored pool. Two identical grants do not double charges. Separately sourced access to the same spell does **not** automatically establish additional free cast pools.
16. A relationship disposition may keep both, replace one, bind parameters, combine duplicate evidence, or block both as unresolved. A missing catalog rule or unauthenticated table claim must never silently produce a playable effective grant.

**Specific fixtures, not universal rules:** Red/Bronze produces four potential (Fire/Lightning × Line/Cone) ordinary Breath Weapon configurations; one ordinary Breath Weapon and pool remain. The selected Dwarf/Rock outcome retains only the complete Dwarf Stonebound Tremorsense; there is no simultaneous 15-foot Rock Earthbound passive mode. A proposed Drow adaptive bond would normally keep the authored 120-foot Darkvision and Drow Sunlight Sensitivity together, but that bond **is not encoded or approved** as a universal first-party rule. Darkvision independently obtained from another source must not acquire Drow's drawback.

### 2.4 Provisional allocation and balance safeguards

**Revised scope (owner clarification, 2026-11-15):** A mechanically coherent, scalable, playable construction procedure is the immediate objective. Final numerical parity is **not** an acceptance gate while combat and other gameplay systems are still in development. The research evaluator remains useful but must not delay the inheritance implementation or become a second rules engine.

17. Keep **mechanical validity**, **construction allowance**, and **power confidence** distinct:
    - Mechanical validity: are transmitted characteristics eligible, expressed effects resolvable, required fundamentals present, dependencies closed, resource ownership coherent and choices legally bound? This is a hard requirement.
    - Construction allowance: does the complete proposed Origin respect the selected origination method's *provisional*, versioned allocation policy? This may use deliberately coarse authored weights. This is a configurable game-design limit, not a claim of measured power equality.
    - Power confidence: what do the existing conditional scenarios actually show? Report known outliers and unmodeled power as diagnostics. Missing complete combat, spell, or class modeling is **not** automatically a mechanical invalidity.

18. For a practical first policy, permit a method-scoped **weighted characteristic-bundle allowance**: a catalog author assigns each complete mechanical characteristic or inseparable linked bundle a coarse, revisioned load; the origination method supplies an allowance. Load applies to the **final effective, nonduplicated bundles**, not contributor count, UI choice groups, names, JSON effects, or source occurrences. Foundations such as type, size, movement and senses must be explicitly represented and included consistently rather than treated as free defaults. Stronger and more universally useful abilities can have higher provisional loads. No new fixed two-feature rule, two-parent quota, numerical budget, cost table, or scalar power equivalence is approved by this document.

19. Authoring may initially classify load into a **small number of coarse relative tiers** to avoid delaying the system until every mechanical effect has a quantitative estimate. Both tiers and allowance must be editable and content-versioned. Conventional Species/Ancestry presets remain directly selectable as full authored configurations even if their diagnostic loads are not identical; conventional and mixed configurations should be reviewed together when tuning. This avoids converting unfinished first-party baselines into immutable price anchors.

20. Inseparable benefit/drawback adaptations are one priced bundle. Negative traits do not generate transferable coupons to purchase unrelated positives. Multiple offers of one effective grant or pool do not increase capacity. A cross-trait synergy not reflected in provisional costs can generate a design warning or prompt review; it does not require general-purpose combat simulation before construction is possible.

21. Apply allowance to **the complete committed progression** at level 1, 3, 4, 5, 11, 17 and additional relevant unlocks. The policy may use per-level loads and allowances or an all-level committed allocation cost, but must not allow a character to gain unrestricted high-level capabilities solely because the current level is low. Preserve the authoring-policy revision in the approval record and define explicit migration/re-adjudication when that policy changes.

22. An **unknown mechanical definition** or missing relation required to execute a grant blocks automatic authorization until authored or explicitly decided by a trusted DM. A **known executable mechanic with unmeasured power** need not block authorization merely because the research evaluator cannot price its real impact. A native characteristic without a provisional construction load needs an authored default or explicit DM allocation decision; it must never be silently valued at zero. Record the distinction in the result.

23. First-party automatic mixed construction can begin when the **small, provisional allocation policy and characteristic semantics** have been authored and tested. It does **not** require a calibrated damage-to-utility exchange rate, representative campaign distributions, statistical parity, completed combat, or full class modeling. Preserve table-authorized overrides as a separate and clearly attributed path.

### 2.5 Reconstruction and status contract

On successful admission, create one current inherent configuration with exactly resolved intrinsics, effective characteristics, chosen parameters, linked relationships, present/future unlocks, resource identities, provenance and construction-policy revision. Preserve the source proposal, decisions and history without granting anything from history. Do not change Upbringing, Background or independent grants. This procedure creates an initial Origin and does not execute a temporary or permanent transformation.

Required outcomes: **invalid** for malformed provenance/choice data; **conflicting** for incompatible expressed traits; **incomplete** for missing required selection/foundation; **unresolved** for unapproved transmission, missing mechanical relationship, unresolved executable effect or missing required allocation policy (not merely unmeasured power); **authorized** only when all required first-party or scoped table policies have actually executed. The current C# status types can represent these outcomes, but first-party transmission and balance admission are not yet implemented.

## 3. Focused correctness and stress-test matrix

These are **candidate acceptance cases**. They specify expected semantics, not claims that the existing C# engine already produces all outcomes without DM authorization.

| Case | Expected result |
| --- | --- |
| Direct native Human or High Elf | Direct authored package remains selectable; required size, skill, feat/spell-choice evidence remains mandatory; do not infer missing open-catalog options |
| Red + Bronze Dragonborn | Four eligible parameter combinations for **one** complete ordinary Breath Weapon; no doubled ordinary charges; no automatic Chromatic Warding, Metallic Breath or subtype adaptation |
| Previously mixed Lightning-Line parent | Offspring may be offered the parent's effective Lightning-Line behavior; no automatic Fire, Cone or unexpressed ancestor package |
| Human + High Elf + Dragonborn | Three contributors broaden offers, not capacity; full union of Human ranks/feat, full Elf course and all Dragonborn mechanics requires independent admission and may fail |
| High + Wood Elf | One shared Elf foundation; preserve independent source-bound spellcasting ability and course if both otherwise eligible; no automatically merged or stage-interleaved spell choices |
| Drow + Dwarf | Choose one effective ordinary Darkvision profile; Drow sunlight drawback applies only if its relationship is actually transmitted under the selected rule; no automatic 120+60 range |
| Drow + Stone Goliath | Cavernous Sight can yield 180-foot range from 120 only if **both** the underlying grant and the authorized level-4 upgrade are effective |
| Orc + Goliath Powerful Build | Never double the common carry-size effect; preserve genuinely separate push/drag/lift and grapple-ending effects only under supported composition |
| Dwarf + Rock Gnome Tremorsense | The selected one-capability test result is **Dwarf Stonebound only**, not the three-contact unresolved union |
| Stone Goliath | Stone Strike and Stone's Endurance share the actual pool; Stone Throw retains its separate pool; advanced unlocks remain committed |
| Duplicate exact source offered by three contributors | One effective grant and pool; three evidence paths; no capacity multiplier |
| Incompatible size/type/speed sources | No arbitrary winner; require exactly one verified effective scalar value and account for its actual targeting/utility consequences |
| Linked adaptation without linked cost | Reject or leave unresolved; do not let a player gain a free separate drawback credit or unpriced advantage |
| Unknown homebrew or unselected Human feat | Cannot certify parity; identify the unresolved mechanic instead of marking it power-neutral |
| At level 1 under capacity, at level 5 over capacity | Reject the entire selection under that revision at creation |
| Temporary form or Rite | Outside this biological construction procedure; no union of temporary/current, historical or independently acquired grants |

### Independent quantitative checks against the existing research

The following **separate endpoints** can be reproduced algebraically from the documented fixtures; they must not be added into a single power score.

| Controlled measurement | Level / scenario | Result | Construction implication |
| --- | --- | --- | --- |
| Human Adaptable Learning | 1 / 5 / 17 | 4 / 8 / 20 extra ranks | Treat continuous advancement as one full progression, not free later ranks |
| High Elf fixed leveled spell entitlements | 1 / 5 / 17 | 0 / 2 / 2 distinct free leveled castings per Long Rest | Entitlement count is not spell-effect power |
| Red Breath, two unresistant enemies, one attack replaced | 1 / 5 / 17 | +3.325 / +11.85 / +28.90 expected net HP damage per selected opportunity in this fixture | Target count and level radically affect conditional marginal benefit |
| Combined Human ranks + High Elf free spells + Red Breath (stipulated manifestation) | level 5 | **8 ranks, 2 spell entitlements, +11.85 conditional net damage** | Demonstrates an independent-benefit union; not a legal or balanced inherited allocation |
| Drow Sunlight Sensitivity | 8 attacks + 4 Search/Spot checks, exposure q=0/0.5/1 | 0 / 0.30 / 0.60 expected successes lost | Avoidability and exposure prevent a universal negative price |
| Dwarf/Rock Tremorsense | four authored contact events | Dwarf-only 2, Rock-only 2, unresolved union 3, selected Stonebound-only 2 | Reconciliation materially changes the benefit before any pricing |
| Gem limited flight vs. synthetic permanent flight | three crossings 15 minutes apart at level 5 | Gem 1, permanent 3 solo gates reached | Frequency/duration must matter; one movement ability label is not one power unit |
| Stone's Endurance/Stone Strike | PB 2, sufficiently large hits, conditional hits | Two defense uses can prevent 19 expected damage; two offensive uses add 7 conditional damage, **not both** | Shared-resource opportunity frontier is not two independent pools |

The first three rows intentionally retain the report's simplified assumptions: 5.2 expected replacement attack damage; Red Breath gross per target 4.2625, 8.525 and 17.05 at the shown levels; no enemy resistance, geometry solver, overkill or class-feature effects. These comparisons are scenario calculations, **not** measured campaign outcomes. The broader checked-in suite has 26 diagnostic contexts with known unevaluated mechanics and does not sample representative encounter prevalence. No complete power-parity conclusion follows.

**Reproduction from repository root:**

    python3 -m unittest discover -s tests -p 'test_character_origins_power_evaluation.py' -v
    python3 tools/character_origins_power_evaluation.py --scenario-suite tests/fixtures/pillar2-power-scenarios.json --output /tmp/pillar2-species-context-comparison.json
    python3 tools/character_origins_power_evaluation.py --scenario-suite tests/fixtures/pillar2-power-scenarios.json --source ancestry.dragonborn.red --compare-source ancestry.dragonborn.bronze --output /tmp/pillar2-red-bronze-context-comparison.json

These commands exercise **the existing diagnostics**, not the unimplemented candidate allocation rule. Run the complete native and C# Origin construction suites after any production policy implementation.

## 4. Proposed implementation boundary and required authoring

Apply the candidate as a **native/catalog-backed policy layer feeding the existing source-neutral engine**, not a second Python-origin runtime or a Species-pair switch statement. In particular:

1. **SourceDeconstructor:** expose a heritability decision per manifested instance under the selected origination method, using verified current profile and source/procedure metadata. Do not infer from category or name.
2. **Transmission selector:** collect submitted *proposals* but resolve eligible transmitted choices server-side from actual trusted profiles and policy. Record field-level provenance for authored parameter recombination; prevent historical offers and unsupported variants.
3. **Reconciler/constraints:** add verified relations for mutually exclusive intrinsics, linked benefits/drawbacks, subeffect overlap, source-bound choices, dependency closure and resource ownership. Do not claim that existing general-purpose relationship enums already execute every needed semantic.
4. **Allocation admissibility:** use a catalog-versioned policy with complete configured bundle valuation at every relevant level and explicit coverage. Separate pending, table-authorized and first-party calibrated outcomes. Never accept a browser-provided budget or cost ledger as trusted authority.
5. **Reconstruction:** retain the full configured grants and provenance in current/underlying state; update the owning profile transactionally only through its authenticated server integration. Diagnostic power output is not a mechanical grant or character entitlement.
6. **Tests:** implement the matrix above as genuine C# production integration tests once the policy is approved. Include two-source, three-source, nested parent, same-occurrence duplication, parameter recombination, type conflicts, temporal unlock and candidate-cost adversarial cases; separately verify read-only evaluator reproducibility.

No new compulsory Species/Ancestry JSON schema is approved by this document. A versioned native schema and migration must be reviewed before execution.

## 5. Revised completion priorities and unresolved approvals

**P0 — fix the governing rule:** This Pillar 2 increment is successful when the same shared engine can construct coherent conventional and mixed Origins, replay its decisions and reconstruct the result with exact provenance. **Do not require perfect power balance or a completed combat engine.** The previous full-calibration prerequisite is superseded by the owner's 2026-11-15 clarification.

**P1 — inherit and manifest:** Author the biological procedure's contributor eligibility, transmitted characteristic choices, direct-versus-latent status, and complete manifestation behavior. The recommended **candidate default** remains deliberate player selection with no implicit latent cache. Allow any procedure-supported number of donors and mixed descendants without rediscovering ancestral offers. Random generation, if desired, may choose inputs but must follow identical validation.

**P2 — reconcile:** Author reusable relationships for intrinsic slots, complete/partial overlap, explicit parameter recombination, benefit/drawback coupling, whole-progressions, required prerequisites, and independent versus shared resources. Keep authored relationships source-neutral, not pair-specific. The selected Dwarf/Rock Tremorsense consolidation and Red/Bronze parameter combinations remain regression fixtures; they do not establish universal shortcut rules.

**P3 — provisionally allocate:** Choose a small, editable burden classification and one biological allowance with meaningful default constraints. Preserve coherent conventional presets; do not force them through the player-facing assembly workflow. Avoid obvious full-parent stacking without claiming that approximately equal nominal load proves gameplay parity. **Exact tiers, allowances, cost mapping, and whether suppression has hereditary effects remain candidate decisions requiring gameplay approval.**

**P4 — execute and test:** Implement the adopted procedure by extending the existing C# Origin construction pipeline and catalog policy. Test direct, two-contributor, three-or-more-contributor, nested mixed, repeated sources, two mechanical sources with the same name, parameter recombination, linked adaptation, intrinsic conflict, dependency failure, duplicate resource ownership, staged unlocks, homebrew unknowns, DM override and deterministic order independence. Tests must assert actual final grants, not merely calculate power vectors.

**P5 — subsequent balance work:** Use the retained evaluator selectively to detect large outliers and obvious exploits. Refine costs, initial Species and Ancestries as combat and other rules mature, without changing the identity/provenance/expression model or requiring a new inheritance engine. No universal scalar ranking, campaign-prevalence study or class feature implementation is required for this milestone.

**Decisions still awaiting approval:** default eligibility of hereditary versus acquired characteristics; precise initial allocation tiers/allowance; latent suppression and generational reappearance (recommended initial default: no latent carry-forward); authored coupling of Drow's superior Darkvision and sunlight drawback; general rules for independent source-bound spell courses and overlapping resource pools; how catalog revision changes affect existing completed characters. None is silently approved because it is recommended here.

### Release acceptance criteria

- The same canonical characteristic model constructs a directly selected conventional Origin and a mixed Origin; conventional presets require no manual decomposition by the player.
- The selected biological procedure accepts more than two contributors and previously mixed contributors, and no unexpressed ancestry history, duplicate offers or extra donor grants unearned abilities.
- One reconstructed Origin has a complete, resolved foundation, exact configured characteristics, source evidence, legal choices, well-defined suppression status, valid dependencies and accurately owned uses/resources at every authored unlock.
- Identical inputs and policy revision produce identical results regardless of donor enumeration order; invalid/unsupported proposals yield specific actionable failures.
- The provisional, editable allowance prevents obvious automatic unions of entire powerful source packages while preserving legitimate cross-source combinations and DM overrides.
- End-to-end C# tests cover the inheritance/reconciliation cases above and prove that output is actually mechanically projectable within the deliberately supported implementation subset. Unsupported future mechanics remain explicitly identified, not invented.
- Initial balance diagnostics compare complete conventional and representative mixed profiles sufficiently to expose major accidental stacking or extreme advantages. They **do not** need to prove precise power equality across classes, combat, spells, utility and all encounter types.
- No production change to native Species/Ancestry characteristics or deployment to main is implied by adoption of this design candidate.


**Conclusion:** Pillar 2's architecture is sufficiently established to implement a tested, source-neutral mixed-construction procedure. Exact power calibration remains future work; a provisional, editable construction allowance and coherent characteristic interactions are sufficient to advance the current milestone. The immediate work is inheritance, manifestation, reconciliation and working reconstruction—not expanding the research evaluator.
