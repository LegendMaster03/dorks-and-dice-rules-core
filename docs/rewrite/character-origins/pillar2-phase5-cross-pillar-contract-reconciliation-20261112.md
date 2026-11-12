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

**Rules Core exposes no privileged Origin mutation API.** Its `/api/mechanics/v1/origins/preview` is authenticated and stateless; it rejects client assertions of previously authorized profiles, history, temporary forms and DM authority, supplies no independent earned grants, and marks output `previewOnly`. A browser cannot convert preview output into trusted persistent state. The separate Character Sheet Phase 5 branch now implements a restricted native **initial direct** activation route, which rechecks the saved draft and Rules Core evaluation before a character-owned atomic commit. No generic Rite, mixed/DM, or form activation route exists.

A fixed native fingerprint is not a permission. It detects content drift; stored character choices must be revalidated on actual relevant content changes rather than automatically accepted or rejected by the fingerprint alone.

## First-party creation-choice eligibility (current increment)

`NativeOriginFirstPartyChoiceEvaluator` now supplies current-catalog
membership verification for a selected-native **Skill** chosen from the
authored `any-native-skill` requirement. Its evidence is derived by Rules Core
from its immutable `NativeCatalogSnapshot`, the actual direct source
candidate, the current catalog fingerprint, and an exact `skill` category
match. The client is not a trusted evidence issuer: stateless preview rejects
caller-supplied `CatalogEligibilityEvidenceId`, and the evaluator inserts
a source-checked reference only after validating the selected Skill.

This is **catalog membership**, not an authorization token, DM ruling, or
guarantee that any separately earned Training occurrence is already present.
`INativeOriginAuthority.Authorizes` in the native-only evaluator always
returns false, and the internal catalog sentinel scope is never a campaign
permission. Mixed, custom and transformational Origins receive no automatic
catalog evidence. Unrecognized catalog types (including `feat:origin`)
remain unresolved pending an authored eligibility/prerequisite contract;
we do not infer an Origin feat from its name or from an unrelated feat list.
Finite choices such as Human size continue to use their authored finite
options. Real native Human construction can now verify its Skill choice, but
remains incomplete while required Origin feat selection is unsupported.

The Rules Core unit and authenticated HTTP tests cover valid native Skills,
invalid category substitution, forged client catalog evidence, and the
deliberately unresolved feat catalog. Character Sheet's temporary Phase 5
branch-to-branch probe exercises the same native Human Skill contract against
the **unmerged** Rules Core branch rather than `main`.

## First-party native intrinsic projection (current increment)

`NativeOriginIntrinsicProjector` interprets a deliberately small subset
of the **evaluated** native `NativeOriginProfile.Current.InherentGrants`:
`creatureType`, `size`, and `walkingSpeedFeet`. It requires an
`Authorized`, zero-issue, first-party supported **Direct** initial Origin,
`Current` identical to `Underlying`, no temporary form, no independent
grants or persistent influences, and exact, eligible native candidate
occurrence provenance for every projected intrinsic grant. Missing slots
remain explicitly partial; duplicate slots, mismatched source evidence or
unsupported forms fail closed. It does not combine traits or infer a value
from the name of the selected Species.

The stateless authenticated `/api/mechanics/v1/origins/preview` response
now carries `intrinsics`, containing at most those source-attributed
scalars and `previewOnly: true` **in addition** to the existing evaluated
proposal. These are not permissions, parentage, DM rulings, or persistent
entitlements: browser previews cannot activate them. The present native
`NativeCharacterProjector` already accepts an explicit `BaseWalkSpeed`
and can resolve `movement.walk` including supported condition effects;
it still does **not** accept an entire authorized Origin profile or execute
other Species/Ancestry traits.

Character Sheet's Phase 5 feature branch now revalidates its own committed
initial direct Origin against the **latest** Rules Core profile and matches
each intrinsic scalar, grant ID, source ID and occurrence path against the
immutable committed configuration before passing base speed into native
character projection. Base size and creature type are labeled distinctly
from complete/effective character mechanics; everything else stays partial.
No old fingerprint is used as an authorization gate. The cross-repository
probe exercises real Orc (Medium, Humanoid, base walk 30 feet) and verifies
that incomplete Human and mixed Human–Elf produce no active intrinsic
projection. This is a narrow first-party mechanics increment, not general
Rite or effective-character projection support.

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

Character Sheet now has a distinct authorized-Origin storage substrate, revisioned receipts and a restricted first-party initial activation service, with related PostgreSQL tests; that does not constitute a general trusted grant loader. Phase 5 still requires authentic DM/campaign ruling evidence, persistent Rite/reconstruction commit, effective cross-pillar projection, Origin builder UI, and actual development acceptance. No live merge/deployment is claimed by these corrections.
