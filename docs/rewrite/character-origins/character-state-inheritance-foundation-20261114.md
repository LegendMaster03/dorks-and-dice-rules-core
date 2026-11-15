# Character State Inheritance — first Rules Core increment

**Date:** 2026-11-14  
**Scope:** Character mechanics inheritance foundation; no first-party supernatural gameplay approval, Character Sheet migration, or transformation activation endpoint.

## Authority boundaries

`NativeCharacterState` identifies one character and contains a **host-assembled** `NativeCharacterRequest` for the current authoritative primary mechanics plus persistent alternate-sheet instances. The host continues to own verified Origin, Class, Background, Upbringing, inventory, state history, campaign authorization and writes. `NativeOriginProfile.Underlying` remains the authoritative Origin baseline; its `Current` contract is not replaced or silently reinterpreted.

The resolver **does not certify** a character's primary mechanics. Before constructing `NativeCharacterState`, the trusted Character Sheet adapter must verify the saved character and compile its mechanics from owned authoritative domains. No public endpoint in Rules Core accepts an alternate sheet or awards its progression.

The mandatory `INativeCharacterSheetAuthority` is a second trust boundary for the selected character's persisted alternate-sheet definitions, overrides, advancement records and source-qualified grants. It is deliberately an interface without a permissive production implementation. The host must verify source occurrence IDs, acquisition event receipts, ruleset/catalog version, sheet ownership, eligible choices, granted entitlements, and any DM ruling. A string naming a rule, grant or receipt is not itself evidence that it was issued. Authorization changes should be checked again on each evaluation; use revision/transaction checks for future writes.

## Representation

- **Primary ID**: `@primary` is the reserved parent reference for the authoritative primary mechanics, not a second entity.
- **Persistent sheet**: `NativePersistentCharacterSheet` has a stable ID, character-scoped parent, definition source, acquisition evidence, typed overrides, locally owned progression, and applicability decisions. It can exist when no form is manifested. A parent may itself be an unmanifested advancement-owning sheet.
- **Inheritance graph**: one parent per alternate sheet. The resolver rejects missing parents, cycles, duplicate sheet IDs, and duplicate progression/grant identities deterministically across the entire character. It resolves parent to child; unrelated sheets do not share overrides.
- **Nested overrides**: omission inherits; `NativeSheetMapPatch<T>` merges individual entries by default, or replaces the complete map when `Replace` is true. `NativeSheetTrainingPatch` overrides individual `Ranks`, `Proficient`, `MiscModifier`, or `Ability` properties, or replaces an entire `NativeSkillTraining` object first. `NativeSheetValue<T>` distinguishes omitted properties from explicit null values.
- **Collections**: conditions and selected-option lists can be scoped by adding/removing entries or explicitly replacing the collection; collection edits are not authority to delete earned grants. The other supported domains are ability scores, competencies, defense contributions, current resource values, Hit Point gains and base walk speed.
- **Class levels**: `NativeCharacterRequest.Advancements` belongs to the primary character, is copied unchanged into effective mechanics, and can be updated on the authoritative primary request without rebuilding alternate sheets.
- **Sheet progression**: `NativeSheetEarnedProgression` owns its ID, canonical definition, exact source occurrence, acquisition evidence, grant references and choices. `NativeSheetEarnedGrant` has a separate durable ID. A descendant inherits the *same* progression/grant reference with its original owner; it never duplicates the authoritative acquisition. Identical definition IDs at different occurrences are not automatically merged.
- **Applicability**: inherited entitlements retain ownership when suppressed. Per-sheet `NativeSheetGrantApplicability` decisions override ancestor decisions for reachable grants. Missing decisions are **Unresolved**, not implicitly Applicable. These entries require a verified supported rule or DM decision and separate trusted-host authorization.
- **Derived projection**: `NativeCharacterSheetResolver.Resolve` returns a fresh effective `NativeCharacterRequest`, owner-qualified earned records and grant references, and issues. The result is a read-only calculation; no write, activation, change of resource ownership, Origin replacement, or grant issuance occurs.

### Illustrative graph (not approved lycanthropy rules)

```text
@primary (Origin / Class / other authoritative character mechanics)
  └── werewolf (persistent shared supernatural progression owner)
        ├── wolf (wolf-only overrides and additional progression)
        └── hybrid (hybrid-only overrides)
```

A Class level gained while all forms are inactive updates the next effective projection through `@primary`. An ability owned by `werewolf` appears under that owner in both descendants. Wolf-only progression is not inherited sideways into hybrid. Choosing to manifest either form, or applying activation-scoped resource semantics, remains a separate operation.

## Verified projection, safe applicability, and latest-Origin bridge

The follow-up increment tightens the contract around *trusted sources* and
*usable mechanics*, without defining a new transformation gameplay grammar:

- `INativeCharacterSheetAuthority.AuthorizesPrimary` now explicitly verifies
  current primary mechanics. `AuthorizesApplicability` must verify the
  **exact source-/campaign-scoped decision** before it can mark a grant
  applicable or suppressed. A supported-looking rule ID is not proof.
- `Resolve` detaches its ability/training/resource/selection input collections,
  progression choices and grant lists. Downstream consumers can not mutate
  the saved authoritative records through the result, nor can post-resolution
  changes to those inputs change an already resolved projection.
- `NativeSheetLevelReference` optionally points a verified earned grant to
  **total Class levels** or a **specific primary Class level**. The
  `NativeCharacterSheetProjector` validates the primary progression using
  the existing `NativeCharacterProjector`, then returns the resolved
  level *reference*, not a copied level owned by the alternate sheet and
  not an invented formula for a supernatural effect.
- The new projector returns only **Applicable** sheet grants in
  `UsableCapabilities`. It reports Suppressed/Unresolved grants separately
  and withholds level-bound use if native advancement cannot be verified.
  Class feature IDs and resource pools from a numerical base projection
  remain **withheld in alternate forms** pending individual authored
  applicability rules. They are never treated as form-earned grants.
  The numeric base projection remains a partial input, not a fully playable
  effective-character result.
- `NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed` provides an
  intentionally narrow, separately trusted Origin connection. The host must
  validate the complete current Origin baseline and exact walking-speed
  grant/provenance; this adapter reads the latest `Underlying` and never an
  older `Current` or `History.PreviousConfiguration`. It updates the
  *derived request*, not the authoritative Origin or alternate sheets.
  Other intrinsic/trait domains and old-ID migration are not covered.

These additions are validated by synthetic tests for denied authorization,
mutable input detachment, real native Class progression, level-bound
invalidation, suppressed capability exclusion, withheld native Class features,
and latest-Underlying walk speed after a synthetic reconstruction.

## Validation and synthetic acceptance

`tests/RulesCore.Mechanics.Tests/NativeCharacterSheetResolverTests.cs`, `NativeCharacterSheetProjectorTests.cs`, and `NativeCharacterSheetOriginBaselineAdapterTests.cs` cover persistent inactive sheets, shared and local ownership, changed Class/baseline fields, individual nested properties, whole-object/map replacement, sibling isolation, resource override isolation, suppression without revocation, missing parents, graph cycles, duplicate IDs, unknown applicability references, unauthorized acquisition/grants, canonical definition vs occurrence identity, and repeatable non-mutating reads.

Run:

```sh
dotnet restore RulesCore.slnx
dotnet build RulesCore.slnx -c Release --no-restore
dotnet test RulesCore.slnx -c Release --no-build
python3 -m unittest discover -s tests -p 'test_*.py' -v
python3 tools/audit_native_baseline.py --native data/rulesets/first-party-001/native --strict
python3 tools/validate_ruleset_split.py
```

## Explicit limitations and next increments

1. **Origin bridge**: the current resolver takes the host's trusted base mechanics rather than interpreting arbitrary `NativeOriginProfile`s. A version-aware adapter must verify canonical `origin-trait` IDs and source occurrence/grant receipts and derive applicable mechanics from the newest `Underlying`. Older serialized `MechanicalDefinitionId` data needs explicit migration; do not treat former IDs as equivalent.
2. **Progressive curse / primary-form modifications**: add separately owned persistent influence and severity/expression state, with authored stage-dependent effects applying to the primary effective projection. Earned progression survives severity regression unless authorized rules explicitly say otherwise. Do not hard-code percentage blending or universal stage order.
3. **Activation and resources**: introduce trusted manifestation-instance transitions, activation-time captures, scoped resource owner/writer IDs, revision checks and idempotent events. The current `CurrentResources` override is a read value owned by the persisted input state, **not** a command to spend, heal, or choose how two pools interact.
4. **Multiple interactions**: independent effects targeting an overlapping characteristic need source-authored interaction rules or authenticated DM adjudication. The first increment supports a verified single-parent sheet chain, not global multiple-inheritance precedence.
5. **Character Sheet integration**: define a durable relational representation for one character with multiple sheets, progression/receipt tables and optimistic concurrency. Load verified records, resolve on demand, surface unresolved capabilities separately, and preserve the pre-existing `NativeOriginProfile.Current` compatibility contract until a tested migration or adapter exists.
6. **First-party mechanics**: Werewolf/Vampire Lord progression trees, curses and resource formulas remain unauthored. Wild Shape and Polymorph are optional future consumers.

A passing first-increment test suite establishes only this **persistent nested inheritance and progression reference foundation**, not complete Character State Resolution.
