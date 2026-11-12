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

## Owner direction: transformation outcome and tempo

**Status:** Owner-selected *conceptual distinction* (2026-11-11), not approval of individual creature mechanics, transformation milestones, numeric grants, or native JSON.

A supernatural transformation's **duration/progression** is independent of its **eventual effect on the underlying Species/Ancestry configuration**. The general Character Origins pipeline should therefore accommodate at least two outcomes:

1. **Additive transformation:** The subject retains its underlying Species/Ancestry identity and configuration at full manifestation, while a supernatural condition or nature modifies or adds characteristics. Werewolves and vampires are the owner's conceptual examples. A particular supernatural effect can explicitly modify, suppress, or replace individual traits or change effective creature classification without by itself replacing the Species/Ancestry configuration.
2. **Reconstructive transformation:** Over time or immediately, the subject's underlying Species/Ancestry configuration is replaced by a different one. Developing into an illithid or zombie is the owner's conceptual example. The intermediate stages may preserve, modify, suppress, or replace selected original characteristics. Upon completion, old Species/Ancestry grants no longer remain active merely because they existed in a preceding stage. Historical identity and unrelated independently sourced grants remain, subject to the applicable effect's explicitly authored interactions.

**Lycanthropy correction (2026-11-12):** The additive outcome describes the **persistent supernatural condition**, not every momentary form. When a lycanthrope changes to an alternate form, that form **temporarily replaces the effective Species/Ancestry configuration**. The authoritative baseline is preserved and restored/re-resolved when the form ends, rather than being permanently overwritten. Loss of control and the trigger for transformation depend on the applicable **setting or individual supernatural effect**, not on a universal moon, rage, or DM-control rule. See [the first lycanthropy progression reconciliation](pillar4-lycanthropy-reconciliation-20261112.md). This refinement does not change the distinct permanent-reconstruction outcome or the Pillar 2 `supernatural-reconstruction` origination reference.

Neither outcome prescribes a particular speed, number of stages, stage prerequisites, ability advancement schedule, reversibility, or narrative process. A Rite of Rebirth continues to specify **complete configuration replacement** under its approved first-party default; not every reconstructive transformation is a Rite, and this classification does not alter the Rite's approved grant semantics. Both outcomes must reuse the common event/characteristic reconciliation operations rather than separate engines.

**Shared-system implication:** A gradual transformation should be representable as a series of authorized transitions, each yielding a coherent current state (or explicit unresolved issues) with recorded source occurrences, progression triggers, active/suppressed/superseded grants, and history. The terminal stage's scope of replacement must be **explicit**, including cases where an effect changes effective creature type without wholesale Species replacement. Do not treat the *first* increment of a future reconstructive transformation as already completing its final replacement unless the rule states so.

This is not a universal claim about the published rules for vampires, lycanthropes, illithids, or zombies; they are illustrative design cases. Their actual Dorks & Dice implementations, including any effects on consciousness, ability scores, Creature type, classes, Background, Upbringing and independently sourced supernatural grants, require separate reconciliation.

**Consequences for the earlier proposal:** "Effect-specific replacement policies" now include an explicit *terminal outcome* and stage-dependent changes. "Lineage versus Bloodline" remains a distinct open classification question; being gradual does not itself make a change a Bloodline, and being reconstructive does not require a Bloodline.

## Owner direction: three supernatural change categories

**Status:** Owner-selected taxonomy (2026-11-12); implementation details and particular supernatural progressions remain unapproved. This **refines**, rather than contradicts, the earlier broad two-outcome distinction by dividing persistent additive supernatural effects into two categories.

