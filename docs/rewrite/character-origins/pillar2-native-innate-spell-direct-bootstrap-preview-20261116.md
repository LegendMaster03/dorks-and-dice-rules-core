# Pillar 2 — Delegated Direct-Origin Innate Spell Ledger Bootstrap Preview

**Date:** November 16, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft)

## Objective

Provide Character Sheet with a **stateless, source-matched native ledger bootstrap candidate** for an already committed conventional *direct biological Origin*. This is the narrow first network-facing adapter around `NativeOriginInnateSpellResourceLedger.Initialize`. It does not grant ownership, execute a casting action, persist resource balances, approve a table ruling or broaden biological inheritance eligibility.

The action remains **preview-only** even on the authenticated ingress. Character Sheet supplies the source proposal it independently retrieves from its character-owned saved draft, the committed Origin profile it independently retrieves from its authorized Origin store, and current native character projection inputs it constructs from the owner's saved ability scores and class advancement.

## Endpoint and request

`POST /api/mechanics/v1/origins/innate-spells/ledger/initialize-preview` is available only on Rules Core's **public, ordinary Character Sheet-delegated authenticated mechanics ingress**. It is not available through the anonymous API, direct user ticket, Wiki delegation or private-tunnel surface.

The request carries:

- `rulesetId` and **exact current** native `fingerprint`;
- the exact initial direct `NativeOriginProposal`, with conventional Species/Ancestry selections and any required creation choices, but **no** DM rulings, independent grant assertions, historical source profile, transformation or existing configuration;
- the last saved `NativeOriginProfile` from Character Sheet's authorized Origin store, which the native service **reconstructs independently** and compares with the input;
- a `NativeCharacterRequest` constructed by Character Sheet from saved character advancement, ability scores and optional native choices, with the same ruleset and fingerprint.

The endpoint obtains the current immutable `NativeCatalogSnapshot`, refuses stale fingerprints, checks bounded request collections and the first-party-only initial Origin shape, then invokes `NativeOriginFirstPartyChoiceEvaluator.Evaluate`. It requires an authorized, issue-free direct result and checks the **entire canonical serialized saved profile** against the fresh result. A caller cannot simply add a native grant, substitute an ancestry, or turn a serialized Origin into authority without triggering source mismatch.

It projects the submitted native character through `NativeCharacterProjector`. Actual class level and proficiency bonus must resolve from the native ruleset; no invented class progression or numeric proficiency formula is used. For the *read-only spell course only*, a partial character projection is accepted **solely** when its remaining issue codes belong to the fixed, unrelated set `hit-point-input-required`, `movement-input-required`, `defense-input-required`. This is not an allowance to execute the missing HP, movement or defense mechanics. Invalid or unresolved class progressions, option-selection defects, missing proficiency and all other issues fail closed.

The endpoint finally calls `NativeOriginInnateSpellResourceLedger.Initialize` and returns `contractVersion: 1`, ruleset/fingerprint, `previewOnly: true`, `committedOriginMatched: true`, and the native ledger calculation `result` (also `PreviewOnly: true`), including its initial revision-0, per-effective-grant resource state. There is no remote persisted ledger, revision mutation, spell-slot spend, rest or new grant.

## Trust and integration limits

- Rules Core's HTTP ticket validates Character Sheet delegation, **not ownership of the asserted character** or the historical draft/committed profile. Character Sheet must source the proposal, committed profile and current class state from its own owner-authenticated server stores, and must perform its own revision checks.
- The endpoint is deliberately **direct first-party only**. Mixed DM decisions, contributor provenance, temporary forms, profile reconstructions, in-place Rites, newly acquired grants and complex/origination alternatives are not eligible in this slice. A separate trusted evaluation pathway is required for each.
- Character Sheet's existing persistent `CharacterInnateSpellLedgerState` and CAS store must perform create-once bootstrap, exact current Origin version checks, atomic revision/event persistence, and transport replay protection. Neither a preview nor the profile comparison is a storage operation.
- The same native ledger library calculates free-use spending, Long Rest recovery, and advancement migration, but **none of those runtime events** is authorized by this endpoint. They require separate authentication and trusted event-policy checks before exposing a write path.
- An external client must never be permitted to submit a candidate ledger state directly to the Character Sheet storage service. All candidate resource maximums are recomputed from first-party native content.

## Tests

The Host authenticated API tests exercise anonymous refusal, correct ordinary delegation, denied foreign delegation, denied private-tunnel route, exact source-matched conventional direct profile, changed fingerprint rejection, forged committed profile rejection and DM-decision injection rejection. Native mechanics tests continue to verify actual spell progression and grant-specific pool recovery independently.

The corresponding Character Sheet work is prepared on [draft PR #68](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/pull/68). Keep both branches draft; this endpoint must not be mistaken for production spell casting.
