# Pillar 2, Phase 1 — production Origin contract boundary

**Date:** 2026-11-12
**Implementation branch:** `feature/pillar2-phase1-origin-contracts-20261112`
**Roadmap:** [Pillar 2 implementation roadmap](pillar2-implementation-roadmap-20261111.md)

## Implemented contract

The production C# types reside in `src/RulesCore.Mechanics/NativeOriginContracts.cs`, with bounded structural inspection in `NativeOriginContractValidator.cs`. They are not an API, character-creation workflow, inherited-trait policy, or source-to-grant evaluator.

The model distinguishes:

- **Origination:** A native `rule.creature-origination` method reference, resolved by reading the selected catalog's `methodDefinitions` without a second method registry or a fixed method count. Contributors have typed general roles and optional process-specific role IDs. Arbitrary input counts and explicit process-specific contributor-count requirements are representable without enforcing a universal parent count.
- **Source:** Named native, custom, or previously manifested profiles. A previously mixed parent includes its **current manifested configuration**, with historical source IDs retained separately. History is never a source of automatic entitlement.
- **Identity:** The source occurrence is always separate from an optional canonical mechanical definition, trait concept and intrinsic slot. Equal names, values, concepts or provisional mappings do not prove equivalent mechanics.
- **Eligibility:** A candidate carries its own selection basis, authored source occurrence, offered value/parameters and unresolved evidence. Candidate existence does not create a manifestation or a grant. Unknown native bindings remain unresolved.
- **Manifestation:** A candidate-bound selection carries chosen intrinsic values, parameters and persistent dependent choices. Finite menus and references to open catalogs are distinct. On-use choices are not persisted as creation choices.
- **Reconciliation:** Declarative relationships, whole-profile selectors and constraints (requirements, exclusions, counts, linked parameters), and explicit dispositions are recorded separately. Distinctions between supported, conflicting, unresolved and DM-authorized rulings are preserved. No automatic winner, duplicate stacking, cross-source equivalence, linked parameter or whole-profile closure is inferred.
- **Current state:** Each effective inherent grant has its own grant ID, manifested instance, candidate provenance, complete source occurrence and selected parameters/choices. The current Species/Ancestry configuration is separate from independent grants and historical configurations; a profile need not have a standard Species label.
- **Transformations:** Historical events can be pending, in progress or completed and can preserve prior configurations. Both Rite of Rebirth (a complete replacement default) and Supernatural Reconstruction (possibly staged) fit this shape. They are not executed by the validator.
- **Universal DM rulings:** The reusable `NativeTableRuleDecision` contract in `src/RulesCore.Mechanics/NativeTableRuleDecisions.cs` records an actor, authorization evidence reference, scope, target and effective replacement JSON. These may replace mechanical outcomes, not merely waive a prohibition. A profile claiming DM authority must reference a scoped table decision; these are still *claims to be verified* by trusted campaign authorization, not permissions granted by submitting JSON. First-party catalog contents remain unchanged.

## What structural inspection can and cannot establish

`ReviewProposal` checks catalog method references, unique input/candidate/selection IDs and contributor-requirement bounds, contributor and source occurrence provenance, authored choice references and bounds, unresolved eligibility/evidence, explicit relationship dispositions and table-decision references. `ReviewProfile` checks grant attribution, independent versus inherent grant identity, historical preservation and repeated intrinsic slots. Results are **structurally consistent**, **incomplete**, **unresolved**, **conflicting** or **invalid**. A structurally consistent result is never a mechanically authorized or playable Origin.

The validator intentionally does not issue effective grants or certify DM authority. The standalone profile records in tests represent possible states the Phase 2 engine will later need to produce. They are not examples of grant issuance by this Phase 1 validator. The data model permits more detailed authored relationships and arbitrary effective decisions without requiring Species-specific executable rules.

## Minimum semantic decisions and open questions

- **Confirmed boundaries:** Unselected candidate offers do not grant abilities; a manifested profile's current grants differ from its history; an authorized Rite replaces the previous current Species/Ancestry configuration by default, preserving independent grants; multiple methods per type and nonparent input roles are valid; exact source provenance must survive.
- **Explicitly unresolved:** General descendant-versus-parent intrinsic precedence (including Wood Elf movement), cross-source mechanical equivalence, duplicate resource/trait stacking, upgrade/substitution policy, incompatible physical properties, contributor transmission of latent traits, unrestricted custom-selection permission, and complete choice/parameter eligibility. A relation with no approved disposition remains unresolved rather than guessed.
- **Phase 2 dependencies:** Source deconstruction using the actual native definitions, catalog-backed option eligibility, choice binding, whole-profile dependency closure, scoped DM authorization verification and execution, grant/retraction attribution, and a single coherent composition/reconstruction evaluator. Full Rite execution and Pillar 4 progression remain later phases.

