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
| `incoming_damage_prevented` | Nonstacking passive typed damage resistance on independent declared packets | Save-based damage and supported reaction defenses have separate endpoints; immunity, other defenses unresolved |
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
3. Expand the new save-event and defensive-reaction traces into combat with initiative, positioning, action contention, allied targets, short-rest boundaries, and end-of-day resources. Breath Weapon opportunity selection is still a separate optimistic model.
4. Extend movement access into controlled geometry, timed hazards, creature-type targeting, party assistance, and alternative solutions. The existing mobility gates explicitly measure *one Origin's movement capability*, not full task success.
5. Expand the scenario corpus and replay all selectable paths at levels 1, 3, 4, 5, 11, 17. Report scenario sensitivity, prevalence, and uncertainty before comparing complete Species.
6. Use the resulting measurements to tune **editable** native Species/Ancestries and only then investigate whether biological inheritance burden can be mapped to the measured power vector.

No source of this prototype is a normative balance rule or authorization to modify native Species mechanics.

## Versioned multi-context sensitivity suite (research increment)

The repository's checked-in `tests/fixtures/pillar2-power-scenarios.json` is a
**thirteen-context diagnostic corpus (version 2)**, not a statistically sampled campaign. It
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
supply randomized encounters or observed prevalence, a complete combat action-state
simulator, spell/feat choice resolution,
initiative and ally geometry, creature-type targeting, or a complete native
mechanics interpreter. Unsupported effects are listed rather than silently
priced at zero. The native integration suite tests every terminal preset
against every context when the complete repository checkout is present.

## Saving throws, defensive reactions, and shared pools (research increment)

Version 2 of the diagnostic suite adds three contexts: a damage-bearing Poison
save, magical mental/charm saves, and an incoming-hit reaction schedule.
The new `saveEvents` objects require `id`, `ability`, and `baseSuccess`.
Authored `savingThrows.advantageAgainst` tags and `advantageAbilities` with
`scope: magic-only` modify a matching success probability using
`1 - (1 - p)^2`, without counting multiple advantage sources twice. A save
may have `conditionOnFailure`, or conditional `damageOnFailure`,
`damageOnSuccess`, and `damageType`. Damage resistance on the same packet
is applied only once after expected save damage. These save packets are
**separate** from `incomingDamage`; they are not added twice. The reported
endpoints are chance of success gained, expected damage prevented relative
to the unmodified saving throw, and expected on-failure condition
applications avoided. Conditions of different severity are not equivalent.

`reactionDamageEvents` require a unique `id`, a `round`, `amount`,
`damageType`, `isAttackHit`, `attackerVisible`, and `reactionAvailable`.
The model currently supports the exact authored Stone's Endurance
single-die damage reduction and Cloudy Escape's attack-specific 50% resistance,
with Proficiency Bonus uses per Long Rest. It considers at most one reaction
per round and selects the highest mitigation across all declared rounds
using perfect knowledge. This is an optimistic **resource frontier**, not a
player prediction. Stone's reduction is capped per hit using exact die
enumeration; a small hit does not receive an imaginary 9.5 prevention.
Cloudy's separate teleport effect remains unmodeled. Damage-reduction
ordering with an already matching passive resistance is explicitly unresolved
and excluded from the selected reaction value.

`sharedResourceFrontiers` exposes verified Stone's Endurance / Stone Strike
links as conditional, theoretical defensive-use versus on-hit offense options
from **one pool**. It does not add the two full PB resource pools or count
the offense and defense endpoints as simultaneously realizable. Other native
shared pools and alternative attack effects remain pending.

These measurements remain endpoint-specific diagnostics. A successful charm
save, a damage-prevention reaction, and one extra damage die are not converted
into a universal Species power score.

## Innate spell access, Origin choices, and control applications (research increment)

The earlier diagnostic corpus was **version 4 with eighteen named contexts**. It adds
explicit `spellDemands` and `controlOpportunities` alongside the existing
combat, exploration, saving-throw, and reaction fixtures. These are conditional,
source-backed mechanics tests, **not** additional balancing weights.

### Fixed innate spell access

The evaluator reads native fixed-spell entries including their character-level
unlock, at-will entitlement, free-use count, Long Rest recharge, slot-casting
permission, and authored free-cast spell level. The `innateFixedSpellEntitlements`
ledger retains source-trait identity and special spell-level information such
as Infernal Tiefling's level-2 free Hellish Rebuke. Neither preparing a spell
nor permission to spend a class slot generates new spell slots.

