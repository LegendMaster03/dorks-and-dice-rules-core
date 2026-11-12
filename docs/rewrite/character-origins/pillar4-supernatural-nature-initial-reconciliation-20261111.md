# Pillar 4 — Supernatural Nature: initial reconciliation

**Date:** 2026-11-11
**Status:** Research findings and **proposed** mechanical decisions for owner review. No Pillar 4 native mechanics, playable Lineages/Bloodlines, executable Origin changes, or Class revisions are approved by this note.
**Ruleset:** `first-party-001`
**Foundation:** Latest `main`, with open draft PR #141 (Pillar 2 characteristics) and PR #142 (creature types, origination, Rite of Rebirth) reviewed separately. The approved Pillar 2 implementation roadmap is present on the PR #142 head branch, not on the PR #141 head branch at this review.

## Existing rules that must be preserved

- Species and Ancestry already grant magical traits and level-dependent powers. Not all supernatural characteristics belong in a separate Pillar 4 selection. For example, native Elf is a Fey with Fey Ancestry, Trance and Magical Sleep; Wood Elf has level-gated spells; Dragonborn have innate breath capabilities.
- Pillar 2's common proposed pipeline is **deconstruct, combine, reconcile, manifest, reconstruct**. Trait identity, candidate provenance and manifestation have read-only prototypes, but no completed authoritative C# Origin builder. Do not implement an independent Pillar 4 engine.
- Pillar 2's **approved Rite of Rebirth first-party default** completely replaces the current Species/Ancestry configuration and its attributable grants, with independent grants and historical identity retained. Further Rites replace the then-current configuration. The Rite is not a merge or a new individual.
- Background and Upbringing grants are independent. Their existing authorization and equipment/competency projection contracts must not be bypassed by a new supernatural mechanism.
- Warlock: historical power, current Patron-derived power, the operative Patron relationship, and inherited obligations are separate; an inherited connection may support Warlock identity without a personal bargain, but no such connection automatically grants Warlock levels or features. Sorcerer class features and resources remain class-derived, not inherited merely from supernatural history.
- All first-party defaults are overridable by an authorized DM/table ruling at every level, with authority, scope, target, and effective decision recorded separately from published defaults.

## Historical comparison

| Reference | Useful idea | Do not carry forward automatically |
| --- | --- | --- |
| *Van Richten's Guide to Ravenloft*: Dhampir, Hexblood, Reborn | A supernatural identity can be selected initially or acquired during play; Ancestral Legacy preserves specified earlier race benefits after replacement | Universal all-or-nothing racial replacement; the exact narrow preserved-benefits list; treating proprietary book content as unrestricted native content |
| D&D 3.5e *Unearthed Arcana* Bloodlines | A supernatural source with minor/intermediate/major potency and abilities manifesting over levels | Mandatory Bloodline levels, missing-advancement XP penalties, general class-level inflation, fixed twenty-level trait tables |
| D&D 3.5e acquired/inherited templates | An individual is transformed or born with a modification, and effects specify concrete changes to its previous configuration | Automatic 3.5e augmented subtype, monster HD/CR/level-adjustment, or universal inheritance of type-based statblock traits |
| Existing Dorks & Dice Species and Class design | Progressive native traits already use general levels, and source/power/relationship distinctions are established | Auto-awarding supernatural grants because a concept has the same ancestry, Patron, spell, or power source |

Research references:
- https://www.dndbeyond.com/posts/1014-play-a-dhampir-hexblood-or-reborn-with-rules-from
- https://srd.dndtools.org/srd/variant/unearthedBloodlines.html
- https://www.d20srd.org/srd/improvingMonsters.htm
- https://www.dndbeyond.com/srd

## Initial proposed mechanical boundaries — approval requested

