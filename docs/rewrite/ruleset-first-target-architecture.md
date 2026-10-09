# Rules Core rewrite — ruleset-first target architecture

Status: **architectural direction**, 2026-10-09. This describes the intended product, not an already implemented design or a final choice of game mechanics.

## Mission

Rules Core is becoming the authoritative host and execution engine for the **Dorks & Dice custom tabletop role-playing game system**. It is no longer primarily a program that resolves between external tabletop rulesets. The Dorks & Dice system itself defines the primary vocabulary, mechanics, applicability, interaction policies, and defaults. Rules from 3e, 3.5e, 5e, 5.5e, and additional compatible sources are imported and reconciled **into** this hosted system and with one another.

This is a more fundamental change than moving the existing house-rule baseline into a separate source package. **The game system is not being invented from zero**: the existing cross-edition resolver has already made numerous substantive Dorks & Dice design decisions when recognizing equivalent mechanics, preserving distinct calculations, building universal competencies, and deciding how imported rules coexist. These choices are the starting design evidence for the independent system, not architectural code to copy wholesale. The custom system must ultimately constitute a playable, independently versioned ruleset whose procedures operate without silently selecting an older edition as the default. We must recover and verify previously settled decisions while identifying genuine design gaps, not reopen everything as if it had never been considered.

Compatibility remains a fundamental objective. Moving authority to Dorks & Dice must not erase mechanically important differences, discard imported options, or force false equivalence among editions.

**Governing play-experience requirement (owner, 2026-10-09):** One player should be able to engage with the accessible, comparatively straightforward gameplay associated with 5.x at the **same table and in the same encounter** as another player who elects the greater tactical/build complexity associated with 3.x. **Each player chooses how deeply to interact with mechanics**; both share one coherent Dorks & Dice rule/effect system. Accept source-edition compromises when necessary to maintain this mixed-complexity experience. This is a **game design constraint on conversion, engine semantics and user-facing projections**, not authorization to implement a particular option system.

- **Shared core:** All participants use the same effective turn/action budget, targets, defenses, damage, conditions, positions and effect semantics; no mutually inconsistent source-edition modes per player.
- **Graduated procedure depth:** A straightforward character and an involved tactical character both have functional paths through combat. Richer options may entail prerequisites and extra decisions; ordinary play must not depend on learning another character's procedures.
- **Effect normalization, not false procedural equivalence:** Shove, Topple, Trip, Push, spell and class-feature effects can share a displacement or condition executor while preserving each source's authorized attack trigger, save/check, action cost, limitation and acquisition requirements.
- **Complexity must not mechanically dominate by default:** The rewrite must surface and test balance/action-cost tradeoffs so that more rules knowledge or bookkeeping is not itself a systematic source of free actions/damage, without promising exact numerical parity before rules are reviewed.
- **Consequence-focused consumer API:** Character Sheet and game Tools should be able to expose a simple actionable view and optional in-depth explanations using the **same effective rule resolution**, not separate simplified numerical physics. A player only needs an understandable outcome from another player's advanced ability; GM adjudication and authorized campaign options remain available.
- **Validation:** Include mixed-depth characters in one combat scenario, test shared state and effects, compare usability and efficacy, and verify no 3.x or 5.x switch is required for normal participation. A non-digital table must still be viable.

**Game-design prerequisite:** before implementing this target architecture, complete the owner-reviewed [native rules specification](native-rules-design-specification.md). The recovered resolver policy is the starting point, not the entire game. Specify previously unresolved native combat procedures (including combat maneuvers), progression, and other end-to-end gameplay fundamentals in Dorks & Dice terms first. **Level Bands supersede an epic-specific native progression engine.** The architecture must implement the resulting approved game; it must not determine game rules in advance.

**Compatibility premise:** as Pathfinder First Edition was compatible with D&D 3.5e through manual conversion, Dorks & Dice aims for **automated** rule/content conversion from 3e, 3.5e, 5e (2014), and 5.5e (2024) into its own governing rules. PF1e is a potential additional source. Automated conversion includes faithful automatic mappings where provable and explicit review states for ambiguous or incompatible cases; it does not assert that different numeric systems or source rules are identical, or promise lossless migration of every existing character and campaign.


## Clean-slate implementation and licensing

