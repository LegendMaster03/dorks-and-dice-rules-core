# Pillar 2 Construct Assembly — candidate first-party recipe corpus

**Working branch:** `feature/construct-recipe-derived-starting-cost-20261120`

**Status:** Draft first-party authoring; do not advertise these recipes, power numbers, or materials as balanced playable character options. The first-party Construct Assembly method selects the native `recipe-inputs` cost path, but intentionally has **no approved starting allowance or unit-price schedule**. The Origin service must therefore remain unresolved for an unapproved first-party starting Construct.

## Corpus coverage

- 31 existing component definitions linked to source-specific native manufacturing recipes.
- 4 designs and 5 enhancements linked to recipes for their physical production.
- 6 existing power-source mechanisms linked to recipes, with provisional simultaneous-output figures.
- 8 existing materials described with qualitative physical profiles; density, weight, resilience, and damage interaction are not yet mechanically authorized.
- 8 candidate supplemental crafting inputs needed for source recipes, including one intermediate alchemical product. These are ingredient definitions, **not** new Construct Assembly components.
- 46 exact-output recipes, all referencing existing native competency IDs. The alchemical actuator illustrates a two-stage recipe whose intermediate is not charged twice.

## Rules and authoring boundaries

1. Every installed component retains its own required material slot. The recipe consumes that slot's actual selected material, rather than charging for a hardcoded default. Multiple instances repeat the recipe.
2. Designs, enhancements, and power sources contribute their own required manufacturing inputs when selected; their older independent `constructionCost` values are not charged again.
3. Every recipe carries a required competency and minimum rank. These are **candidate authoring values**, separate from qualified creator identity; a historical NPC creator is not a player-owned progression track.
4. The starting-character assembly may quote ingredients without replaying historical manufacturing checks. The ordinary crafting procedures govern actual manufacturing and modification during gameplay.
5. Source-specific power output and demand are provisional. A finite-powered source's output remains subject to its actual availability and any applicable sustenance/energy rules; a numeric `powerOutput` does not grant unlimited fuel.
6. A component's physical installation, the availability of a power source, and a canonical Origin characteristic are different facts. The current corpus does **not** invent automatic PC immunities or magic animation.
7. Material `physicalProfile` values are qualitative design notes. They are **not** game effects until paired with general weight, equipment, durability, movement, and other approved rules. Steel-heavy builds are not automatically penalized by this authoring alone.
8. No fixed body layout, mandatory frame, arbitrary maximum installed attachments, or post-creation construction-point ceiling is introduced.

## Remaining work before first-party authorization

- Resolve size as a result of the actual assembly and its relevant parts, rather than a free, unrelated choice. Do not impose a mandatory universal frame.
- Add canonical characteristic mappings for supported sensing, manipulation, locomotion, protection, and other effective physical functions, with dependencies and operating applicability as appropriate.
- Make mass and other material interactions observable through existing universal equipment/encumbrance and combat rules; avoid special Construct-only encumbrance.
- Compare complete builds in concrete gameplay contexts, then set supported physical properties, recipe quantities, power costs, material resource valuations and an initial allowance. Retain the distinction between resources purchased and actual mechanical effectiveness.
- Run the aggregate validation suite and sample first-party assembly fixtures **after** the content/integration pass, rather than treating individual catalog edits as independent approval gates.

## Candidate pricing and minimal functional trait sources

The native method now includes **provisional resource-unit values** for quotation, but intentionally omits `initialConstructionAllowance`. The reference file `data/reference/construct-assembly-balance-candidates-20261120.json` contains illustrative assemblies and a proposed allowance of 220 **for analysis only**. These are not approved prices or playable starting character entitlements.

The method explicitly authors the Construct creature-type intrinsic. This is classification, not the monster stat-block package. Size remains unresolved until supported by physical construction. Optical sight and physical manipulation now have source-bound canonical trait definitions attached to their corresponding operational parts. Neither effect grants a numeric bonus, extra action, magic sense, or weapon proficiency; power or dependency failure can suppress operational applicability. Other senses, movement speeds, structural protection, and weight still require mechanical authoring and balancing.

## Consolidated integration regression scope

`NativeFirstPartyConstructCraftingTests` exercises the actual first-party native catalog (not only synthetic fixtures) for source-bound slot quantities, substituted materials, intermediate-stage offsetting, physical presence while disabled, optical power gating, nonhumanoid construction, and the deliberate refusal to authorize a starting character without an approved allowance. These tests are intended to be run with the other Rules Core suites **after** the content batch, not as individual external acceptance gates.

## Body-defining manufactured variants

An installed component can now select a catalog-authored `constructionVariantId`, bound to a different native `crafting-recipe` with the same exact produced component ID. A component explicitly authored with `definesBodySize: true` may have variants with `bodySize` (ordinary creature-size classification). The default recipe without a variant offers no size intrinsic. The sample Structural Frame and External Casing each have *small, medium, large* recipe variants with different candidate ingredient quantities, which demonstrate size derived from what was manufactured rather than separately purchased scale or an arbitrary independent size selection. **Neither part is mandatory.** Multiple verified body-defining variants must agree on creature size or the assembly reports a conflict. A body with no qualifying sized construction remains incomplete for playable custom origin. No novel creature-size or crafting engine is introduced.

These source-constrained size labels are a limited model of the **body envelope**, not a claim of computed three-dimensional geometry. Further work must reconcile other attached components' physical envelopes and ensure their combined body has an appropriate size; a small enclosure with a giant attached limb must not silently count as Small. The recipe input amounts remain provisional and do not establish final creature encumbrance or mass.

## Retained physical material mass (general crafting input metadata)

All 52 candidate recipes now distinguish ingredients **retained in the finished product** (`retainedInProduct: true`) from reagents consumed during manufacturing. `NativeCraftingMaterialQuantity` carries the same optional retention metadata without altering manufacturing prerequisites, quantities, or inventory transactions. Physical materials and selected manufactured subparts have provisional `massPoundsPerUnit` values.

The generic resource quoter reports `KnownRetainedMaterialMassPounds` and any `UnresolvedRetainedMassMaterials` separately from currency/resource cost. These measurements are **partial evidence**, not the whole item's weight: unmarked ingredients, unknown component hardware, additive processing, and possible production losses can remain unresolved. Do not treat an unmarked or unmeasured input as proof of zero mass, infer a movement penalty from the subtotal, or use the subtotal to certify an unencumbered Construct.

A complete mass total and ordinary universal encumbrance integration remain required before the rules can claim that excessively heavy steel assemblies impose their intended mobility tradeoff. These calculations remain general crafting/physical inventory concerns, not an invented Construct-only carrying-capacity system.

The synthetic `NativeConstructBodySizeIntegrationTests` fixture exercises the complete Universal Origin pathway: manufactured shell variant → sourced size candidate → explicit manifestation → mechanically authorized custom profile, including rejection of a different client-asserted size. This proof is deliberately independent of first-party allowance approval.