The contract permits all current native origination methods by reference, including `supernatural-reconstruction`, without adding an independent C# taxonomy. It preserves future method fields by retaining a clone of the authored JSON and does not interpret unknown fields as approved behavior. The fixture tests exercise more than one method and unknown future methods without freezing the first-party catalog count.

## Phase 1 follow-up: temporary effective Species/Ancestry substitution

**Sources:** [Pillar 4 initial reconciliation](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/pillar4-supernatural-nature-design-20261111/docs/rewrite/character-origins/pillar4-supernatural-nature-initial-reconciliation-20261111.md) and [lycanthropy reconciliation](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/pillar4-supernatural-nature-design-20261111/docs/rewrite/character-origins/pillar4-lycanthropy-reconciliation-20261112.md), approved architectural boundaries only. Their individual supernatural progression candidates and numerical mechanics are *not* approved gameplay.

The shared `NativeOriginProfile` holds three orthogonal concepts:

1. **`Underlying`** — the latest authoritative Species/Ancestry configuration, including current intrinsic grants and any permanent reconstruction. This baseline persists while a separate form is active.
2. **`Current`** — the momentarily effective Species/Ancestry configuration. Without an active temporary replacement it ordinarily matches `Underlying`, but an explicit `EffectiveConfigurationBasis` may record an authored non-form modifier or scoped DM ruling that changes the effective state. Snapshot IDs and grant enumeration order do not constitute differences. An unexplained mechanical difference remains unresolved. With an active form, this is an independent, complete form-specific inherent-grant set; the baseline is *not* automatically unioned into it.
3. **`ActiveTemporaryTransformation`** — a source-qualified active form, activation evidence, optional reference to a persistent influence, optional explicit form-grant identities, and optional shared DM adjudication. This record **does not** store a baseline or any `RestoreConfiguration` snapshot.

`PersistentInfluences` records acquired or inherited long-lived conditions and their provenance independently of the currently active form. Having lycanthropy does not imply that the subject has unlocked wolf form, is transformed, has lost control, or has inherited a monster stat block. A Human with an inactive werewolf influence has `Underlying == Current` in its Species/Ancestry state even when the influence record persists.

`NativeOriginCurrentConfiguration` remains a reusable configuration value record and can represent either role depending on its owning profile. `NativeOriginHistoryEvent.ReplacesConfiguration` continues to describe **permanent baseline** replacements such as a completed Rite of Rebirth; temporary form activation is not misclassified as origination or permanent reconstruction. Any historical `PreviousConfiguration` is audit data, not a restoration target.

`NativeOriginProposal.ExistingConfiguration` identifies the authoritative **underlying** state at proposal time, not an active form. The optional `ExistingEffectiveConfiguration` and `ActiveTemporaryTransformation` capture distinct inputs when a proposal is considered during an existing form. `ExistingEffectiveConfigurationBasis` records a legitimate modifier or scoped DM ruling when inputs differ without an active form. The records do not confer entitlement or execute transitions.

**Concurrent active effects:** `OtherActiveTemporaryTransformations` preserves the source and activation evidence of additional active transformations. `TemporaryTransformationPrecedence` records a supported authored rule, unresolved or conflicting interaction, or a scoped table decision. The primary `ActiveTemporaryTransformation` identifies the proposed source of the effective form, **not** an unconditional priority over other effects. With multiple active transformations and no explicit precedence basis, structural review returns `Unresolved`; it never chooses the first or last effect automatically. These same fields exist on the proposal and resulting profile.

**Independently earned benefits:** `IndependentGrants` retains source-linked entitlement, while optional `IndependentGrantApplicability` records `Applicable`, `Suppressed`, or `Unresolved` use in the current mechanical state. A missing applicability record is not implicit permission to use a benefit. A claimed applicable or suppressed state requires an authored rule reference or scoped DM ruling; validation checks structural attribution but does not execute it. A transformation can therefore suppress use of a Class or Background grant without deleting its entitlement or historical origin.

