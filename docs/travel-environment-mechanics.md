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

Definitions use explicit units and factor semantics. In particular, a distance multiplier, movement-cost multiplier, and travel-time-cost multiplier are distinct mechanic kinds and are not silently converted into one another.

Resolution returns `resolved`, `input-required`, or `not-applicable`. Rules Layer conflicts and unresolved adjudication are preserved separately at the mechanic level.

## Reviewed source projections

The first reviewed source projection recognizes exact checked-in SRD source identities and validates characteristic source text before emitting structured mechanics. It does not parse publisher prose in Hex Crawl.

### SRD 3e — Movement

Projected mechanics:

- `travel.overland.walk-distance`
  - base speeds 15, 20, 30, and 40 feet;
  - per-hour and per-day overland walking distances.
- `travel.overland.hustle-distance`
  - base speeds 15, 20, 30, and 40 feet;
  - per-hour overland hustle distances;
  - endurance damage/fatigue from extended hustling is not applied by this rate mechanic.
- `travel.overland.standard-travel-duration`
  - 8-hour land travel day;
  - 10-hour rowed-watercraft day;
  - 24-hour sailing-ship day.
- `travel.overland.terrain-distance-factor`
  - source-specific highway, road, and trackless terrain multipliers.
- `travel.overland.forced-march-check`
  - Constitution check;
  - DC 10 + 1 per extra hour beyond eight;
  - source consequence is represented as `forced-march-damage`, while applying damage remains a higher-level game workflow.
- `travel.overland.mount-vehicle-distance`
  - source table per-hour and per-day rates for mounts and vehicles.

### SRD 3.5e — Overland Movement

The same stable mechanic categories are projected where the semantic role is shared, while the source-specific definition is retained.

Important material differences are preserved:

- forced march uses DC 10 + 2 per extra hour;
- terrain/route rows differ from the 3e table;
- mount and vehicle table rows differ.

The hourly hustle-distance table is identical for the reviewed base speeds, so the stable `travel.overland.hustle-distance` definition can merge provenance when both editions are effective.

The reviewed 3.5e source also projects mechanics that fit the existing generic contract without requiring a new effect engine:

- `travel.environment.hampered-movement-cost`
  - required booleans: `difficult-terrain`, `obstacle`, and `poor-visibility`;
  - each applicable condition contributes the source x2 additional movement cost;
  - the source-specific multiplication rule is preserved exactly, producing x1, x2, x4, or x8 from the explicit applicability inputs;
  - scale is `movement-space`, not expedition distance.
- `travel.water.downstream-current-speed-bonus`
  - source typical downstream current contribution: 3 miles per hour;
  - this is the additive current-speed contribution described by the source, not a replacement vehicle rate.
- `travel.water.guided-downstream-float-duration`
  - applies to `raft-or-barge`, `keelboat`, and `rowboat`;
  - requires explicit `guided` and `traveling-downstream` booleans;
  - resolves to the source-defined additional 14 hours of floating per day when both applicability conditions are true.

The 14-hour duration is exposed directly instead of hard-coding the source's derived +42-mile example. This preserves the duration rule independently of the actual current speed used by a downstream travel consumer.

If both editions are independently made effective under separate Rules Layer concepts, identical mechanics can reconcile at the consumer boundary while materially different mechanics become `conflicted`. No edition precedence is invented.

### SRD 3.5e — Getting Lost

The reviewed wilderness source projects three separate mechanics. Rules Core exposes their checks and consequences, while Hex Crawl retains all persistent lost/direction state.

`travel.navigation.avoid-getting-lost` exposes the source table as a maximum-applicable DC check:

- Moor or hill with map: DC 6
- Mountain with map: DC 8
- Moor or hill without map: DC 10
- Poor visibility: DC 12
- Mountain without map: DC 12
- Forest: DC 15

The check uses stable competency concept key `skill.survival` and records the source cadence as once per hour or portion of an hour.

`travel.navigation.recognize-lost` exposes the source progression for recognizing that the party is already lost:

- required input: `random-travel-hours`;
- Survival DC: `20 - random-travel-hours`;
- cadence: once per hour of random travel;
- failure consequence key: `remain-unaware-lost`.

`travel.navigation.set-new-course` exposes the source progression for choosing a new course while lost:

- required input: `random-travel-hours`;
- Survival DC: `15 + (2 * random-travel-hours)`;
- failure consequence key: `choose-random-direction`.

The consequence keys are descriptive mechanical outputs only. Rules Core does not choose a random direction, persist that direction, decide whether the party is lost, or clear lost state.

The source-specific +2 bonus from sufficient Knowledge (geography/local) ranks is not yet encoded. The source also grants +4 to the set-new-course Survival check when conditions suddenly improve while the party is lost. Both are check modifiers and require a reviewed modifier/capability composition contract rather than converting bonuses into artificial DC reductions.

### SRD 5.2.1 — Difficult Terrain

`travel.environment.difficult-terrain-movement-cost` is a movement-cost multiplier of 2 at movement-space scale.

It is deliberately not projected as an overland distance multiplier. The source rule describes movement-space cost, and Rules Core does not infer expedition-scale equivalence.

### SRD 5.2.1 — High Altitude

`travel.environment.high-altitude-travel-time-cost` exposes:

- a 10,000-foot threshold;
- a travel-time-cost multiplier of 2.

The resolver also requires an explicit `subject-to-high-altitude-travel-cost` boolean. This prevents Rules Core from guessing whether a particular creature is exempt, acclimated, native to altitude, or otherwise outside the source rule's applicability. Character/environment capability integration can resolve that input later.