**Starting from scratch means a clean native rules engine and implementation architecture, not discarding existing Dorks & Dice mechanics decisions or treating previous work as absent.** **Greenfield implementation is the default**: author new domain entities, rule-composition semantics, engine code, and persistence without inheriting the old resolver's class structure or database as constraints. Existing source pipelines, fixtures, API contracts, and cross-edition conclusions are reference material and migration targets, not building blocks that must be copied. Retain the historical service unchanged while the replacement is constructed and verified. Inventory the existing solver's adjudications, stable universal concepts, cross-edition relationships, mechanics contracts, and accepted house rules. Distinguish established design policy from incidental translation behavior, temporary fallbacks, and implementation constraints. Reimplement established policy deliberately under the new Dorks & Dice ontology.

Under United States copyright law, 17 U.S.C. § 102(b), abstract game procedures, systems, processes, and methods of operation are not protected by copyright. Original explanatory text, artwork, and other expressive content can be protected. The D&D SRD 5.1 and SRD 5.2.1 are **not public domain**; Wizards released them under **Creative Commons Attribution 4.0 International**, allowing use, adaptation, distribution, and commercial reuse with required attribution. The **3e and 3.5e SRD** publications and independently licensed third-party source bodies have different permissions (not a blanket CC BY 4.0 grant) and require separate source-specific handling; the availability of a game mechanic does not itself authorize reproducing someone else's protected explanatory text. The distinction matters when we choose between independently authoring descriptions/implementations and incorporating actual SRD material.

### Pathfinder First Edition as the 3.x precedent

**Pathfinder First Edition is our concrete precedent for reusing and adapting 3.x mechanics within an independent TTRPG.** Paizo's March 18, 2008 announcement said that Pathfinder's rules were based on D&D 3.5 Open Game License rules, with backward compatibility as a primary goal. This demonstrates an established historical route: preserve the familiar mechanical foundation, introduce a distinct game's rules and changes, and publish designated open content under its applicable license.

Dorks & Dice need not adopt Pathfinder's actual choices or copy its implementation. For 3e/3.5e-derived mechanics, distinguish these routes per material:

- **Independently implemented methods of play.** U.S. copyright law does not protect abstract procedures, systems, or methods of operation under 17 U.S.C. § 102(b). Write the Dorks & Dice engine and explanations independently, without copying protected prose, illustrations, setting expression, or trademark presentation. The precise copyright/expression line may still require examination for an individual rule.
- **Text or other content explicitly released as Open Game Content.** Material taken under OGL 1.0a must follow that license's terms, including required license/copyright notices and exclusion of designated Product Identity. Pathfinder 1e's use of OGL 1.0a is the model for this route.
- **Materials published under other licenses.** SRD 5.1/5.2.1 CC BY 4.0 content, ORC-licensed Pathfinder material, and restricted third-party works must be tracked separately. The release of mechanics under one license does not relicense OGL-only material under CC BY or ORC, or license trademarks and worldbuilding.

Paizo's newer ORC-era Pathfinder publications are not interchangeable with Pathfinder First Edition's OGL foundation. Use Pathfinder 1e as the precedent for **3.x mechanics and OGL practice**, not as a blanket license from Paizo or Wizards. If publishing a substantial compilation that actually incorporates licensed game-rule expression, review the content and notices before release, particularly where multiple source licenses meet.

