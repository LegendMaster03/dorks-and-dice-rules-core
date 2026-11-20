# Crafting rank and external-content qualification — initial mechanics slice

**Status:** Implemented as pure mechanics and opt-in qualified manufacturing on `feature/competency-crafting-qualification-20261119`. This is not a completed crafting subsystem, an item importer, or a character-authorized API.

## Approved design

Crafting requires **expertise**, **knowledge**, and **materials** independently.

| Item rarity | Minimum Competency rank |
| --- | --- |
| Common | 1 — Apprentice |
| Uncommon | 2 — Journeyman |
| Rare | 3 — Expert |
| Very Rare | 4 — Artisan |
| Legendary | 5 — Master |
| Artifact | 6 — Grandmaster |

**Rank 0 is Untrained.** A higher Competency rank qualifies for lower rarities, but qualification is not an automatic success, recipe grant, or material award. An instructor cannot teach a Competency to a rank greater than their own. The training evaluator enforces this upper bound only; existing advancement costs, provenance, and authorization still apply.

A fundamental method may satisfy knowledge without a unique recipe. Specialized items require their own known designs or techniques. Magical essence, provenance, equipment, facilities, DCs, timing, and actual manufacturing or enchanting outcomes remain the responsibilities of their respective recipes and crafting procedures.

**Source compatibility:** Third-party items and recipes are user-provided, not bundled or rebalanced by default. Keep their original recipe costs, materials, times, and DCs. Ordinary content editing and campaign/DM rules overrides handle setting-specific changes. Avoid publisher-specific branches of the mechanic.

## Code contract and boundaries

- `NativeCraftingQualification.Evaluate` returns `eligible`, `ineligible`, or `unresolved`, together with a minimum Competency rank and explicit reasons. It recognizes only standard magical item rarity names. Unknown or missing rarity, rank, knowledge, and materials do not silently qualify a project. It does not decide minimum tiers for nonmagical items.
- `NativeCraftingQualification.EvaluateTraining` enforces the instructor's Competency-rank ceiling, without awarding ranks or training by itself.
- `NativeCraftingResolver.ResolveQualifiedManufacturing` composes these qualifications with the existing manual Loot Tavern manufacturing check. Failed or unresolved qualifications produce no manufacturing check/result. It does not consume components or roll dice itself.
- Existing `ResolveManufacturing` and the authenticated manual manufacturing endpoint are preserved for compatibility and explicit playtests. **No new API route authorizes crafting or instruction.**

**Rank mapping:** Numeric Competency ranks are the ranks used by this qualification gate: 0 Untrained, 1 Apprentice, 2 Journeyman, 3 Expert, 4 Artisan, 5 Master, and 6 Grandmaster. There is no separate mastery-tier state or conversion step. An existing accumulated rank above 6 retains Grandmaster crafting capability without imposing a new cap on grant aggregation. The independent proficiency flag remains unchanged.

**Authority boundary:** The helper assumes a verified rank supplied by a trusted/authorized character workflow. It does not authorize client-supplied ranks, recipe knowledge, or inventory. The final advancement costs and maximum gameplay rank remain independent balance decisions; do not add an unchecked public endpoint.

The existing manual manufacturing resolver is not a recipe/inventory authorizer. Future production wiring must validate character identity, owned recipes/techniques, material quantities, competent helpers, and source version before qualification; then retain the existing recipe-specific checks, outcomes, and resource handling.

## Out of scope

- No Loot Tavern item-by-item rebalancing or distributing third-party content.
- No independent crafting-only Competency advancement track or extra trade-specific rank.
- No universal material substitutions or creature-family loot from incidental travel gathering; any future commonplace travel gathering belongs to the Travel System.
- No replacement or modification of Heliana's existing harvesting, essence, item-specific salvage or enchanting procedures, and no changes to third-party recipes, exploration, UI, permissions, or inventory storage in this slice.

## Test coverage

The new unit tests check all six rarity boundaries and rank 0 Untrained, higher-tier compatibility, missing recipe/material gating, unknown/invalid inputs, instructor rank ceilings, and short-circuiting manufacturing when requirements are not met.

## Warcraft-derived recipe acquisition, training, and specializations

