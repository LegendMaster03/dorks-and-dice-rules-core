# Pillar 2 — experimental whole-Origin dependency review

**Date:** 2026-11-11  
**Status:** Non-authoritative, source-neutral proof of concept. **No new game rule, inherited eligibility, mandatory custom-character budget, actual DM permission, authorized trait grant, or final profile is created.**

**Tool:** `tools/character_origins_whole_profile_validation.py`  
**Tests:** `tests/test_character_origins_pillar2_whole_profile_validation.py`

## Problem and pipeline position

The [general reconciliation review](pillar2-general-reconciliation-preview-20261111.md) identifies relationships between proposed characteristic offers. The [explicit decision proposal contract](pillar2-reconciliation-decision-contract-20261111.md) tests hypothetical retain/suppress/consolidate choices, and already checks some contradictions across several candidate pairs.

But **a pairwise-consistent choice is not necessarily a coherent character**. A retained trait may require another absent trait, an authored prerequisite, an unselected embedded spell or proficiency, or a resource-sharing rule. Similarly, multiple individually permissible grants may violate a character-wide cap, exclusion, or parameter relationship.

This tool performs the first **whole-profile dependency review** of those **tentatively retained characteristics**. It revalidates the exact earlier decision proposal and derives the active, suppressed, and undecided candidate IDs. It then evaluates **explicit synthetic** constraints over the whole proposed set. The tool does not infer unapproved Species-specific dependencies or silently assume the constraints in its tests apply to current Dragonborn, Elf, or other content.

### Inputs and trust boundaries

- The prior **reconciliation preview**, containing the exact selected candidate evidence, observed relationships and source diagnostics.
- The prior **proposed decision contract**, bound to that preview's fingerprint. Contradictory or incomplete decisions cause a **blocked-by-reconciliation** result; they never become an authorized current profile.
- A **declarative synthetic dependency study**, with explicitly authored characteristic selectors. A selector can refer to a source-qualified trait, an explicitly mapped mechanical definition, or an intrinsic slot (optionally one specific intrinsic value). These fields are generic: they do not require a fixed number of parents or a pre-existing Species name.
- Optional **recorded dependent choices** attached to exact provisional candidate IDs. No anonymous global choice is accepted as satisfying an unrelated trait's requirement.

A rule in a synthetic dependency study is **not** proof that it is an actual first-party rules constraint. The study exists to prove representation and review behavior, not to authorize the trait or validate inheritance eligibility.

## Declarative review operations

| Rule kind | General review purpose | Non-authoritative output |
| --- | --- | --- |
| `requires-any` | A retained characteristic needs at least one characteristic matching an explicitly declared selector set | Satisfied, missing prerequisite, or pending another decision |
| `excludes` | Two selected characteristic sets are not allowed together under **this proposed policy** | Excluded combination or pending |
| `selection-count` | Count all retained matching candidates, including choices spread across several contributors | Too few, too many, satisfied or pending |
| `requires-choice` | A retained characteristic needs explicit choices from a declared option set within an authored cardinality | Missing, invalid, or satisfied choice evidence |
| `linked-parameters` | A contract expressly relates nested parameter values of two different characteristic selectors | Equal, conflicting, missing counterpart, or unresolved parameter |
| `resource-coordination` | Two retained characteristics refer to a proposed common resource coordination group | **Requires resource policy** rather than fabricating a shared use pool |

Source-wide unresolved fields and review flags remain present for content-authoring review, but are **not converted into conflicts with unrelated selected traits**. The study also retains observations from currently authored internal-choice fields (`chooseFrom`, `choiceCount`, `spellList`, ability choices and player allocations), using the pre-existing standard-preset audit helper. These observations are still **not covered by a trusted choice schema** merely because some synthetic choice record was supplied.

### Status boundaries

The output distinguishes:

