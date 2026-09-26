# Travel and environment mechanics

Rules Core owns the source-provenanced mechanical definition of travel and environment rules. Hex Crawl owns expedition state, watch flow, navigation state transitions, encounter cadence, random outcomes, map state, and DM workflow.

The consumer boundary is intentionally narrower than the Source Layer. A travel consumer reads mechanics only from the published global or Campaign-effective Rules Layer snapshot. It does not select a source document, edition, or competing profile.

## Contract

Global effective catalog:

`GET /api/rules/travel-environment`

Resolve one global mechanic from explicit runtime inputs:

`POST /api/rules/travel-environment/{mechanicKey}/resolve`

Campaign-effective equivalents:

`GET /api/campaigns/{campaignId}/rules/travel-environment`

`POST /api/campaigns/{campaignId}/rules/travel-environment/{mechanicKey}/resolve`

The catalog exposes:

- stable mechanic keys;
- a typed mechanic definition;
- explicit required inputs and allowed selector values;
- quantity, factor, or check semantics;
- source/work/edition attribution inherited from the selected effective source revision;
- effective rule-resolution state;
- a consumer-safe conflict state when multiple effective rules attempt to define the same mechanic differently.

If two effective definitions for one mechanic key are structurally identical, Rules Core can expose one merged effective definition with combined provenance. If they differ, the mechanic is `conflicted`, its definition is withheld from resolution, and the downstream tool is not asked to select a winner.

## Generic operations

The evaluator is edition-neutral. It supports:

- exact quantity lookup;
- exact factor lookup;
- constant factors;
- linearly increasing or decreasing check DCs;
- maximum-applicable-option check DCs;
- threshold-triggered factors.

Definitions use explicit units and factor semantics. A distance multiplier, movement-cost multiplier, and travel-time-cost multiplier are distinct mechanic kinds and are not silently converted into one another.

Resolution returns `resolved`, `input-required`, or `not-applicable`. Rules Layer conflicts and unresolved adjudication are preserved separately at the mechanic level.

## Reviewed source projections

The reviewed source projections recognize exact SRD source identities and validate characteristic source text before emitting structured mechanics. Downstream tools do not parse publisher prose.

### SRD 3e — Movement

Projected mechanics:

- `travel.overland.walk-distance`
  - base speeds 15, 20, 30, and 40 feet;
  - per-hour and per-day overland walking distances.
- `travel.overland.hustle-distance`
  - base speeds 15, 20, 30, and 40 feet;
  - per-hour overland hustle distances.
- `travel.overland.standard-travel-duration`
  - 8-hour land travel day;
  - 10-hour rowed-watercraft day;
  - 24-hour sailing-ship day.
- `travel.overland.terrain-distance-factor`
  - source-specific highway, road, and trackless terrain multipliers.
- `travel.overland.forced-march-check`
  - Constitution check;
  - DC 10 + 1 per extra hour beyond eight;
  - failure consequence key `forced-march-damage`.
- `travel.overland.mount-vehicle-distance`
  - source table per-hour and per-day rates for mounts and vehicles.
- `travel.environment.hampered-movement`
  - source-native distance multipliers for obstruction, surface, and poor visibility;
  - obstruction values: none x1, moderate x3/4, heavy x1/2;
  - surface values: none x1, bad x1/2, very bad x1/4;
  - poor visibility x1/2;
  - multiple applicable penalties multiply together exactly as specified by the source;
  - semantic is `distance-multiplier` at `movement-distance` scale.
- `travel.water.downstream-current-speed-bonus`
  - typical downstream current contribution: 3 miles per hour.
- `travel.water.guided-downstream-float-duration`
  - source-defined additional 14 hours of guided downstream floating;
  - reviewed 3e applicability is `raft-or-barge` and `keelboat`.

Extended-hustle damage/fatigue and mounted endurance effects are not applied by these rate definitions because they require generalized consequence/state handling.

### SRD 3.5e — Overland Movement

The same stable mechanic categories are projected where the semantic role is shared, while source-specific definitions are retained.

Important material differences are preserved:

- forced march uses DC 10 + 2 per extra hour;
- terrain/route rows differ from the 3e table;
- mount and vehicle table rows differ.

