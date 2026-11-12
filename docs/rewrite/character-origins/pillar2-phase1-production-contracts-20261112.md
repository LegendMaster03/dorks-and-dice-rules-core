# Pillar 2, Phase 1 — production Origin contract boundary

**Date:** 2026-11-12
**Implementation branch:** \`feature/pillar2-phase1-origin-contracts-20261112\`
**Roadmap:** [Pillar 2 implementation roadmap](pillar2-implementation-roadmap-20261111.md)

## Implemented contract

The production C# types reside in \`src/RulesCore.Mechanics/NativeOriginContracts.cs\`, with bounded structural inspection in \`NativeOriginContractValidator.cs\`. They are not an API, character-creation workflow, inherited-trait policy, or source-to-grant evaluator.

The model distinguishes:

- **Origination:** A native \`rule.creature-origination\` method reference, resolved by reading the selected catalog's \`methodDefinitions\` without a second method registry or a fixed method count. Contributors have typed general roles and optional process-specific role IDs. Arbitrary input counts are supported.
- **Source:** Named native, custom, or previously manifested profiles. A previously mixed parent includes its **current manifested configuration**, with historical source IDs retained separately. History is never a source of automatic entitlement.
- **Identity:** The source occurrence is always separate from an optional canonical mechanical definition, trait concept and intrinsic slot. Equal names, values, concepts or provisional mappings do not prove equivalent mechanics.
- **Eligibility:** A candidate carries its own selection basis, authored source occurrence, offered value/parameters and unresolved evidence. Candidate existence does not create a manifestation or a grant. Unknown native bindings remain unresolved.
- **Manifestation:** A candidate-bound selection carries chosen intrinsic values, parameters and persistent dependent choices. Finite menus and references to open catalogs are distinct. On-use choices are not persisted as creation choices.
- **Reconciliation:** Declarative relationships and explicit dispositions are recorded separately. Distinctions between supported, conflicting, unresolved and DM-authorized rulings are preserved. No automatic winner, duplicate stacking, cross-source equivalence, linked parameter or whole-profile closure is inferred.
- **Current state:** Each effective inherent grant has its own grant ID, manifested instance, candidate provenance, complete source occurrence and selected parameters/choices. The current Species/Ancestry configuration is separate from independent grants and historical configurations; a profile need not have a standard Species label.
- **Transformations:** Historical events can be pending, in progress or completed and can preserve prior configurations. Both Rite of Rebirth (a complete replacement default) and Supernatural Reconstruction (possibly staged) fit this shape. They are not executed by the validator.
- **Universal DM rulings:** A common decision records an actor, authorization evidence reference, scope, target and effective replacement JSON. These may replace mechanical outcomes, not merely waive a prohibition. These are *claims to be verified* by trusted campaign authorization, not permissions granted by submitting JSON. First-party catalog contents remain unchanged.

## What structural inspection can and cannot establish

\`ReviewProposal\` checks catalog method references, unique input/candidate/selection IDs, contributor and source occurrence provenance, authored choice references and bounds, unresolved eligibility/evidence, explicit relationship dispositions and table-decision references. \`ReviewProfile\` checks grant attribution, independent versus inherent grant identity, historical preservation and repeated intrinsic slots. Results are **structurally consistent**, **incomplete**, **unresolved**, **conflicting** or **invalid**. A structurally consistent result is never a mechanically authorized or playable Origin.

The validator intentionally does not issue effective grants or certify DM authority. The standalone profile records in tests represent possible states the Phase 2 engine will later need to produce. They are not examples of grant issuance by this Phase 1 validator. The data model permits more detailed authored relationships and arbitrary effective decisions without requiring Species-specific executable rules.

## Minimum semantic decisions and open questions

- **Confirmed boundaries:** Unselected candidate offers do not grant abilities; a manifested profile's current grants differ from its history; an authorized Rite replaces the previous current Species/Ancestry configuration by default, preserving independent grants; multiple methods per type and nonparent input roles are valid; exact source provenance must survive.
- **Explicitly unresolved:** General descendant-versus-parent intrinsic precedence (including Wood Elf movement), cross-source mechanical equivalence, duplicate resource/trait stacking, upgrade/substitution policy, incompatible physical properties, contributor transmission of latent traits, unrestricted custom-selection permission, and complete choice/parameter eligibility. A relation with no approved disposition remains unresolved rather than guessed.
- **Phase 2 dependencies:** Source deconstruction using the actual native definitions, catalog-backed option eligibility, choice binding, whole-profile dependency closure, scoped DM authorization verification and execution, grant/retraction attribution, and a single coherent composition/reconstruction evaluator. Full Rite execution and Pillar 4 progression remain later phases.

The contract permits all current native origination methods by reference, including \`supernatural-reconstruction\`, without adding an independent C# taxonomy. It preserves future method fields by retaining a clone of the authored JSON and does not interpret unknown fields as approved behavior. The fixture tests exercise more than one method and unknown future methods without freezing the first-party catalog count.

## Validation

Tests: \`tests/RulesCore.Mechanics.Tests/NativeOriginContractTests.cs\`.

Run \`dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj\` and \`python -m unittest discover -s tests\` from the repository root. Report results separately from passing structural fixtures and do not treat unrelated existing content errors as newly introduced Origin regressions.

**Phase boundary:** No API/Character Sheet integration, general Origin executor, Species-specific mixture rules, or native exceptional Dragonborn Ancestry records are created here. Phase 2 begins only under separate authorization.
