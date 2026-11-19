# Pillar 2 — Native Human Skill-Rank Entitlement Projection

**Date:** November 15, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft)

## Scope and existing native definition

Native `trait.human.adaptable-learning`, defined by `data/rulesets/first-party-001/native/traits/human/species.human.json`, already supplies two numerical effects:

- Four additional Skill ranks to allocate at **character level 1**.
- One additional Skill rank at **every subsequent character level**.

The benefit remains `allocation: "player-choice"` and `obeysNormalSkillRankLimits: true`. These are **entitlements**, not automatic points allocated to any Skill, not additional proficiency and not Origin Feat permissions. The original native Human Species additionally grants Skillful (one creation-time Skill proficiency) and Versatile (an Origin Feat choice). Those remain distinct and cumulative. There is no mechanical authority to spend extra ranks until the ordinary rank allocation limits, per-level rank history and character-facing integration are implemented.

## Implementation

`src/RulesCore.Mechanics/NativeOriginSkillRankEntitlementProjector.cs` is a read-only, source-neutral interpreter of canonical `origin-trait` rank progression fields. It does not assume that every inherited trait is automatically biologically eligible.

For an **authorized completed Origin** with the selected and verified Adaptable Learning effective grant, at an externally validated total character level `L ≥ 1`:

```text
available additional rank entitlement through level L = 4 + (L - 1)
new entitlement at level 1                      = 4
new entitlement at each later level             = 1
```

The interpreter reads the actual catalog definition, checks the original selected candidate and matching effective grant, preserves original source/provenance and rejects altered/stripped executable trait fields. It accepts a legitimate JSON round trip, but **never** authenticates a user-supplied profile. For mixed biological construction it requires the existing approved mixed foundation and exact authorized manifested contributor evidence, with a DM decision when the native trait is not first-party opted in. Multiple contributors of the identical native trait consolidate into **one** entitlement schedule; multiple distinct rank schedules remain unresolved pending a gameplay rule, rather than being silently stacked.

`ForCharacter` accepts a `NativeCharacterProjection` and derives the level only from its resolved `advancement.total-level`, requiring an identical ruleset and fingerprint. The return record is `PreviewOnly: true`, with total entitlement and new-per-level entitlement, and contains **no** assumed allocation, expenditure, unspent balance, rank cap or character mutation.

The direct native construction process authenticates an automatically selected ordinary trait with its **concrete Species/Ancestry source ID**, while a mixed biological selection carries the creature-origination rule reference or a verified DM ruling. The projector checks the correct source-specific grant authorization and does not accept one in place of the other.

## Native tests

`tests/RulesCore.Mechanics.Tests/NativeOriginSkillRankEntitlementTests.cs` checks the real native Human catalog progression at levels 1 (4 ranks), 2 (5), 5 (8), 10 (13) and 17 (20), with the correct new rank count per level. It checks serialized profiles, a deliberately unselected Human whose required Origin Feat is unresolved, a DM-scoped mixed Elf + Human descendant, multiple same-canonical Human contributor offers, a direct Elf without any Human rank entitlement, invalid level inputs and forged or stripped executable fields.

**Test-only Human prerequisite:** A completed direct Human is constructed with an in-process `INativeOriginAuthority` authorizing a synthetic Origin Feat ID and an exact native Skill ID. This authorizes **only the test** so the rank trajectory can be exercised against a finished conventional Origin. It is not an implemented first-party Origin Feat catalog and does not make arbitrary feats eligible in production. Under the actual first-party catalog, Human creation still remains incomplete without an approved Origin Feat choice.

## Remaining integration and policy

1. Native, authoritative per-level skill rank allocation history, normal rank caps, edits/retraining, and validation of spend vs. distinct sources.
2. Approved production Origin Feat eligibility, including the Human Versatile selection, and complete Skillful downstream Skill proficiency consumption.
3. Additional source-neutral Character Origins Skill bonuses and composite Skill interactions (Elf Keen Senses, Lightfoot Natural Agility).
4. Biological opt-in review for Adaptable Learning: its presence in a native Human donor **does not** automatically transmit it.
5. Production character/profile revision authentication and Character Sheet integration. The projector is explicitly **read-only** and never modifies character state.
6. Innate spells, movement modes and other unreviewed characteristic dependencies; final allocation capacity/price decisions remain open.

No native rules, price budgets, normal rank limits, or Character Sheet site files are modified. PR #157 remains draft and unmerged.