The hourly hustle-distance table is identical for the reviewed base speeds, so `travel.overland.hustle-distance` can merge provenance when both editions are effective.

Additional reviewed mechanics:

- `travel.environment.hampered-movement`
  - required booleans: `difficult-terrain`, `obstacle`, and `poor-visibility`;
  - each applicable condition contributes x2 additional movement cost;
  - multiple conditions multiply together, producing x1, x2, x4, or x8;
  - semantic is `movement-cost-multiplier` at `movement-space` scale.
- `travel.water.downstream-current-speed-bonus`
  - typical downstream current contribution: 3 miles per hour.
- `travel.water.guided-downstream-float-duration`
  - additional 14 hours of guided downstream floating;
  - reviewed 3.5e applicability is `raft-or-barge`, `keelboat`, and `rowboat`.

The 14-hour duration is exposed directly instead of hard-coding the source's derived +42-mile example. This preserves the duration rule independently of the actual current speed used by a consumer.

The stable `travel.environment.hampered-movement` key intentionally has structurally different 3e and 3.5e definitions. 3e reduces movement distance, while 3.5e increases movement-space cost. If both definitions are independently effective, Rules Core reports the mechanic as conflicted rather than pretending those semantics are interchangeable.

`travel.water.downstream-current-speed-bonus` is structurally identical across the reviewed 3e and 3.5e sources and can merge provenance. The guided-float definition differs because the reviewed 3e source does not include rowboats in that rule while the reviewed 3.5e source does; simultaneous effective definitions therefore remain distinguishable/conflicting.

No edition precedence is invented.

### SRD 3.5e — Getting Lost

The reviewed wilderness source projects three mechanics. Rules Core exposes checks and consequences, while Hex Crawl retains persistent lost/direction state.

`travel.navigation.avoid-getting-lost` uses the highest applicable source DC:

- Moor or hill with map: DC 6
- Mountain with map: DC 8
- Moor or hill without map: DC 10
- Poor visibility: DC 12
- Mountain without map: DC 12
- Forest: DC 15

The check uses stable competency concept key `skill.survival` and cadence `once-per-hour-or-portion`.

`travel.navigation.recognize-lost`:

- required input: `random-travel-hours`;
- Survival DC: `20 - random-travel-hours`;
- cadence: once per hour of random travel;
- failure consequence key: `remain-unaware-lost`.

`travel.navigation.set-new-course`:

- required input: `random-travel-hours`;
- Survival DC: `15 + (2 * random-travel-hours)`;
- failure consequence key: `choose-random-direction`.

The consequence keys are descriptive outputs only. Rules Core does not choose or persist random directions, set lost state, or clear lost state.

The source-specific +2 bonus from sufficient Knowledge (geography/local) ranks and the +4 Survival-check bonus when conditions suddenly improve while lost are not encoded as DC changes. They are check modifiers and require a reusable modifier/capability composition contract.

### SRD 5.2.1 — Difficult Terrain

`travel.environment.difficult-terrain-movement-cost` is a movement-cost multiplier of 2 at movement-space scale.

It is deliberately not projected as an overland distance multiplier. Rules Core does not infer expedition-scale equivalence from a movement-space rule.

### SRD 5.2.1 — High Altitude

`travel.environment.high-altitude-travel-time-cost` exposes:

- a 10,000-foot threshold;
- a travel-time-cost multiplier of 2;
- explicit required boolean `subject-to-high-altitude-travel-cost`.

The applicability boolean prevents Rules Core from guessing whether a creature is exempt, acclimated, native to altitude, or otherwise outside the rule's applicability.

## Source extension shape

Imported or reviewed translators can provide structured travel mechanics under:

`_rulesCore.travel.mechanics`

Each entry uses the same `TravelEnvironmentMechanicDefinition` contract returned by the consumer API. This is the preferred path for future source adapters. The exact SRD projections are reviewed compatibility projections for source material whose imported shape does not yet carry that extension.

## Relationship to existing systems

The foundation reuses:

- immutable Source Layer revisions;
- canonical source/work/edition attribution;
- Rule Concepts and global/Campaign decisions;
- effective-rule merge patches and published snapshots;
- effective resolution state;
- stable Character competency concept keys for travel checks;
- the shared resolved-rules pagination reader.

