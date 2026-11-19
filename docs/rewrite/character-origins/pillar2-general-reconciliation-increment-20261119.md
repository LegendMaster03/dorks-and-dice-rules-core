# Pillar 2 — General characteristic reconciliation infrastructure increment

**Workstream:** A (general reconciliation)  
**Base:** `6b17cdac7d5d93bdccac0038aa179da3004ad2f6`  
**Branch:** `feature/pillar2-general-reconciliation-20261119`  
**Scope:** Post-reconciliation whole-profile prerequisites. No native gameplay definitions, projector, balance rules, biological eligibility or Character Sheet changes.

## Before/after coverage

| Interaction | Before | After |
| --- | --- | --- |
| Identical authorized occurrence from multiple contributors | Already consolidates and preserves source candidate/lineage evidence | Unchanged; effective prerequisite selectors can recognize every consolidated source offer |
| Shared definition across different occurrences | Unresolved without authored disposition | Unchanged |
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
