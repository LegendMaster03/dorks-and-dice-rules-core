# Pillar 2 — Native Tiefling Parent-to-Ancestry Spellcasting Binding

**Date:** November 15, 2026  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**PR:** [#157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157) (draft; unmerged)

## Existing native semantics

The playable Tiefling Ancestries are Infernal, Chthonic, and Abyssal. Each leaf separately authors a persistent `spellcastingAbilityChoice` of Intelligence, Wisdom or Charisma, which governs **all of that Ancestry's native spells**. The required base Species `species.tiefling` is nonselectable alone. Its shared `trait.otherworldly-presence` grants Thaumaturgy at level 1, at will, and states `spellcastingAbilityFrom: "selected-tiefling-ancestry"`.

Before this increment, the shared base trait described that choice only in text and a non-executable reference. All nine leaf spells also lacked durable `originBindings`, despite their respective selected Ancestry's ability choice. Direct and mixed constructions could therefore retain unbound spell grants without reliable downstream source metadata.

## Source-neutral implementation

`NativeOriginDependentBinder` now accepts a second native, explicitly authored `manifested-intrinsic` scope:

- `scope: "source"`: an existing exact-source dependency (as for High/Wood/Drow Elf spells), with selected **contributor ownership** required.
- `scope: "descendant"`: an ability choice may be resolved from a **selected native Ancestry descendant** of the trait's own native source, if the selected grant comes from the same actual contributor. The binder traverses catalog-authoritative Ancestry `parent` links, requires each intermediate entity to be an Ancestry, stops only at a valid Species/Ancestry ancestor, and rejects unrelated sources, missing parents, cycles and excessive depth. Name similarity or a claimed source identity never substitutes for the native ancestry chain.

The Tiefling base Species trait now authors:

```json
"originBindings": {
  "spellcastingAbility": {
    "lookup": "manifested-intrinsic",
    "intrinsicSlot": "spellcastingAbility",
    "scope": "descendant"
  }
}
```

Every ancestry-specific innate spell retains the same `originBindings` structure but uses `"scope": "source"`. The original base `spellcastingAbilityFrom` authoring remains intact for compatibility and to preserve the selected gameplay meaning.

The binder reuses the existing `SharesContributor` evidence gate. Two different native parent contributors of the same canonical ancestry **cannot borrow each other's spellcasting ability**. An ancestry-level choice does not retroactively select absent traits or award a new spell.

## Original spell courses preserved

| Origin | Level 1 | Level 3 | Level 5 |
| --- | --- | --- | --- |
| Shared Tiefling base | Thaumaturgy at will | — | — |
| Infernal | Fire Bolt at will | Hellish Rebuke, one free cast per Long Rest | Darkness, one free cast per Long Rest |
| Chthonic | Chill Touch at will | False Life, one free cast per Long Rest | Ray of Enfeeblement, one free cast per Long Rest |
| Abyssal | Poison Spray at will | Ray of Sickness, one free cast per Long Rest | Hold Person, one free cast per Long Rest |

The level 3/5 spell traits preserve independent free uses and `slotCasting: true`. Infernal Hellish Rebuke retains `freeCastSpellLevel: 2`. There is **no** new slot execution, spell-resource consolidation, casting-action implementation, conditional stacking permission, or per-level projection engine in this increment. The original traits and entire level trajectory are committed by their selected effective grant.

## Saved parameter validation

`NativeOriginContractValidator.ReviewProfile`, **when given the construction proposal**, rebinds canonically authored `manifested-intrinsic` fields against that proposal. A serialized effective grant cannot change or omit its chosen spellcasting ability without `profile-intrinsic-binding-invalid`. This is a shape and consistency check, **not production authentication** of arbitrary caller-provided profiles or DM authority.

Historical nested grants with an already manifested parameter can legitimately retain that parameter even if their ancestor's selected ability is no longer separately present as a new descendant grant. Those cases still require the previously defined authenticated parent-profile ID/revision verification; this increment does **not** invent a replacement historical trust source.

The validator's existing `manifested-trait-choice` integrity review for Giant Ancestry Aptitude remains distinct and unchanged.

## Real-catalog validation

`tests/RulesCore.Mechanics.Tests/NativeOriginTieflingSpellBindingTests.cs` exercises:

1. All three native selectable Fiendish Ancestries with each of the three allowed spellcasting abilities (nine direct combinations).
2. The parent base's Thaumaturgy binding and every leaf's full 1/3/5 level authoring, independent one-use free cast fields and Infernal Hellish Rebuke's second-level free casting.
3. JSON persistence of the already-selected parameter.
4. Missing Ancestry ability: direct creation remains incomplete and does not publish unbound spell grants.
5. Scoped DM-authorized mixed donor construction, including cross-contributor failure with *both different and identical* Ancestry IDs.
6. Correct selected descendant ability on the same contributor binding both the shared Species spell and its leaf spell.
7. Forged or omitted saved spellcasting-ability parameters on both parent and leaf grants rejected by `NativeOriginContractValidator.ReviewProfile`.

Tests use the **existing** native construction, canonical catalog and rules. Mixed offers still require exact test-only DM authorizations because first-party biological inheritance of these spells has not been approved.

## Explicitly deferred

- Production parent-profile lookup and revision authentication; no public client self-authorized bypass.
- Character Sheet site integration and execution of spellcasting ability modifiers, slots, free-use counters, recharge and unlock timing.
- Cross-source spell-course consolidation, selection cardinality, cost/allocation admission and future mixed-character specializations.
- General first-party biological opt-in for Tiefling spells, new Species eligibility or any nonbiological origination.
- Form-changing or persistent supernatural mechanics, which remain Pillar 4 and independent of underlying biological inheritance.

No authoritative numerical price or capacity has been assigned. Keep the existing branch and PR draft; do not merge or deploy.
