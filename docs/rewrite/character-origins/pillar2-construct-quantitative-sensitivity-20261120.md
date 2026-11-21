# Pillar 2 — Construct Assembly quantitative sensitivity

**Date:** November 20, 2026  
**Branch:** `feature/construct-recipe-derived-starting-cost-20261120`  
**Status:** Historical, reproducible candidate sensitivity analysis. The numerical load-support and flat-DR hypotheses below were explicitly withdrawn from native rules. No movement-tier policy is approved or implemented.

## Method and boundaries

The paired comparison follows the endpoint-specific method in `pillar2-species-power-evaluation.md`: compare configurations under the same assumptions, state which physical resources and capabilities changed, and keep cost, body mass, power, movement, defense and actions in distinct units. There is no approved exchange rate between these domains.

Source inputs include draft native recipes, materials, components and power sources, plus candidate recipe-input unit values in `rule.creature-origination`. **Invented nominal drive-support and hypothetical protective-layer DR values now live exclusively in `data/reference/construct-assembly-balance-candidates-20261120.json`, not in the native gameplay definitions.** The configurations and price comparison candidates also live in that reference file.

Reproduce all nine results from the repository root:

```bash
python3 tools/research_construct_assembly_balance.py --format markdown
python3 tools/research_construct_assembly_balance.py --format json
python3 -m unittest discover -s tests -p 'test_construct_assembly_balance_research.py' -v
```

The Python utility **independently audits authored ingredient arithmetic**, including selected body-size variants and manufacture-then-consume recipe stages. It refuses unknown ingredients, missing prices and mismatched recipe products. It deliberately does **not** replace the C# assembly evaluator, simulate connections, determine power-network availability, authorize inventory, assert protective coverage, or award Origin traits.

The source-defined *nominal power output* assumes the installed source could be available. Total *requested power demand* assumes every installed powered component were operating. Consequently a positive shortfall disproves simultaneous full operation, but no shortfall does **not** establish that operation is possible. The hypothetical gross-load-support totals use **reference-only, unapproved** drive capacity assumptions, not supported first-party hardware limits. Even under those assumptions, they would require power, connections and compatibility that these scenarios do not establish. Retained mass remains a lower bound until **every** manufactured product's mass is certified. Regional DR sums assume hypothetical co-location of independently installed protective surfaces and are not damage mitigation.

## Reproduced source quantities

The candidate starting allowance is **220 points, unapproved**. The ten source-based comparisons below are provisional, and each scenario currently has *incomplete* mass accounting.

| Assembly | Resource cost | Known minimum mass (lb) | Requested demand / nominal output | Maximum potential gross support (lb) | Hypothetical same-region DR sum |
| --- | ---: | ---: | ---: | ---: | ---: |
| Medium mechanical Construct | 173 | 223 | 7 / 4 | 300 | 0 |
| Medium Construct, Infernal Engine substituted | 212 | 230 | 7 / 8 | 300 | 0 |
| Overbuilt steel, 1 drive | 253 | 414 | 2 / 4 | 300 | 12 |
| Overbuilt steel, 2 drives | 275 | 444 | 3 / 4 | 600 | 12 |
| Overbuilt steel, 3 drives | 297 | 474 | 4 / 4 | 900 | 12 |
| Overbuilt with wood plating, 3 drives | 225 | 294 | 4 / 4 | 900 | 0 |
| Overbuilt with stone plating, 3 drives | 225 | 546 | 4 / 4 | 900 | 12 |
| Overbuilt with cloth plating, 3 drives | 189 | 258 | 4 / 4 | 900 | 0 |
| Overbuilt with five stone plates and one cloth plate, 3 drives | 219 | 498 | 4 / 4 | 900 | 10 |
| Small nonhumanoid Construct | 95 | 58 | 3 / 5 | 300 | 0 |

**No row demonstrates a fully operational or playable Construct.** No row proves a speed or attack capability. The provisional protection sums do not prove actual covered area, attack applicability, stacking, bypass, or effective Armor Class.

## Findings

### 1. Starting budget and sustained operation are separate constraints

The 173-point Medium assembly fits the proposed 220-point starting allowance by **47 points**, yet needs 7 nominal power to operate its requested subsystems and only contains 4. The difference is a **3-power shortfall**, even before considering whether its alchemical circulation, actuator, manipulation and control dependencies are available in a valid connected network.

Changing only its power source to the candidate Infernal Engine costs **39 additional points**, for **212 total**, and increases nominal output from **4 to 8**. It therefore removes the arithmetic power shortfall and leaves only **8 allowance points**, *if* 220 is ever approved. The engine's fuel, failure behavior, acquisition and sustained availability remain unresolved; output arithmetic alone is not equivalent to operational uptime.

