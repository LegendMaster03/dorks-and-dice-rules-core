# Pillar 4 — Phase 5 contract verification and resumption gate

**Reviewed:** 2026-11-12  
**Status:** Engineering review of **unmerged** Pillar 2 Phase 5, not approval to merge drafts or issue supernatural gameplay content.  
**Pillar 2 Phase 5 head:** `feature/pillar2-phase5-origin-api-20261112` at `0b491eb71460767c781419ed717aacea256f3e35`, draft [PR #151](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/151), based on Pillar 2 `feature/pillar2-characteristic-inventory-20261111`.  
**Pillar 4 draft stack:** [#146](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/146) → [#148](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/148) → [#149](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/149). The stack was built on earlier Pillar 2 and is **not** reconciled/tested against this new Phase 5 head.  
**Canonical reconciliation:** [Shared Origin and effect contract](pillar4-shared-origin-effect-contract-reconciliation-20261112.md).

## Verdict

**The required structural corrections from Pillar 4's pre-API handoff are mostly implemented, so Pillar 4 library work can resume in a separate integration branch.** Waiting for the entire Phase 5 API or UI is unnecessary. However, the Phase 5 work is still a draft, and its restricted initial-direct API does **not** prove general host authorization for independently earned supernatural grants, a completed native supernatural reconstruction, or an effective transformed character.

Distinguish *implemented contract*, *test-only proof*, and *production capability*. In particular, do **not** merge the three earlier Pillar 4 PRs as-is because their effect-definition authoring and grant-identity assumptions predate Pillar 2 Phases 3–5.

## Verification matrix

| Prior blocker | Present implementation | Remaining dependency |
| --- | --- | --- |
| Trusted existing-character state | The stateless authenticated `/origins/preview` rejects submitted old profiles, historical/temporary forms, table rulings and contributor evidence; Character Sheet draft [PR #67](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/pull/67) contains owner-scoped draft persistence, revisioned receipts and a restricted server-side direct native activation path | No general server-backed loader/authorization path for independent supernatural entitlements, active forms, scoped DM decisions and permanent transition commits |
| Duplicate baseline replacement engines | `NativeOriginPermanentReconstructionEngine` centralizes atomic complete reconstruction; `NativeOriginRebirthEngine` delegates as a Rite-specific facade | General `supernatural-reconstruction` selected-native method has **no executable complete-replacement operation** or individual completion procedure; do not imply ceremorphosis is executable |
| First-party authorization vs DM | `INativeOriginNativeCompletionAuthority` separates verified individual native completion from scoped DM decisions. General method listing is not completion proof | No production authority provider or authorized native supernatural completion content yet. Existing Rite still requires DM decision in practice |
| Stable grant provenance | Origin carries source occurrence, source candidate, manifestation, effective parameters and resolved choices. Phase 5 `NativeOriginIntrinsicProjector` verifies creature type, size, walking speed; `NativeOriginVisionProjector` verifies actual canonical `rule.low-light-vision` effect | Both projectors intentionally reject independent grants, persistent influences and active temporary forms. A shared effective-character projection layer remains needed for Pillar 4 |
| Browser/saved state separation | Preview-only response and restricted initial direct activation distinguish proposals from server-held authorized state | No general source-qualified **supernatural acquisition and progression** authority |
| Prevent unauthorized form and mixed-Origin shortcuts | Preview refuses table/history/forms and missing contributor authority; first-party native Skill evidence is generated server-side only for authored eligible choice | No complete mixed, post-origination form-entry/reversion or overlap resolution |
| Regression validation | Phase 5 focused [CI 43288359068](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43288359068) passed 113 Host, 208 Mechanics and 345 focused Python tests on its head | Broader native/working consistency audit remains separately failing for 33 documented mirror conflicts. No deployed general supernatural features or end-to-end cross-pillar transformation |

**Important:** Source-backed **native effect execution now exists**, albeit narrowly, in `NativeOriginVisionProjector`: it checks the selected native trait occurrence and exact `rule.low-light-vision.effects` data. Pillar 4's speculative `effectiveEffectDefinitions` authoring grammar in PR #149 should **not** be adopted merely because that prototype's synthetic tests pass. Reconcile it with actual canonical source-local trait shapes, existing `originBindings`, dynamic/level-dependent parameters, and typed effect handling before authoring content.

## One remaining Phase 5 provenance concern

`NativeOriginPermanentReconstructionEngine.Execute` accepts `INativeOriginNativeCompletionAuthority.VerifyNativeCompletion` which returns `NativeOriginNativeCompletionEvidence(NativeRuleReference, CompletionEvidenceId)`. The engine verifies that the evidence ID is nonempty and that the rule reference identifies a selected native `rule`. In the completed `NativeOriginHistoryEvent`, however, the final argument is `completion?.Id` — a DM decision ID when one exists, otherwise **null** for native completion. The resulting profile-level `Adjudication.NativeRuleReference` records a **rule reference**, but does not record the actual native **CompletionEvidenceId** per completed event.

This is not a reason to block **Pillar 4 library-level design or synthetic tests**. It is an **audit/reload contract question before an authenticated persistent *native* reconstruction is enabled**: decide whether to add a separate source-qualified completion evidence field/event receipt or to require a linked trusted receipt in the character store. Preserve event-to-proof traceability for multiple completed reconstructions without confusing the DM decision ID with the native proof ID. Do not attempt to solve this by fabricating or changing an approved first-party supernatural procedure.

A related limitation is `NativeOriginIntrinsicProjector` using `ReferenceEquals(Current, Underlying)` as one of its **fresh direct-construction** restrictions. This is fine for the current stateless preview, which reconstructs those objects together. It cannot become a generic persisted/loaded character identity test, because equivalent serialized configurations are not reference-equal. Do not expand that projector into form/post-rebirth evaluation without a different authoritative-source check.

## Reconcile each Pillar 4 experimental PR

### PR #146 — effective-character effect composition

- Keep the exact-source/duplicate-target/unknown-status regression suite and the conceptual `retain / modify / suspend / replace` design.
- **Change the input boundary**: a `NativeOriginConstructionResult` may carry `ExistingIndependentGrants` supplied by another subsystem, but does not prove those non-Origin grants were earned. Obtain each Class/Background/Upbringing/Supernatural entitlement from a trusted character-grant provider or verified character-state adapter, not by assuming the Origin engine minted it.
- Distinguish a source influence, the earned progression node, and a currently active form grant; preserve source IDs and parameter/choice values through effect composition.
- Treat form-dependent applicability as an output of explicit native/DM rules, not as a general assumption that independent grants are usable.

### PR #148 — numeric character projection bridge

- Keep the restriction that it publishes **only fully resolved ability scores/modifiers**; do not turn its result into an authoritative complete character.
- The new Phase 5 intrinsic and vision projectors provide genuine source-proof examples but **do not replace** the general effective-character bridge.
- Validate actual trusted baseline score input, resource/HP change interactions, and unresolved classes/equipment/actions; do not use a client-provided fingerprint as permission to approve grants. The catalog fingerprint may remain **consistency/version evidence**.
- Do not try to consume the stateless `/origins/preview` as though its `previewOnly` results were committed character mechanics.

### PR #149 — catalog effect authorizer

- **Reconcile the native authoring contract before merging:** `effectiveEffectDefinitions` is an unapproved candidate; Phase 5 already interprets canonical `rule.low-light-vision.effects` with exact provenance. Retain narrowly typed/strict native proof and choose an extensible, compatible shared authoring approach rather than a second grammar.
- Support source-bound parameters, form-specific conditions, level-gated effects and optional choices from actual native definitions; do not require all future authoring to have permanently literal integer values.
- Correct the current `AuthorizesForm` rule, which requires `HasVerifiedFormOverride` even for an otherwise first-party supported form. A verified native **first-party form activation** must be authorized without a mandatory DM ruling; the table override is an alternative.
- Reuse one authenticated scope and character identity across the native effect, Origin construction, and character input authorizers rather than separately pretending synthetic test doubles grant ownership.

## Immediate Pillar 4 work (no need to wait for completed Phase 5)

1. **Freeze the old stacked PRs as review/test references.** No merge, and do not rebase them onto `main`. Use a **new focused integration branch based on the current Phase 5 head** when code integration starts. Because Phase 5 is changing, record its exact base commit and re-run after coordinated updates.
2. Define the **minimum common earned-grant adapter** with distinct verified inherent vs independent vs supernatural-acquired vs form-only sources, while retaining native `NativeOriginEffectiveGrant` provenance and parameter values. Do not invent completed Class or supernatural entitlements.
3. Resolve one **typed canonical effect** using actual current native data and a genuine `NativeOriginConstructionEngine` result (e.g., verified first-party Low-Light Vision as a *source/authorization integration* regression). It is **not** new Draconic Bloodline content. Use this established test to check that the shared effect framework does not double-grant or contradict existing `NativeOriginVisionProjector`.
4. Define an **independently earned, test-only supernatural modifier** whose source acquisition is verified by an explicit test-state adapter, then project its narrowly scoped numeric effect. Do not publish unapproved numbers or native content.
5. Design **native first-party form eligibility and activation** against existing `ActiveTemporaryTransformation` and `Current`. Test source-backed entry, two differently restricted forms, latest-baseline reversion and concurrency; keep the full lycanthropy/vampire gameplay details unapproved.
6. Coordinate **terminal ceremorphosis** with `NativeOriginPermanentReconstructionEngine`; no second reconstruction path, and no permanent default without an authored native completion rule.

**Next implementation gate:** one tested, source-to-projection vertical slice, incorporating authentic direct Origin construction plus an independently verified supernatural grant occurrence; no server/browser privilege claims. After that, move to temporary form evaluation. Full API integration is gated on trusted storage and event history, not a fixed native fingerprint approval scheme.

## Required coordination with Pillar 2 Phase 5

- **Confirm native completion evidence persistence** (native `CompletionEvidenceId` tied to the completed event/revision) before exposing privileged native reconstruction commits.
- Keep the initial-direct stateless projector narrow; a general effective-character projection is **shared Rules Core work**, not something to bolt onto the browser Origin preview.
- Provide a trusted character-state/entitlement adapter contract when Phase 5 introduces more than a restricted initial-direct activation; its security semantics must not rely on an `Authorized` Origin result alone.
- Continue to distinguish a selected-native first-party completion from a DM override and from a method listing.

**No change to first-party supernatural content, `main`, Pillar 2 Phase 5 branch, or the three draft PRs is made by this review.**
