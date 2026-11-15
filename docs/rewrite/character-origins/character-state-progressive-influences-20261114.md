# Character State Inheritance — Increment 2: persistent progressive influences

**Date:** 2026-11-14  
**Scope:** A shared, read-only native mechanics overlay for host-verified persistent influences. This is an implementation foundation, not vampire/curse gameplay authoring, a form-activation system, or a persistence migration.

## Ownership and trust

`NativeCharacterState` now accepts an optional `PersistentInfluences` collection. This is character-level persistent state, **not a replacement for `NativeOriginProfile`**. The Origin `Underlying` baseline, Origin `Current` compatibility state, Class advancement, and alternate sheet ownership remain separate and authoritative.

Each `NativeCharacterPersistentInfluence` has a character-scoped identity, canonical definition ID, versioned source, acquisition receipt, independently authenticated expression receipt, expression dimensions, effect declarations, locally acquired progression records, and current-use decisions. It can exist without any currently active effects. Receipt strings, source IDs, authored rules, and claimed DM decisions are **not** proof of entitlement. An `INativeCharacterInfluenceAuthority` implementation must verify all of them against current saved character/campaign state. It also implements the original `INativeCharacterSheetAuthority` for primary and alternate-sheet verification; no allow-all production implementation is included.

Earned influence progression uses existing `NativeSheetEarnedProgression` and `NativeSheetEarnedGrant` records and exact source occurrences, but owns them under an **influence identity** rather than duplicating them in every alternate sheet. `NativeInfluenceResolvedProgression` and `NativeInfluenceResolvedGrant` preserve that owner, progression ID, canonical grant definition, source occurrence, and evidence. IDs are checked against other influences and existing alternate sheets to prevent duplicate grant ownership.

## Current expression versus progression

`Expression` is a map of exact, source-authored dimensions. Each effect or use decision declares its own `RequiredExpression` map: **all** named dimensions must match for that declaration to be considered. A source can thereby express discrete stages, independent manifestations, conditions, branching or reversible progression without an engine-defined integer level, severity percentage, or interpolated transition schedule.

Increasing expression does not automatically award grants. Decreasing expression does not remove a progression record. A separately authenticated `NativeInfluenceGrantApplicability` may mark a previously earned grant `Applicable` or `Suppressed` under a particular expression. No matching decision is `Unresolved`; multiple simultaneously matching decisions for the same grant are also unresolved. There is no rule that earned benefits disappear when a curse improves or returns.

## Derived characteristic modifications

`NativeCharacterInfluenceResolver.Resolve` first uses Increment 1's `NativeCharacterSheetResolver` and its mandatory authorization checks. It then evaluates the verified influence declarations against the selected sheet's latest **effective** mechanics. This order does not modify the saved primary, overwrite the Origin profile, activate a form, or rewrite any persistent sheet.

The increment supports a deliberately finite typed subset:

| Characteristic | Supported operations |
| --- | --- |
| Ability score, defense contribution, Hit Point gain | Replace numeric value or adjust the latest inherited numeric value |
| Skill/competency ranks and miscellaneous modifier | Replace numeric property or adjust its inherited value |
| Skill/competency proficiency | Replace Boolean property |
| Skill/competency ability association | Replace value or explicitly suppress binding |
| Base walking speed | Replace, adjust, or explicitly suppress optional value |
| Condition ID | Add or suppress in the derived collection |

Nested training properties reuse `NativeCharacterSheetResolver.Apply` and `NativeSheetTrainingPatch`. Omitted sibling properties keep their inherited values. An adjustment to a missing numeric property is **unresolved**, not silently computed from zero. Checked integer arithmetic rejects overflow as unresolved. There is **no** arbitrary content scripting, universal creature-statblock copying, or current-resource quantity edit. Altered Hit Point gain inputs do not refill current Hit Points or make any persistent write.

Effects declare exact `TargetSheetIds`. `@primary` is the normal effective character; a persistent alternate sheet must be explicitly named to receive an effect. Merely naming `@primary` never universally applies an influence to every possible alternate form. A targeted alternate sheet evaluates its own latest inherited mechanics, then the influence. Changes to the authoritative primary continue to flow through ordinary alternate-sheet inheritance; influence presence is not required for this behavior.

