# Pillar 2 Phase 5 — Cross-pillar contract reconciliation

**Date:** 2026-11-12  
**Branch:** `feature/pillar2-phase5-origin-api-20261112`  
**Dependencies reviewed:** Pillar 4's shared Origin/effect contract and its revised dependency plan on `feature/pillar4-supernatural-nature-design-20261111`.

This is a **Phase 5 integration record**, not a grant of new Species, supernatural progression, automatic completion permission, or approval to deploy/merge.

## Review findings

| Topic | Finding | Scope |
| --- | --- | --- |
| `Underlying` / `Current` / active form | Existing records distinguish persistent baseline, effective form, form activation, and other concurrent forms correctly | Preserve; Pillar 4 still owns form execution/reversion |
| Independent grants and persistent influences | The construction engine preserves supplied data but **does not independently authenticate acquisition** | Confirmed host-integration gap, not an engine-issued entitlement |
| Post-origination replacement | Rite already has a complete, atomic replacement algorithm; copying it for supernatural terminal transformations would be wrong | Shared engine implemented; native completion requirements still missing |
| Rite DM authorization | Necessary with the current selected-native content because an individual executable Rite is unavailable | Retained, but not a universal rule for other methods |
| Shared effect directive authoring | `effectiveEffectDefinitions` on Pillar 4's experiment is not an approved native grammar | Deferred to Pillar 4/shared mechanics |
| Broad native/working check | The failing consistency check reports 33 source-mirror conflicts across seven Classes, Competencies and Weapon Mastery features | Independent content-promotion audit required |

## Trusted state and ownership

**Character Sheet** owns the single character database, Site-issued character identity, saved builder decisions and effective state, and persistence revisions. **Rules Core** owns construction, reconciliation, selected-native definitions, effective grant mechanics, and transition evaluation. The browser owns **proposals only**.

A production *state-changing* Origin operation must, on the trusted server side:

1. Authenticate Site-issued user identity and the authorized Character Sheet session.
2. Verify character access, editability, archive state, relevant campaign/table membership, and the separate DM permission if an override is requested.
3. Load the **current** authorized Origin profile, independent earned entitlement occurrences, applicability evidence, persistent influences, completed events, temporary form evidence, and revision from Character Sheet PostgreSQL. No corresponding browser fields may substitute for this load.
4. Obtain a scoped DM decision from a server-verified source or an individually authored first-party completion decision from the selected native catalog.
5. Evaluate the proposed update through Rules Core, revalidate source identities under the active ruleset and **reject** incomplete, invalid, conflicting, unresolved or unauthorized results.
6. Commit the authoritative decisions, result, source/event identities and next revision **atomically** with an optimistic-concurrency check and an idempotent completed-event identifier. Failure leaves the earlier authorized character unchanged.
7. Project only current authorized grants; independent entitlements remain separate and must not be silently removed, duplicated, or granted through historical evidence.

**No privileged API operation has yet been exposed.** The existing `/api/mechanics/v1/origins/preview` is authenticated but stateless, accepts only an initial-construction proposal, rejects purported previous profiles/history/temporary forms/DM state, supplies no trusted grant lists, does not commit anything, and marks output `previewOnly`. The client cannot turn its response into a trusted persistence record without a server reevaluation.

A fixed native fingerprint is not a permission. It detects content drift; stored character choices must be revalidated on actual relevant content changes rather than automatically accepted or rejected by the fingerprint alone.

## Shared permanent reconstruction

`NativeOriginPermanentReconstructionEngine` now owns the existing Rite replacement sequence; `NativeOriginRebirthEngine` remains a Rite-specific compatibility facade. Both use `NativeOriginConstructionEngine.EvaluateReconstructionTarget` for a post-origination target. Ordinary construction still rejects a post-origination method as an initial-creation shortcut.

The executor requires an actual selected post-origination method with `createsNewIndividual: false` and explicit `replace-complete-species-and-ancestry-configuration` operation. It requires separately verified completion for the exact target. A method listing by itself never suffices. The generic `supernatural-reconstruction` native method currently has **no executable replacement operation or individual completion rule**; it therefore remains unresolved rather than gaining a new gameplay default.

Completion authorization is distinct:

- A **scoped DM decision** can authorize the exact target and, where verified, explicit retained/suppressed grant identities. Existing Rite decision kind `rite` and form-interaction kind `rite-form-interaction` remain unchanged. Other permanent methods use `permanent-reconstruction` and `permanent-form-interaction`.
- An optional **trusted native completion authority** can later verify an individual authored completion, returning exact native rule and completion evidence. The executor checks that the claimed rule is a selected-native rule **separate from the general method catalog**. There is no production authority implementation or newly approved first-party completion procedure in this phase.
- Missing, mismatched, denied, or unsupported completion fails without exposing a new profile. Universal DM authorization does not bypass application ownership, campaign authorization, or required completed target validation.

Duplicate event IDs fail; completed events preserve the prior `Underlying` and the new baseline. Exact source/manifestation identities, resolved parameters, selected choices, independent grants and history are preserved. Old inherent grants are not restored by historical evidence. Baseline replacement under an active form needs an explicit verified interaction; its `Current` may remain the active form, but later form reversion must use the **latest** `Underlying`. No form entry, expiry, reversion, suppression, or equipment transformation executor is created here.

## Grant transport boundary

`NativeOriginEffectiveGrant` remains the type for manifested inherent characteristics, with character grant ID, source occurrence/path, mechanical definition, source candidates, manifestation ID, value, parameters, dependent choices and authorization. `NativeOriginIndependentGrantApplicability` remains distinct from earned independent entitlements. Class, Background, Upbringing, inventory, and supernatural progression must be authenticated by their own acquisition services, not treated as traits inherited from Species.

A shared effective-character projection adapter may be required later for arbitrary earned source occurrences and form-specific applicability. Do not invent that schema or apply the Pillar 4 experimental `effectiveEffectDefinitions` grammar during this correction.

## Validation issues and remaining boundaries

The earlier focused post-merge Origin run passed **186 Mechanics + 111 Host + 345 focused Python tests**, but reported `CS8602` on the Rite history collection. The shared executor uses `existing.History ?? []` when appending events to eliminate that nullable dereference. Check the newest branch CI for confirmation.

The broader `test_ruleset_split.NativeWorkingBoundaryTests.test_current_native_and_working_contracts` failed with 33 `authoritative <ID> conflicts with preserved working evidence` errors. The checker requires an exact preserved mirror or documented `sourceDivergences` ledger entry. The checked promotion ledger lacks divergence entries for representative failing entries (`class.barbarian`, `class.bard`, `competency.alchemy`, `class-feature.barbarian.weapon-mastery-level-1`). That establishes the **reason for the check failure**, not that authoritative native content is wrong. Audit and reconcile individually with the source-promotion owner; do not silently rewrite native rules or disable all mirror checks.

Phase 5's full end-to-end milestone still requires the Character Sheet trusted state loader, PostgreSQL Origin persistence and revision checks, authentic DM/campaign authority, persistent Rite commit, builder UI, effective projection, security regressions, and actual development deployment acceptance. No database migration or live merge/deployment is claimed by these corrections.
