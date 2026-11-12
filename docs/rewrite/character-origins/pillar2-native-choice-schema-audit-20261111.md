# Pillar 2 — general native choice-schema audit

**Date:** 2026-11-11  
**Status:** Read-only, source-neutral choice-authoring evidence. The output is not a complete or authoritative character-creation schema, trait grant, automatic choice, validated custom-Heritage entitlement, or finished Origin.

**Tool:** `tools/character_origins_choice_schema.py`  
**Tests:** `tests/test_character_origins_pillar2_choice_schema.py`

## Why this is needed

The general characteristic/identity model preserves each complete authored trait and its source provenance. The [whole-profile dependency study](pillar2-whole-profile-dependency-closure-20261111.md) can evaluate explicitly declared synthetic choice rules, but the current native source corpus expresses **different kinds of choices** using different fields and timing semantics.

A universally reconstructed Origin must not mistake an on-use attack option for an inherited permanent trait, a source-selection control for an inherent mechanical property, or a dependent spellcasting reference for another independent choice. A player-allocated resource and an open catalog entitlement cannot be checked as if they were a fixed finite menu.

This audit is a **classification step before** a first-party choice/validation schema can be promoted. It is not permitted to invent source-specific rules or assume that every authored trait has already been selected in a mixed Heritage.

## Recognized authoring patterns

| Native pattern | Example | General classification and limit |
| --- | --- | --- |
| `sizeChoice` in a Species | Human Small or Medium | One `creature.size` intrinsic choice, not two grants |
| `ancestrySelection` on a Species | Tiefling required ancestry leaf | **Source-selection control**, not an inheritable biological trait; mixed contributors need separate treatment |
| `spellcastingAbilityChoice` on an Ancestry | High Elf Intelligence/Wisdom/Charisma | Finite ability choice; **exact timing remains unspecified** unless explicitly authored |
| `chooseFrom` + `choiceCount` on a trait | Human Skillful | Open skill catalog entitlement, requires authoritative catalog lookup and permission validation |
| `featCategory` + `choiceCount` | Human Versatile | Open Origin feat catalog entitlement, not a free arbitrary trait |
| `spellList` without a fixed `spell` | High Elf Arcane Cantrip | Spell list selection, requiring actual spell catalog/qualification and replacement semantics |
| `allocation: player-choice` | Human Adaptable Learning | Rank allocation under advancement and skill-rank limits, not a simple pick-one benefit |
| `abilityChoice.from` | Goliath Giant Ancestry Aptitude | Explicit finite governing-ability options with their authored timing and per-character scope |
| `options` + `choiceTiming: on-use` | Metallic Breath Weapon | **Use-time action choice**, which must not become a mandatory birth-time choice or a separate passive inherited trait |
| `spellcastingAbilityFrom` | Tiefling Otherworldly Presence | **Derived binding** to an earlier choice, not a new independent ability selection |

All records preserve the exact authored field value and source-qualified occurrence ID. Their classification is generic by field semantics, not by Species name or ancestry pair.

The report includes `unmodeledEvidenceIds` for unrecognized choice fields or unresolved source bindings and `unresolvedTimingEvidenceIds` for choices whose selection timing has not been authored. Neither list is silently omitted for convenience. Only declared finite option sets can be represented without a catalog resolver.

## Scope and limitations

- **Corpus-wide audit:** Reads current native Species/Ancestry records, including unselectable family nodes, without fixed record counts. An optional direct-source lineage view keeps a shared ancestor represented once.
- **No inferred grants:** All observations remain `selection: not-selected` and `authority: not-authorized`. Authoring a choice does not establish that a mixed character receives the corresponding trait.
- **No new special Species handling:** Recognizes common authored fields across any native Species/Ancestry; synthetic homebrew records can introduce the same choice patterns without code changes.
- **No assumed timing:** A finite list whose `when` is unspecified remains a finite *potential* choice with an explicit timing gap. Goliath `goliath-ancestry-selection` and Metallic `on-use` remain separate.
- **No global ancestry restriction:** The source-level Tiefling ancestry selection is an observed standard-source control, not an automatic prohibition on mixed Species or custom Heritage.
- **No automatic resource merging:** The audit does not decide whether repeated choice-bearing traits share use pools, independent selections or prerequisites.
- **No silent schema promotion:** Future unknown choice fields remain review evidence. A complete first-party choice schema must formally describe requirements, timing, eligibility, persistence, duplicate selection, advancement and grant identity.

## Relationship to the three-stage Origin workflow

**Deconstruct/Assemble:** Preserve choice authoring and source occurrence provenance alongside intrinsic properties and complete trait definitions.

**Reconcile/Manifest:** Only after the proposed characteristics and their applicable rules are known should required permanent choices be solicited. A source's unselected alternative is not a conflict in the character's selected configuration. Nested contributors' prior choices must not be treated as automatically forgotten or reselected.

**Reconstruct:** Store permitted persistent choices as part of the actual manifested configuration, while leaving on-use action options available for gameplay rather than permanently resolving them at creation. If a required choice remains unresolved, the profile is incomplete; absence of authored timing/schema data is not authorization to make a guess.

## Next general implementation work

1. **Project applicable choices from the tentative reconstructed characteristic set**, distinguishing native direct offers from previously manifested mixed contributors and custom selections. A trait offered but not selected should not introduce a mandatory character-creation choice.
2. **Establish the authoritative choice contract** for finite options, catalog-backed selections, per-character governing abilities, allocation limits, spell replacement and use-time actions.
3. **Validate submitted choices with real catalog and rule authority**, preserving which trait, source, and policy entitled each selection.
4. **Verify unchanged standard Species/Ancestry reconstruction**, checking actual grant definitions, selected values, choice completeness, and resource behavior.
5. Continue to distinguish **mechanical coherence** from **balance**, and keep mixed-Species compatibility best effort with explicit table customization.

### Commands

```bash
python3 tools/character_origins_choice_schema.py --summary
python3 tools/character_origins_choice_schema.py \
  --sources ancestry.elf.high species.human

python3 -m unittest discover -s tests \
  -p 'test_character_origins_pillar2_choice_schema.py' -v
```

No native gameplay content, Rules Core runtime API, Character Sheet or `main` branch changes are implied by this study.