Two simultaneously active effects on the **same logical characteristic property** have no presumed stacking order. The evaluator returns `unresolved` with no effective mechanics rather than choosing input order. It also reports overlap between a targeted influence and an explicit override from the selected alternate sheet or one of its ancestors; a source-defined interaction or verified DM ruling requires a later contract and does not silently change this behavior. Disjoint nested-property changes remain compatible.

## Verification and failure boundaries

The resolver runs the existing character/sheet authorization and validates all influence records, including dormant ones. It rejects malformed version/source information, absent or duplicate durable IDs, invalid expression gates, unsupported typed effect operations, unknown form targets, unknown earned grants, missing evidence, and duplicate advancement/grant ownership. Authorization callbacks verify acquisition, expression history, effect declarations and their targets, independently earned progressions and grants, and scoped current-use decisions. Negative results fail closed.

Results are fresh read-only derived calculations. The resolver never executes acquisition, reversion, activation, award, suspension, current-quantity restoration, or authoritative resource mutation. Returned effect target lists and expression gates, and progression grant/choice collections, are detached from host-supplied mutable collections. The existing character-state snapshot helper also detaches the new influence collections. Host integrations must provide revision-current authorized state and handle concurrent writes transactionally.

Processing bounds limit one character to 512 influences, 16,384 effect declarations, 4,096 influence progression records, 16,384 influence grants and 16,384 use decisions. These are evaluator safeguards, not supernatural gameplay limits.

## Synthetic acceptance and local commands

`tests/RulesCore.Mechanics.Tests/NativeCharacterInfluenceResolverTests.cs` provides synthetic normal-form vampire and reversible-curse cases, independent expression dimensions, progression retention, suppressed/reinstated use, inheritance and alternate-form isolation, compatible and conflicting effects, explicit alternate-target behavior, authorization denials, malformed records, duplicate ownership, repeated read-only resolution, and unchanged current resource quantities.

Run in a .NET 10 environment:

```sh
dotnet restore RulesCore.slnx
dotnet build RulesCore.slnx -c Release --no-restore
dotnet test RulesCore.slnx -c Release --no-build
python3 -m unittest discover -s tests -p 'test_*.py' -v
python3 tools/audit_native_baseline.py --native data/rulesets/first-party-001/native --strict
python3 tools/validate_ruleset_split.py
```

## Explicitly unsupported and next integration

1. **Authoritative transitions:** Acquisition, expression mutation, earned progression awarding/revocation and activation/reversion transactions need separately authorized write operations and durable optimistic concurrency. No public mutation endpoint or production Character Sheet schema is added.
2. **General interactions:** An explicit source/DM interaction contract is needed to resolve overlapping writers; this slice always withholds overlapping mechanics. Explicit local form overrides likewise remain authoritative *inputs* without assuming precedence against a targeted influence.
3. **Full-character projection:** The native sheet projector still withholds form applicability for Class features, current resources, form-specific HP policy and manifestation. Influence-grant applicability is independently authenticated but does **not** itself turn a source reference into a fully numerical/usable feature: level references, prerequisites, native Class projection and applicability to equipment and other subsystems remain for later integration.
4. **Authoring:** Actual first-party vampire/curse progressions, balance, transmissions, triggers, and form statistics are not authored. No universal stages, severity scale, transformation hierarchy or implicit monster conversion is introduced.
5. **Origin bridge:** Origin `PersistentInfluences` may supply references to the host's verified character-level influence state, but Origin cannot acquire ownership of Class, resource, or all supernatural mechanics. The existing `Underlying`/`Current` contracts remain unchanged.
6. **Future consumers:** Wild Shape and Polymorph are optional. Permanent reconstruction and Rite reversal remain separate source-authorized operations.

**Recommended Increment 3:** Define an authenticated state-transition and manifestation-instance contract with revision checks, per-resource ownership/policy, explicit overlap resolution, and a full effective-character projection boundary; then integrate one representative first-party transformation without introducing a mandatory global progression schedule.
