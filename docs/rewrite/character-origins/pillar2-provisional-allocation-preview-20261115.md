# Pillar 2 — provisional biological allocation preview

**Date:** November 15, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114` / draft PR #157  
**Status:** **Implemented as an advisory, reproducible analysis contract.** No numerical budget, default prices, burden tiers or admission decision has been approved.

## Purpose

The combined biological Origin system deliberately separates three questions:

1. **Mechanical validity:** Does the final manifestation satisfy authorized transmission, dependencies, reconciliation, provenance, resources and foundation rules? The existing C# Origin construction engine answers this and must remain authoritative.
2. **Allocation allowance:** Does a complete, mechanically valid Origin fit an expressly selected, revisioned biological allocation policy? The current preview can *simulate* this, but a normative first-party policy and trusted admission path do not yet exist.
3. **Gameplay power:** What are the relative effects in relevant scenarios? The existing research evaluator reports separate endpoints and important unknowns; a burden total must never be presented as measured gameplay power.

The goal remains player-directed construction of a mechanically coherent expression of the player's intended backstory. Multiple contributing Species expand the choice set, not the automatic allowance. Conventional Species/Ancestry presets continue to work without forcing players through a mixed-origin assembly process.

## Implemented contract

`src/RulesCore.Mechanics/NativeOriginAllocationPreview.cs` declares:

- `NativeOriginAllocationPolicy` — `Revision`, `RuleId`, `MethodId`, `Allowance`, and explicit `Prices`. At present this is **caller-provided hypothetical data for analysis**, not a trusted grant of authority. No live native `biologicalTransmission.allocation` object is authored.
- `NativeOriginAllocationPrice` — a priced characteristic, or an *inseparable linked bundle* with all member keys. Experimental positive load values are necessary for a test. No actual Species/Ancestry has received an approved value.
- `NativeOriginAllocationComponent` — one **final effective** inherent grant, a stable component key, original candidate IDs, earliest authored unlock level, and any matched candidate price.
- `NativeOriginAllocationCharge` — one charge for one effective characteristic, or one charge for a complete group of effective characteristics; references all contributing effective grants.
- `NativeOriginAllocationAssessment` — a preview-only outcome, exact revision, optional allowance/committed total, components, charges, and explicit errors.

Canonical keys are `intrinsic:<slot>` or `trait:<canonical-origin-trait-id>`. They are **not** trait display names, ancestry-specific hardcoded if-statements, a particular number of biological parents, or inherited historical claims.

### Accounting invariants

- **Only effective grants are charged.** Source candidate offers never create costs by themselves. Three Elf donors offering a single consolidated Low-Light Vision result incur one hypothetical charge, not three. Distinct effective instances that survive explicit reconciliation are separate charges.
- **Complete linked bundles are one unit.** A candidate policy can price Drow's `darkvisionFeet` with its actual Sunlight Sensitivity as one group. Every member must be present and verified from a **shared contributing lineage**. Different contributors may not satisfy an arbitrary bundle merely because their traits have matching names. The `originLinkedSelections` and verified table-waiver rules continue to be enforced by the Origin engine, not by this preview.
- **Committed progression is charged once at creation.** An inherited Cavernous Sight trait has an earliest level-4 unlock; its hypothetical load is part of the Origin's initial burden. No independent new charge appears at level 4. This does not turn an unavailable pre-level-4 effect into a usable effect.
- **Every effective characteristic needs an explicit price.** A missing cost never becomes 0 by omission, even if its effect is currently unmodeled by the quantitative evaluator. Nonpositive hypothetical prices and duplicate/overlapping pricing definitions are invalid for the prototype. An unsupported canonical trait or unknown required unlock remains unresolved despite an asserted price.
- **No negative-trait coupon.** Linked disadvantages do not create unbounded transferable credits; a bundled price represents the whole expression. Other source-neutral tradeoff policies would need separate approval and tests.
- **No trust escalation.** A supplied hypothetical allowance cannot authorize a construction, override a verified DM decision, make a restricted trait biologically inheritable, or alter the profile. `within-allowance` says only that a *candidate accounting ledger* adds up.

### Outcomes

| Status | Meaning |
| --- | --- |
| `not-applicable` | Not a mixed biological Origin under the native method. Direct presets are exempt from mixed-method allowance, though complete direct profiles remain useful research comparisons. |
| `not-configured` | No explicitly supplied hypothetical policy. There is **no** numeric verdict and no implied zero burden. |
| `invalid-policy` | Missing revision, mismatched method, invalid price, duplicate pricing, or overflow. |
| `unresolved` | The Origin is incomplete/unapproved; a characteristic cannot be recognized; a cost is missing; or the linked candidate bundle is incomplete/ambiguous/cross-source. No total is certified. |
| `within-allowance` | **Hypothetical only:** each effective item was priced, and the committed load is within the candidate allowance. |
| `over-allowance` | **Hypothetical only:** each effective item was priced, and the candidate total exceeds the candidate allowance. |

## Coverage and validation

`tests/RulesCore.Mechanics.Tests/NativeOriginAllocationPreviewTests.cs` uses real canonical traits and actual constructed Origin results:

- Human/Elf-style full foundation equivalents via Elf physical intrinsics plus Low-Light Vision and Trance.
- Three Elf contributors offering the same selected Low-Light Vision characteristic, tested against a single-donor equivalent.
- Drow's source-linked intrinsic Darkvision plus Sunlight Sensitivity, counted as one candidate bundle while the three physical foundation slots are counted separately.
- Stone Goliath Cavernous Sight, an authorized example of a selected higher-level mechanic, burdened in full at creation despite its level-4 unlock.
- Incomplete, deliberately malformed or underpriced scenarios, untrusted/unknown native definitions and direct Species selection.
- Preview results leaving the original construction result, authorization, recorded grants, progression and source provenance unchanged.

The test prices (e.g., `1`, `2`, `3`, `4`, `6`) and allowances (`6`–`100`) are **illustrative arithmetic fixtures only**, not recommendations for first-party costs.

## Remaining work for enforceable allocation

A later approved implementation must:

1. Author an **editable, versioned method-specific allowance** and price/linked-bundle taxonomy in trusted first-party rule data. Review conventional and representative mixed Species together; do not presuppose today's conventional Species are equally priced.
2. Define explicit fallback behavior for an authored/DM-waived linked adaptation or custom mechanic, and an authenticated DM allocation override that applies only within its actual table scope.
3. Include the complete future feature trajectory and cost treatment for all currently eligible characteristics, with no accidental omission of size, creature type, movement, senses, limited resources or acquired choices.
4. Execute admission in the trusted native construction path, **only after** final reconciliation/dependency closure and an authoritative rule revision lookup. A browser-provided ledger is not a budget decision.
5. Persist the effective policy revision and adjudication evidence with the completed Origin, and define how future rule revisions affect already constructed characters.
6. Verify API and Character Sheet displays for allocation diagnostics and final admission outcomes. Continue treating quantitative power confidence as a separate report, not a surrogate for permission.

**No change in this increment authorizes a new biological trait, caps the number of parents, forces a dominant/primary Species, or modifies other origination procedures such as construct assembly.**
