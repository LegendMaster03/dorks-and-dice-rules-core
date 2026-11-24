# Pillar 4 Workstream A — Supernatural acquisition and progression

**Branch:** feature/pillar4-acquisition-progression-20261119
**Status:** Generic mechanics only. Produces proposals, not committed writes or first-party content.

## Implemented versus missing

Current main already provides NativeCharacterState, NativeCharacterPersistentInfluence, source/version-qualified progression and earned grants, expression and applicability evaluation in NativeCharacterInfluenceResolver, as well as independent temporary-form manifestation planning. Pillar 2 already owns construction and permanent reconstruction of authoritative Origins. Workstream A does not duplicate these.

New NativeSupernaturalProgressionPlanner supplies source-authorized transitions for acquisition, advancement and reversible expression changes.

## Inputs and host trust

Plan takes existing verified NativeCharacterState, a revision-current NativeSupernaturalTransitionJournal, exact version-pinned NativeSupernaturalDefinition, one NativeSupernaturalTransitionRequest, and INativeSupernaturalProgressionAuthority.

The authority extends existing character/influence verification. It independently verifies the canonical rule or scoped table decision, exact node/effect/grant/choice definition, ownership, acquisition, progression, event evidence and expression change. A string ID, submitted receipt, submitted rule definition or saved-state copy is not an entitlement. Absent positive verification yields unresolved, without a proposed state.

The definition contains optional immediately acquired nodes and later nodes. Each node may specify minimum total character level, prior nodes, mutually exclusive branch, verifiable event keys, other condition keys, finite one-of choices, grants, and existing typed influence effects. The model imposes no universal supernatural level/points/stages/Class expenditure, and no biological inheritance. An absent prior-node requirement permits a direct transition to any eligible authored node.

Choices persist using the existing progression choices dictionary (one selected value per finite choice). More complex multi-option cardinality remains a potential shared-contract extension, not an invented serialized format.

Event requirement evidence is independently host verified and uniquely consumed across the character journal. Reusing a consumed event for another transition, including with a different transaction ID, is rejected. A genuinely repeatable event requires independently authenticated new evidence.

## Outputs and commit semantics

A successful result has Status planned, ProposedState, ProposedJournal, NewProgression, and NewGrants. These use the existing NativeSheetEarnedProgression, NativeSheetEarnedGrant, NativeCharacterInfluenceEffect, and NativeInfluenceGrantApplicability contracts and exact source occurrences. Grant use defaults to unresolved until Workstream B evaluates authored applicability. Existing Class, Origin, primary mechanics, other influences, alternate sheets and unrelated history are not replaced. Reversible expression never removes earned progression.

The host must atomically compare the expected revision against durable storage, consume the transaction and event evidence once, and save proposed state plus journal. The planner cannot commit, lock, or authenticate a client on its own. Concurrent writes must use transactionally enforced compare-and-swap. Proposed node/effect authorization must bind the full source definition, not merely its name.

## Workstream boundaries

**A owns:** authenticated source-specific acquisition, eligibility, earned advancement, finite one-of progression choices, consumed event identity, and revision-checked proposal generation.

**B owns:** effective character resolution, collisions, mechanical applicability, transformations, and authored effect interactions. It consumes the existing influence/progression/effect records and may use the separate manifestation ledger. No new effect-operation grammar is introduced.

**C and host own:** canonical source-definition loading, ownership/campaign authentication, event proof, authoritative persistence, atomic journals, Character Sheet storage and public endpoints. Unifying journals with manifestation transactions requires cross-workstream agreement before any shared contract change.

## Synthetic tests and commands

Synthetic tests cover immediate acquisition/effect, dormant grants, event and level gates, branching and finite choices, independently progressing acquisitions, expression suppression/restoration without loss, denial of source/record authority, event/revision/operation replay, changing the underlying character without progression resets, and unresolved condition eligibility. Fixtures are not production gameplay authorities.

.NET 10 commands:

    dotnet restore RulesCore.slnx
    dotnet build RulesCore.slnx -c Release --no-restore
    dotnet test RulesCore.slnx -c Release --no-build
    python3 tools/validate_ruleset_split.py
    python3 tools/audit_native_baseline.py --native data/rulesets/first-party-001/native --strict

## Out of scope

No first-party Draconic Bloodline, lycanthropy, vampirism or ceremorphosis content, numeric balance, gameplay endpoints, production host authorizers, inheritance, UI, or permanent reconstruction is supplied.