It does not introduce a database table or migration.

Character Sheet can continue to own character movement modes and character state. Hex Crawl can combine explicit expedition inputs with Rules Core quantities, factors, and check definitions without moving expedition state into Rules Core.

## Deferred mechanics for a later cycle

The remaining source-supported areas require additional generalized contracts or belong to downstream expedition state:

- extended-hustle endurance consequences, including damage progression and fatigue state;
- forced-march damage/fatigue application after a failed check;
- mounted hustle endurance, mounted forced-march automatic failure, lethal damage, and resulting fatigue;
- 3.5e Knowledge (geography/local) rank-based navigation modifier;
- the 3.5e +4 set-new-course bonus when conditions suddenly improve while lost;
- automatic composition of Navigator's Tools or other tool/competency proficiency into navigation checks;
- persistent lost state, deviation, random-direction selection, unmistakable-landmark recovery, destination recovery, and other navigation state transitions;
- weather and visibility effects that modify checks, attacks, perception, damage, or other systems beyond the directly modeled movement multipliers;
- environmental damage, saves, conditions, exposure, exhaustion/fatigue, and similar effects requiring a generalized effect/consequence contract;
- encumbrance and carrying-state composition with Character movement capabilities;
- difficult-terrain exceptions and special movement capabilities such as climb/swim exemptions;
- rider/load occurrence state and character-specific mount capability composition;
- significant-current upstream restrictions, towing, variable current speed, wind, crew/passenger constraints, and other vehicle/water-travel interactions not expressible as the current rates/durations;
- source-defined travel responsibilities such as foraging, mapping, and scouting where the current corpus lacks a reviewed normalized mechanic contract;
- 5e/5.5e character features that modify travel, which need character-capability/modifier composition;
- additional B/X, AD&D, or other source mechanics not currently present in the Rules Core source corpus.

These omissions are explicit. Downstream tools should continue to accept DM-authored/resolved inputs rather than substitute assumed defaults.

## Known source gaps

- The checked-in 5e SRD corpus does not currently expose one reviewed top-level general Travel Pace entity comparable to the 3.x overland movement rules, so no 5e/5.5e universal expedition pace table is fabricated.
- 5e Ranger Natural Explorer travel interactions should enter through a reviewed character-capability/modifier contract.
- Navigator's Tools and similar competency interactions exist in the universal competency model, but travel does not yet compose them automatically into navigation checks.
- The 3.5e Knowledge (geography/local) modifier and improved-conditions +4 modifier remain check-modifier composition gaps.
- Weather, exhaustion/fatigue, encumbrance, special movement capabilities, and mount endurance still require reusable effect/capability hooks.
- The reviewed sources provide a typical 3-mile-per-hour downstream current contribution and 14 additional guided floating hours. Non-typical current speed, significant-current upstream restrictions, towing, and navigation/handling state remain future inputs/contracts.
- 3e and 3.5e overland movement differ materially. Rules Core does not select one merely because it is newer.
- DM-authored travel rates remain valid Hex Crawl inputs and are not overwritten by the source-backed catalog.

## Next Hex Crawl integration step

Hex Crawl should consume the effective catalog by stable mechanic key and use only mechanics whose `canResolve` value is true.

A first integration can:

1. keep the current explicit party movement reference as the base;
2. resolve `travel.overland.terrain-distance-factor` from current terrain/route when available;
3. use `travel.navigation.*` mechanics for source-backed DC/cadence/consequence metadata while Hex Crawl owns navigation rolls and lost-state transitions;
4. use `travel.overland.forced-march-check` for source-backed endurance DCs;
5. use `travel.overland.hustle-distance` only when the expedition explicitly chooses the source-defined hustle mode;
6. resolve `travel.environment.hampered-movement` only when the active source definition is unambiguous and the downstream procedure has the required source-specific inputs;
7. use `travel.water.downstream-current-speed-bonus` and `travel.water.guided-downstream-float-duration` when the expedition explicitly uses the reviewed river-watercraft procedure;
8. surface `requires-adjudication` or `conflicted` mechanics as Rules Core configuration issues rather than choosing a profile in Hex Crawl.

Character-dependent modifiers, generalized consequences, and remaining vehicle/environment interactions should be layered in only after their reusable contracts exist.
