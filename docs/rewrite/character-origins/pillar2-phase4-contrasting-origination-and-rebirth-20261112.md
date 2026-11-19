# Pillar 2 Phase 4 — Contrasting origination and Rite of Rebirth

**Date:** 2026-11-12
**Base:** `feature/pillar2-characteristic-inventory-20261111`
**Implementation branch:** `feature/pillar2-phase4-origination-rebirth-20261112`
**Review:** [PR #150](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/150)
**Boundary:** Rules Core mechanics and C# acceptance tests only; no persistence, API endpoints, Character Sheet integration, or new first-party playable Species.

## Common construction pathway

The existing `NativeOriginConstructionEngine`, source deconstructor,
process evaluator, reconciler, dependent binder and constraint evaluator
continue to provide **Deconstruct → Combine → Reconcile → Manifest → Reconstruct**.
There is no Plant, Construct, Fiend or Dragonborn-specific construction engine.

`NativeOriginMethodAvailabilityEvaluator` reads the **nearest explicitly authored**
`originationMethods` from the native creature-type hierarchy. An absent
default is **NotGranted**, not an explicit prohibition. An unknown method/type
is **Unresolved**, not automatically authorized. Fiends retain both
`biological` and `spawning`; Plants use `plant-reproduction`;
Constructs use `construct-assembly`.

`NativeOriginProposal.ProcessCreatureTypeId` is optional process-eligibility
evidence. It is grounded in an actual selected native Species when available.
An independently asserted or synthetic type needs an authenticated
`process-type` decision whose `creatureTypeId` matches the proposed type.
This is **not** a grant of creature classification or type-based statistics.
Method defaults and contributor/process requirements remain distinct:
a process may demand a creator, blueprint, spores, magic, materials, or
other authored roles without automatically transmitting the inputs' traits.
The established `NativeOriginInputRequirement` and verified table decisions
supply nonparent bounds without a universal contributor count.

The Phase 4 C# tests construct deliberately synthetic Plant, Construct, and
Fiend profiles. They use the common engine and explicitly approved
process-type, contributor-requirement and characteristic-selection decisions.
**These fixtures do not publish creature Species, reproduction rules, or a
universal Fiend spawning procedure.** Where a process lacks approved native
rules or verified table permission, it remains unresolved/incomplete.

## Executable Rite of Rebirth

`NativeOriginRebirthEngine.Execute` accepts an authoritative **existing
Origin profile**, a distinct **target construction proposal**, a Rite ID,
and the trusted application authority and scope. The existing profile must
come from authenticated, already-authorized character state; a caller-supplied
serialized profile must not be trusted as that input.

1. Validate the existing profile and require the selected native
   `rite-of-rebirth` complete-replacement operation.
2. Build the proposed destination through the same
   `NativeOriginConstructionEngine`, using its internal reconstruction-target
   entrypoint. Ordinary direct creation with a post-origination method is
   unresolved and can not become an initial-creation shortcut.
3. Require a verified, scoped `rite` decision for the particular Rite ID,
   with `allow: true` and **exact** `targetSpeciesId` and `targetAncestryId`.
   The general method catalog is not an authorization to perform a Rite.
4. If an active temporary form exists, require a separate authenticated
   `rite-form-interaction` outcome naming the retained effective form;
   absent that decision, do not guess the interaction. A non-form change to
   the current effective configuration is also unresolved without its rule.
5. Exclude the previous baseline's inherent grants by default; install the
   target configuration's independently validated inherent grants and
   Species-derived creature type. Reserve independent grant identities before
   assigning target grant IDs, preventing identity collisions from erasing
   unrelated entitlements.
6. Preserve all independent grants, applicability evidence, persistent
   influences, earlier historical contributor and origin events, and the
   same individual/profile ID. Append one **completed** Rite history event
   with the prior underlying configuration as historical evidence.
7. Return the new profile only if **all** authorization and coherence checks
   succeed. There is no partial mutation or rollback.

An authenticated Rite decision may name `retainPreviousGrantIds` and
`suppressTargetGrantIds` to implement a **table-specific** partial or altered
result. Referenced IDs must identify actual previously earned or newly
constructed grants. Unresolved intrinsic collisions remain unresolved until
the table defines a coherent outcome. This does not change native defaults.
The resulting history event records the authorizing table decision.

For example, an authorized Human → Red Dragonborn Rite removes the active
Human Species package, replaces its Humanoid inherent classification with
the Dragonborn-derived Dragon classification, leaves independent
Class/Background/Upbringing/training entitlements intact, and retains the
Human baseline and Rite as historical evidence. A subsequent Rite replaces
the then-current Dragonborn baseline, not the historical Human configuration.

When a temporary alternate form is active, `Underlying` changes only after
the explicit Rite interaction is accepted; `Current` may remain the active
temporary form. Future form reversion must use the latest `Underlying`.
The actual temporary transformation and reversion executor remain Pillar 4
responsibilities.

## Acquisition boundaries

A native Ancestry with `acquisitionMethods: ["rite-of-rebirth"]` remains
restricted during normal initial creation. Such an Ancestry can become an
eligible **target** under the Rite method even if marked nonselectable for
ordinary creation; eligibility alone never authorizes completing the Rite.
The synthetic restricted-Ancestry test proves this behavior. No unfinished
Platinum, Fivefold Chromatic or Ruby Dragonborn records were added.

## Acceptance and regressions

`NativeOriginPhase4Tests.cs` exercises:

- Native Plant, Construct and both Fiend method defaults, including missing
  defaults that require a separately verified method permission.
- Nonanimal and nonparent input roles, exact contributor bounds and no
  automatic creator/material trait transmission, including incomplete inputs.
- Native Human → Red Dragonborn replacement, inherent creature type,
  preservation of independent grants, historical events and source identities.
- Unavailable target/unauthorized Rite failure with no committed replacement.
- Two successive Rites, previous baseline snapshots and no historical trait stacking.
- Authenticated partial retention, altered creature type outcome and duplicate
  inherent/independent grant-ID collision safety.
- Active temporary form interaction as separately authorized effective state.
- Synthetic Rite-only Ancestry acquisition gating, without inventing published content.

Existing Phase 1–3 acceptance suites remain in place, including the genuine
selected-native biological mixed-Origin and dependent-parameter binding cases.

CI workflow `.github/workflows/pillar2-phase1-origin-contracts.yml` runs:

```sh
dotnet restore RulesCore.slnx
dotnet build RulesCore.slnx -c Release --no-restore
dotnet test RulesCore.slnx -c Release --no-build
python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```

The latest PR run is the authoritative validation record; no local .NET SDK
or complete checkout is available in the authoring environment.

## Remaining limits and Phase 5 dependencies

- Real playable Plant/Construct Species, authored Fiend spawning specifics,
  individual Rite procedures/requirements, and exceptional Dragonborn
  native content are not yet selected-native executable material.
- A selected native specific Rite authoring format and eligibility evaluator
  remain future content work. This phase uses exact verified table decisions
  for examples, not an invented universal rite procedure.
- `NativeOriginMethodAvailabilityEvaluator` covers native type defaults, not
  every Species-specific reproductive exception or full contributor ancestry
  compatibility. Those require authored process evidence and authorized policy.
- The current contracts and mechanics can preview authorized Origin grants,
  report incomplete choices, accept trusted table decisions, and reconstruct
  coherent underlying state. Phase 5 must expose these operations through
  the authoritative Rules Core API, persist/reload profiles and verified
  decisions, provide authenticated existing-profile provenance, enforce
  ownership and process permissions, and project effective grants to Character
  Sheet. No consumer-side rule reimplementation is authorized.
- Pillar 4 retains ownership of staged supernatural progression, temporary
  alternate forms, triggers, agency, and form reversion behavior.

**Merge boundary:** This PR targets the Pillar 2 development branch only.
Do not merge into `main`, merge this PR, or start Phase 5 without authorization.
