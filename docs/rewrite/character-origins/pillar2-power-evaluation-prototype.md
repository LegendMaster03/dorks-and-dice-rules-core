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

For a different assumption set, pass `--scenario path/to/fixture.json`. The file is a JSON object with the same recognized keys as `DEFAULT_SCENARIO` in the tool: incoming typed damage packets, eligible skill checks, condition exposure frequency, area-target counts by shape, Tremorsense contacts, and explicit solo movement gates. A supplied scenario is a **complete replacement**, not a partial merge. The evaluator rejects unknown top-level scenario keys, negative damage, nonfinite/out-of-range probabilities, invalid counts, unsupported movement modes, missing burrow material, and duplicate movement challenge IDs instead of silently producing misleading output. `--include-deferred` includes nonselectable terminal Gem Dragonborn records for reference analysis; it does not make them playable. `--source` evaluates only the specified direct preset. `--compare-source` adds paired endpoint differences for another complete preset at the six report levels.

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
| `origin_mobility_gates_satisfied` | Count of *specified solo movement-mode gates* supplied by intrinsic movement or canonical trait, including limited flight and material-specific burrowing | Not adventure objective success or a generic mobility score; class abilities, equipment, assistance, hazards, and alternative routes are excluded |

Each profile also records its complete intrinsic context, modeled trait IDs, future/unlocked traits, `unmodeledIntrinsicFields`, and an `unmodeled` list. For partially supported traits the list now identifies unmodeled **effect-field names**; a movement definition that cannot be interpreted remains explicitly unresolved. A breath-event trace reports all candidate attack replacements, including opportunities the optimizer correctly declines. `mobilityGateResults` explains the result and selected capability for each declared gate. Other effects, including feats, control, spellcasting, most activated defenses, target-type interactions, extra abilities inside compound traits, and novel homebrew, require future modules; they are **not silently priced as zero**. All comparisons must report their unmodeled portions.

The exported default scenario adapts diagnostic examples from the WorkChat report. It now contains a single-move 35-foot walking gate, a 15-foot climb, loose-sand burrowing, and three flight crossings fifteen minutes apart. Each mobility event has an ID, movement mode (`walk`, `climb`, `swim`, `burrow`, `fly`), required single-move distance, optional `maxMoves`, start time in minutes, and duration (default 0.1 minute). A burrowing gate must specify `material`. Optional `requiresHover` and `requiresWholeParty` distinguish further constraints. A whole-party gate is **unresolved** because a single character's movement mode does not prove that the party can cross. A material-unspecified intrinsic burrowing capability is likewise **unresolved**, not falsely described as either permitted or prohibited for a specific substrate.

Finite flight requires an authored duration and resource pool. One activation can cover multiple mobility events during its window; later separated events must spend new uses. The evaluator does not presently model action competition, concentration, rest boundaries within the movement trace, or carrying allies. A gate reports only what this Origin explicitly supplies in that fixture; a character may still solve the adventure problem by other means.

The exported default scenario is **not** a claimed representative campaign distribution. Use alternative fixtures and review failure modes before interpreting any apparent advantage.

## Capability/resource frontiers and pairs

The reusable functions `pareto_frontier`, `shared_pool_frontier`, `endpoint_difference`, and `interaction` implement the report's complementary method: examine nondominated action/resource choices and calculate paired effects without converting unrelated endpoints into a single score.

`shared_pool_frontier` requires an explicit `sharedUsesFromTrait` reference matching the base trait's local ID. With a Stone's Endurance pool of PB 2, Constitution +3, and Stone Strike using the same pool, the frontier has three tradeoffs between 0, 1, or 2 uses devoted to defense. Its 9.5 expected mitigation per defensive use assumes sufficiently large hits; 3.5 damage per offensive use is **conditional on a hit**. This is a theoretical frontier, not expected total encounter outcomes.