Primary evidence:
- [Paizo's Pathfinder announcement, March 18, 2008](https://paizo.com/blog/paizo-publishing-reg-announces-the-em-pathfinder-rpg-em-trade)
- [Paizo licenses and OGL/ORC distinctions](https://paizo.com/licenses)
- [Paizo Pathfinder compatibility licensing FAQ](https://paizo.com/licenses/compatibility/faq)
- [Copyright Office, 17 U.S.C. § 102(b)](https://copyright.gov/title17/92chap1.html)

Authoritative references:
- [17 U.S.C. § 102(b), U.S. Copyright Office](https://www.copyright.gov/title17/92chap1.html)
- [Wizards of the Coast SRD downloads and licensing FAQ](https://www.dndbeyond.com/srd)
- [CC BY 4.0 terms](https://creativecommons.org/licenses/by/4.0/)

Maintain clear attribution/provenance and a content-use policy for every incorporated source; the ability to implement a mechanic does not automatically permit wholesale reuse of third-party rulebook text, settings, artwork, or marks.

## Generalized Level Bands and advancement-track semantics

The old `docs/epic-content-modeling.md` preserves a record of the legacy implementation's epic normalization; **the new Dorks & Dice ruleset does not use epic as an engine-level special case**. Instead, 20-level **Level Bands** apply to relevant level-based tracks (1–20, 21–40, etc.). Character total level, each class progression, and each separately leveled prestige progression may be in different bands concurrently; a subclass inherits the effective parent-class track. A feature may determine acquisition, effect scaling, and prerequisites from different named tracks.

A rule's band/threshold behavior may introduce the mechanic, preserve it without new improvements, continue its progression, evolve its behavior, or terminate its effect; absence of a rule must not be confused with a default transition. Labels such as **Epic Feat**, **Epic Boon**, and **Epic Spell** remain source/catalog descriptors, not a separate level-21 algorithm. Full details and unresolved defaults belong in the [native rules specification](native-rules-design-specification.md), not in persistence or translation assumptions.

## Automated conversion boundary

A versioned adapter should identify a source's **mechanical intent**, map it into native Dorks & Dice concept/procedure/track identities, and retain its original semantics as an implementation variant when the native procedure is not equivalent. Conversion may produce: accepted equivalent, explicitly adapted, additive extension, mutually exclusive alternative, incompatible/conflicted, or unresolved/needs review. The precise accepted status vocabulary is a subsequent schema decision, not a rule to hard-code during game design.

**Rules-content conversion** is distinct from a future **character/creature/encounter/campaign migration** tool, because converting a class feature definition does not itself decide how an existing character's rank points, levels, used spell slots or encounter conditions should map. Avoid hiding incompatible rule-system assumptions under a “successfully converted” status. Preserve source provenance, grants, history, and permissions.

## Primary architectural distinction

**Old mental model**

```text
multiple external rulesets
  -> normalize / recognize overlapping records
  -> choose, patch, or combine a source implementation
  -> expose the effective external rule
```

**Target mental model**

```text
versioned Dorks & Dice core ruleset
  -> defines mechanic concepts, behavior, evaluation, defaults,
     interaction, extension points, and conflict policy
                        |
source artifacts        |
  -> native preservation|
  -> interpretation     |
  -> semantic mapping ---+-> resolve imported rules into the core model
  -> source reconciliation----> reconcile overlapping imported rules
                                 and imported-to-core relationships
                                      |
                               accepted/adjudicated rule contributions
                                      |
                             versioned effective ruleset composition
                                      |
                      typed evaluation and Tool-specific projections
```

The Dorks & Dice ruleset is a normative input to both composition and execution. The import pipeline provides source evidence and mechanically meaningful contributions; it is not allowed to choose runtime semantics independently of the core ruleset.

## Responsibilities and boundaries

### 1. First-class hosted Dorks & Dice ruleset

- Define first-class, stable **system mechanic identities** independently of Source Layer and imported publication identities.
- Own default game procedures, applicability criteria, interaction semantics, and extension points. Authoritative mechanics must be executable/testable, not merely text rendered by Rules Wiki.
- Model the ruleset as versioned, publishable data and executable behavior with deterministic resolution snapshots.
- Allow Rules Layer adjudication to extend, replace, or deliberately override core behavior. The exact permissions, workflow, and precedence are design questions; automatic imported-source precedence is not.
- Distinguish the system's own mechanics from contributions and evidence derived from externally authored systems.
- Provide a credible path to an independent playable baseline; avoid declaring compatibility completed before the system itself is mechanically defined.

### 2. Source-native preservation and canonical recognition

The existing source-evidence guarantees remain important: immutable source artifacts/revisions, per-package grants, publication/entity recognition, lineage and origin provenance, no implicit disclosure of restricted bodies, and no fake native revision on translation upgrades.

Canonical source identity answers *what source item is this?* It does not answer *what is its Dorks & Dice meaning?* A core system mechanic has its own identity independent of both canonical source history and the Rules Lawyer's record of a particular imported implementation.

### 3. Semantic adaptation and compatibility

- Interpret native input (5e.tools, PCGen, SRDs, PDFs, later source formats) into **meaningful mechanical contributions** rather than treating any single imported schema as the engine's native ontology.
- Map contributions to existing core concepts when supported by evidence; support explicit extension concepts when a source introduces mechanics the core does not already represent.
- Record mapping relation and certainty: direct/equivalent concept, compatible facet, extension, revision/reprint, mechanically distinct variant, conflict, or unresolved. Avoid conflating a shared name with a shared implementation.
- Reconcile imported rules with core definitions **and with other imports** using shared semantic identity and interaction rules. Do not resolve a disagreement by import order, newest-edition wins, or an unexplained numeric conversion.
- Retain native calculations/profile semantics when mechanically significant until the core ruleset establishes an explicit, reviewed compatibility treatment.
- Failed, partial, ambiguous, or unsupported translation must remain visible as such, not silently approximate.

### 4. Composition, precedence, and execution

Proposed **conceptual layers** (not yet a committed runtime storage design):

1. Published Dorks & Dice system version — normative defaults and behavior.
2. Eligible, accessible imported mechanical contributions — mapped to core concepts or extensions.
3. Reviewed/global adjudication — accepted, excluded, modified, or consolidated contributions and explicit conflict resolutions.
4. Campaign rules and selected context — overrides and conditions within their authority.
5. Character/scene/action state — input facts, choices, capabilities, and transient results.

Composition must distinguish additive options from mutually exclusive implementations, prerequisites, replacements, modifiers, conditions, triggers, and explicit conflicts. Not all imported rules are automatically active. Exactly how an imported contribution becomes effective, and how core rules govern it, must be specified and tested.

The evaluator should consume a deterministic compiled/effective rules context, with stable references to its system version, rule contributions, selected decisions, and provenance. Game Tools provide state and present the resulting effects; they do not independently reimplement game logic or choose an edition to make a calculation.

### 5. Examples that must remain distinguishable

- **Alchemy**: a universal learned competency can reconcile 3.x ranked skills and later tool proficiency while preserving their different calculations. The core ruleset determines their interaction in a mixed character.
- **Target defenses**: Touch AC, Flat-Footed AC, standard Armor Class, advantage/disadvantage, and denied Dexterity are not interchangeable. A core procedure may use or reconcile them only through explicit applicability/compatibility rules.
- **Classes and advancement**: different advancement progressions, feat cadence, prestige prerequisites, spellcasting progression, and historically epic-labeled content must not be flattened into a 5e-only class schema; Level Bands and their per-track transitions govern native advancement. The core defines what mixed progression means.
- **Third-party rules**: new damage types, subsystems, proficiencies, or procedures need an explicit way to extend the core model, not an assumption that only known SRD categories can exist.

## Security and integration

Rules Wiki remains the human-facing authoring/reference surface via the private Tool-to-Tool boundary; it does not become a second rules engine. Character Sheet owns character state and presentation and asks Rules Core for authoritative mechanics. Other game Tools receive effective rules/procedures rather than raw source alternatives.

The Dorks & Dice system's public/readable mechanics and imported restricted source bodies have different access policies. Adopting or referencing a private source must not turn its text into globally accessible baseline content. All composed outputs and metadata remain subject to provenance-aware grants.

## Essential early design deliverables

Before writing replacement mechanics infrastructure, **finish the owner-led native gameplay design** and then produce:

1. An **owner-reviewed complete Dorks & Dice gameplay specification**: first recover the actual existing decisions (competencies, class taxonomy, advancement, combat, spellcasting, additive behavior, and known house rules), then establish the missing native play procedures and Level Bands **before** comparing outside design alternatives. Mark each as settled, proposed, or unresolved. Include the owner's intended combat-maneuver design and a full playable loop. The six formally seeded house rules are only one subset of the pre-existing design work.
2. A **mechanical ontology** separating stable system concepts, source implementations, compatible facets, procedures, operators, typed values, triggers, capabilities, and extensions.
3. An **automated compatibility and adjudication policy** for 3e/3.5e/5e/5.5e conversions, source-to-core and source-to-source relations, mechanical fidelity, native versus source-specific procedure behavior, effective participation, precedence, contradictory mechanics, explicit unresolved translations, and approval. PF1e remains an optional candidate source.
4. A **cross-edition acceptance matrix** using 3e/3.5e/5e/5.5e and third-party-shaped fixtures, with pure and mixed-system cases.
5. A **deterministic runtime execution proposal** with performance budgets and migration/consumer compatibility constraints.

These design deliverables must precede the replacement domain and persistence schema. Reusing an existing name, JSON model, or per-edition calculation does not establish that it is a canonical Dorks & Dice rule.

## Gate requirements

The initial foundation and cutover gates remain in force for branch isolation, provenance, user data, authorization, performance, observable behavior, reversible migration, and separate cutover approval. Their implementation stages must be interpreted through this ruleset-first objective. Do not merely rebuild the legacy resolver and attach a house-rules table.

## Open questions to settle through design

- Which mechanics and procedures are the Dorks & Dice system's own defaults, and which are deliberately optional or still undecided?
- Does compatibility require exact standalone behavior for each historical edition, mixed use within one character/campaign, or both? Test the intended cases rather than assuming either.
- What is the distinction between an imported *available option*, an *adopted rule*, and an *active runtime implementation*?
- Which mechanical interactions are automatic under the custom ruleset, and which require Rules Lawyer or campaign adjudication?
- What are the versioning and migration semantics when a new Dorks & Dice ruleset version changes default behavior while existing campaigns reference older published snapshots?
- How will Rules Wiki express and author new core mechanics and external extensions without exposing unsafe or ambiguous program execution?
