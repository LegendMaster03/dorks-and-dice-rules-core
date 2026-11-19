# Pillar 2 — Explicit Adjudication of Innate Spell Resource Capacity Changes

**Date:** November 16, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft, unmerged)

## Design question

An already-unlocked Origin spell grant can derive its Long Rest free-cast maximum from character progression rather than a constant. Forest Gnome's native Speak with Animals has `freeUses: "proficiency-bonus"`. On progression from total character level 4 to 5 its maximum rises with the native proficiency bonus. The first ledger advancement implementation **intentionally** reported `spell-ledger-capacity-change-requires-policy` rather than silently restoring all uses, adding the capacity difference, or preserving a fraction of remaining uses. None of those automatic outcomes has been approved as a universal rule.

The source-neutral solution in this increment preserves that default. It adds an **explicit table-effective decision** for the *actual remaining use count* of each changed pool, verified using the existing `INativeOriginAuthority` and `NativeTableRuleDecision` contracts. It does **not** establish one of those candidate policies as a first-party default, authorize new biological inheritance, modify authored maxima, or broaden the meaning of a Long Rest.

## Existing native first-party behavior (unchanged)

For an already-unlocked spell whose maximum remains unchanged, `Advance` preserves its saved remaining uses. For a newly unlocked grant, `Advance` allocates its full new pool once. For any existing grant whose maximum changes, `Advance` still returns unresolved **unless** the table supplies a matching, independently verified decision. The modified grant must remain selected and canonical; a missing or replaced grant is a different reconciliation problem and still fails closed.

## Exact decision contract

A verified table decision must be:

- Scoped to the **exact trusted** `NativeTableDecisionScope` supplied by the host, with a nonempty ID and existing verified authority. Caller-provided `Authority`, `Scope`, or `AuthorizationEvidenceId` is not itself proof; the host's actual `INativeOriginAuthority.Authorizes` must accept it.
- Targeted to `new NativeTableDecisionTarget("origin-innate-spell-capacity", grantId)`, where `grantId` is the selected, previously unlocked, authoritatively derived **effective grant ID**, not a spell name, generic Species name, or donor proposal claim.
- One-to-one with **each** existing pool whose numeric maximum changes. Duplicate, unrelated, additional or missing capacity decisions fail closed.
- Bound to **both** exact native spell-course fingerprints, total character levels, the previous saved ledger revision and remaining count, and both old and new native maximums.

The `NativeTableRuleDecision.EffectiveOutcome` object must contain **exactly** these ten fields, with exact types and values:

```json
{
  "grantId": "grant-id-from-authorized-manifestation",
  "previousProjectionFingerprint": "old-projection-sha256",
  "advancedProjectionFingerprint": "new-projection-sha256",
  "previousCharacterLevel": 4,
  "advancedCharacterLevel": 5,
  "previousMaximum": 2,
  "advancedMaximum": 3,
  "previousRemaining": 1,
  "previousRevision": 1,
  "remainingAfter": 1
}
```

The numeric example is illustrative of one **table-selected** result, not an automatic rule. `remainingAfter` may be any explicitly authorized whole number from **0 through the new native maximum**. The table can thereby choose to preserve the previous remaining count, add newly earned uses, replenish fully, or impose another bounded outcome, without requiring the Rules Core engine to choose or hard-code any of them. The old and new maximums remain derived from **native rules**, not from the table decision. The decision does **not** replace, suppress, or change the grant, spell, native course, resource owner, or rest schedule.

The result's `AppliedCapacityDecisionIds` records the IDs of decisions actually used for a successful candidate migration, in sorted order. This is an audit handoff; the production host must persist those decision IDs and their verified audit records alongside the state transition. A serialized ID alone is not proof.

### Call shape

```csharp
NativeOriginInnateSpellResourceLedger.Advance(
    catalog, origin, previousCharacter, advancedCharacter,
    storedLedger, expectedRevision,
    capacityDecisions: [verifiedTableDecision],
    trustedScope: trustedCampaignScope,
    authority: hostAuthorityVerifier);
```

All existing callers that supply no additional parameters retain their original fail-closed behavior for changing maxima. Normal advancement with no changed maximum continues unchanged and rejects spurious capacity decisions.

## Validation and safety properties

The existing `ValidateSnapshot` still verifies the saved old state, old level, current projection fingerprint, source IDs, exact free-cast resource keys and capacities, and expected revision before the capacity decision is evaluated. An adjusted pool must be an **existing** grant in both previous and advanced projections; a decision can not create a new arbitrary pool or authorize a removed grant. Missing or mismatched scope, rejected authority, changed previous remaining uses, stale revision, changed course fingerprints, wrong old/new maximums, out-of-range remaining count, duplicates, extra decisions, or wrong target must fail without a migrated ledger.

The response continues to be a **candidate** immutable snapshot. Even an approved capacity outcome requires a production host to authenticate the actual persisted Origin/profile and character snapshots, enforce the table's authority and exact campaign scope, and **atomically compare-and-swap** the stored ledger revision. `INativeOriginAuthority` here is an integration contract, not a hidden production permission service. The output SHA-256 fingerprints detect consistency changes; they are not authentication signatures. A client-supplied older state or a repeat call to `Initialize` must never be treated as authoritative.

## Regression tests

The existing native Forest Gnome 4→5 test confirms that an absent decision still blocks migration. New parameterized tests cover several distinct **verified** remaining balances on that same native progression and confirm subsequent Long Rest recovery uses the new authored proficiency maximum. Negative tests verify wrong trusted scope, mismatched decision scope, absent authority, revision mismatch within the outcome, out-of-range balance, duplicate and unrelated targets, and spurious decisions on an unchanged native Tiefling capacity. The source baseline and authored trait payloads are never rewritten.

## Remaining decisions and integrations

A **default** resource-capacity growth rule for non-overridden first-party play is still unapproved. The exact remaining-use decision is a scoped table-level escape hatch, not a universal numerical policy. An approved first-party rule can later be authored and interpreted by the same general progression/resource mechanisms, subject to separate review.

The next major work is an **authenticated, atomic production ledger store** and actor/character/profile ownership verification in the appropriate host. Rules Core's native mechanics project currently exposes pure computations, not a trusted durable character database. Do not claim concurrency safety or replay protection until the host actually commits the state with an atomic revision check. Character Sheet, Pillar 4 state applicability, and actual spell execution/slot accounting remain separate integration gates.

Keep PR #157 draft and do not merge or deploy without owner authorization.
