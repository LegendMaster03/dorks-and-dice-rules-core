# Pillar 2 Workstream C — Mechanical projection coverage (2026-11-19)

Base: `feature/pillar2-power-evaluation-prototype-20261114` at
`6b17cdac7d5d93bdccac0038aa179da3004ad2f6`.
Working branch: `feature/pillar2-mechanical-projection-coverage-20261119`.

## Audit inventory

This is an audit of the **native Origin read-only projectors** at the base
commit. A resolved Origin does not authenticate a saved Character Sheet,
perform combat/rest actions, or authorize a new biological combination.

| Existing reader | Coverage at base | Boundary / gap |
| --- | --- | --- |
| `NativeOriginIntrinsicProjector` and `NativeOriginApprovedMixedIntrinsicProjector` | Direct and explicitly authorized mixed creature type, size, walking speed | Baseline and contributor verification, not arbitrary current transformations |
| `NativeOriginVisionProjector` | Canonical low-light perception rule | Only dim-to-bright perception; no line of sight or magical-darkness inference |
| `NativeOriginDarkvisionProjector` | Authored native range and level-unlocked extension | No general new sense grammar or runtime lighting engine |
| `NativeOriginTremorsenseProjector` | Passive and activated native modes, limits and use formulas | No activation, spent-use count or sensed-target resolution |
| `NativeOriginMovementModeProjector` | Canonical climb, swim, burrow or other authored movement-mode entries | No implicit speed stacking or action movement |
| `NativeOriginPhysiologyProjector` | Shared Amphibious and Goliath Altitude Acclimation | Other physiological or conditional traits still require review |
| `NativeOriginRestSurvivalProjector` | Elf Trance and Orc Relentless Endurance | No actual rest, recovery, HP mutation or trigger execution |
| `NativeOriginHitPointProgressionProjector` | Dwarven Toughness per-character-level maximum-HP entitlement | Combines preview totals only against resolved native character values |
| `NativeOriginSkillProficiencyProjector` | Native Human Skillful selected proficiency | No new proficiency selection, duplicate-rank stacking or arbitrary feat authorization |
| `NativeOriginSkillRankEntitlementProjector` | Native Human Adaptable Learning trajectory | No authorized rank allocation, caps or independently resolved Feat infrastructure |
| `NativeOriginInnateSpellProgressionProjector` | Native spell unlock, at-will and free-use preview for supported definitions | No cast execution; open spell lists and unsupported casting rules defer |
| `NativeOriginInnateSpellResourceLedger` and resource dependency binder | Existing shared-ownership and spell use allowance evidence | No fabricated pool, unauthorized casting or arbitrary rest event |
| Saving-throw advantage definitions | **No dedicated generic Origin projector** | Elf Fey Ancestry, Dwarven Resilience and Gnomish Cunning have authored structured entries |
| Fixed native skill-bonus definitions | **No dedicated generic Origin projector** | Elf Keen Senses and Halfling Keen Hearing have authored `skillBonuses` maps |
| Damage-resistance / defense execution | Some authoring and reconciliation evidence; no universal execution consumer | Dwarf, Tiefling, Goliath and bound Dragonborn resistance require separate policy and runtime work |
| Other triggered/conditional traits | Selected special-case readers, but no universal executable trait engine | Examples include Dragonborn Breath Weapon, Goliath Large Form, Orc Adrenaline Rush / Savage Attacks and condition-dependent skill advantage |

## Before-and-after capability matrix

| Capability | Before | After this branch |
| --- | --- | --- |
| Native direct fixed `savingThrows.advantageAgainst` | Authored and constructible, not projected generically | Independent read-only entitlement with exact scope |
| Native direct `savingThrows.advantageAbilities` with `scope: magic-only` | Authored and constructible, not projected generically | Explicit abilities and magic-only restriction retained |
| Native `skillBonuses` mapped to real native skill IDs | Authored, but no dedicated Origin bonus view | Independent sourced fixed bonus entries |
| Authorized mixed sources | Existing native foundation and evidence checks | Same effects from selected mixed contributors, no Species dispatch |
| Same skill/condition supplied by more than one source | No new projection | Entries remain separate; no automatic addition, precedence or stacking |
| Changed native payload / missing evidence | Existing validation varies by consumer | Both authored and effective shapes checked; fail closed for supported effects |
| Unsupported parameter, resource owner, or per-use choice | No new projection | Explicitly unresolved; no independent pools or free choices |
| Level-gated passive grammar | Not supported for this subset | Explicitly unresolved rather than becoming an immediate entitlement |
| Combat, saving throws, skill checks and HP mutation | No new gameplay execution | Unchanged |

