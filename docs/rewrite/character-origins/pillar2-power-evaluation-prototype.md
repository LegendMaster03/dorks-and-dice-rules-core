# Pillar 2 — reproducible power-evaluation prototype

**Status:** Research tool only; no balance score, inheritance eligibility, numerical budget, or Species changes are approved.
**Research basis:** [Quantitative Species and Ancestry evaluation](pillar2-species-power-evaluation.md), pinned to revision `e87fd21`.
**Related model:** [Characteristic inheritance and balance design](pillar2-characteristic-inheritance-and-balance-design.md).

## Scope

`tools/character_origins_power_evaluation.py` reconstructs the complete **authored source chain** of each terminal native Species/Ancestry preset, resolves current canonical `origin-trait` references and inherited intrinsic fields, and evaluates the *subset* of mechanics supported by its deterministic scenario interpreter.

It deliberately reports a **vector of endpoints in native units** and explicitly lists unsupported or partly modeled features. A zero endpoint in one scenario means only that the selected scenario did not produce a modeled effect. It never means a characteristic has zero power. This tool does not select an actual biological inheritance, authorize a mixed Origin, determine transmission probabilities, compute a D&D-style point budget, or execute the full Rules Core mechanics.

It treats the source chain's direct child overrides (such as Wood Elf's 35-foot walking speed) as authored source semantics. This is **not** a general mixed-contributor intrinsic replacement policy. Source bindings and player choice eligibility remain the responsibility of the existing authoritative Origin layer. A preset source reference is not itself proof that a final character has resolved all its mandatory choices.

## Run

From the repository root:

```sh
python3 tools/character_origins_power_evaluation.py --output /tmp/pillar2-power-evaluation.json
python3 tools/character_origins_power_evaluation.py --source ancestry.dragonborn.red --compare-source ancestry.dragonborn.bronze --output /tmp/pillar2-red-versus-bronze.json
python3 -m unittest discover -s tests -p 'test_character_origins_power_evaluation.py' -v
```

For a different assumption set, pass `--scenario path/to/fixture.json`. The file is a JSON object with the same conceptual keys as `DEFAULT_SCENARIO` in the tool: incoming typed damage packets, eligible skill checks, condition exposure frequency, area-target counts by shape, and Tremorsense contacts. A supplied scenario is a **complete replacement**, not a partial merge. `--include-deferred` includes nonselectable terminal Gem Dragonborn records for reference analysis; it does not make them playable. `--source` evaluates only the specified direct preset. `--compare-source` adds paired endpoint differences for another complete preset at the six report levels.

## Currently computed endpoints

| Endpoint | Modeled behavior | Deliberate limit |
| --- | --- | --- |
| `maximum_hit_points_added` | Per-character-level intrinsic toughness grants | No healing, death or survival simulation |
| `additional_skill_ranks_entitled` | Level-1 and subsequent rank entitlements | Actual legal distribution, rank caps, and skill use not priced |
| `additional_expected_skill_successes` | Skill bonuses on declared interior-threshold d20 checks | Not a complete competency system; no double-counting Perception composites |
| `incoming_damage_prevented` | Nonstacking passive typed damage resistance on declared packets | Saving throw changes, temporary immunity, reactions, other defenses unmodeled |
| `condition_expected_success_delta` | Known structured additive roll penalties, with explicit exposure frequency | Only declared rolls and supported condition effects; context assumed |
| `breath_expected_net_damage_delta` | Area-targeted, proficiency-limited Attack-replacement breath attacks, with chosen opportunities | No geometry solver, rounding, friendly fire or full class action economy; damage multipliers are scenario assumptions |
| `breath_uses_selected` | Chosen positive-margin opportunities within one long-rest pool | Assumes sufficient distinct legal attack opportunities; no unmodeled competing uses |
| `tremorsense_contacts_detected` | Qualified passive and anticipated activated contacts under substrate/range restrictions | One contact per fixture, no full visibility targeting or duration overlap optimization |

Each profile also records its complete intrinsic context, modeled trait IDs, future/unlocked traits, and an `unmodeled` list. Other effects, including feats, control, spellcasting, most activated defenses, target-type interactions, extra abilities inside compound traits, and novel homebrew, require future modules; they are **not silently priced as zero**. All comparisons must report their unmodeled portions.

The exported default scenario adapts diagnostic examples from the WorkChat report. It is **not** a claimed representative campaign distribution. Use alternative fixtures and review failure modes before interpreting any apparent advantage.

## Capability/resource frontiers and pairs

The reusable functions `pareto_frontier`, `shared_pool_frontier`, `endpoint_difference`, and `interaction` implement the report's complementary method: examine nondominated action/resource choices and calculate paired effects without converting unrelated endpoints into a single score.

`shared_pool_frontier` requires an explicit `sharedUsesFromTrait` reference matching the base trait's local ID. With a Stone's Endurance pool of PB 2, Constitution +3, and Stone Strike using the same pool, the frontier has three tradeoffs between 0, 1, or 2 uses devoted to defense. Its 9.5 expected mitigation per defensive use assumes sufficiently large hits; 3.5 damage per offensive use is **conditional on a hit**. This is a theoretical frontier, not expected total encounter outcomes.

The selected **Dwarf–Rock Gnome Tremorsense** consolidation is a precise test fixture: use only the Dwarf's existing Stonebound Tremorsense definition (60 feet, Bonus Action, 10 minutes, PB/Long Rest, contact with stone); do not retain the Rock Gnome's 15-foot passive mode. For the WorkChat four-contact fixture, Dwarf-only detects two, Rock-only two, the *unresolved independent union* three, and the **selected consolidation two**. A union result from the earlier report must not be mistaken for the new selected result.

## Limitations and next development increments

1. Normalize scenario schemas and validate every comparator's assumptions before producing numerical balance guidance.
2. Add exact source-bound spell and feat choice resolution through trusted native mechanisms, and evaluate their effect on representative class builds.
3. Add event traces, damage packets with saving throws and reactions, initiative, action contention and short-rest boundaries. Expose unknown mechanics at **effect-field** level, not only trait level.
4. Integrate generalized capability gates for flight, climbing, travel, creature type, and group objectives; evaluate hard access alongside expected outcomes.
5. Expand the scenario corpus and replay against all selectable paths at levels 1, 3, 4, 5, 11, 17. Record sensitivity, scenario prevalence, and confidence before comparing complete Species.
6. Use the resulting measurements to tune the **editable** native Species/Ancestries and only afterward investigate whether biological inheritance burden can be mapped to the measured power vector.

No source of this prototype is a normative balance rule or authorization to modify native Species mechanics.
