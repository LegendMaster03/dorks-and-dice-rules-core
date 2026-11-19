# Pillar 2 — General characteristic reconciliation infrastructure increment

**Workstream:** A (general reconciliation)  
**Base:** `6b17cdac7d5d93bdccac0038aa179da3004ad2f6`  
**Branch:** `feature/pillar2-general-reconciliation-20261119`  
**Scope:** Post-reconciliation whole-profile prerequisites. No native gameplay definitions, projector, balance rules, biological eligibility or Character Sheet changes.

## Before/after coverage

| Interaction | Before | After |
| --- | --- | --- |
| Identical authorized occurrence from multiple contributors | Already consolidates and preserves source candidate/lineage evidence | Unchanged; effective prerequisite selectors can recognize every consolidated source offer |
| Shared definition across different occurrences | Unresolved without authored disposition; explicit verified coexistence was not honored | Fixed: a verified pair-specific RetainBoth preserves distinct grants; unadjudicated pairs stay unresolved |
| Conflicting intrinsic slots | Unresolved unless scoped disposition | Unchanged |
| Same typed sense, permanent resistance, movement mode, or executable passive rule from distinct traits | Already detected; requires explicit complete-trait disposition | Unchanged |
| Partial semantic overlap without executable authored evidence | Not inferred from labels or common fields | Unchanged; requires gameplay authoring |
| Independent and shared-use pools | Canonical same-contributor ownership is bound after reconciliation; no extra pool | Bound dependencies are revalidated after any later suppression; orphan dependents are removed, not published with stale owner IDs |
| Parameter recombination and choices | Same-complete-definition replacement requires offered values and authored composition or exact authenticated table outcome | Unchanged |
| First-party linked intrinsic/trait prerequisite | Source-authored link checked before and after reconciliation | Retained; also participates in general final prerequisite closure |
| General authored or authenticated Requires | Checked only against pre-reconciliation selected grants | **Fixed:** rechecked against final effective grants until transitive closure; suppressed prerequisites remove affected dependents |
| Requires minimum count after duplicate consolidation | Original selected grants could count multiple offers for one effective characteristic | **Fixed:** checks count of distinct effective grants rather than duplicated source offers |
| More than two contributors, previously mixed evidence, and ordering | Existing native mixed, nested provenance, and deterministic round-trip coverage | Unchanged; focused two-donor consolidated-evidence regression added |
| Malformed, forged or incomplete policy evidence | Structural validator and authority boundary already reject or leave unresolved | Unchanged; no promotion of unsupported policy in final pass |

## Implementation boundary

`NativeOriginConstraintEvaluator.VerifyEffectiveRequirements` executes only **Requires** constraints with native authoring or an authenticated exact-scoped `enforce` outcome. An authenticated `allow` still waives its exact constraint. A constraint that declares a table decision ID must match that exact authenticated decision rather than borrowing another ruling for the same target. It matches selectors against **one source offer at a time** using each grant's `SourceCandidateIds`; a merged grant cannot fabricate a selector by mixing fields from separate contributors. A candidate is not counted twice because two donors share its effective grant. Parameter conditions inspect current, not merely offered, parameters.

The method runs after reconciliation, dependent bindings, shared-resource binding, and existing native linked-selection verification, repeating until no effective grant loses a required companion. When that closure removes a grant, the existing canonical choice and resource binders run again until no more grants are removed. This prevents a shared-resource spender from retaining a stale owner after a general prerequisite removes its pool. It only suppresses dependent grants; it never resurrects a deleted grant, adds a grant, selects an alternative, or authorizes unsupported relationships. The prior pre-reconciliation check remains in place.

## Regressions

New production-engine tests use the actual Orc native Species (three physical intrinsic slots) and the native biological mixed-contributor selection flow, supplemented by **test-only authenticated table outcomes** to exercise rule shapes without writing first-party policies.

- Satisfied direct prerequisite stays effective.
- Authorized suppression breaks a two-hop prerequisite chain; closure removes both dependents, even when ordered ahead of the first failing constraint.
- Two donors' identical walking-speed occurrences consolidate into one grant with two source-candidate references. A one-grant requirement succeeds; a two-grant requirement remains incomplete.
- Forged rule pointer without executable catalog support does not become authority.
- A mismatched declared table decision ID cannot borrow another authenticated constraint outcome.
- A canonical Goliath shared-use spender is removed when a verified prerequisite suppresses its selected resource owner; no orphaned resource owner reference remains.

The existing Tremorsense owner-selected Dwarf/Rock Gnome scenarios, provisional Drow relationship, all conventional round trips, and complete native catalog definitions are left untouched.

## Decisions still required

1. Whether two distinct complete characteristics with partial shared effects can coexist, suppress one another, or be componentized.
2. Whether shared canonical definitions across different authored occurrences count as one selectable acquisition or separate retained grants.
3. Whether competing intrinsic values may use chosen replacement, coexistence, or a new player choice procedure.
4. When separately owned resource pools should be shared, replaced, or remain independent.
5. Rules for nonrecombinable parameter choices, power balance, progression-dependent interactions, and special inheritance eligibility.

These are gameplay/authoring decisions, not gaps that this increment silently fills.

## Validation

