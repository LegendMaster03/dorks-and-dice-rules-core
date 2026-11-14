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

## Applicability to tentative characteristics

The companion `tools/character_origins_applicable_choices.py` now projects this choice evidence onto the actual **tentatively retained** characteristics from the general reconciliation and whole-profile studies. The relevant source-qualified trait occurrence must match the candidate's current native definition; the tool does **not** infer a grant from a source's mere presence.

- **Selected direct native traits:** carry only their own authored choice evidence, with explicit `active-trait-choice-needs-authority` or `use-time-option-not-creation` status. Unselected entitlements do not become mandatory current choices.
- **Intrinsic source choices:** a selected Human `creature.size` candidate retains the single applicable `sizeChoice` declaration as provenance. The projection does not create both sizes or authorize the selection.
- **Source-wide controls:** `ancestrySelection` and `spellcastingAbilityChoice` are visible **only for participating native source lineages**, and are explicitly **not applied** as new inherent grants in mixed/custom construction. An unrelated Species' control must never leak into the current proposal.
- **Previously mixed contributors:** the existing nested profile carries the manifested trait but may not preserve a trusted record of earlier internal-choice decisions. Report `prior-choice-history-not-verified`; **do not force an unapproved reselection or regenerate hidden ancestral grants**.
- **Fully custom proposals:** no fictitious Species lineage, and no native entitlement inferred by copying a source-qualified trait ID. Proposed custom authoring authority remains distinct and unapproved.
- **Unresolved binding:** a native trait deferred by the source-to-parameter binder, such as Tiefling Otherworldly Presence's `spellcastingAbilityFrom`, remains **source authoring evidence**. It is not automatically counted as an active manifested trait until the binding contract is resolved.

The output remains `authoritativeChoiceClosure: not-certified`, `selectionEligibility: not-evaluated`, and `grantsIssued: false`. These distinctions are necessary to build a trusted persistent choice schema later, not a replacement for it.

**Tests:** `tests/test_character_origins_pillar2_applicable_choices.py`.

## Proposed choice-instance validation and previously mixed history

The [choice-instance and nested-Heritage history study](pillar2-choice-instances-and-nested-history-20261111.md) now checks **explicit proposed selections** against exact retained characteristic/choice identities, authored finite option sets, and a separately declared **synthetic** catalog evidence snapshot. It refuses to persist use-time options or treat derived bindings and player allocations as independent finite choice selections. Missing timing and genuine authoring gaps remain unresolved.

A separate version-bound proposed sidecar demonstrates how a previously mixed parent might **record** its actual choices against its specific manifested profile, rather than reselecting them for a descendant. The fingerprint only detects profile drift; it does **not** certify that the parent made that choice, that a catalog entitlement is valid, or that the choice is inheritable. A production profile must eventually store authenticated choice instances with its grants.

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
