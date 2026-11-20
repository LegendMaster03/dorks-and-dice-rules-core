# Crafting mastery and external-content qualification — initial mechanics slice

**Status:** Implemented as pure mechanics and opt-in qualified manufacturing on `feature/competency-crafting-qualification-20261119`. This is not a completed crafting subsystem, an item importer, or a character-authorized API.

## Approved design

Crafting requires **expertise**, **knowledge**, and **materials** independently.

| Item rarity | Minimum mastery milestone |
| --- | --- |
| Common | Apprentice |
| Uncommon | Journeyman |
| Rare | Expert |
| Very Rare | Artisan |
| Legendary | Master |
| Artifact | Grandmaster |

A higher milestone qualifies for lower rarities, but qualification is not an automatic success, recipe grant, or material award. An instructor cannot teach a Competency to a mastery milestone greater than their own. The training evaluator enforces this upper bound only; existing advancement costs, provenance, and authorization still apply.

A fundamental method may satisfy knowledge without a unique recipe. Specialized items require their own known designs or techniques. Magical essence, provenance, equipment, facilities, DCs, timing, and actual manufacturing or enchanting outcomes remain the responsibilities of their respective recipes and crafting procedures.

**Source compatibility:** Third-party items and recipes are user-provided, not bundled or rebalanced by default. Keep their original recipe costs, materials, times, and DCs. Ordinary content editing and campaign/DM rules overrides handle setting-specific changes. Avoid publisher-specific branches of the mechanic.

## Code contract and boundaries

- `NativeCraftingQualification.Evaluate` returns `eligible`, `ineligible`, or `unresolved`, together with a minimum mastery milestone and explicit reasons. It recognizes only standard magical item rarity names. Unknown or missing rarity, tier, knowledge, and materials do not silently qualify a project. It does not decide minimum tiers for nonmagical items.
- `NativeCraftingQualification.EvaluateTraining` enforces the instructor's mastery ceiling, without awarding ranks or training by itself.
- `NativeCraftingResolver.ResolveQualifiedManufacturing` composes these qualifications with the existing manual Loot Tavern manufacturing check. Failed or unresolved qualifications produce no manufacturing check/result. It does not consume components or roll dice itself.
- Existing `ResolveManufacturing` and the authenticated manual manufacturing endpoint are preserved for compatibility and explicit playtests. **No new API route authorizes crafting or instruction.**

**Important unresolved dependency:** The native Competency profile currently stores an unbounded numeric rank count and independent proficiency flag. The conversion from earned numeric ranks to Apprentice–Grandmaster titles has **not** been specified. The named mastery tier supplied to this new helper therefore must come from an **upstream trusted/authorized advancement resolver** once milestone thresholds are approved. Do not infer `Master` from five raw numeric ranks, accept a client-asserted mastery title as entitlement, or add an unchecked public endpoint.

The existing manual manufacturing resolver is not a recipe/inventory authorizer. Future production wiring must validate character identity, owned recipes/techniques, material quantities, competent helpers, and source version before qualification; then retain the existing recipe-specific checks, outcomes, and resource handling.

## Out of scope

- No Loot Tavern item-by-item rebalancing or distributing third-party content.
- No independent crafting-only Competency advancement track or extra trade-specific rank.
- No universal material substitutions or creature-family loot from incidental travel gathering; any future commonplace travel gathering belongs to the Travel System.
- No changes to Heliana harvesting, magic essence, disenchanting, recipe schemas, exploration, UI, permissions, or material inventory storage in this slice.

## Test coverage

The new unit tests check all six rarity boundaries, higher-tier compatibility, missing recipe/material gating, unknown/invalid inputs, instructor ceilings, and short-circuiting manufacturing when requirements are not met.
