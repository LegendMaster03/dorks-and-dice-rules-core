# Pillar 4 — first executable effective-character slice

**Date:** 2026-11-12  
**Branch:** `feature/pillar4-effective-character-slice-20261112`  
**Base:** `feature/pillar2-characteristic-inventory-20261111` (currently includes Pillar 2 Phases 1 and 2; not `main`)  
**Status:** Experimental, restricted implementation. **No supernatural progression or monster-derived player-character grants are approved.**

Design evidence remains on `feature/pillar4-supernatural-nature-design-20261111`:
- [Shared effect/replacement-profile specification](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/pillar4-supernatural-nature-design-20261111/docs/rewrite/character-origins/pillar4-shared-effect-and-replacement-profiles-20261112.md)
- [Four-case acceptance fixture](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/pillar4-supernatural-nature-design-20261111/docs/rewrite/character-origins/pillar4-four-case-profile-acceptance-20261112.json)

## What this code does

`src/RulesCore.Mechanics/NativeEffectiveCharacterEffects.cs` defines a narrowly scoped *post-Origin* evaluator, accepting a completed `NativeOriginConstructionResult` and requiring a non-null `INativeEffectiveEffectAuthority` supplied by the trusted caller. It cannot mint entitlements or change Class levels, inventory ownership, Origin history, or resources.

Supported directives in this first slice:
- **Add** an explicitly authorized numeric modifier to an existing ability score.
- **Replace** an explicitly authorized numeric effective ability score.
- **Suspend** use of an earned **independent grant**, without removing its entitlement or any stored state.

An effect identifies the specific **earned SourceGrantId** and rule, the subsystem target, and an optional `RequiredFormId`. A form-scoped directive only applies while that exact form is active. A source must be an applicable earned independent grant or an actual current-form grant; the named supernatural influence by itself cannot award powers.

The output records current effective Species/Ancestry from the **existing Pillar 2 `Current`** configuration, effective ability scores, and grant applicability. The authoritative `Underlying` remains untouched. Temporary form expiry is represented by the next correctly reconstructed Origin state; this evaluator does not execute reversion.

**No implicit stacking:** Multiple active directives on the same target are reported as unresolved, rather than added automatically. Out-of-range arithmetic, missing source grant, incorrect authorizer, missing applicability, concurrent temporary forms without a precedence executor, or unresolved Origin construction produce no playable effective output.

## Security and trust limitations

`INativeEffectiveEffectAuthority` is **an integration interface, not a built-in authorization service**. A host must implement actual authenticated permission checks and verify selected native rule references, source/grant identity, form transitions, and applicability. The evaluator verifies basic source occurrence presence, effect shape, and the authority callback; it cannot prove the callback is trustworthy. The tests use a tightly scoped *synthetic fixture authority* only.

A caller must not expose this new evaluator with an unconditional-true authorization adapter or treat client-provided `NativeOriginConstructionResult.Status = Authorized` as proof that its grants were constructed by the real engine. The eventual host must pass a verified result from `NativeOriginConstructionEngine`; the host does not yet provide that integration. This is **not a public Rules Core API**.

## What is still outside this slice

- Actual supernatural source acquisition, Bloodline advancement, vampire weaknesses, and authored form-definition loading.
- Form activation, reversion, precedence resolution, and permanent reconstruction execution; Pillar 2 contracts represent these but their actual transitions remain deferred.
- Full character projection: the new output does not yet recalculate attack rolls, saves, skill totals, defenses, movement, equipment usability, spellcasting, actions, HP, concentration, or resource maxima. Effective ability scores must subsequently feed the existing `NativeCharacterProjector` via the authorized host.
- Entitlement/application reconciliation for non-Origin sources that do not yet have verified grant identity and applicability; no grant is assumed usable merely because it exists.
- All four full case progressions: Draconic Bloodline, full vampire, lycanthropy and ceremorphosis. The tests are contrast cases and do not establish their actual numerical benefits.

## Regression coverage and next gate

`tests/RulesCore.Mechanics.Tests/NativeEffectiveCharacterEffectsTests.cs` exercises a hypothetical Draconic Bloodline persistent modifier, wolf-form Class-grant suppression, reversion to an updated baseline, untrusted origin/form/source rejection, duplicate-target uncertainty, missing applicability, unsupported resource-free arithmetic and invalid Origin status. The fixture **does not** grant an actual playable Bloodline or werewolf.

Next gate: demonstrate this slice on a genuinely verified constructed Origin with a real authenticated rule/authority adapter and a native-authored effect. Only then begin integrating the effective values into the character projector. Keep grant acquisition and gameplay authorization strictly in shared Rules Core rather than Character Sheet.

No `main` merge or native ruleset change is authorized here.
