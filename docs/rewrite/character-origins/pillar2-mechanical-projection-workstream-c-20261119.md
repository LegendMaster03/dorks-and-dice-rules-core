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

## Verification status at handoff

- First-party source definitions were independently re-read from the base
  commit. All five expected saving-throw/skill-bonus shapes match the
  intentionally bounded grammar; the native `skill.listen` and `skill.spot`
  identifiers exist in `skills.json`.
- Self-review found and tightened the rejection of fabricated
  `ParameterSources`, `SelectedParameters`, `Choices` and mismatched
  manifestation values. Additional focused adversarial tests were committed.
- A lexical delimiter check of the authored C# projector and focused test
  source found no unbalanced brackets or unterminated string/comment regions.
  This **is not compilation or an executed unit test**.
- **C# focused tests, full Mechanics suite, full solution suite and Python
  suites have not been executed**: the available environment lacks `dotnet`,
  can not clone the repository over the network, and the existing GitHub
  Actions C# workflow runs on `main` pushes and pull requests rather than
  this isolated worker branch. No CI status is reported for the branch head.
- This increment must not be merged until a .NET 10 runner compiles it and
  executes the focused and full relevant suites, with any failures addressed.
  The test commands above are the exact intended commands.

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
