# Pillar 4 — guarded effective-character numeric projection bridge

**Date:** 2026-11-12  
**Branch:** `feature/pillar4-effective-projection-bridge-20261112`  
**Base:** `feature/pillar4-effective-character-slice-20261112` (draft PR #146, itself based on Pillar 2's development branch). No main merge.

## Purpose

The first Pillar 4 effective-character slice can calculate authorized ability-score changes, but its result was not consumed by the native Character projector. This bridge connects the two **without** implying that complete form, Class, equipment, spellcasting or resource interactions are already executable.

`NativeEffectiveCharacterProjectionBridge`:
1. Requires a trusted, exact character-input authorization boundary (`INativeEffectiveCharacterInputAuthority`) in addition to the existing source/effect/form authorization boundary (`INativeEffectiveEffectAuthority`). Neither is implemented by the current host. Test doubles do not certify player entitlement.
2. Requires the unmodified character scores to match the scores supplied for effect evaluation, and uses the **same selected catalog** as the existing Character projector. The catalog fingerprint equality is inherited from the current native character request contract; this does not introduce ruleset-fingerprint-based authoring approval or a new deployment gate.
3. Calls `NativeEffectiveCharacterEffects.Evaluate` to resolve approved source-qualified numeric directives before applying the resulting ability-score dictionary to `NativeCharacterProjector.Project`.
4. Returns **only a numeric value map** as `calculated-subset` when the existing projector fully resolves. It does **not** expose its Class feature grants, Resource entitlements, or any independent claim of a playable character.
5. Fails closed without numerical output if the Origin or character input is untrusted, if effective state is an active temporary form or differs mechanically from the underlying Species/Ancestry, if a Class/other independent grant is suppressed, or if native projection reports unresolved or invalid derived mechanics. The absence of a calculated subset is not a direction to display unmodified scores as if authorized.

This remains intentionally narrower than the three approved transformation categories. It is one technical milestone for **Type 1 modification-only numeric effects**, not an implementation of actual Draconic Bloodline content or of a supernatural source/progression.

## Explicit unimplemented dependencies

- A production trusted character-input adapter that authenticates campaign ownership, Class advancements and previous independent grants.
- A native-catalog-driven effect authorizer that verifies a specific rule's operation, target, source and value, or an authenticated DM/table override. The earlier slice accepts the verification through a host-supplied interface; merely naming a rule is insufficient.
- End-to-end creation of authenticated `NativeOriginConstructionResult` by `NativeOriginConstructionEngine`, combined with verified previously earned independent grants.
- Temporary form transitions and cross-domain effective profiles for creature type, Class feature suspension, spellcasting, physical equipment, actions, HP/current health, conditions, and resources.
- Native authoring and actual progression/balance for full vampire, lycanthropy, ceremorphosis and Draconic Bloodline.
- Character Sheet/API integration; do not expose this bridge with permissive authorizers.

## Verification scope

Synthetic tests exercise numeric modifier derivation, no mutation of the underlying character scores, unverified input/references, absent independent entitlement, mismatched baseline, catalog mismatch, suppression and alternate form failure, and projector incompleteness. This does not prove actual first-party supernatural mechanics.

The shared architectural specification and four-case fixture remain on the independent Pillar 4 design branch:
- [Design specification](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/pillar4-supernatural-nature-design-20261111/docs/rewrite/character-origins/pillar4-shared-effect-and-replacement-profiles-20261112.md)
- [Four primary tests](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/pillar4-supernatural-nature-design-20261111/docs/rewrite/character-origins/pillar4-four-case-profile-acceptance-20261112.json)

No native ruleset, API, or deployed Character Sheet file was changed.