For each explicitly eligible `spellDemands` entry (`id`, `spell`, optional
`eligible`), it counts available at-will access or a remaining independent
fixed-spell free use. The trace records an exhausted pool, ineligible task,
unknown spell-list selection, or unresolved duplicate pool rather than asserting
that the underlying objective was accomplished. In particular:

- High Elf's Detect Magic and Misty Step produce one free use each after their
  level-3 and level-5 unlocks. Its changeable Wizard cantrip cannot be valued
  until a legal spell has been chosen.
- Infernal Tiefling's Fire Bolt is at-will, while Hellish Rebuke and Darkness
  have separately authored free uses. The spell's damage, reactions, duration,
  spellcasting DC, concentration competition, and class-specific value remain
  unmeasured.
- Multiple independently sourced *limited uses of the same fixed spell*, such
  as Drow and Infernal Darkness, are **unresolved** until a source-reconciliation
  rule approves their interaction. Their potential capacities are enumerated,
  but the evaluation does not automatically spend both pools.

Human's extra Origin feat and skill proficiency are counted as distinct
**choice entitlements**, not selected mechanical benefits. The current native
catalog does not yet provide the complete, approved feat and spell-list choices
necessary for power valuation, so both are recorded as unresolved. The evaluator
does not infer a free feature or score unfamiliar effects as zero.

### Conditional Metallic Breath control

`controlOpportunities` provide a chosen option, cone geometry, explicitly
counted enemy and ally targets, and a stipulated saving-throw failure
probability. The evaluator recognizes the authored Metallic Breath options
only after the level-5 unlock: **Enervating Breath** (expected Incapacitated
applications) and **Repulsion Breath** (expected Prone applications and forced
movement feet). It spends the family-level once-per-Long-Rest pool once, separate
from ordinary damaging Breath Weapon uses. The declared policy spends the
first eligible opportunity; it is not a globally optimal action policy.

For two enemies and one ally in the Enervating cone with 0.55 save failure,
the outputs are **1.10 expected enemy Incapacitated applications** and **0.55
expected allied Incapacitated applications**, not 1.10 successful objective
outcomes or lost turns. Three enemies facing Repulsion under the same failure
rate yield **1.65 expected Prone applications** and **33 expected feet of total
forced movement** before accounting for obstacles. These represent separate
control endpoints, and the evaluator does not equate their tactical value.

The source option's save DC formula is preserved, while the fixture's supplied
failure probability is used rather than guessing enemy statistics. A successful
save has no effect under the authored option. Ally-safe positioning, actual player decisions, immunities, forced-movement
obstructions, and effects on subsequent actions beyond the explicitly scheduled
window remain **unvalued**. Unknown
control-option shapes fail closed instead of being interpreted partially.

Run the existing suite command to reproduce all eighteen contexts. Native
integration tests check the real High Elf, Infernal Tiefling, Human, Bronze
Dragonborn, and Red Dragonborn entries when a complete repository checkout is
available. This increment neither modifies native rules nor establishes a
Species power score or inheritance allocation cost.

## Action-cost and condition-window measurements (research increment)

In the prior increment, the corpus was **version 4 with eighteen named scenarios**. This increment
adds explicit condition-window measurements to the existing Metallic Breath
control fixture, and two timing contrasts. It **does not** convert an
expected condition application into an unconditional lost turn.

For any selected Metallic Breath, `control_replaced_attack_expected_damage_forgone`
records the expected damage of the **one attack replaced**, using the event's
`attackReplacementExpectedDamage` or the scenario's explicitly assumed attack
alternative (default reference 5.2). This is an opportunity cost separate from
control outcomes; it is not subtracted from expected denied actions or from
expected condition applications. An actual caster without an available Attack
action still requires its own legal-build fixture.

An Enervating Breath event may explicitly declare, separately for enemy and ally
recipients, `enemyActionSlots`, `enemyBonusActionSlots`, `enemyReactionSlots`,
`enemyConcentratingTargets`, and corresponding `ally*` fields. These specify
**distinct target opportunities within the effect's actual duration**, not
how often those opportunities occur in typical campaigns. Each value is
bounded by the corresponding target count. The evaluator reads the **native
condition** and the authored `endsAt: start-of-your-next-turn` expiry; only
recognized action restrictions and concentration ending qualify. For each
specified slot class, expected slots denied = listed at-risk slots × supplied
save-failure probability. The test assumes those targets would otherwise have
used the declared slots. Missing fields mean **not measured**, not zero.

