# Pillar 2 — Native Innate Spell Resource Ledger (Bounded Integration Slice)

**Date:** November 16, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft, unmerged)

## Goal and scope

`NativeOriginInnateSpellResourceLedger` consumes the **existing** canonical, authorized `NativeOriginInnateSpellProgressionProjector.ForCharacter` result to calculate a read-only-hosted, revision-checked state transition for **individual innate free-cast pools**. It uses the general native `NativeResourceResolution.Spend` and `Recover` implementations for actual arithmetic and `NativeRecoveryEvent` handling.

This is an in-process candidate **ledger transition**. It is not production persistence, authentication, a spell action execution engine, or a new authorization grant. The project still lacks a trusted persistent character-owner service and validated parent-profile revision binding. Do not expose its candidate state as an independently authorized client command.

## State contract

`NativeOriginInnateSpellLedgerState` records the matching `RulesetId`, catalog `Fingerprint`, Origin `ProfileId`, original `ProposalId`, total `CharacterLevel`, SHA-256 `ProjectionFingerprint` of the complete projected spell course, a nonnegative monotonically increasing `Revision`, and `RemainingFreeCasts` keyed by **effective grant ID**.

The fingerprint is a *consistency check*, **not** a cryptographic signature, authenticated provenance or protection from state forgery. It covers all projected spell entries, sorted by grant ID, including canonical trait/source, unlock level, selected spellcasting ability and resource metadata. It invalidates this ledger when the course changes without explicit rebase. It does not prevent the owner of a serialized payload from forging a new snapshot; the host must retain the authoritative copy.

The projector must return `resolved` or `none`. A locked future spell never has a live pool. A selected spell with unresolved symbolic free-use entitlement cannot bootstrap an apparently numeric pool. The only currently supported free-use recharge is the source-authored `long-rest`. At creation, each *available* free-cast grant begins with the native **maximum entitled uses**, in a **bootstrap candidate only**.

### Operations

- `Initialize(catalog, origin, nativeCharacter)` produces an initial **candidate** `State` with all initially granted free-cast pools at maximum. The **host must require create-once** semantics; calling `Initialize` again is not a legitimate way to regain uses.
- `Apply(..., state, new(SpendFreeCast, expectedRevision, grantId))`: validates complete native provenance, exact saved state, current level, projection fingerprint and grant entitlement, then subtracts **exactly one** from that grant's own free-cast pool. Attempting to overspend does not change state.
- `Apply(..., state, new(ShortRest, expectedRevision))`: validates the state and uses native recovery; Long Rest-only spells remain unchanged.
- `Apply(..., state, new(LongRest, expectedRevision))`: validates the state and restores **each** existing free-cast pool to its own authored maximum, without changing or creating other grant pools.
- `CheckAtWill`: checks whether this already-available selected spell has `atWill: true` and returns `permitted`, without changing state or pretending that a casting action happened.
- `RequestSlotCast`: checks whether this already-available selected spell has `slotCasting: true` and returns `external-slot-required` without using an innate free-cast charge. The caller must hand off to the separately authenticated character spell-slot rules, including the appropriate spell/slot level and action execution. No Origin spell-slot spend is implemented.

On an accepted state-changing transition, `Revision` increments by one. The operation's supplied `ExpectedRevision` must match the stored revision; stale commands fail closed. Invalid IDs, missing keys, extraneous keys, values below zero or above the exact native maximum, invalid or changed profile identities, invalid levels and changed projection fingerprints are rejected without state changes. The returned maps are frozen so callers cannot edit an already-returned computed state in place.

**Critical production invariant:** The host must obtain the authoritative state from its own authenticated storage and commit the new state **atomically** if the stored revision still matches the expected revision. These pure methods **cannot** guarantee that by themselves. The host must store any event IDs/replay metadata required by its transport, apply duplicate-command policy, and prevent a replacement ledger from being initialized once a prior one exists. Caller-provided copies are not trusted. Revision numbers alone are not an authorization mechanism.

## Native behavior retained

- **Tiefling:** Infernal Hellish Rebuke (character level 3) and Darkness (level 5) have independent once-per-Long-Rest free-use pools, even if they share the same casting ability. Infernal Hellish Rebuke's `freeCastSpellLevel: 2` remains authored and is not treated as a separate resource pool or a spell-slot deduction. The other two Tiefling leaves and Drow follow the same source-qualified per-grant accounting.
- **Forest Gnome:** Speak with Animals uses a native character **resolved proficiency bonus** for the per-Long-Rest maximum. Without it, `Initialize` fails. Its at-will Minor Illusion has **no** charge pool.
- **Selected mixed biological Origin:** With already-verified scoped DM evidence, only selected inherited spells are considered. Merely being a donor does not supply unselected spell charges. The two selected at-will Tiefling cantrips in the covered mixed case create no free-use pools.
- **Level and character changes:** A ledger tied to an old `CharacterLevel` or course `ProjectionFingerprint` cannot be silently reused after advancement, selection changes or temporary modification. A future trusted migration/rebase path must preserve spent prior pools and establish the new entitlements. Restarting from `Initialize` is not a substitute.

## Tests and exclusions

`NativeOriginInnateSpellResourceLedgerTests.cs` uses real native Infernal Tiefling/Forest Gnome progression and character advancement projections. Cases cover level 1 locked spells, level 3/5 grants, independent pool spending, exhaustion, Short Rest not restoring Long Rest uses, Long Rest recharge, slot-mode isolation, at-will checks, revision mismatch, invalid/forged state values and fingerprints, JSON round-trip, level-change rejection, and symbolic proficiency. The existing `NativeOriginTieflingSpellBindingTests.cs` also tests mixed selected-spell ledger initialization and cross-donor failure.

This increment deliberately does **not** implement: execution/cast success, spell-level prerequisite checking for spell-slot casts, a spell-slot decrement in the character state, multiple grant overlap resolution, Level Up ledger migrations, production authenticated storage, persistence locks, full rest-event routing, temporary form applicability, Character Sheet integration, or new inheritance policy/cost limits.

The host must not interpret the `PreviewOnly` result flag as an implementation artifact that permits client-authorized spends. API/runtime integration and authoritative state transitions are separate future work.

PR #157 remains draft. No merge, deployment, or main-branch changes are authorized by this increment.
