# Pillar 2 — Universal Character Origins implementation roadmap

**Date:** 2026-11-11  
**Status:** Agreed implementation direction and execution plan; not a claim that the engine, API, or Character Sheet integration is complete.  
**Scope:** Pillar 2 (origination, Species/Ancestry contribution, reconciliation, manifestation, reconstruction, and subsequent transformations).

## Objective and delivery strategy

Deliver a **functional mixed-Species/mixed-Ancestry character** built from real
authoritative Rules Core content. The same construction pipeline must also
reconstruct a directly selected standard Species/Ancestry without loss of
mechanical meaning.

Use **one primary implementation vertical slice** (ordinary biological
reproduction and mixed Heritage) while maintaining **small contrasting
architectural tests** for Plant reproduction, Construct assembly, Fiend
spawning, and Rite of Rebirth. Do **not** implement four complete parallel
origination systems before the first working mixed Origin.

The universal pipeline is:

1. **Deconstruct** eligible source configurations into identifiable
   characteristics, parameters, and source evidence.
2. **Combine** the characteristics offered by the applicable contributors
   under the selected origination process.
3. **Reconcile** shared foundations, conflicts, duplication, intrinsic
   replacements, prerequisites, and dependent choices.
4. **Manifest** actual selected characteristics and resolved parameter values.
5. **Reconstruct** a coherent, usable current Origin with retained historical
   sources, choices, and adjudication provenance.

Origination **precedes** candidate assembly: it identifies applicable
processes, input roles, and candidate sources. It does not automatically grant
each input's complete Species/Ancestry package. The common five operations
must not assume exactly two biological parents, exactly two contributors, or
even a biological process. A contributor may be a creature, an already mixed
character, a material, a creator, a magical force, or another input appropriate
to its process. The role of an input determines whether and how it may
contribute characteristics.

## Standing rules and architectural boundaries

- **Rules Core is authoritative.** The C# production pipeline reads selected
  native rules. Python inventory and proof tools are research/test support,
  not a second production rules engine. Character Sheet and other tools must
  not independently reinterpret Origin rules.
- **Creature types supply default methods, not exhaustive prohibitions.**
  The reconciled catalog has 17 types, with Monstrous Humanoid and True
  Elemental removed. Genies remain Elementals. More specific creature types
  and Species may refine origination-method defaults. Plant reproduction is
  distinct from ordinary biological reproduction; a Fiend can reproduce
  biologically or be spawned.
- **Methods and compatibility remain understandable at the table.** Use the
  existing creature-type hierarchy and specific authored exceptions, not an
  independent reproductive taxonomy or exhaustive Species-pair matrix.
  Different creature types are not inherently incompatible. The chosen
  process determines required contributor roles and inputs, and unknown
  first-party compatibility remains reviewable rather than silently prohibited.
- **Creature classification, origination, Heritage, and manifestation are
  separate concepts.** Process eligibility does not dictate effective
  creature type, targeting eligibility, or complete mechanical inheritance.
  Historical ancestry does not automatically grant every ancestral trait.
- **DM authority is universal throughout Dorks & Dice.** First-party rules
  are defaults. An authorized DM/table decision can alter or replace any
  classification, method, eligibility requirement, contribution, conflict
  resolution, grant, characteristic, transformation, or resulting character
  configuration. Store decisions with authority, scope, target, and effective
  outcome; preserve first-party definitions and apply the decision only
  within its declared table context. This is not a Pillar 2-specific
  exception and does not bypass software access controls.
- **Rite of Rebirth is a post-origination transformation.** Under first-party
  defaults it completely replaces current Species/Ancestry and their grants,
  while preserving independent grants, Background, Upbringing, and history.
  It does not create a second creature or merge full prior Species packages.
  A qualifying Rite or authorized DM ruling can grant access to restricted
  Ancestries; an individual DM can override even the default replacement
  behavior. Exceptional Dragonborn forms remain subject to their separately
  documented design and acquisition gates.
- **First-party content is evolving.** Do not gate deployment or runtime
  interpretation on fixed catalog counts, fingerprints, or imagined stable
  content freezes. No phase here authorizes a merge to `main`.

## Phase 0 — Consolidate the approved foundation

