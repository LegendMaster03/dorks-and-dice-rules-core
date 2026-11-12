# Pillar 2 Phase 2 — native Origin construction implementation

**Date:** 2026-11-12
**Branch:** feature/pillar2-phase2-construction-engine-20261112
**Base:** feature/pillar2-characteristic-inventory-20261111
**Roadmap:** [Pillar 2 implementation](pillar2-implementation-roadmap-20261111.md)
**Prior contracts:** [Phase 1 production boundary](pillar2-phase1-production-contracts-20261112.md)

## Architectural gaps identified

Phase 1 supplied the vocabulary for contributors, source occurrences, candidates, manifestations, adjudication, underlying/effective configurations, and history, but **no runtime construction**. Its validator only checked structural and reference consistency, not permission to issue mechanical grants.

The native catalog contains real Species, multi-level Ancestry families, and origination methods. It does **not** yet contain complete executable cross-source equivalence, transmission, precedence, choice-catalog, and whole-profile compatibility rules for every mixed Heritage. Some traits are descriptive-only or refer to leaf-specific parameters without approved binding contracts. A matching trait label alone never authorizes equivalent mechanics or duplicate grants.

An authenticated campaign DM authorization service, persistent API, form transition executor, and complete character-option eligibility adapters are not part of Phase 1.

## Production components

- **NativeOriginSourceDeconstructor** uses real selected-native Species and Ancestry definitions, following multi-level parent chains. It extracts exact occurrence identities, intrinsic fields, trait definitions, and dependent selection offers. Nested contributors supply their **current** manifested grants as offers only; historical ancestry is not a grant source. Creators and materials may participate without copying their Species.
- **NativeOriginProcessEvaluator** uses the native method reader and only verifies authored or authenticated table-specific input requirements. No universal parent count or creature-type reproduction ban is introduced.
- **NativeOriginConstructionEngine** accepts a Phase 1 proposal, discovers candidates, evaluates persistent selections and trusted open-catalog proofs, and issues effective grants with source and manifestation attribution. Only directly selected, choice-free native defaults can be manifested automatically; mixed contributor traits require separate authorization and actual manifestation.
- **NativeOriginConstraintEvaluator** enforces supported or authenticated prerequisites, exclusions, cardinality, and linked-parameter conditions over selected mechanics. Unsupported semantics remain unresolved.
- **NativeOriginReconciler** consolidates exact identical authored occurrences while retaining source-offer references. Distinct source occurrences, shared cross-source definitions, intrinsic-slot collisions and unapproved substitutions require explicit policy or a verified DM disposition.
- **NativeOriginConstructionContracts** defines the evaluation statuses and an application-authenticated INativeOriginAuthority boundary. Serialized table decision claims never prove their own authority.

All components reuse the Phase 1 NativeOriginProposal, NativeOriginCandidate, NativeOriginManifestation, NativeOriginEffectiveGrant, and NativeOriginProfile contracts; no second Origin authority or profile format was created.

## Authorization and effective-state boundaries

Authorized, Incomplete, Unresolved, Conflicting, and Invalid results are distinguishable. A **partial profile is not an authorized playable profile**. Consumers must check IsMechanicallyAuthorized rather than treating a nonnull Profile as permission.

The authenticated host is responsible for supplying the trusted scope, table authority, and open-catalog option proof. Each effective table outcome is scoped to its decision target and does not alter first-party native definitions. Raw client-supplied decisions and option evidence are not accepted as authority.

Underlying remains the latest authoritative baseline. Current can represent a separate active form. Independent entitlements are preserved separately. Former configurations and contributors remain historical evidence, never an automatic reversion snapshot or latent inherited grants. Phase 2 does **not** execute Rite of Rebirth, permanent reconstruction, active-form transitions, or overlapping-form precedence.

## Acceptance coverage

Real-catalog and targeted synthetic tests exercise standard Human and High Elf sources, multi-level Dragonborn Ancestry parent chains, multiple contributors, duplicate source offers, Human/Elf conflicts, finite and open dependent choices, nested manifested contributors, DM-authorized overrides and custom Sources, rejected unverified claims, process-specific input roles, whole-profile exclusions, and preservation of underlying versus temporary effective configurations.

Several real sources remain correctly incomplete or unresolved because the selected content does not yet authorize a compatible manifestation or verified option selection. The engine retains those issues rather than guessing.

## Outstanding work

1. **Phase 3 mixed-Heritage vertical slice:** Author first-party cross-source identities, transmission and relationship policies; complete source-dependent parameter bindings and whole-profile closure. Demonstrate a fully authorized mixed character with real native traits.
2. **Native mechanical normalization:** Descriptive-only trait definitions and unbound leaf parameters still require selected executable rules. Downstream combat, competency, spellcasting and advancement projectors do not yet execute every raw Origin trait effect.
3. **Trusted application integration:** Connect authenticated DM decisions, open-catalog choice eligibility and previously earned independent entitlements to the hosting API. Never accept a client-provided authority callback as authentication.
4. **Transformation execution:** Implement Rite and supernatural form transitions, reversion and precedence in their later phases, not here.
5. **Phase 5:** Persist and expose create/preview/adjudication/retrieval through Rules Core and integrate Character Sheet. Do not gate ordinary ruleset changes on a fixed fingerprint.

## Verification

The existing Pillar 2 Phase 1 Origin Contracts workflow is triggered by the draft pull request to the Pillar 2 feature branch. Its steps restore/build the complete .NET solution, run all .NET tests, and run existing Pillar 2 Python research tests. The CI result for the tested commit is authoritative; this document does not substitute for executing those checks.

The change is confined to the feature branch and is **not** merged to main.