- `blocked-by-reconciliation` — the previous proposed keep/suppress choices are contradictory, incomplete, deferred, or need an additional intrinsic-slot policy.
- `declared-dependency-conflicts` — one or more declared rules finds a missing prerequisite, invalid choice, exclusion, missing selection, incompatible linked value, or invalid selection count.
- `declared-dependencies-require-review` — explicit rules may need an additional resource policy, unbound parameter, or pending decision, or source-authored choice semantics still need modeling.
- `no-declared-blockers-not-certified` — no issue identified by these **particular declared rules**; it does **not** establish complete dependency closure, eligibility, mechanical compatibility or balance.

All outcomes explicitly retain `fullDependencyClosure: not-certified`, `selectionEligibility: not-evaluated`, `grantsIssued: false`, `authorization: not-authorized`, and `balance: not-evaluated`.

A record that passes a synthetic constraint is **satisfied evidence** only. It does not become a legitimate in-game Origin choice or a Rule Core grant.

## Representative acceptance scenarios

- Human + High Elf competing `creature.type` alternatives are reviewed through the **existing explicit reconciliation contract**; an inconsistent or incomplete choice blocks whole-profile evaluation. Human Small/Medium options are not both silently granted.
- A synthetic prerequisite for Human Skillful is reported missing when its target is not retained, and can be satisfied when another contributor supplies the selected characteristic. **The prerequisite is synthetic, not a real Human rule.**
- A synthetic exclusion or choice cap across two unrelated traits detects an invalid **whole-character** proposal even if the pairwise characteristic identity audit itself finds no conflict.
- A retained trait with a declared dependent-choice requirement cannot be completed without explicit option selections. Authored internal-choice observations remain separately visible until their exact first-party rules are modeled.
- A synthetic link between two custom traits' `damageType` parameters detects a mismatch only if the relation was explicitly authored. **This does not add a global Breath Weapon/Resistance coupling.**
- Two traits assigned to one hypothetical resource group are **flagged for policy** rather than merging charges, actions, recharge behavior, or uses.
- A verified previously mixed contributor carries its actually manifested trait as a proposed candidate, not the entire unmanifested source Species.
- Untouched native source ambiguity such as Wood Elf walking speed remains reported in the source evidence without contaminating a Druidcraft-only proposal.
- All outputs remain **non-authoritative** and retain exact proposal fingerprint and source diagnostics, even when no declared rule reports a conflict.

## Production work still required

1. **Authoritative prerequisite and choice schema:** Build a general content contract that covers current native traits and their embedded, dependent choices. A heuristic observation is not equivalent to interpreting an effect.
2. **Actual grant identity and policy:** Convert *approved* reconciliation outcomes to current grant instances with complete identity, parameters and contributor provenance, without inventing duplications or combining resource pools. Do not persist provisional `evidence-N` IDs.
3. **Complete intrinsic policy:** Specify standard leaf-versus-parent replacements, default mixed-contributor selection and exceptional multi-valued intrinsic properties. These must be distinguished from simple candidate equality.
4. **Trusted authority and eligibility:** Differentiate standard selection, actual inherited eligibility, partially custom and fully custom policies, and explicitly approved DM adjustments. Neither a proposed policy ID nor catalog presence establishes an entitlement.
5. **Full dependency closure and validation:** The engine must validate all required effects and constraints of the **current** reconstructed configuration, not only the test contract's declared rules. Unknown or prose-only effects remain unresolved until modeled or explicitly adjudicated.
6. **Lossless standard reconstruction:** Verify that current standard Species/Ancestry configurations round-trip through decomposition, choices, authorized interaction rules, and reconstruction without changing their mechanics.
7. **Balance and playtesting:** Evaluate power independently of structural coherence. Mixed-Species defaults remain best-effort and the accepted disclaimer stands.

This is the first whole-character consistency **review**, not the completed system. The project continues to use **three public workflow stages** (deconstruct/assemble, reconcile/manifest, reconstruct), with the **five internal operations** maintained for precise implementation and tests.