- A recipe can be learned by advancement (fundamental recipes only), instruction, research, discovery, treasure, quest rewards, renown rewards, or purchase. These are **acquisition pathways**, not item grants or automatic permissions to import third-party content.
- Advanced Competency ranks alone do not provide specialized recipes. A learner must have the recipe's required numeric Competency rank and a verified learning source. A trainer must know the recipe and have at least the recipe's required rank; the existing professional-training rule separately prohibits an NPC from teaching a Competency rank higher than their own.
- Specialized traditions can be represented by source-defined recipe requirement identifiers and verified character knowledge. They **do not** add new Competency progression tracks, imply mutually exclusive traditions, or introduce deferred rank-granted traits.
- `NativeRecipeLearning.Evaluate` and the optional specialization check on `NativeCraftingQualification.Evaluate` are pure eligibility operations. They do not create, save, consume, grant, or authorize content. Their caller must validate claimed ranks, sources, recipes, and specialization knowledge.

## Material processing and collaborative stages

`NativeCraftingProcess.Evaluate` accepts source-defined stages with method Competency IDs, required ranks, declared input/output materials, prerequisites, verified completed dependencies, and facility/knowledge/material readiness. This supports refining, smelting, alloying, manufacturing intermediate components, and collaborative multistage construction. Stage order and cycles are validated; independent workers can supply their own earned Competency ranks. The result is a read-only plan, not an inventory transaction. Intermediate processing may remain an internal part of a single recipe rather than requiring additional rolls, costs, or downtime.

## Salvaging and disenchanting

`NativeCraftingRecovery.Evaluate` supports generic source-defined salvaging and a Warcraft-style **ordinary disenchanting** eligibility policy: Common requires rank 2; Uncommon 3; Rare 4; Very Rare 5. This ordinary method requires a permanent magic item not attuned to someone else; Legendary and Artifact items are excluded. An explicitly authored exceptional rule/recipe can permit different recovery without silently broadening ordinary disenchanting. Outputs and losses are always defined by the active recipe/source: the engine never creates a generic residue value, duplicates materials, or replaces Heliana's harvesting/salvaging outcomes. Actual ownership, inventory and resource transfer must be verified elsewhere.

## Archaeology and relic restoration

`NativeRelicReconstruction.Evaluate` checks source-verified fragment counts and provenance, a qualified historical method, required knowledge and facilities, and the existing rarity-to-Competency-rank gate for magical results. Warcraft's six matching fragments are a configurable default, not an assumption that all relics in every setting require six fragments. This operation evaluates **whether reconstruction may be attempted**. It neither gathers fragments in travel, selects an arbitrary magic item, grants an item, nor bypasses the source-defined restoration check.

## Implementation scope and future wiring

These are typed, pure mechanics with unit tests, not character-owned inventory, recipe persistence, NPC training UI, or authenticated API endpoints. No first-party or third-party item catalogs are rewritten. No rank traits, individual profession advancement tracks, new travel gathering, or universal material substitutions are introduced. Production integration must resolve the user's authorized source recipe, character training, known designs, participating characters, facility, and actual inventory before executing item-specific procedures.

## Explicit archaeology dig-site activity

`NativeArchaeologyExcavation.Evaluate` supports a deliberately designated dig site (not a travel-stage gather). It accepts a verified historical-method qualification, a source-defined DC, one hour of excavation, and caller-supplied checks/dice. Normal success yields a caller-rolled 1d6 fragment count, a margin of 5 or more indicates a completed relic, and a natural 20 indicates a magical fragment, following the Warcraft-inspired exceptional result. The DM determines the site's provenance and resulting relic; the output is advisory and creates **no** inventory resource on its own. This does not change the existing monster-family harvesting mechanics.

## Relic assembly and retry handling

`NativeRelicAssembly.Resolve` completes the Warcraft-inspired assembly procedure without mutating inventory: an ordinary relic needs six matching fragments (or its source-defined requirement) and a History check against DC 15 by default; failure blocks another attempt until an additional matching fragment has been acquired. Six matching magic fragments use the existing item-rarity qualification rather than an extra History assembly check. The caller must verify fragments collected after a failed attempt and authorize any resulting item. No character-owned items, fragment quantities, or failed-attempt histories are generated by this pure resolver.