For Repulsion Breath, `enemyNormalAttackRollsWhileProne` and
`allyNormalAttackRollsWhileProne`, together with an explicit `baseHitChance`,
count attacks assumed to take place **before a failed-save target stands**,
without preexisting Advantage/Disadvantage or other roll modes. The native
Prone condition supplies Disadvantage. For ordinary hit probability *p*,
expected hits prevented = exposed rolls × failed-save probability ×
*p(1−p)*. This does not account for Prone's different melee/ranged Armor Class
adjustments, time spent standing, spatial obstruction, or downstream damage.
The model never equates Prone with an automatic denied Action.

**Illustrative level-5 Bronze Dragonborn case:** Two enemy targets and one ally
are inside the cone with 0.55 save-failure probability. If two enemy Actions,
one allied Action and one enemy concentration are explicitly at risk before
the user's next turn, Enervating Breath prevents an expected 1.10 enemy Action
slots, 0.55 allied Action slots, and ends an expected 0.55 enemy concentrations.
With identical failed-save assumptions **but no exposed Actions**, both
action-denial outputs are zero while expected Incapacitated applications remain
1.10 enemy and 0.55 ally. For Repulsion, three exposed normal attack rolls
at baseline 0.65 hit probability and 0.55 save-failure probability yield
3 × 0.55 × 0.65 × 0.35 = **0.375375 expected hits prevented**, conditional
on targets actually attacking while still Prone. None of these are observed
player actions, guaranteed loss of enemy turns, or campaign average values.

The sensitivity summary now preserves an endpoint's *not-evaluated contexts*
separately from contexts with a measured zero, avoiding artificially low minima
when a mechanic was not evaluated in a fixture. Existing numerical endpoints
remain in their original units. The tool still cannot price unselected feats,
execute spells without an authoritative spell-effect catalog, or estimate
control effects in scenes whose initiative and target behavior are unknown.

## Confirmed-hit Goliath control diagnostics (version 5)

The earlier **version-5 scenario corpus contained 23 named contexts**, five of
which isolate conditional Goliath control. `strikeOpportunities` are deliberate
single-feature activation trials, **not** simulated attack rotations or permission
to inherit any feature. Each event identifies an **expressed canonical `traitId`**,
character `turn`, delivery (`melee-weapon`, `thrown-weapon`, or
`other-attack-roll`), target size, whether an attack hit, whether the hit dealt
damage, whether the target is a creature, a stipulated save-failure probability,
and optional timed exposure. Confirmed-hit and damage requirements are kept
separate: Stone and Storm Strikes trigger on the appropriate hit even if the
weapon dealt no damage; Hill's Tumble and the Frost alternatives require a hit
**and** damage. The fixture does not independently determine attack accuracy.

- **Hill Goliath:** Base Hill's Tumble automatically applies Prone to an eligible
  Large-or-smaller creature after a qualifying hit, without granting an
  additional damage die or inventing a saving throw. Its alternate Hill Strike
  instead has 1d6 additional weapon-type damage, a save-dependent Prone effect,
  and no Large-size limit. The two modes use the **same PB/Long Rest pool**.
- **Frost Goliath:** Frost's Chill gives 1d6 additional Cold damage and an
  unconditional 10-foot Speed reduction; the alternate uses the **same pool**
  to replace the reduction with a save-dependent Speed 0 effect. When the
  scenario specifies a target's current Speed, the endpoint counts **nominal
  Speed reduction**, capped by that Speed. It does not claim lost travel
  distance, lost turns, or the duration of an entire encounter.
- **Stone Goliath:** Stone Strike deals 1d6 extra Force damage on an eligible
  confirmed hit and can push a creature 10 feet on a failed Strength save. Its
  uses come from **Stone's Endurance**, never a separate pool. An attack
  against an object can still deal the extra damage without a creature-only
  push. Obstacles and actual displacement remain unresolved.
- **Storm Goliath:** Storm Strike deals 1d6 Lightning damage; if the enemy
  fails its Constitution save, the character imposes attack Disadvantage until
  the next-turn expiry. Expected hits prevented are calculated only when the
  scenario explicitly supplies attacks **before the effect ends**, with no
  other advantage/disadvantage sources. Storm Strike consumes Storm's Thunder
  uses rather than adding a new resource pool.

For Prone the model inspects the real native `condition.prone` definition. It
only counts **explicitly scheduled** attacks while the target is still Prone;
normal hit probability `p` becomes `p²` under Disadvantage, so conditional hits
prevented per qualifying attack are `p(1−p)`. At 0.65 base hit probability,
two such attacks can lose **0.455** expected hits after automatic Prone, or
**0.25025** if the Prone application also depends on a 0.55 failed save. Prone
still does **not** automatically deny an Action. Its effects on AC, mobility,
and positioning are not yet valued. No other conditional ability is assumed to
be equivalent to Prone merely because it impairs attacks.