| Category | Ongoing effect | Alternate form behavior | Terminal result |
| --- | --- | --- | --- |
| **1. Modification-only** | Modifies the existing Species/Ancestry configuration and/or other sourced characteristics | No separate temporary replacement of the active configuration is required | Underlying Species/Ancestry remains authoritative |
| **2. Modification with temporary transformation** | May modify the character between transformations | A particular form can temporarily substitute a different **effective** Species/Ancestry and other gameplay characteristics; reversion re-resolves the *current* authoritative baseline | Underlying Species/Ancestry remains authoritative |
| **3. Progressive permanent reconstruction** | Intermediate changes can modify, suppress, or progressively displace characteristics | A completed transformation replaces the authoritative underlying Species/Ancestry configuration, not merely the active form; the previous configuration remains historical | New Species/Ancestry is authoritative |

These are **categories of how a supernatural effect interacts with an Origin**, not separate engines, classes, creature types, or mandatory advancement tracks. A single supernatural condition can have several authored alternate forms with different replacement scopes. The individual form determines which active characteristics are retained, modified, suspended or temporarily replaced, potentially including ability scores, Class features, spellcasting, equipment usability, actions and resources. Underlying Class levels, equipment records, historical grants, and other persistent character state are not deleted simply because the form makes them unavailable. Persistent resource and effect interaction must be authored, not reset implicitly.

**Important distinction:** Category 2 contains a *persistent additive condition* and a *temporary active replacement*. It does not qualify as Pillar 2's `supernatural-reconstruction` origination, which is needed only for the eventual permanent replacement in category 3. The Rite of Rebirth remains a specific immediate/complete-replacement process, equivalent to a category 3 endpoint without obligatory intermediate stages. If a category 3 effect progresses over time, every stage's actual effective characteristics must be specified; do not treat partial development as a completed Species replacement.

The three categories do not prescribe how frequently advancement occurs, how many stages exist, whether a form is controllable, what triggers it, whether a change is curable, or which features are gained. Each supernatural effect and setting supplies those rules where relevant. An authorized DM/table ruling can override any level of classification, stage, form replacement profile, trigger, or result.

**Provisional examples, not finalized native content:** A draconic influence that only modifies existing traits fits category 1; a werewolf retaining its baseline but using alternate wolf/hybrid forms fits category 2; gradual ceremorphosis ultimately replacing Species/Ancestry fits category 3. Other published or homebrew versions of these phenomena may use different rules.

## Owner direction: general progression framework and individual progression rules

**Status:** Owner design direction, 2026-11-11. Each individual supernatural condition, Lineage, Bloodline or transformation may require its **own authored progression specification**. This does not authorize particular milestones, numerical benefits, mandatory uniform staging, or a new software engine per option.

The shared Character Origins/Rules Core framework must represent and resolve **general progression operations**: current state, eligible transitions, conditions/triggers, choice and grant evaluation, modifications, additive and reconstructive transformations, suppression/restoration, history, dependency revalidation, and authorized DM/table decisions. It must not impose one fixed schedule or a single obligatory progression variable on all supernatural options.

Each actual supernatural option specifies **its own progression rules and structure** where needed: ordered or branching stages, event-driven development, character-level conditions, time/exposure/resource triggers, voluntary versus involuntary change, transformations between forms, regression or stabilization, and any cure/removal semantics. These are capabilities of the general framework, **not automatic properties** of every option. An option that grants its effect immediately need not have a progression track at all.

Illustrative studies for later individual reconciliation:
- Lycanthropy: shifts in control, forms, and circumstances triggering change.
- Vampirism: supernatural capabilities alongside possible weaknesses and feeding requirements.
- Ceremorphosis: staged replacement of former characteristics, culminating in a reconstructed Species/Ancestry configuration.
- Zombification: potentially staged change ending in an alternative creature configuration.
- Draconic inherited potential: progressive manifestation without necessarily changing Species/Ancestry.

These examples do not prescribe official published mechanics or approve original first-party details.

**Design boundary:** Distinct authored progressions should use shared state/event/characteristic processing rather than bespoke resolvers in C# or Character Sheet. The generic framework should support nonlinear, branched, reversible and condition-driven transitions when justified, without forcing that complexity into the ordinary character-creation path. Before implementing specific cases, use representative contrasting examples to prove that the framework is expressive enough; revise shared structures if a concrete case exposes a gap.

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
