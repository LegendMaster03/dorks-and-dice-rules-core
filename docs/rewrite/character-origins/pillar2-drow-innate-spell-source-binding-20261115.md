# Pillar 2 — Drow Innate Spellcasting Ability: Source-Bound Dependency

**Date:** November 15, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft, no merge)

## Catalog authority and design intent

The playable `ancestry.elf.drow` already declares a character-creation `spellcastingAbilityChoice` of Intelligence, Wisdom or Charisma. Its historical native review and associated Origin pipeline require that one chosen ability govern granted Drow ancestry spells; the original three Drow spell traits did not yet express the binding.

The separately stored Drow genes `trait.dancing-lights`, `trait.faerie-fire`, and `trait.darkness` now specify the same canonical `originBindings` mechanism already used by High Elf and Wood Elf traits. Their current files live directly under `data/rulesets/first-party-001/native/origination/biological/genes/`, named by their stable IDs:

```json
"originBindings": {
  "spellcastingAbility": {
    "lookup": "manifested-intrinsic",
    "intrinsicSlot": "spellcastingAbility",
    "scope": "source"
  }
}
```

This exact binding is attached to all three separately authored Drow traits:

| Native trait | Spell | Character-level availability | Preserved semantics |
| --- | --- | --- | --- |
| `trait.dancing-lights` | Dancing Lights | Level 1 | At-will casting |
| `trait.faerie-fire` | Faerie Fire | Level 3 | One free use per Long Rest; also spell-slot casting |
| `trait.darkness` | Darkness | Level 5 | One free use per Long Rest; also spell-slot casting |

The binder does **not** alter spell level, learn/cast behavior, slot rules, action economy, or progression timing; it merely makes the previously authored choice a required, durable parameter of each completed spell grant.

## Isolation and provenance

`NativeOriginDependentBinder` resolves the exact selected intrinsic `spellcastingAbility` from the **same native source occurrence** as the spell trait. This ensures a mixed Drow descendant can not satisfy a spell from contributor B using contributor A's spellcasting ability without an explicitly chosen ability from that same donor source. It also prevents blindly adopting one global spellcasting stat or regenerating new choices at each spell unlock.

The method is source-neutral, not Drow-specific runtime branching. First-party biological eligibility of the Drow spells is **not** expanded. In mixed tests, the selected spell and source intrinsic require exact campaign-scoped DM authorization; direct Drow remains a conventional valid choice. A source's spellcasting ability choice is persistent across the whole selected spell course.

## Real-catalog tests

`tests/RulesCore.Mechanics.Tests/NativeOriginDrowSpellBindingTests.cs` covers a direct Drow with each of the three possible spellcasting abilities, including all native spell-level/free-use/slot-casting fields and JSON persistence of the chosen source parameter. It tests that an unselected ability makes the direct course incomplete, that a permitted mixed Drow spell binds to its own contributor, and that contributor B's spell fails with `origin-binding-choice-missing` when only contributor A supplies the selection.

The tests use the existing `NativeOriginConstructionEngine` and standard `NativeOriginDependentBinder`. No separately stored or independently mutable spellcasting stat was added.

## Remaining boundaries

- A source-neutral, read-only spellcasting projection with level-gated activation and distinct resource ownership is still required. The persisted Origin grant retains the native future unlock but does not itself execute spellcasting.
- Other spell systems require explicit reviews: Tiefling's base `Otherworldly Presence` inherits the *selected ancestry* ability across a parent/leaf source boundary; High Elf's Wizard cantrip selection and replacement, Forest Gnome Animal Speech, Drow base choice alignment and others need their own validated models.
- No blanket rule that all innate spells or all traits are biologically inheritable was introduced. Mixed contributor source eligibility is unchanged.
- Production authentication of parents and Character Sheet spellcasting/ability integration remains outstanding. Temporary-form application is separate from underlying inheritance, and no site files were changed.

This increment does not approve a new spellcasting rule, new feat eligibility, allocation budgets, or production biological access.


## Regression discovered during implementation

The first cross-contributor Drow test exposed a source-neutral binder defect: `manifested-intrinsic` originally matched only the canonical source reference. Two separate Drow contributors using `ancestry.elf.drow` could therefore share one contributor's selected spellcasting ability without that contributor actually owning the spell. `NativeOriginDependentBinder` now additionally requires matching selected **contributor evidence** through the existing `SharesContributor` helper. Same-native-source identity is no longer mistaken for same-donor ownership.

The preexisting Drow Darkvision/Sunlight Sensitivity relationship test also selected Dancing Lights without any spellcasting ability. It now explicitly selects the same Drow contributor's Wisdom intrinsic and supplies its scoped DM eligibility. This preserves the original test's coverage of the Darkvision drawback rather than weakening the new spell dependency.

These are fixes to an existing binding authorization gap and its dependent fixture, not new permissions for cross-lineage trait substitution or biological transmission.