A level-1 Stone Goliath with three scheduled eligible confirmed hits has only
two shared uses at PB 2. With 0.55 failed saves, those two Stone Strikes produce
**7 gross expected extra Force damage** and **11 expected feet of forced
movement before obstacles**, not a third use or a guaranteed 11-foot push to
one creature. The nominal endpoints are conditional and separate. Spending
those same charges on Stone's Endurance could instead prevent incoming damage.
The previous defensive optimization and the new explicit offensive replay are
**not treated as one simultaneous plan**: a mixed scenario reports
`joint-offense-defense-resource-plan-unresolved` where their selected sources
consume the same pool. Joint scheduling is a future research task.

The evaluator rejects unknown event keys, unsupported deliveries, incorrect
level/resource access, invalid probabilities, and malformed hit/damage claims.
Where an effect's expiry is not authored in the expected structure, it leaves
the timed consequence unresolved. Incomplete movement, opponent behavior,
conditional spells, class-specific feat choices, and real session frequencies
continue to block a definitive whole-Species balance judgment.

## Bonus-action and area/aura diagnostics (version 6)

The **version-6 corpus contains 26 diagnostic contexts**. The three added
fixtures use `tacticalOpportunities`, each with a referenced, expressed
canonical `traitId`, a round number, explicit action availability and
eligible targets. The evaluator recognizes **only the verified authored forms**
of Stone Throw, Searing Ignition, and Maelstrom Aura; altered or unfamiliar
mechanical definitions are not silently treated as equivalent.

Events replay **in fixture order**, with at most one Bonus Action and one
attack replacement per round *within this tactical submodel*. This is a
conditional sequence, **not** an optimizer choosing between actions, and does
not combine action accounting automatically with other evaluation modules.
Each supported ability uses its own PB/Long Rest pool, including independently
owned Stone Throw charges. The older Stone Strike shares **Stone's Endurance**
charges instead; Stone Throw does not draw from either of those pools. No
additional pool is granted by receiving multiple source occurrences.

- **Stone Throw:** A rock must be available and the declared target must be
  within its authored 60-foot range. Gross expected Force damage is
  hit probability × 1d10 mean, adjusted only by an explicit damage-type
  multiplier. A qualifying creature makes a Strength save; expected Prone
  applications equal hit probability × failed-save probability. Attack hits
  prevented are measured **only** when attacks are declared before the target
  stands and native `condition.prone` includes the matching Disadvantage.
  A separate *net Bonus Action damage* endpoint is available only when the
  fixture states the alternative action's expected damage. The calculation
  does not claim that forcing Prone is worth a fixed amount of damage.
- **Searing Ignition:** Requires an available Attack action with one attack
  to replace. The evaluator takes the fixture's number of enemies **chosen
  by the user and within the 15-foot radius**; no friendly-fire or positioning
  solution is inferred. Gross expected Fire damage uses the authored 1d8+PB
  and full/half on failed/successful saves. Net attack-replacement damage
  subtracts the specified alternative attack. Blinded applications and
  attack-hit prevention are separate endpoints, with the latter requiring
  declared enemy attacks before the author's next-turn expiry. Additional
  Blinded effects, such as sight loss, Speed, and AC, remain unmodeled.
- **Maelstrom Aura:** An uninterrupted Bonus Action window lasts until the
  beginning of the user's next turn. The fixture declares how many attacks
  target the user in that window, how many other creatures begin their turns
  within the unobstructed 10-foot area, and their current Speed. The output
  reports expected attack hits prevented by Disadvantage and nominal
  Speed reduction after a failed Strength save; these are *not* lost turns
  or completed travel reductions. Separately identified **nonattack**
  Lightning/Thunder packets allow marginal 50% resistance to be measured
  without double-counting attack-hit prevention. Existing resistance to the
  same type reduces or eliminates the aura's incremental prevention.
  If interruption by Incapacitated is stipulated, the event stays unresolved
  rather than projecting a full-duration benefit.

**Reproduction examples at level 4** (PB 2, baseline hit chance 0.65,
failed-save probability 0.55): two eligible Stone Throws yield 7.15 gross
expected Force damage, 0.715 expected Prone applications, and 1.15 expected
net Bonus Action damage against an explicit 3-damage alternative each time.
One two-enemy Searing Ignition yields 10.075 gross expected Fire damage and
1.1 expected Blinded applications, at a cost of one replaced attack.
One Maelstrom Aura facing two eligible attacks and separate nonattack packets
of 20 Lightning and 10 Thunder damage prevents 0.455 expected attack hits
and 15 expected incoming damage, while two 30-foot-Speed enemies starting
their turns inside the aura lose 16.5 expected **nominal** Speed feet.