## Implemented scope

`NativeOriginPassiveEntitlementProjector` consumes the existing direct or
approved-mixed foundational projector, then reads each **manifested** native
trait's canonical `origin-trait.trait` payload. It verifies the grant identity,
source category, authorization/effective evidence, candidate offer, one
manifestation per candidate, unmodified payload, and unsupported parameters or
resources before returning any output.

For saving throws, the bounded grammar is a nonempty
`advantageAgainst` array, or `advantageAbilities` restricted to the six
ability identifiers and `scope: magic-only`. For fixed skills, it is a
nonempty object of real catalog `skill.*` identifiers with positive integral
bonuses. Full canonical effects are retained for contextual restrictions and
composite-trait evidence, including the Dwarf's poison resistance alongside
its saving-throw entitlement. The projector **does not** grant or execute that
damage resistance.

Results contain a stable source ID, occurrence path, grant ID, native trait
definition ID, contributing candidate IDs, full effect payload and independent
typed entries. No mechanism may infer an extra proficiency, effective skill
total, resource recharge, combined advantage, resolved damage, or permitted
Origin from this view.

The accepted grammar intentionally omits parameterized choices and
level-dependent traits: neither can be represented faithfully by this
creation-time fixed-passive interpretation. Such a grant fails unresolved if it
claims these supported shapes while using parameters, resource owners,
creation choices or a required character level. This does not broaden
first-party biological eligibility.

## Test coverage added

Focused xUnit tests use first-party Elf, Dwarf, Gnome, Halfling and Orc
definitions. They cover direct entitlement semantics, approved mixed selection
and unselected offers, serialized profile reconstruction, complete composite
trait preservation, invalid payload changes, fabricated candidate evidence,
substituted definition IDs, unsupported parameters, fabricated resource
ownership, incomplete construction and direct/mixed interface separation.

The tests deliberately make no live calls to a combat, roll, rest or Character
Sheet mutation service.

**Verification commands** (from repository root):