**Implication:** Starting cost calculations must not be used as a proxy for what a character can actually do. The creator-NPC competency purchase hypothesis also remains unresolved and has not been priced into either scenario.

### 2. Movement tradeoffs are discontinuous under the present candidate values

The steel-plated build with one drive has **at least 414 lb** of retained mass against a **300 lb maximum candidate support**. Even with incomplete mass evidence, this can prove overload *if that drive is the verified applicable support*. A second drive increases candidate support to **600 lb** and raises known mass to **444 lb**, costing **22 more points**. A third increases support to **900 lb** at **474 lb**, again for **22 additional points**.

The two-drive example's known minimum mass is 74% of its **reference-only hypothetical** support estimate; the three-drive example's is about 53%. **There is no approved 75% speed breakpoint or 30/15/0-foot tier mechanic.** These ratios are retained solely to illustrate how changing an assumed capacity alters a mathematical comparison. They do not prove any movement speed, stall condition, or effective drive support.

**Implication:** Do not describe extreme steel construction as automatically self-balancing through slowness. The benefit and cost of installing additional drives must be evaluated together, including power use, mechanical compatibility, fuel, movement modes and payload. No arbitrary attachment count is required.

### 3. Candidate material prices and armor effects can produce unusual incentives

With six independent plating components and all other construction choices unchanged:

- Steel plating gives a hypothetical same-region protection sum of **12**, with a 297-point total and 474 lb known mass.
- Stone gives the **same hypothetical protection sum of 12** for **225 points**, but increases known mass to **546 lb**.
- Wood changes the total to **225 points and 294 lb**, with **zero** candidate protection.
- Cloth changes the total to **189 points and 258 lb**, also with **zero** candidate protection.

Stone is **72 points cheaper than steel** for the same *provisional* per-layer DR in these assumptions, at the cost of **72 additional pounds**. Replacing just one stone plate with cloth yields a **219-point** assembly (1 point under the candidate allowance), with **498 lb known minimum mass** and a hypothetical single-region protection sum of **10**. This mixed-material result is particularly sensitive to the allowance choice: nominal three-drive support is still 900 lb and nominal demand equals output at 4. The hypothetical aggregate drive support is not a verified operating capacity, and no loaded-speed threshold is approved. Even a heavy plated assembly might offset some mass with additional locomotion hardware; the magnitude of that effect requires actual locomotion and material rules. These data do not justify changing the source values yet, but they identify a potential pricing/protection mismatch.

**Implication:** Price, protective coverage, durability and mobility must be compared as a joint configuration. Candidate local protection values are **not** a license to stack six DR layers in an actual attack resolution.

### 4. The low-cost small form is not proven competitive with conventional Origins

The Small nonhumanoid fixture costs **95 points** and has **58 lb known retained mass**, with nominal demand **3** against solar output **5**. However, its source lacks approved sensory/locomotion characteristic projections and complete operating/energy rules. The missing abilities can not be assigned a value of zero and compared with a complete biological Species.

The previous conventional Species power evaluation uses a fixed **Medium Humanoid, 30-ft speed** reference for isolated effects and measures performance separately in movement access, successful tests, damage, resources and targeting. The former **30/15/0-ft** candidate Construct movement profile has been removed from executable runtime and native content. No replacement movement values have been approved. Creature type, size, biological needs, limitations, sensory interfaces, passive protection and construction resources also differ.

**Implication:** A numeric claim such as “220 points equals a balanced conventional Species” is not yet defensible. Test explicit matched character scenarios with a legal, operating Construct before proposing such equivalence.

## Decision boundary

No new component family, fixed maximum attachment count, mandatory humanoid frame, separate Construct crafting engine, permanent post-creation budget, approved creator-NPC competency price or automatic Construct immunities are warranted by this analysis.

Before first-party authorization, the high-value work is:

1. Verify **one complete playable assembly** with real mechanical/power connections, full mass and inventory payload, effective movement and characteristic grants.
2. Run endpoint-specific paired fixtures against ordinary Species builds in the same encounters and exploration tasks. Preserve survival, actions, mobility, resource use, capability gates and costs as separate outputs.
3. Evaluate alternative source/fuel and protection coverage arrangements, then revise candidate costs, capacities, thresholds or requirements only where those comparisons identify a consequential imbalance.
4. Select a starting allowance only after the first-party assembly meets its baseline functional requirements.

The standalone research utility is intentionally narrow and does not itself implement these remaining rules.