All examples use modeled means without integer half-damage rounding, overkill,
actual geometry search, reaction competition, class-specific opportunity costs,
condition immunities, encounter prevalence or concentration tradeoffs.
Independent fixture endpoints must not be summed into a total Species score.
The research report and inheritance design remain authoritative about what
is *not* decided: effective power targets, mixed-contributor eligibility,
hereditary burden and numerical allocation rules.

## Joint class-archetype action and shared-resource plans (version 7)

The **version-7 corpus has 29 contexts**. Three additional cases nominate
chronologically ordered actions for a **weapon martial**, a **bonus-action
skirmisher**, and a **prepared caster**. These are *hypothetical archetype
fixtures*, not instantiated native character classes. Although the native
catalog contains Barbarian, Rogue, and Wizard class records, the evaluator has
not yet resolved their features, level progression, spell preparations, or
class-appropriate attack rotations. All alternative-action values in these
fixtures are **explicit assumptions**, never class-derived damage estimates.

The optional `jointCombatPlans` scenario field is an array of plans. Each plan
has a distinct `id`, an `archetype` label (`weapon-martial`,
`bonus-action-skirmisher`, or `prepared-caster`), and an ordered `events` array.
Each event has a `type` (`strike`, `tactical`, or `reaction`) and an
`opportunity` in the *existing corresponding event schema*. A reaction also
requires the exact owned `traitId` and a nonnegative
`reactionAlternativeExpectedDamage` (explicitly zero if the fixture assumes no
competing reaction). Tactical bonus actions must state the alternative bonus
action's expected damage; attack replacements must state the forgone alternative
attack/action's expected damage. These assumptions may be class-sensitive.

`jointCombatPlanResults` provides each plan's **separate** event trace, action
status, source-owned pool charges spent and their capacities, in-domain
mechanical endpoints, and unresolved effects. It is **not added to** the
independently computed `metrics` vector; doing so would double-count the same
potential abilities. Each plan is a deterministic *nominated sequence*, not
an optimizer or a mechanically legal build validator.

For the scoped interpreter, one attack opportunity, one Bonus Action, and one
Reaction are available **at most once per declared round**, with no Extra
Attack scheduling or separate initiative-order handling yet. A confirmed-hit
strike rider consumes the declared attack opportunity but does not create or
charge a second Action. All source-specific limited-use pools persist across
rounds in the same modeled day, including Stone Strike consuming the single
Stone's Endurance pool. Unknown, unexpressed, or level-locked traits do not
activate. Invalid action constraints and resources fail closed; no biological
inheritance permissions are implied by tests with combined traits.

**Reproducible examples at level 4 (PB 2):**

- **Stone Goliath / weapon martial:** A confirmed Stone Strike spends one
  shared charge and adds 3.5 expected Force damage *conditional on that hit*.
  A later qualifying 20-damage reaction can spend the other charge to prevent
  9.5 expected damage with Constitution +3. A third attempted Stone Strike
  is exhausted. This is one joint plan, not two independently full pools.
- **Stone Goliath / skirmisher:** Each qualifying Stone Throw has gross
  expected damage 0.65 × 5.5 = 3.575 and costs a Bonus Action that could
  have supplied 3 expected damage; marginal net is 0.575 for that
  opportunity. A second attempt in the same round is blocked. The two
  rounds of attacks together yield 1.15 net damage before valuing Prone.
- **Fire Goliath / prepared caster:** With two eligible enemies, 0.55
  failed-save probability, and 1d8+PB damage, Searing Ignition has 10.075
  expected gross damage. Against an *assumed* forgone 12-damage caster
  alternative, the net damage is **−1.925**. A different character with a
  5.2-damage alternative reaches a different conclusion. Blinded remains
  a separate, unpriced endpoint.

**Important limitations:** The conservative one-attack-opportunity-per-round
model rejects some potentially legal higher-level extra-attack combinations.
A real character's Attack action can include multiple attacks, and spell
casting can have different action, resource, target and concentration costs.
Reactions can also become available at turn rather than round boundaries.
Non-damage opportunity costs, pending feat choices, full initiative sequencing,
allied actions, end-of-day rest refresh, and optimized decisions remain
unresolved. Do not use these scenarios to rank Species or set inheritance
allocation costs without explicitly resolving those gaps.