The selected **Dwarf–Rock Gnome Tremorsense** consolidation is a precise test fixture: use only the Dwarf's existing Stonebound Tremorsense definition (60 feet, Bonus Action, 10 minutes, PB/Long Rest, contact with stone); do not retain the Rock Gnome's 15-foot passive mode. For the WorkChat four-contact fixture, Dwarf-only detects two, Rock-only two, the *unresolved independent union* three, and the **selected consolidation two**. A union result from the earlier report must not be mistaken for the new selected result.

## Limitations and next development increments

1. Run the full native-catalog integration suite and audit every unsupported effect field; the isolated regression fixtures do not substitute for complete content coverage. The real native conditions catalog is stored under `conditions`, not `entries`; both shapes are supported and an empty catalog fails closed.
2. Resolve exact source-bound spell and feat choices through trusted native mechanisms, then compare their effects on representative class builds.
3. Add combat traces with saving throws, reactions, initiative, positioning, action contention, allied targets, short-rest boundaries, and end-of-day resources. Currently only the Breath Weapon's opportunity selections are traced.
4. Extend movement access into controlled geometry, timed hazards, creature-type targeting, party assistance, and alternative solutions. The existing mobility gates explicitly measure *one Origin's movement capability*, not full task success.
5. Expand the scenario corpus and replay all selectable paths at levels 1, 3, 4, 5, 11, 17. Report scenario sensitivity, prevalence, and uncertainty before comparing complete Species.
6. Use the resulting measurements to tune **editable** native Species/Ancestries and only then investigate whether biological inheritance burden can be mapped to the measured power vector.

No source of this prototype is a normative balance rule or authorization to modify native Species mechanics.

## Versioned multi-context sensitivity suite (research increment)

The repository's checked-in `tests/fixtures/pillar2-power-scenarios.json` is a
**ten-context diagnostic corpus**, not a statistically sampled campaign. It
exercises line versus cone opportunities, Fire and Poison resistance exposure,
unprotected versus protected Drow sunlight, connected stone/earth detection,
separated versus concentrated flight windows, and eligible native skill checks.
The scenario IDs, purposes, numerical assumptions, and event timelines are
versioned so a change to an actual Species or a test fixture can be reviewed.

From the repository root, run:

```sh
python3 tools/character_origins_power_evaluation.py \
  --scenario-suite tests/fixtures/pillar2-power-scenarios.json \
  --output /tmp/pillar2-species-context-comparison.json
python3 tools/character_origins_power_evaluation.py \
  --scenario-suite tests/fixtures/pillar2-power-scenarios.json \
  --source ancestry.dragonborn.red --compare-source ancestry.dragonborn.bronze \
  --output /tmp/pillar2-red-bronze-context-comparison.json
python3 -m unittest discover -s tests -p 'test_character_origins_power_evaluation.py' -v
```

`--scenario-suite` is mutually exclusive with the one-off `--scenario` flag.
The JSON output includes all selected conventional source chains at levels
1, 3, 4, 5, 11, and 17 **for each context**, optionally the paired differences
between two complete presets, and a `sourceSensitivity` section. For each
mechanical endpoint, the sensitivity section records the minimum, maximum,
and the named diagnostic contexts producing those values. It also records
complete, partially supported, or unknown mechanics through a structured
coverage record. `isCompleteForCurrentLevel` excludes known future unlocks
but remains false where active mechanics or intrinsic characteristics have
not been fully evaluated. Nothing in this output ranks Species, equates
unrelated units, or extrapolates unobserved campaign frequencies.

Notably, Red Dragonborn's line-dominant context and the cone-dominant context
produce different expected benefits at the *same character level*; comparing
one outcome without its context is unsound. Drow's expected sunlight penalty
also differs when its protective equipment is assumed effective. These are
intended sensitivity demonstrations, **not** findings that any conventional
Species is balanced or unbalanced overall.

**Known limitations for the next increment:** The corpus still does not
supply randomized encounters or observed prevalence, a combat action-state
simulator, spell/feat choice resolution, saving-throw advantage modeling,
initiative and ally geometry, creature-type targeting, or a complete native
mechanics interpreter. Unsupported effects are listed rather than silently
priced at zero. The native integration suite tests every terminal preset
against every context when the complete repository checkout is present.
