# Pillar 4 — shared supernatural effect and transformation-profile design

**Date:** 2026-11-12  
**Status:** Proposed integration **specification for owner review**, not approved native mechanics, an executable implementation, or a new C# API. The three supernatural categories and four primary architectural test cases were selected by the owner; field names, resolution sequence, policy defaults and fixture outcomes below are **design candidates**.  
**Ruleset:** `first-party-001`  
**Branch:** `feature/pillar4-supernatural-nature-design-20261111`  
**Acceptance scenarios:** [four-case design fixture](pillar4-four-case-profile-acceptance-20261112.json)

## Goal and scope

Use one Rules Core characteristic/effect evaluator to support:

1. **Modification only** — inherited/acquired supernatural characteristics modify the current profile without creating a temporary form or replacing authoritative Species/Ancestry.
2. **Modification with temporary transformation** — a persistent supernatural nature may change normal-form mechanics, while each active alternate form may temporarily replace the effective Species/Ancestry and other selected gameplay subsystems. Return to normal re-resolves the **latest underlying baseline**.
3. **Progression to permanent reconstruction** — individual progression may modify the baseline and grant options; a specifically authorized completion event replaces the underlying Species/Ancestry. The terminal replacement can selectively retain otherwise independently earned characteristics according to its authored rules.

These classifications describe the effect's behavior, **not three engines**, a universal stage count, a required progression schedule, or mutually exclusive feature operations. Supernatural progression may be immediate, conditional, event-driven, incremental, branching, reversible, or non-linear, only where separately authored.

## Verified existing Rules Core boundaries

