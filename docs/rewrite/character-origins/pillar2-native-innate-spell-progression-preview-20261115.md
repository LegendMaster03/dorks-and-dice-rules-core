# Pillar 2 — Native Innate Spell Progression Preview

**Date:** November 15, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft)

## Scope

`NativeOriginInnateSpellProgressionProjector` is a **read-only projection** of already manifested and authorized native Origin spell traits at a supplied **total character level**. It does not grant a spell, spend a free use, decrement a resource, open a spell slot, change a character sheet or approve a biological inheritance choice. It is also not a caster action resolver or a new spellcasting subsystem.

The native `origin-trait` definitions, not a class-specific or Species-specific switch statement, supply each spell's authored `spell`, `level`, `atWill`, `alwaysPrepared`, `slotCasting`, `freeUses`, `recharge`, `freeCastSpellLevel` and optional source-selected `spellcastingAbility` binding.

Each displayed entry retains the original **grant ID**, **native trait ID** and **native source ID**. The preview identifies whether the spell is `available` at the character level or `locked` for a future level. A selected grant commits its complete future trajectory; it does not immediately make every granted spell usable at level one. `FreeUsesPerRecharge` describes the authored **maximum entitlement**, not remaining uses.

## Implemented behavior

- **Elf and Tiefling authored courses:** Level 1 at-will spells, level 3 and 5 unlocks, one independent free Long Rest use on the authored level 3/5 spells, and separately preserved `slotCasting`. Infernal Hellish Rebuke's free casting retains `freeCastSpellLevel = 2` without rewriting actual slot casting.
- **Shared Tiefling Thaumaturgy:** Retains the verified base-Species to selected Ancestry ability through the previously implemented `descendant` binding. Each of the three leaf Ancestries retains its own spells and same-source selected ability.
- **Forest Gnome:** Minor Illusion remains at will. Speak with Animals has `alwaysPrepared = true`, `slotCasting = true`, one Long Rest recharge period and a symbolic `freeUses = "proficiency-bonus"`. The level-only preview preserves that expression but **does not invent its numeric value**. `ForCharacter` reads `proficiency.bonus` only from the matching native character projection's resolved value. Missing or unresolved proficiency leaves the result unresolved instead of substituting zero or one.
- **Level provenance:** `ForCharacter` accepts only matching native ruleset/fingerprint and resolved `advancement.total-level`. It does not calculate a separate Origin level or base spell availability on class level alone.
- **Actual grant verification:** The projector requires a completed authorized direct or scoped approved mixed construction, an unchanged underlying/current baseline with no unsupported independent/temporary modifications, an exact canonical selected trait and matching source identity/manifestation. Mixed grants require the existing verified contributor evidence and completed three-slot biological foundation. The canonical native definition and effective payload must be identical.
- **Integrity:** For native source-selected intrinsic spellcasting abilities, the original construction proposal is used for `NativeOriginContractValidator.ReviewProfile` to detect forged saved ability parameters. An altered or stripped executable spell payload is rejected. Multiple distinct effective grants for the **same spell ID** remain unresolved pending an approved rule for their interaction; no free-use stacking or accidental resource merging is introduced.

`Project` reports all committed spell entries, including still-locked future abilities, and a `PreviewOnly = true` flag. An `unresolved` view is **not** authorization to use any contained partial entries.

## Deferred cases

- **High Elf Wizard cantrip:** The selected native trait uses `spellList: "wizard"` and allows replacement after each Long Rest. Open-catalog selection, replacement provenance and per-spell authorization are not ready for this renderer; the view remains unresolved rather than choosing a cantrip or treating a list reference as a spell. The existing creation-choice checks remain intact.
- **Wood Elf speed overlap:** The base Elf has 30-foot walking speed, while its Wood Elf Ancestry has 35 feet. This is an **Origin foundation reconciliation** question, not a spell issue. Ordinary construction remains unresolved until an existing native/table relationship explicitly chooses the effective movement mode. The test verifies the spell projector cannot authorize Wood Elf spell access through that unresolved configuration. It does **not** automatically prefer the faster value or rewrite movement rules.
- **Use and recharge accounting:** No usage ledger, remaining-free-casts field, Long Rest event application, spell-slot check, at-will spell execution, shared casting-owner policy, or action/target/range resolver is implemented. The preview preserves grant ownership and maximum entitlements as groundwork for that later implementation.
- **Transient forms / independent effects:** The projector reads only an authorized effective underlying Species/Ancestry baseline. Pillar 4 state applicability and a common temporary-form evaluator must be reconciled later.
- **Production authentication:** As in the existing construction slice, self-overrides exist only for local tests. Verified campaign scope, source-profile revision and the Character Sheet site remain external integration gates.
- **Biological permission and power:** No additional traits were opted into first-party biological transmission, and no numeric allocation costs, allowance or automatic Species balance admission rule were approved.

## Tests

`tests/RulesCore.Mechanics.Tests/NativeOriginInnateSpellProgressionTests.cs` covers real native courses at total levels 1, 2, 3, 4, 5 and 10, source IDs and separate grant IDs, serialized profile round trips, protected Infernal Hellish Rebuke metadata, native Drow and other supported spells, Forest Gnome symbolic proficiency bonus, invalid levels, missing advancement/proficiency and altered or stripped trait payloads. It retains unresolved Wood Elf speed reconciliation as a regression rather than inventing movement policy.

`NativeOriginTieflingSpellBindingTests` additionally verifies the preview for a **scoped, DM-authorized** mixed descendant: only its two explicitly selected grants appear, and a cross-donor ability mismatch cannot produce a usable spell projection.

Run `dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release`, `dotnet test RulesCore.slnx -c Release`, `python3 -m unittest discover -s tests -p 'test_*.py'`, and both existing GitHub Actions workflows. Green CI covers the tested bounded preview, **not** real character spell expenditure or production Character Sheet behavior.

PR #157 remains draft. Do not merge, deploy or change `main` before owner approval.
