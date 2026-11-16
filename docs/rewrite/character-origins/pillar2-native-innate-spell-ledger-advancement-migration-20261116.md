# Pillar 2 — Native Innate Spell Ledger Advancement Migration

**Date:** November 16, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**Draft PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157)

## Problem and constraint

The first native Origin free-cast ledger is bound to the total character level and the canonical projected spell-course fingerprint. After level advancement, `Apply` correctly refuses the previous snapshot rather than letting a changed character cast or restore resources under a stale entitlement. Repeated `Initialize` is **not** a migration: it would reset already-spent charges.

The bounded `NativeOriginInnateSpellResourceLedger.Advance` method now computes a **candidate** transition from the existing saved level/revision and canonical source progression to the new, verified native character projection. Its purpose is to maintain previously spent charges, introduce legitimately unlocked native free-cast pools once, and reject entitlement changes that do not have an approved migration policy.

## Inputs and output

```csharp
NativeOriginInnateSpellResourceLedger.Advance(
    NativeCatalogSnapshot catalog,
    NativeOriginConstructionResult origin,
    NativeCharacterProjection previousCharacter,
    NativeCharacterProjection advancedCharacter,
    NativeOriginInnateSpellLedgerState state,
    long expectedRevision)
```

The two character projections must resolve against the **same** native catalog and the original authorized Origin. Both must have a resolved `advancement.total-level`. The existing ledger must match the *previous* level, Origin profile/proposal IDs, ruleset/catalog fingerprints, complete original spell-course fingerprint and expected optimistic revision. Every saved free-cast entry must match an actual, previously available effective grant and its authored remaining-use bounds. This reuses a common `ValidateSnapshot` routine also called by ordinary `Apply` transitions.

The new total character level must be **strictly greater** than the previous level. The projected set of spell grant IDs and their canonical spell/source properties must be unchanged, apart from the level-derived `Availability` and `FreeUsesPerRecharge` fields. A revoked, replaced, suppressed or changed spell grant requires **separate** authorized reconciliation; `Advance` does not transform an Origin or decide what happens to lost-resource pools.

For every free-cast grant:
- **Previously unlocked, unchanged maximum:** copy the **actual saved remaining count** unchanged. Level advancement does not imply a Long Rest.
- **Newly unlocked:** create the grant's authorized free-use pool at the new full maximum **once**; a correct host stores and advances the revision atomically, so this can not be applied a second time.
- **Previously unlocked, changed maximum:** return `spell-ledger-capacity-change-requires-policy` with no candidate state. This includes, for example, Forest Gnome Speak with Animals where the existing proficiency-bonus-derived per-rest maximum grows. Whether to grant additional unspent uses, preserve the fraction spent or retain the old remaining total is not established here; no unilateral gameplay decision is embedded.
- **Still locked / at-will / no free pool:** never allocate a free-use resource by default.
- **Newly unselected or removed grant / different source course:** return `spell-ledger-course-changed` and no state.

A successful migration increments the ledger revision once, changes `CharacterLevel` and `ProjectionFingerprint` to the new verified projection, and returns a *frozen, independent* resource map. The old ledger remains unchanged and is rejected if used against the new level.

## Production trust requirements

This method computes a **state candidate**, not a persisted character update. The eventual host must:

1. Authenticate the profile, old and new character snapshots, owner and correct advancement event. The native character projector validates ruleset mechanics, but does not prove a user's right to submit a level change.
2. Load the actual **authoritative saved ledger**, not a client-supplied revision or balance.
3. Check the expected revision and commit the new state in an **atomic compare-and-swap transaction**; only one advancement transition can win. Reusing an old snapshot or rerunning `Initialize` must not create charges.
4. Preserve event identity and replay protection as required by transport; this pure helper does not store event IDs.
5. Use a separately approved reconciliation policy for grant/capacity changes, form transitions or Origination changes.

SHA-256 of the projected spell course is **not a signature**, and the `Revision` integer is **not authentication**. The production transaction boundary and trusted source-profile/revision proof remain unresolved.

## Real-native regression coverage

The expanded `NativeOriginInnateSpellResourceLedgerTests` exercises:

- Infernal Tiefling level 3 to 5: Hellish Rebuke already spent stays at zero, new Darkness gains one authorized use, and Short/Long Rest behavior continues at the new level.
- Level 1 to 3: a previously empty ledger gains the newly unlocked Hellish Rebuke pool once; attempting a repeat migration with the migrated snapshot fails.
- Advancement with **no new unlock** (level 5 to 6) retains all remaining counts and survives JSON serialization/deserialization.
- Stale revision, unchanged/rolled-back level, unrelated Origin, invalid ruleset/fingerprint, outdated state and cross-level application all fail without a candidate state.
- Forest Gnome level 4 to 5: when proficiency changes its existing Speak with Animals maximum, the transition remains unresolved rather than guessing extra charges or silently refilling.
- Existing tests continue to cover free-cast exhaustion, rest semantics, at-will/nonmutating slot handoff and scoped mixed biological Origin isolation.

No native traits, Origin allocation mechanics, character classes, spell action execution or Character Sheet API were changed. Production profile authentication and Pillar 4 applicability remain separate.

**Required CI:** existing `Validate Rules Core Rewrite Structure` and `Rules Core Phase 3B Public Native API CI` workflows at the final branch HEAD. Keep PR #157 draft; do not merge or deploy without owner authorization.