### Acceptance examples and reversion invariant

- **Dormant influence:** Human underlying and effective, persistent lycanthropy source, no active form.
- **Active wolf:** Human underlying and wolf-form effective, wolf activation evidence and form grants recorded separately; Human-specific grants need not remain active in the effective form.
- **Human reversion:** A later explicit state has no active form and `Current` matches `Underlying`. The test constructs this new state; the Phase 1 validator does not perform a reversion.
- **Change during wolf form:** A completed authorized Rite may replace the underlying Human with another Species/Ancestry while wolf remains the recorded effective form. This represents a possible intermediate state, **not** a universal rule that the wolf effect necessarily survives the Rite.
- **Updated reversion:** A later explicit state uses the **new** underlying configuration for `Current`, without resurrecting superseded Human grants from the previous baseline or an activation-time snapshot. Attempting to substitute the obsolete Human snapshot with no active effect or valid override is reported as unresolved. Independently sourced grants remain separately recorded.
- **Concurrent wolf and external form:** Both active sources are represented; precedence is unresolved unless a rule or scoped DM decision supplies the effective choice. An explicitly conflicting precedence basis is reported as `Conflicting`.
- **DM-authorized reversion variation:** A distinct effective result after the form ends is permitted as a contract representation with a `NativeOriginDecisionBasis` linked to the shared `NativeTableRuleDecision` (authority, scope, target, outcome); this is not self-verifying permission.
- **Independent Class entitlement:** A Class feature can remain earned while use is recorded as suppressed in wolf form, and later recorded as applicable in another state. Phase 1 performs neither transition nor suppression.

The validator checks source and activation evidence, optional linkage to persistent influences and named form grants, missing distinct baseline/effective inputs on an active proposal, competing form identities, effect precedence and independent-grant applicability references, and independent/inherent grant identity. Baseline grant verification uses the baseline proposal; it does not falsely demand that a wolf-form grant originate from a Human candidate. These are *structural* checks, not proof of actual effect authority, player agency, or effective gameplay mechanics.

### Outstanding transformation precedence decisions

No general rule has been adopted for overlapping temporary forms, polymorph, permanent Rite execution during an active form, changing baseline trait bindings while the form is active, form persistence after death/cure/dispel, precedence among other active effects, or when a form-specific grant should cease or be retained. A transformed contributor's effective form versus underlying inheritable characteristics also requires an explicit transmission policy; neither is automatically eligible. Form-specific creature type, targeting, anatomy, available actions, and suppression/retention of underlying traits still require authored rules or an authorized DM decision. In particular, the reversion invariant identifies the **latest authoritative baseline** as the source of truth but does not authorize or implement a transition algorithm.

No universal trigger, moon phase, loss of control, alignment change, stage unlock, animal Hit Dice, or werewolf monster features have been added. No `supernatural-reconstruction` origination method is attached to an ordinary reversible form change; permanent reconstructive processes remain separate. The shared Origin evaluator and transformation execution belong to later phases.

## Validation

Tests: `tests/RulesCore.Mechanics.Tests/NativeOriginContractTests.cs`.

Validated commands (branch CI):
- `dotnet restore RulesCore.slnx`
- `dotnet build RulesCore.slnx -c Release --no-restore`
- `dotnet test RulesCore.slnx -c Release --no-build`
- `python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v`

Phase 1 baseline CI run [43161998430](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43161998430) passed the complete .NET solution build, **119 Mechanics tests**, **111 Host tests**, and **345 Pillar 2 Python tests**, with zero warnings.

Temporary effective configuration follow-up CI [43168553663](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43168553663) passed **138 Mechanics tests**, **111 Host tests**, and **345 Pillar 2 Python tests**, with **zero warnings and zero errors**. The expanded Origin contract test class has **44 focused tests**, including original mixed/nested/Rite/reconstruction coverage and additive condition, temporary form, reversion, baseline reconstruction during an active form, stale restoration, simultaneous effects and DM precedence, independent-grant applicability, and JSON round trips. This run covers all production code and tests; subsequent edits in this branch update documentation only. The full repository-wide Python suite was not rerun; previously known native/working corpus divergences remain outside this Phase 1 follow-up.

**Phase boundary:** No API/Character Sheet integration, general Origin executor, Species-specific mixture rules, or native exceptional Dragonborn Ancestry records are created here. Phase 2 begins only under separate authorization.