```bash
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release --filter FullyQualifiedName~NativeOriginPassiveEntitlementProjectorTests
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release
dotnet test RulesCore.slnx -c Release
python3 -m unittest discover -s tests -p 'test_character_origins_*.py' -v
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

## Independent-review follow-up: exact coverage and boundaries

The combined infrastructure **does not** fully project or execute every
characteristic. This workstream adds an intentionally bounded consumer for two
already-authored passive effect families. Other Traits may be fully present in
the Origin's canonical data without a typed mechanical consumer. Do not read
`Status="none"` as no Origin entitlements: the passive projector now preserves
canonical nonmatching native Traits in `OutsideScopeTraits` with source,
occurrence and the complete definition. They remain nonexecuted.

| Mechanical domain | Existing interpreter and definition fidelity | Remaining work / classification |
| --- | --- | --- |
| Creature type, size, walking speed | Direct and approved-mixed intrinsic projectors; authored source/occurrence retained | Only baseline, not arbitrary transformed state. Future integration infrastructure |
| Low-light vision and Darkvision | Vision/Darkvision projectors read authored passive and extension effects, level gate and provenance | Actual environment/target adjudication is runtime-dependent; no new vision rule |
| Tremorsense | Specialized projector retains canonical effect with range, contact, activation, time, uses and owning grant | Sensing targets, activation and spent uses need authenticated gameplay runtime |
| Movement modes | Movement projector returns full individual canonical trait, speed, material limitations and provenance | Does not select combined effective speed or move a creature; overlap requires explicit disposition |
| Amphibious, altitude and rest | Physiology and RestSurvival projectors retain their supported restrictions | Other physiological adaptations not generalized; HP/rest events need runtime |
| Max-HP progression | HitPointProgression projector handles per-level authored trait, provenance and level; read-only character combine | Not an HP mutation; other hit point recovery/triggers lack generic runtime |
| Skill proficiency and ranks | Existing specialized projectors retain native Human choices and advancement entitlements | Actual rank allocations, feat prerequisite and rank policy not complete |
| Innate spell progression | InnateSpellProgression and ResourceLedger handle authored unlocks, ability bindings and source-specific allowances | Unsupported open spell lists and general spell execution are not approved |
| Shared resource ownership | ResourceDependencyBinder and Origin ledger preserve source-owner links for recognized cases | No generic action/charge consumption; independent pool invention prohibited |
| Fixed saving throw advantage | **Added here**: source-neutral typed canonical advantage against named effects or magic-only ability saves | No saving throw execution; no automatic overlap/stack policy |
| Fixed skill bonuses | **Added here**: exact catalog skill IDs and numeric bonuses with complete source definitions | No composite skill total, stacking, or skill check execution |
| Damage resistance | Authored in Dwarf, Tiefling, Goliath and Dragonborn traits; reconciliation detects overlapping damage type | **Reusable read-only projection missing** for type/percent/parameter-source; implementable without new balance policy only if no stacking is inferred. Runtime damage application needs separate contract |
| Parameterized abilities | Deconstructor, selected parameters, source binder and contract validator retain some authorized binding evidence | **Generic typed projection missing** for breath damage type/area, selected aptitude and other source-bound fields. Structural projection feasible; new inheritance/eligibility rules not |
| Level-gated nonspell abilities | Specialized Darkvision and innate spell readers cover their authored paths | **Generic capability progression missing** for Goliath Large Form and other staged actions; read-only locked/available view possible with verified level but activation requires runtime |
| Conditional checks, defenses, triggers | Canonical payload retained by construction and now explicitly enumerated outside this passive reader | **Typed conditional projection missing** for Orc Menacing Presence, Powerful Build check advantage, reactions, critical triggers, Halfling Luck; some traits are prose-only and require native data authoring rather than inferred behavior |
| Active/rest/combat use pools | Specialized Tremorsense, innate spell and rest readers preserve certain entitlement formulas | General trigger gating, spending, recovery and state changes require authenticated execution; no standalone pools |
| Composition/overlaps | Existing Reconciler and ConstraintEvaluator (Workstream A owns subsequent changes) | Unresolved relationships must stay unresolved. Read-only separate entries do not establish stacking or precedence |
| Origin source admission | Native construction, contributor evidence and first-party choice evaluators | **Unchanged**. Four Halfling descriptive traits still cause unresolved direct-source evidence; no implied approval |

### Confirmed follow-up corrections

1. All saved native trait grants must resolve a valid `origin-trait`
   definition, even when the effect is outside this projector's narrow grammar.
   An unknown definition can no longer disappear into `Status="none"`.
2. The source/occurrence/`MechanicalDefinitionId` triple is checked against
   the native catalog's original `OriginTraitDefinitionId(sourceId, index)`
   mapping, including a canonical `/traits/N` path. A legitimate source ID
   alone is insufficient evidence of possession.
3. An attempt to reclassify a canonical native trait as `Intrinsic` or
   `Other`, substitute a manifestation ID, or invent a concept/slot marker
   fails closed. Mixed evidence still uses the existing
   `HasVerifiedContributingEvidence` contract, including multiple candidates
   consolidated into one actually selected effective grant.
4. Other verified native traits are returned as `OutsideScopeTraits` with
   their full canonical payload and provenance. The separate typed projections
   do not add overlapping numeric bonuses, aggregate save advantage or
   create an actionable resource pool.
5. Direct Halfling is **already unresolved** in the first-party Origin
   constructor due to prose-only traits. The earlier assumed-authorized
   fixture was corrected: direct admission remains unresolved, while
   explicitly authorized mixed Keen Hearing can be inspected without
   changing admission policy.

### Integration ownership and conflicts

Workstream A currently changes `NativeOriginConstraintEvaluator.cs`,
`NativeOriginConstructionEngine.cs` and its construction tests, but none of
Workstream C's implementation, test, documentation or branch-only workflow
files. There is no known direct path conflict. **Behavioral integration risk
remains:** Workstream C relies on A's final candidate/manifestation IDs,
consolidated source evidence, profile configuration equality and eligibility
gating. Run the full focused tests and relevant suites on the combined
integration commit before any merge approval.

## Deferred dependencies for integration review

1. A generic passive-damage-resistance and scoped defense projection requires
   a separately reviewed grammar for canonical fixed resistance, parameter-bound
   Dragonborn resistance, temporary reaction defenses and conflict disposition.
   Do not turn this into damage execution or assume additive percentages.
2. Parameterized Origin mechanics require existing parameter-source binding,
   prerequisite and selected-value evidence to survive projection without new
   downstream authorization. Workstream A owns shared reconciliation changes.
3. Goliath Large Form, breath weapons, conditional bonuses, triggered reactions
   and other active traits need authenticated character execution contracts.
   Their authored capability can be inspected, but execution is not in scope.
4. The host must authenticate the saved character and current Origin before
   consuming even a read-only projection as a playable entitlement.
5. Descriptive-only traits (such as Halfling Brave) can not safely become
   executable rules solely from prose. A first-party structured definition is
   required before generic execution semantics are considered.

No native catalog definitions, admission policies, mutation routes, reconciliation
engines, Character Sheet files, Site files or Pillar 4 code were changed.
