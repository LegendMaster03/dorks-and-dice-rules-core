# Pillar 2 Workstream A — characteristic source/reconstruction round-trip audit

**Baseline:** `feature/pillar2-power-evaluation-prototype-20261114` (commit `4129eb4d0a348ebb01f4e7494afdcdcb83506762`)  
**Scope:** native deconstruction, source-neutral reconstruction checks and synthetic malformed/homebrew evidence. No mechanical projector, Site, Character Sheet, acquisition policy or balancing rules are changed.

## Inventory and assertions

The current first-party catalog contains **nine conventional Species roots** (Dragonborn, Dwarf, Elf, Gnome, Goliath, Halfling, Human, Orc, Tiefling) and **34 native Ancestry nodes**. The nine roots include three nonselectable foundation sources. The Ancestry inventory includes nonselectable Dragonborn families and five gem Dragonborn leaves. The regression enumerates all 43 native source nodes, including those used only as parent components, to check exact source-to-candidate occurrence coverage and deterministic discovery.

A separate construction regression exercises **every playable direct configuration** (currently 32: six directly selectable Species and 26 selectable Ancestry leaves) using the actual catalog and common construction pipeline. It compares repeated construction results, source-qualified identities, every effective grant's candidate provenance and complete raw selected trait JSON (which carries nested effects, resource clauses and level progression). It does **not** equate a deterministic partial construction with a completed playable character: unresolved choices and overlaps remain unresolved.

A compact synthetic catalog covers missing parent versus true cycle (both direct and contributor paths); malformed source bindings; nonnumeric authored choice cardinality; full preservation of unfamiliar nested effect JSON; and untrusted custom candidate payload and parameters. No synthetic entry is installed as first-party content.

## Defects repaired

1. **Spurious cycle diagnostic.** In both direct ancestry and contributor traversal, the previous code had already inserted a missing parent into the visited set before discovering it did not exist. The post-loop heuristic then reported a cycle as well as an unavailable parent. Repetition/limit is now detected explicitly while traversing. Genuine ancestry cycles remain invalid.
2. **Silent malformed authoring fallback.** A trait whose `originBindings` exists but is not an object was previously treated as having no bindings, permitting implicit manifestation of an incomplete mechanic. An authored `choiceCount` that could not be parsed as a nonnegative integer was likewise treated as an absent count and defaulted to one. Both now preserve the complete source trait as evidence, attach unresolved diagnostics and suppress its automatic effective grant. Absence of these optional fields retains its established behavior.

## Losses and decisions deliberately not resolved

- **Dependent choices:** Human size/skill/feat, Elf or Tiefling spellcasting ability, and other authored selections still require player decisions and appropriate trusted catalog membership. The regression does not invent answers.
- **Conflicting physical values:** Wood Elf's base 30-foot versus ancestry 35-foot walking speed remains an explicit unresolved relationship; no default parent/descendant override is introduced.
- **Multiple source interactions:** overlapping canonical senses, movement modes, resistances and resource ownership still require actual authored resolution evidence where applicable. The tests compare faithful source evidence, not unrestricted combination.
- **Source-specific semantic fields:** authored `damageType`, `breathWeaponArea`, movement-mode speed fields and other fields may be binding inputs rather than standalone characteristics. This suite checks complete normalized trait payloads and offers, but does not claim that every such field is an independently manifested trait.
- **Unsupported or unfamiliar effects:** raw nested effect and provenance survive deconstruction. Their execution or migration requires a separate authorized mechanical consumer; preserving the JSON alone does not establish executable equivalence.
- **Origin completion:** these tests cannot certify game balance, full first-party biological eligibility, full playable Character Sheet persistence or conversion equivalence. None of those policies is newly authorized.

## Verification commands

```bash
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release --filter FullyQualifiedName~NativeOriginCharacteristicRoundTripTests
dotnet test RulesCore.slnx -c Release
python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```

Record execution results separately from static/source inspection. The GitHub workflows as currently authored run on pushes to `main`, pull requests targeting `main`, or explicit manual dispatch, rather than ordinary pushes to this feature branch.