1. **One underlying characteristic and change mechanism.** Supernatural Nature is an optional source of Character Origins events, characteristics and relationships. A change targets specific verified source occurrences and grants, rather than creating a parallel Origin. General acquisition/replacement, conditional effects, progressions, authority and dependency validation belong in the shared Rules Core pipeline.
2. **Distinguish source, nature, potential, active grant, relationship and event.** A supernatural source may establish zero or more of: an identity, a current/latent power, a creature-type modification, a Patron relationship, an obligation, or conditional progression. These are not mutually implied. Merely recording a source must not duplicate any grant.
3. **Treat Lineage and Bloodline as different content patterns on shared operations.** A **Lineage** describes a supernatural identity/configuration and may be inherited or acquired. A **Bloodline** describes an inheritable supernatural influence or legacy, commonly with progressive manifestations. Supernatural abilities acquired without such a legacy can use the same progression machinery without being called Bloodlines. A single supernatural influence can establish both a Lineage and a Bloodline, but must not grant the same benefit twice.
4. **Effect-specific replacement policies.** Each transformation identifies which source configurations or grants are replaced, modified, preserved, or suppressed, and what type/characteristic changes are actually granted. The Rite retains its approved complete Species/Ancestry replacement default; no general rule applies that wholesale replacement to Dhampir, Hexblood, Reborn or other supernatural effects.
5. **Separate latent, active, suppressed, and superseded status.** Latent potential is not an earned ability. Suppression temporarily prevents an already earned grant from taking effect; supersession ends its current applicability while retaining provenance. Restoration or reawakening must revalidate prerequisites and effective table rules. This is a conceptual requirement, not yet a JSON state machine.
6. **Progression is an authored feature condition, not another required class.** A supernatural feature may grow with total character level, an event, a milestone, or explicitly another measure. Native level scaling can be reused only where a feature says so. Class levels, Sorcery Points, Pact slots, and other class resources are not conferred by the existence of a Bloodline.
7. **Creature type is an explicit effective mechanical characteristic.** A draconic source does not imply Dragon type; Patron association does not imply Fiend type. The applicable effect must say whether effective type changes, and Pillar 2's current targeting model is not yet equipped to silently assign multiple effective types. Monster statblock defaults are not automatic PC grants.
8. **Allow multiple influences when their actual rules permit coexistence.** Multiple sources may coexist, but the common engine must detect shared grant identity, conflicts, intrinsic slots, resource overlap, and dependencies. It cannot assert that an unresolved composition is balanced or mechanically valid.
9. **Balance requires source-specific acquisition terms.** Progression by itself does not balance an unrestricted extra feature track. Use explicit eligibility, source-related replacements, limited feature selection, tradeoffs, resources or campaign-award conditions where justified, measured against ordinary Species and class progression. Do not adopt generic supernatural point-buy or mandatory historical Bloodline levels at this stage.

## Three primary design cases

### Elf transformed into a Dhampir (reference-only, not native content)

Native Elf currently grants **Fey** effective creature type, Fey Ancestry, Low-Light Vision, Keen Senses, Trance and Magical Sleep. *Ravenloft* Dhampir uses a different inherited/retained-benefit policy and **Humanoid** typing in its original source. Therefore a native transformation specification must resolve **both effective type** and each existing Elf Species/Ancestry source grant. Possible choices include selectively replacing the Elf traits, displacing the entire Species configuration with explicit retained characteristics, or adding a supernatural overlay with individually specified exclusions. No arbitrary automatic stacking of all Elf and Dhampir traits is authorized. Independently earned Background/Upbringing/Class grants remain unless the effect expressly addresses them. Historical Elf identity remains.

### Character with progressively manifesting draconic potential

A source of draconic potential exists without automatically granting Dragonborn Species, Dragon targeting type, a breath weapon, or Sorcerer levels. An initial trait might be dormant; later author-defined character levels, events, or Class interactions might awaken or improve specified traits. An existing Dragonborn breath weapon and a Bloodline feature with a similar effect remain distinct entitlements until a specific shared-definition or interaction rule establishes otherwise. Any additional power must have a balancing/acquisition policy, not simply an extra free progression track.

### Descendant of an ancestral Warlock bargain

Record historical bargain, and independently determine whether the descendant receives (a) self-sustaining magic, (b) operative Patron-derived power, (c) current Patron relationship, and/or (d) enforceable obligations. A descendant may have obligations without abilities, or innate abilities without a current Patron. Neither case grants Warlock or Sorcerer levels automatically. Class eligibility and subsequent Class features follow the already approved Warlock/Sorcerer rules.

## Necessary shared Pillar 2 contract checks (not a parallel engine)

- Stable verified **current grant occurrences** with authoring-source and acquisition-event provenance; distinguish historical ancestry from presently earned mechanical effects.
- Ordered/auditable transformation events with explicit affected occurrences, effective overrides, reversible suppression, subsequent change, and dependency revalidation.
- Shared definition identity and resource coordination: the same spell name or visually similar feature is not proof of duplicate entitlement or pooled uses.
- Trusted authorization of initial selection, later supernatural acquisition, and DM/table scope; untrusted character-sheet inputs must not fabricate sources or granted benefits.
- A complete effective-characteristic snapshot from the same reconstruction pipeline, exposing unresolved type/trait conflicts rather than auto-resolving them.
- Standard Species round-trip and complete Rite-of-Rebirth replacement tests before supernatural variants build on the general pipeline.

## Open first-owner decisions

1. Confirm or revise the proposed **Lineage identity / Bloodline inheritable influence** boundary. Specifically, whether an acquired-but-transmissible supernatural source may later count as a Bloodline.
2. Choose the **Elf-to-Dhampir default**: full replacement with enumerated retention, or targeted overlay with specified removals. This is a Dorks & Dice design choice; source Ravenloft Ancestral Legacy is not automatically adopted.
3. Establish a **first-party balancing approach** for an optional progressive Bloodline available at character creation; unrestricted free parallel power progression is a concrete parity problem.
4. Determine whether changing an Origin requires **immediate revalidation of dependent grants**, and how to present unresolved dependencies during play. Recommended: yes, with unresolved or inactive dependent effects surfaced, rather than silent deletion or indefinite active use.
5. Determine whether the initial alpha should author an **original draconic influence** first, then derived supernatural Lineage cases; published Ravenloft descriptions are not in the SRD 5.1/5.2.1 redistribution baseline.

No native JSON is altered by this design note. All proposals above remain unapproved until owner review.
