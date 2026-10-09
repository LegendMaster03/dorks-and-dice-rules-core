# Rules Core rewrite — ruleset-first target architecture

Status: **architectural direction**, 2026-10-09. This describes the intended product, not an already implemented design or a final choice of game mechanics.

## Mission

Rules Core is becoming the authoritative host and execution engine for the **Dorks & Dice custom tabletop role-playing game system**. It is no longer primarily a program that resolves between external tabletop rulesets. The Dorks & Dice system itself defines the primary vocabulary, mechanics, applicability, interaction policies, and defaults. Rules from 3e, 3.5e, 5e, 5.5e, and additional compatible sources are imported and reconciled **into** this hosted system and with one another.

This is a more fundamental change than moving the existing house-rule baseline into a separate source package. The custom system must eventually constitute a playable, independently versioned ruleset: its core procedures and mechanics can operate without arbitrarily selecting a previous edition as the hidden default. Its exact rules and their published versions are subject to design and review; this document does not invent them.

Compatibility remains a fundamental objective. Moving authority to Dorks & Dice must not erase mechanically important differences, discard imported options, or force false equivalence among editions.

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
- **Classes and advancement**: different advancement progressions, feat cadence, prestige prerequisites, spellcasting progression, and epic content must not be flattened into a 5e-only class schema. The core defines what mixed progression means.
- **Third-party rules**: new damage types, subsystems, proficiencies, or procedures need an explicit way to extend the core model, not an assumption that only known SRD categories can exist.

## Security and integration

Rules Wiki remains the human-facing authoring/reference surface via the private Tool-to-Tool boundary; it does not become a second rules engine. Character Sheet owns character state and presentation and asks Rules Core for authoritative mechanics. Other game Tools receive effective rules/procedures rather than raw source alternatives.

The Dorks & Dice system's public/readable mechanics and imported restricted source bodies have different access policies. Adopting or referencing a private source must not turn its text into globally accessible baseline content. All composed outputs and metadata remain subject to provenance-aware grants.

## Essential early design deliverables

Before writing replacement mechanics infrastructure, produce:

1. A **Dorks & Dice system specification** defining the known core mechanics, intentionally open mechanics, ruleset versioning, and expected player-facing procedures. The existing six house rules are seed policy, not a complete system definition.
2. A **mechanical ontology** separating stable system concepts, source implementations, compatible facets, procedures, operators, typed values, triggers, capabilities, and extensions.
3. A **compatibility and adjudication policy** for import-to-core and import-to-import relations, effective participation, precedence, contradictory mechanics, and approval.
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