```bash
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release --filter FullyQualifiedName~NativeOriginConstructionTests
dotnet test RulesCore.slnx -c Release
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Hosted GitHub Actions is required for executed .NET tests when the development environment has no .NET SDK. Record final run results separately from this design document.

## Executed hosted verification — 2026-11-19 UTC

**Verified source/test head:** `63c6c65bf39e5920e7d1deb4e4385027d907acbd`.

- [Structure validation run 44467124049](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/44467124049): **passed**.
- [Full native API, .NET, Python/catalog, Docker CI run 44467124015](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/44467124015): **passed**.
- .NET Release restore/build: **zero errors**, 10 warnings in existing, out-of-scope code or pre-existing assertions; none introduced by this increment.
- Host: **140/140** tests passed. Mechanics: **578/578** tests passed, including the seven new positive/negative/adversarial cases.
- Complete Python suite: **808/808** tests passed. Pillar 1/3 adjacent regressions and source-split/catalog validation passed.
- Native-only Docker build, layered content build, and public/private/readiness smoke checks passed.
- The feature branch remains unmerged; temporary PR #165 is for hosted CI only. This report addendum changes documentation, not the verified source or tests.

## Adversarial follow-up correction

A second-pass binder originally received parameters derived in the previous pass. If an unconfigured native spell trait was automatically bound to a selected intrinsic and that intrinsic was later suppressed, the binder could mistake the newly bound parameter for preserved nested-contributor evidence. The fixed-point loop now **rebuilds each surviving grant from the original reconciled grants** before binding again. Legitimate previously manifested parameters remain intact because their preexisting inherited evidence is part of the original grant, not introduced by the preceding pass.

The source-neutral `originConstraints` check previously trusted matching catalog `id` and `kind` alone, allowing proposal-supplied selectors or minimum counts to alter an authored relationship. It now requires exact authored subject/condition selectors, minimum/maximum counts and linked parameter paths. Unrecognized or incomplete authoring stays unresolved; no new native rule is published. A test-only native ruleset exercises both valid and forged configurations through the production engine.

Additional native regressions exercise an intrinsic-dependent High Elf spell after its ability source is removed, cyclic prerequisites with reordered constraints, and canonical rule-pointer forgery. The constraint evaluator also skips null constraint entries already marked invalid by earlier structural validation.

These are infrastructure/integrity corrections, not new policy decisions. The original complete CI results remain evidence for their original commit only. The corrected source/test HEAD was revalidated through hosted CI as recorded below.

An additional reconciliation review found that `RetainBoth` was recorded for a selected pair but the shared-definition pass still suppressed both complete grants unconditionally. The shared-definition overlap check now tests every distinct source-occurrence pair against its exact authorized coexistence evidence. Unadjudicated pairs remain unresolved, and no balancing or universal stacking policy is inferred. A native Orc/Elf Low-Light Vision regression verifies default unresolved status, followed by exactly two separately sourced grants only when a trusted decision explicitly authorizes `RetainBoth`. The result is an auditable exception, not a new first-party default.

## Final adversarial review CI evidence (2026-11-19 UTC)

**Verified corrected source/test commit:** `cca6c6be75619958c6daeeb20e45df878a2df1c6`.

- [Structure validation 44469407104](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/44469407104): **passed**.
- [Full .NET/native catalog/Python/Docker run 44469407087](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/44469407087): **passed**.
- .NET: **140/140 Host**, **582/582 Mechanics**, no failed or skipped tests.
- Python: **808/808**. Source split, native Docker build, content layering and API smoke checks passed.
- Four further production-engine xUnit cases were added beyond the original seven; the native generic-rule case also exercises two forged contract variants. All use the actual engine rather than mocked reconciliation.
- **The following commit only records the already completed code/test CI evidence.** No merging or production deployment is authorized by these checks.

### Review scope and incomplete infrastructure

Verified: exact occurrence and contributor evidence, duplicate same-occurrence consolidation, source-authored trait prerequisites, explicitly authorized pairwise dispositions, native dependent binding and pool ownership, conventional source round trips, nested and three-contributor test paths.

Still not universally resolved: complete cross-definition effect overlap semantics, independent grant resource merging or replacement, automatic resolution of competing intrinsic slots, generalized level-gated interaction authoring, and balance/eligibility policies. This increment neither supplies these rules nor proves arbitrary mixed Origin viability.

### Workstream C integration

C's `NativeOriginPassiveEntitlementProjector` reads the already constructed `Underlying.InherentGrants`. A can remove traits and physical foundation grants, and can preserve two separately sourced grants when a verified `RetainBoth` relationship exists. C must therefore be tested with the **combined** A/C build for suppressed passive-trait absence, retained source/occurrence identity, separately authorized coexistence without benefit aggregation, invalid mixed-foundation fail-closed behavior, and serialization round-trip of surviving grants. These are cross-branch integration tests, not evidence of a compiler conflict. No combined executable CI is claimed in this review.

## Final bounded follow-up — exact coexistence authorization

The final review found one actionable boundary defect: the reconciler accepted a verified decision with the same relationship target and requested action even if the resolution identified a *different* `Basis.TableDecisionId`. It now requires exact authenticated decision-ID equality. Neither a decision scoped to another target nor an unrelated valid evidence ID can authorize `RetainBoth`.

Additional production-engine regressions check (1) mismatched versus exact ID on the same selected Elf/Orc pair and (2) three distinct shared-definition occurrences (Elf, Orc, Dragonborn) with only Elf–Orc coexistence authorized. The third occurrence does not become implicitly authorized; reversed contributor ordering yields the same documented unresolved overlap. The existing same-occurrence consolidation and cyclic-prerequisite tests remain in force.

This follow-up is limited to a verified authorization-boundary defect and its adversarial regression. It does not approve group stacking, biological inheritance, or a default winner. The exact CI status for the corrected commit is separately verifiable from GitHub Actions.