Reviewed against the current Pillar 2 development branch `feature/pillar2-characteristic-inventory-20261111` at `146c929e0` (PR #143 merged into that branch; not merged to `main`). This is a cross-branch architecture reference, **not** a dependency copied into this older Pillar 4 branch.

| Existing contract | Currently represented | **Not** currently executed |
| --- | --- | --- |
| `NativeOriginProfile.Underlying`, `Current` | Authoritative Species/Ancestry vs presently effective inherent configuration | Source-backed reconstruction, transition, or reversion |
| `PersistentInfluences` | Acquired/inherited supernatural source and evidence | Power unlocks, triggers, or progression |
| `ActiveTemporaryTransformation` and `OtherActiveTemporaryTransformations` | Active form, occurrence/activation proof, other concurrent forms | Applying form changes or resolving competing form precedence |
| `IndependentGrants` and `IndependentGrantApplicability` | Earned non-Species grants independently from whether form permits them | Class/background feature suppression, activation or trust validation |
| `NativeTableRuleDecision` | Scoped, inspectable DM decision claims | Trusted table permission verification and mechanical execution |
| `NativeCharacterProjector` and `NativeCharacterRequest` | Ability scores, Class advancement, feature IDs, condition IDs, hit-point history, skill/competency input, and derived resources | Consuming a supernatural form's effective-character profile |
| `NativeEquipmentQualifications` | Equipment qualifications with source/occurrence provenance | Ownership/equipment physical state, transformation usability |
| `NativeResourceResolution` | Spend/recover arithmetic for an explicit resource | Persistent canonical resource storage, changes from form replacement |

The current `NativeOriginCurrentConfiguration` holds Species, Ancestry and inherent grants. It is **not** a complete combat/character snapshot. Do not extend its semantics by stuffing temporary Class levels, item ownership or mutable resource counters into an Origin record. The broad projection belongs in a shared Rules Core **effective-character/effect-resolution layer** that consumes the Origin result and independently earned entitlements, and can subsequently be consumed by Character Sheet and other tools. It must not become a separate Pillar 4-only rules engine.

## Proposed shared data concepts

All records below are **conceptual boundaries**, not finalized C# symbols or a native JSON schema.

| Concept | Candidate required fields | Responsibility |
| --- | --- | --- |
| **Supernatural source instance** | Stable occurrence ID, source definition/version, acquisition event, authorization, persistent status | Proves that a character has the influence. Presence alone neither grants abilities nor activates a form |
| **Progression specification and current state** | Definition ID, earned/eligible stage or node IDs, choices, event history, unmet prerequisites, authored transition rules | Represents *each supernatural option's own* progression (including branching or no progression); no generic replacement of Class levels |
| **Effect grant** | Stable earned grant instance ID, original source occurrence, mechanic definition, choices/parameters, status, authorizing rule | Entitlement and provenance. Identical names are not duplicate proof; earned Class and Background features remain independently sourced |
| **Effect directive** | Source grant/effect occurrence, target selector and kind, one operation, value/reference, stage/form condition, duration/scope, precedence/decision basis | Describes what the effective character should do while the applicable source is active |
| **Form/replacement profile** | Form ID, source influence, activation and ending conditions, complete list of relevant subsystem policies, effect directives, creature classification policy | Supplies a temporary effective gameplay profile; does not own an activation-time copy of the baseline |
| **Authoritative reconstruction event** | Existing `NativeOriginHistoryEvent` and method reference, selected new baseline, source-specific retain/replace rules, history and DM basis | Only an explicitly completed permanent event replaces `Underlying` |
| **Effective character projection** | Authoritative baseline reference, active source/form IDs, actually applicable grants/values, suppressed-but-earned references, unresolved issues, ruleset provenance | The output for tools and mechanics; not an alternate persisted character history |

Use source-aware selectors rather than "all abilities named X". Targets can be an intrinsic slot (size, speed, creature type), a specific source-grant occurrence, an authored mechanic definition, a system capability (spellcasting, actions, speech), item usability, or a resource/ongoing-effect **identity**. A target cannot erase Class advancement or inventory ownership through a temporary policy. A whole-subsystem replacement must explicitly declare its scope; absence of a definition is not approval to delete, stack, or reuse everything.

### General operation vocabulary (proposal)

- **Retain:** Original entitlement and effective behavior remain, subject to independently applicable restrictions.
- **Modify:** Preserve source/entitlement and apply a supported change to a specified effective characteristic.
- **Suspend:** Keep the entitlement and stored state, but mark its current applicability as unavailable for the authored duration.
- **Replace:** For the identified **effective slot/set**, use the form or reconstruction-defined configuration instead of implicitly unioning both source sets.
- **Grant (separate from replacement):** An authorized effect or progression node awards a new feature, choice, vulnerability, or other grant. This is an acquisition event with its own source, not evidence that a differently named existing benefit should stack.

The operation is evaluated **per target or explicit subsystem scope**. For example, a form could replace Species/Ancestry and movement, retain selected Class features, suspend spellcasting, alter strength, and change how equipped items can be used. Conversely, an SRD-style vampire bat form may retain most statistics and change only size/speed/speech; a mist form may suppress a much wider collection of actions.

**Absence/unknown:** Missing authoring for an affected subsystem must remain visible as `unresolved` when a rule claims to replace that subsystem or when competing directives require a decision. Do not interpret an omitted whole-profile list as a universal wipe or as universal permission to retain all grants. Preserve unaffected independent entitlements but check current-form applicability separately.

**Creature type:** A persistent effect or form may explicitly change *effective targeting type* without permanently changing the underlying Species/Ancestry. Type inheritance does not automatically grant a monster's Hit Dice or defenses. Simultaneous effective types remain unresolved until the shared type-targeting policy supports them.

## Proposed evaluation sequence and invariants

1. **Trust and normalize inputs.** Load the authoritative native catalog, currently earned source occurrences and DM/table decisions. Verify permission and source references; do not accept self-asserted character-sheet grants or free-form executable rule expressions.
2. **Resolve authoritative baseline.** Build the latest underlying Origin through Pillar 2 and legitimate permanent reconstruction events. Keep historical baselines as audit records, not automatic restoration targets.
3. **Resolve progressive entitlements.** Apply each source-specific stage/choice rule to identify actually earned supernatural grants. Merely carrying a curse/parasite/ancestral origin does not unlock a form, a monster statblock, or a Class feature.
4. **Collect active effects and transitions.** Use source- and setting-specific events, duration and agency rules. Triggering a form and determining control of the character are independent; no universal moon, loss-of-control, alignment change, or compelled action is inferred.
5. **Reconcile interaction/precedence.** Resolve competing forms, modifications, Class features, creature classification and resources using authored rules or scoped DM decisions. When no supported winner exists, report `unresolved` or `conflicting`; never select the first or last input by default.
6. **Compose one effective gameplay profile.** Consume `NativeOriginProfile.Current` and other authoritative earned grants and apply explicit target operations. Whole-form replacement must not silently import monster CR, HP, proficiency bonus, legendary actions, or Class advancement.
7. **Project and validate mechanical applicability.** Recompute ability modifiers and dependent defenses/skills/saves/attack capabilities; resolve actions, casting, speaking, equipment usability, conditions and ongoing effects, creature targeting and resource interactions. Publish source-qualified grants plus any withheld/unresolved effects. The current projectors do **not** yet implement this composition.
8. **Reversion or completion is an authorized state transition.** Reversion drops the temporary effective profile and recomputes from the **latest** baseline, including any changes made while transformed. Permanent reconstruction updates `Underlying` only after a completed authorized transformation and does not delete independently earned Classes, possessions or history except through separately explicit and authorized mechanics.

### Cross-system integrity requirements

- **Advancement:** A form may alter current availability of Class actions or spellcasting without changing earned levels, advancement history or options. An authored terminal reconstruction may define different retention behavior, but do not infer it.
- **Equipment:** Separate ownership, inventory position, equipped state, physical form/shape and usability. Transforming or merging worn gear, unequipping, dropping, or preventing use are distinct effects; no default from a single monster record applies to all forms.
- **Resources:** Preserve actual spent/remaining quantities by stable resource ID. Form-only resource pools need explicit IDs, maxima, recovery and expiration. A change of maximum or related ability score requires an explicit policy for current amounts and over-cap states. Never reset spell slots, hit points, concentration, recharge counts or ongoing status merely because the active profile changes.
- **Persistent conditions:** Record condition identity, source and duration separately from whether its mechanical consequences currently apply. Do not silently clear or double-apply a condition on form change.
- **Threats/weaknesses:** Ongoing vulnerabilities, sunlight/exposure effects, feeding dependencies, and spell or damage restrictions can apply in normal form or selected alternate forms; each needs authored timing and duration.
- **Concurrent effects:** Ordinary supernatural modifiers may coexist with a temporary form even when two forms cannot both supply the sole effective Species slot. Apply explicit relative priority/composition only when authored; use existing `TemporaryTransformationPrecedence` and scoped decision evidence.
- **Permissions and DM scope:** Every operation and default remains overridable through a trusted authorized DM decision; permission to override gameplay does not bypass application authentication/ownership or allow a client to forge server-granted entitlements.

## Four equally important architectural tests

All examples are **read-only design**. The linked fixture contains identifiers, transitions, and checkable expected invariants; it is **not an executable test program** or an approved native catalog.

| Test case | Category tested | Expected pass condition | Distinct failure it should catch |
| --- | --- | --- | --- |
| **Draconic Bloodline** | Modification-only | Earned traits progress by individually specified conditions; Species/Ancestry and Class levels remain unchanged; duplicate-looking breath effects are not pooled automatically | Accidentally granting Dragon type, Dragonborn traits or Sorcerer features; generic free progression |
| **Full vampire** | Extensive persistent modification **plus** optional alternate forms | Vampiric weaknesses, needs, regeneration, creature-type interactions and acquired powers can apply in ordinary form; bat and mist can have different movement, action, speech, and equipment rules | Treating vampirism as cosmetic, copying all monster statistics, or using a global "equipment vanishes" rule |
| **Lycanthropy** | Additive condition with temporary replacement | Active wolf/hybrid form replaces effective Species/Ancestry, with selective changes to Class/equipment use; reversion uses the latest underlying baseline, while control/triggers remain setting-specific | Stale snapshot restoration, accidental permanent replacement, automatic monster HD/DM control |
| **Ceremorphosis** | Progressive permanent reconstruction | Parasite powers may be independently earned, partial changes are not final reconstruction, full completion updates underlying Origin, and direct completion is possible if authored | Treating optional power progression as compulsory reconstruction or deleting earned Class features without a retention policy |

### Source fidelity boundary

- The native `monster.vampire` has persistent weaknesses, regeneration, and Shape-Shift to bat and mist. Its bat form changes movement/speech; mist constrains actions and object manipulation; worn equipment transforms. The native `monster.werewolf` has wolf and hybrid forms and explicitly says its equipment does **not** transform. These are valid *comparative* cases for expression differences, **not player-character entitlements**.
- The 3.5e SRD vampire is an acquired template which preserves the base creature's statistics and special attacks subject to enumerated changes, illustrating additive-but-wide-reaching effects. https://www.d20srd.org/srd/monsters/vampire.htm
- The 3.5e *Unearthed Arcana* Bloodline rules show progressive grants, but mandatory bloodline levels and experience penalties are not approved Dorks & Dice defaults. https://srd.dndtools.org/srd/variant/unearthedBloodlines.html
- BG3's tadpole powers, partial illithid and full ceremorphosis are useful **comparative game systems**; they do not prescribe first-party Illithid progression or require partial evolution before the final event. https://bg3.wiki/wiki/Illithid_powers ; https://bg3.wiki/wiki/Full-illithid

## Open decisions; do not silently implement

1. The **canonical shape of effective-character projection** across Origin, Class, conditions, inventory and actions. Should the common resolution layer return domain-specific projections plus an aggregate result, rather than one large mutable character record? **Recommendation:** compose typed domain results with source references and a common issue list; do not duplicate each subsystem's engine.
2. Whether changing effective form-specific **ability scores**, HP maximum, casting capability or other derived values alters current HP/resources; provide explicit interaction policies and preserve event histories.
3. The **physical equipment** consequences per form, including dropped/merged/transformed items and how persistent equipment effects work when inaccessible.
4. Whether an active **ongoing spell/concentration** remains effective if its source casting feature is temporarily suspended; must be rule-specific, not assumed.
5. The **creature-type targeting** result for full vampires and alternate forms, given current single-type support and the difference between effective type and original Species.
6. Full-vampire **acquisition and balance**: do not equate the selected-native CR 13 monster stat block, a newly created spawn, and an independently advancing player character.
7. Source-specific **progression gating**, including Draconic Bloodline acquisition/cost and independent advancement, vampire progression and weaknesses, werewolf unlocks/control, and ceremorphosis terminal retention.
8. The concrete policy for **simultaneous transformations**, cure during an active form, permanent reconstruction during an active form, and form-dependent source-grant eligibility.

**Recommended next delivery:** Review the four-case fixture, approve the minimal shared target/operation semantics and confirm which open decisions require first-party defaults. Then implement **one thin evaluator slice** in Rules Core that can consume a verified Origin profile and project *one* persistent modifier and *one* temporary form with source-linked applicability. Do not hard-code werewolves or vampires to prove a supposedly general executor; do not begin all four complete progression systems in parallel.

No native ruleset files, C# implementation, APIs, or Character Sheet behavior are changed by this design document.