**Repository state at planning:** Draft [PR #141](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/141)
contains ongoing Pillar 2 research and prototypes. Draft
[PR #142](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/142)
is based on the Pillar 2 feature branch and carries the 17-type taxonomy,
origination-method contract, Rite integration, and explicit system-wide DM
override rule. The parent branch may advance independently.

**Work**

- Reconcile PR #142 with the latest Pillar 2 branch before integration, rather
  than merging unreviewed work to `main`.
- Validate native and working creature-type records, retired-type references,
  and affected content relationships. Preserve existing monster statistics and
  effective type classifications unless an individual migration is reviewed.
- Review the status of existing tests and any known native/working descriptive
  divergences. Fix genuine regressions; do not reintroduce fingerprint or
  fixed-count assumptions for the evolving corpus.
- Keep the approved Pillar 1 and Pillar 3 contracts intact unless an actual
  implementation dependency requires a scoped change.

**Exit condition:** The selected taxonomy and origination definitions form a
coherent, tested input foundation for the next implementation stage, without
unapproved bestiary reclassifications.

## Phase 1 — Establish minimum executable contracts

**Work**

- Define the production representation of an Origin proposal, origination
  method, typed contributor roles and number of contributors, current
  Species/Ancestry, candidate characteristics, manifested characteristics,
  dependent choices, explicit table decisions, and history/provenance.
- Resolve the **minimum blocking** first-party default semantics for:
  standard direct-selection composition and descendant-authored intrinsic
  replacements; semantic characteristic identity and duplicates; eligibility
  versus actual grants; missing or linked parameter choices; and whole-profile
  conflict/dependency closure.
- Distinguish an unknown/unresolved first-party interaction, a supported
  interaction, a conflicting default, and an explicitly authorized DM outcome.
- Keep standard, mixed, nested, custom/homebrew, and reborn Origins representable
  under one contract. History is evidence, not automatic entitlement.
- Define how effective table decisions take precedence without changing
  authoritative native definitions. Do not require a fixed biological
  contributor count or a precomputed pair-compatibility matrix.

**Exit condition:** The runtime contracts can express all above source and
result situations. Remaining policy questions are explicit and do not block
constructing the initial supported profiles.

## Phase 2 — Implement the universal C# construction pipeline

**Work**

- Implement deconstruction, combining eligible candidates, general
  reconciliation, manifestation, and reconstruction in
  `RulesCore.Mechanics` using real native catalog entities.
- Carry source identity, authored occurrence identity, selected parameter
  values, relevant choices, and the provenance of each effective grant.
- Make dependent choices and unresolved/unsupported effects visible; never
  silently turn a candidate entitlement into a complete active grant.
- Integrate common scoped DM decisions into validation and reconstruction,
  rather than implementing independent exception handlers for each Species.
- Return an effective, coherent current Origin and sufficient decisions and
  history to reproduce or inspect the result. Do not rely on a Dragonborn-only
  construction path.

**Exit condition:** One production evaluator can accept real direct and
mixed-source proposals, identify unresolved decisions, and produce a
traceable current configuration. Existing Python studies remain supporting
evidence, not an alternate authority.

## Phase 3 — Finish the biological mixed-Origin vertical slice

First prove **lossless direct reconstruction**, then mixed contribution through
the **same pipeline**. Apply first-party compatibility rules and allow scoped
DM decisions where relevant. Use real selected Species/Ancestry content and
mechanically meaningful grants rather than only synthetic example traits.

| Acceptance case | Required evidence |
| --- | --- |
| Standard Human | Reconstruct the direct Species and its effective traits without inventing a second parent. |
| Standard High Elf | Compose Species and Ancestry, including inherited traits and dependent choices. |
| Human + Elf | Combine different Species and creature types without assuming creature-type targeting or mechanically stacking both full packages. |
| High Elf + Wood Elf | Handle common foundations, duplication, and distinct intrinsic values without silently inventing a generic winner. |
| Red + Bronze Dragonborn | Reconcile actual shared features with distinct damage, area, resistance, and related parameter choices. |
| Already mixed contributor | Use a contributor's manifested configuration without granting every source in its historical ancestry. |
| DM-authorized variant | Evaluate the result under the table's explicitly overridden effective rules and retain the ruling's provenance. |

**First major delivery milestone:** A mixed-Species character can be constructed
from real native content and reconstructed into a mechanically usable current
profile. Completion requires tests proving source and grant attribution,
parameter binding, dependency/conflict handling, and non-stacking behavior.
Do not require full Character Sheet integration before this milestone.

## Phase 4 — Exercise contrasting origination and transformation cases

These are **contract and engine generality checks**, not a requirement to ship
four complete Species or ritual implementations in parallel.

| Case | What must be demonstrable |
| --- | --- |
| Plant reproduction | Its own process, with no forced humanoid reproductive roles. |
| Construct assembly | Nonparent input roles (such as material, design, creator, and animation) without automatically inheriting the creator's Species package. |
| Fiend spawning | Two available methods on one type; spawning must not require biological parents, and ordinary biological reproduction remains possible where applicable. |
| Rite of Rebirth | Replace the current Species/Ancestry and attributable grants, retain other sources and history, enforce any authored restricted acquisition by default, and permit an explicit DM alternative. |

If a case reveals an actual general-contract defect, correct the shared
model before extending the corresponding specialized content. An individual
test fixture is not a claim that every associated creature's detailed
production rules are finalized.

**Exit condition:** The general system can express these processes without
a two-parent hard-code, a special-purpose Dragonborn engine, or separate
incompatible construction pipelines.

## Phase 5 — Rules Core API, persistence, and Character Sheet integration

**Work**

- Expose create/preview, validation/adjudication, reconstruction, update, and
  retrieval through the authoritative Rules Core API, with clearly
  differentiated draft, unresolved, table-authorized, and effective states.
- Persist current manifestation and effective grants separately from
  contributors, historical processes, independent grants, and override
  decisions. Support nested/mixed contributor histories and later rebirth.
- Ensure changes to Origin state recompute or update affected grant
  attribution without duplicating or silently discarding independent grants.
- Make Character Sheet consume Rules Core's resulting Origin and grants, not
  reimplement the compatibility or inheritance rules. Keep Background and
  Upbringing independent of inherent Species/Ancestry characteristics.
- Verify save/reload, repeated evaluation, ordinary content evolution, and
  authorized custom rules. Test a character in the actual development
  environment.

**Second major delivery milestone:** A user can create, save, reload, and use
a mixed-Origin character through Character Sheet and Rules Core, with
traceable effective grants and retained DM decisions.

## Validation and checkpoints

Validation should follow the architecture and cover native content
consistency, characteristic identity, method inheritance, source eligibility,
choice completion, dependency closure, manifest grants, whole-profile
coherence, historical provenance, and table-specific decisions. Tests should
prove behavior, not freeze the number of entries in an actively developed
ruleset.

Progress is measured by **three working checkpoints**:

1. **Foundation validated:** Approved taxonomy/content consolidated; minimal
   executable contracts established.
2. **Mixed Origin working:** Direct and mixed real-content Origin profiles
   constructed by one authoritative Rules Core pipeline.
3. **Playtest ready:** API and Character Sheet can persist and use that result.

Record actual test commands and outcomes in the implementing changes. This
document is a plan; it does not claim those tests or runtime milestones are
already complete.

## Defer until these milestones are supported

- Exhaustive biological compatibility modeling or an independent reproductive
  taxonomy; arbitrary Species-pair tables without specific design need.
- Full authored mechanics for every Plant, Construct, Elemental, Ooze, or
  other origination process.
- Final rituals, costs, and complete native content for exceptional Dragonborn
  Ancestries; separately approved trait designs remain available for later
  implementation and playtesting.
- Universal balance budgets, speculative trait restrictions, or a campaign
  power-equivalence requirement as prerequisites to functional mixed Origins.
- A complete rewrite of the monster bestiary unrelated to the agreed
  creature-type retirement and specific migration findings.

## Related specifications

- [Universal Origin model and the five operations](pillar2-universal-origin-model-first-20261111.md)
- [Direct Species/Ancestry reconstruction readiness](pillar2-standard-profile-readiness-20261111.md)
- [Creature types and origination-method defaults](pillar2-creature-origination-taxonomy-20261111.md)
- [Rite of Rebirth and exceptional acquisition](pillar2-rites-of-rebirth-20261111.md)
- [Native origination contract](../../../data/rulesets/first-party-001/native/rules/creature-origination.json)
- [Global native rules design specification](../native-rules-design-specification.md)
