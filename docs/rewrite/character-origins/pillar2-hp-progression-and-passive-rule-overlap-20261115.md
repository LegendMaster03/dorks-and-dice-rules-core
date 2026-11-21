# Pillar 2 — Native HP Progression and Passive-Rule Overlap

**Date:** November 15, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Working branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft; no merge/deployment)

## Context and boundary

The universal Origin construction and authenticated mixed-descendant increment are already implemented. This increment covers two concrete residual characteristic relationships without approving new gameplay capacity limits or the first-party inheritance eligibility of additional Species packages.

Dwarven Toughness is defined at `data/rulesets/first-party-001/native/origination/biological/traits/dwarf/species.dwarf.json` as `hitPointsPerCharacterLevel: 1`. It is a complete, native level-based mechanic, but lacked an Origin-specific downstream projection. Elf and Tiefling Low-Light Vision have separate canonical trait IDs but each references `rule.low-light-vision`; the existing passive projector rejected duplicate effective grants even though the reconciler did not necessarily require a pregrant disposition.

## Changes

1. **Read-only per-level HP interpreter.** `NativeOriginHitPointProgressionProjector.Project` checks the already authorized current underlying Origin, catalog canonical trait and original selected candidate evidence. It rejects altered trait payloads, missing level, invalid level and more than one distinct effective HP progression. A native trait's per-character-level rate is multiplied by the actual requested total character level, with overflow handling. The trait's entire level trajectory is committed by the selected grant; no extra level-up choices or copies of a resource are created.
2. **Native character HP composition preview.** `Combine` uses the existing native `NativeCharacterProjection.Values["advancement.total-level"]` and `["hit-points.maximum"]`. It requires matching ruleset identity/fingerprint and fully resolved values. The baseline incorporates the actual class Hit Point Gains and Constitution modifier. The Origin maximum-HP bonus is added once as a read-only **preview**, without mutating `NativeCharacterRequest` or character storage. It does not define NPC/monster Hit Die behavior, active-form HP retention, or current HP adjustments. The host must independently authenticate the actual character and Origin revisions before adopting any value.
3. **Direct Species serialization.** `NativeOriginIntrinsicProjector` now checks the same normalized configuration equivalence used by the contract validator. The old reference identity requirement incorrectly rejected valid persisted conventional Species baselines. Eligibility and grant-provenance checks remain unchanged.
4. **Source-neutral passive-rule overlap.** `NativeOriginReconciler.NativePassiveRule` recognizes only complete canonical traits whose substantive mechanics are a native `rule` reference, not traits with independent spells, resources, resistances, durations or other nonpresentation fields. Where distinct canonicals reference one such rule, an actual selected pair requires an explicit relationship disposition before becoming effective. Unselected offers never conflict, and distinct canonical ID or similar display text alone is insufficient.
5. **Eligibility remains separate.** The native Tiefling Low-Light Vision offer from its restricted family node does not gain first-party `inheritanceMethods: ["biological"]`. Cross-definition tests use exact trusted DM eligibility for this offer, and exact DM relationship authorization where selecting both. Verified RetainBoth is represented as two grants, but the existing passive vision projector refuses to claim two identical effects are one supported executable view.

## Tests and expected results

`tests/RulesCore.Mechanics.Tests/NativeOriginHitPointProgressionTests.cs` covers native direct Dwarf at levels 1/3/5/17; for each level, with six rolled/recorded Class HP gain points and Constitution +2 per level, the baseline native maximum HP is 8 × level and the Dwarf bonus is +1 × level, totaling 9 × level. A mixed Dwarf test uses explicit scoped DM eligibility and verifies the same trajectory; two selected same-canonical Dwarf donors consolidate to one effective trait, not two +1 bonuses. Missing Hit Point Gains, invalid levels, failed Origin authorization, forged native trait payloads and JSON round trips are tested.

`tests/RulesCore.Mechanics.Tests/NativeOriginPassiveRuleOverlapTests.cs` uses actual playable Elf/Infernal Tiefling donor chains and their native canonical traits, not synthetic trait names. It verifies the unresolved overlap, each scoped single-source disposition, one-source construction without additional rulings, rejection of a fabricated native `Supported` relationship and limited behavior of scoped RetainBoth. The currently restricted source is verified explicitly.

## Remaining work

- Production Character Sheet HP effect and current HP/application semantics; current adapter/projector changes are read-only.
- Additional source-bound skill/Competency rank progression, innate spellcasting and choice inheritance, movement modes, Dwarf/Tiefling conditional mechanics, Goliath upgrades and source-neutral exclusions.
- Full biological characteristic eligibility review. This increment deliberately does not authorize Dwarven Toughness or Tiefling Low-Light Vision for ordinary biological mixing.
- Owner-approved capacity/allocation safeguards and actual production authenticated profile lookup. The allocation preview is not an admission rule.
- End-to-end tests beyond the supported native character and Origin projection subset, including Character Sheet integration.

## Validation

Run from repository root:

```sh
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release --filter 'FullyQualifiedName~NativeOriginHitPointProgressionTests|FullyQualifiedName~NativeOriginPassiveRuleOverlapTests'
dotnet test RulesCore.slnx -c Release
python3 -m unittest discover -s tests -p 'test_*.py'
```

Require both GitHub workflows: `Validate Rules Core Rewrite Structure` and `Rules Core Phase 3B Public Native API CI`. These cover the native C#, Python, structure and Docker/HTTP checks, but do not imply overall gameplay power calibration or production Character Sheet behavior.

No gameplay policy requiring owner approval was silently enacted. Keep PR #157 draft and do not merge or deploy.