## Source extension shape

Imported or reviewed translators can provide explicit structured travel mechanics under:

`_rulesCore.travel.mechanics`

Each entry uses the same `TravelEnvironmentMechanicDefinition` contract returned by the consumer API. This is the preferred path for future source adapters. The exact checked-in SRD projections exist as reviewed compatibility projections for source material whose current imported shape does not yet carry that extension.

## Relationship to existing systems

The foundation reuses:

- immutable Source Layer revisions;
- canonical source/work/edition attribution;
- Rule Concepts and global/Campaign decisions;
- effective-rule merge patches and published snapshots;
- effective resolution state;
- the existing stable Character competency concept keys for travel checks;
- the shared resolved-rules pagination reader.

It does not introduce a new database table or migration.

Character Sheet can continue to own character movement modes and character state. Hex Crawl can combine an explicit party movement reference with Rules Core factors and check definitions without moving expedition state into Rules Core.

## Deferred mechanics for a later cycle

The remaining source-supported areas are intentionally not implemented in this cycle because they need additional generalized contracts rather than isolated travel special cases:

- extended-hustle endurance consequences, including source-specific damage progression and fatigue state;
- forced-march damage/fatigue application after a failed check;
- mounted hustle endurance, mounted forced-march automatic failure, lethal damage, and resulting fatigue;
- 3.5e Knowledge (geography/local) rank-based navigation modifiers;
- the 3.5e +4 set-new-course check bonus when conditions suddenly improve while lost;
- automatic composition of Navigator's Tools or other tool/competency proficiency into navigation checks;
- persistent lost state, deviation, random-direction selection, unmistakable-landmark recovery, destination recovery, and other navigation state transitions, which remain Hex Crawl responsibilities;
- weather and visibility effects that modify checks, movement, attacks, perception, damage, or other systems beyond the directly modeled 3.5e hampered-movement multiplier;
- environmental damage, saves, conditions, exposure, exhaustion/fatigue, and similar effects that require a generalized effect/consequence contract;
- encumbrance and carrying-state composition with Character movement capabilities;
- difficult-terrain exceptions and special movement capabilities such as climb/swim-specific exemptions;
- rider/load occurrence state and character-specific mount capability composition;
- vehicle handling, crew/passenger constraints, significant-current upstream restrictions, towing, variable current speed, wind, and other vehicle/water-travel interactions not representable as the current source-backed rates/durations;
- source-defined travel responsibilities such as foraging, mapping, and scouting where the checked-in corpus does not yet provide a reviewed normalized mechanic contract;
- 5e/5.5e character features that modify travel, which need character-capability/modifier composition rather than copying feature prose into the generic evaluator;
- additional B/X, AD&D, or other source mechanics that are not currently present in the Rules Core source corpus.

These omissions are explicit. Downstream tools should continue to accept DM-authored/resolved inputs rather than substituting assumed defaults.

## Known source gaps

These are intentionally unresolved rather than filled with invented defaults:

- The checked-in 5e SRD corpus does not currently expose one reviewed top-level general Travel Pace entity comparable to the 3.x overland movement rules, so no 5e/5.5e universal expedition pace table is fabricated.
- 5e Ranger Natural Explorer contains travel interactions, but those benefits should enter through a reviewed character-capability/modifier contract rather than be copied into the generic travel evaluator.
- Navigator's Tools and similar tool/competency interactions are already represented in the universal competency model, but the travel contract does not yet compose tool proficiency automatically into navigation checks.
- The 3.5e Knowledge (geography/local) modifier and improved-conditions +4 modifier remain check-modifier composition gaps.
- Weather, visibility, exhaustion/fatigue, encumbrance, difficult-terrain exceptions, mount endurance, vehicle handling, and special movement features require additional source-specific profiles or interaction hooks.
- The 3.5e source supplies a typical 3-mile-per-hour downstream current contribution and 14 additional guided floating hours; actual non-typical current speed, significant-current upstream restrictions, towing, and navigation/handling state remain consumer or future-contract inputs.
- 3e and 3.5e overland movement differ materially. Rules Core does not select one merely because it is newer.
- DM-authored travel rates remain valid Hex Crawl inputs. They are not overwritten by the source-backed catalog.

## Next Hex Crawl integration step

Hex Crawl should consume the effective catalog by stable mechanic key and use only mechanics whose `canResolve` value is true.

A first integration can:

1. keep the current explicit party movement reference as the base;
2. resolve `travel.overland.terrain-distance-factor` from the current terrain/route when available;
3. use the `travel.navigation.*` mechanics to obtain source-backed DC/cadence/consequence metadata while Hex Crawl still owns navigation rolls and lost-state transitions;
4. use `travel.overland.forced-march-check` for source-backed endurance DCs;
5. optionally use `travel.overland.hustle-distance` where the expedition explicitly chooses the source-defined hustle mode, without assuming the unimplemented endurance consequences;
6. resolve `travel.environment.hampered-movement-cost` when a 3.5e movement-space procedure explicitly supplies the three applicability booleans;
7. use `travel.water.downstream-current-speed-bonus` and `travel.water.guided-downstream-float-duration` when the expedition explicitly uses the reviewed 3.5e river-watercraft procedure;
8. surface `requires-adjudication` or `conflicted` mechanics as Rules Core configuration issues rather than choosing a profile in Hex Crawl.

Character-dependent modifiers, generalized consequences, and the remaining vehicle/environment interactions should be layered in only after their reusable contracts exist.