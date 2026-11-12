# Pillar 4 / Pillar 2 — shared Origin, grant, and transition contract reconciliation

**Review date:** 2026-11-12  
**Status:** Contract recommendations for cross-pillar review. This document does **not** authorize new first-party supernatural mechanics, API endpoints, or a merge.  
**Pillar 2 verified base:** `feature/pillar2-characteristic-inventory-20261111` at `d6f06a6ab058` (Phase 4 PR #150 merged into this development branch; not `main`).  
**Pillar 4 design branch:** `feature/pillar4-supernatural-nature-design-20261111`.  
**Pillar 4 prototype PRs:** [#146](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/146), [#148](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/148), [#149](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/149) — **drafts, not approved integration**.  
**Related plan:** [Pillar 4 dependency review](pillar4-pillar2-dependency-review-and-revised-plan-20261112.md).

## Decision summary

**Pillar 2 owns persistent authoritative Origin construction and baseline transitions.** Pillar 4 owns supernatural source progression, form-specific policies, triggers, and duration. A shared Rules Core effective-character layer composes eligible grants and active/suspended effects with the character's Class, inventory, resources, actions, conditions, and targeting. Neither Pillar 4 nor Character Sheet is to implement a parallel Species constructor or independent source of earned entitlements.

The confirmed `Underlying` / `Current` / `ActiveTemporaryTransformation` contract remains correct. The completion of Pillar 2 Phase 4 removes the need for a second permanent-reconstruction executor: `NativeOriginRebirthEngine` already supplies atomic complete replacement for a specifically authorized Rite. Extending that **shared baseline transition boundary** to other independently authorized post-origination methods is an integration-design task, not an automatic permission to execute `supernatural-reconstruction`.

Universal **DM overrides at every gameplay level** remain mandatory, but an authorized first-party source should not require a DM decision solely because the engine lacks a native-rule execution path. Neither native-rule proof nor table approval bypasses application authentication or character ownership.

## Existing verified APIs and distinctions

| Record/component | Contract as implemented | Must not be assumed |
| --- | --- | --- |
| `NativeOriginConstructionEngine.Evaluate` | Constructs selected Origin grants from catalog and verified choices; `Authorized` means the **construction output** resolved under its known rules | That all values supplied through `ExistingIndependentGrants`, `PersistentInfluences`, and `IndependentGrantApplicability` were earned or authenticated by the construction engine |
| `NativeOriginProfile.Underlying` / `Current` | Authoritative baseline versus current effective Species/Ancestry and inherent grants | A complete current character, including Class features, equipment, spell resources, HP, actions, and conditions |
| `NativeOriginActiveTemporaryTransformation` | Form occurrence, form definition ID, source and activation evidence, related persistent influence, effective form-grant IDs | An executed or mechanically authorized form transition |
| `NativeOriginIndependentGrantApplicability` | Separately tracks earned entitlement versus current applicability with adjudication basis | A source of earned independent features or an automatic permission to activate a feature |
| `NativeOriginRebirthEngine.Execute` | Requires existing authoritative profile, separately reconstructed target, specific DM-verified Rite, atomic baseline replacement, history, independent-grant preservation, explicit current-form interaction | Execution of arbitrary supernatural reconstruction, full form transitions, persistence, or a replacement for a host-origin trust check |
| `INativeOriginAuthority` and `NativeTableRuleDecision` | Host callback for scoped DM decisions and open-catalog choice evidence | An authenticated character/campaign identity by itself, or a native supernatural effect authorizer |

Actual pre-API trust boundary: `NativeOriginConstructionRequest` exposes `ExistingIndependentGrants`, `IndependentGrantApplicability`, and `PersistentInfluences` as inputs. These are **data transport**, not grants automatically verified by `NativeOriginConstructionEngine`. The host or common character authority must supply server-verified canonical values; externally submitted copies must never become playable entitlements.

## Reconciled conceptual contracts

The concrete C# names and public API JSON shapes below are **not finalized**. Adopt minimal changes to existing records where possible; do not add a parallel character-inventory model.

### 1. Source, entitlement, and applicability

A common earned-characteristic reference needs at least:

- A stable **instance/occurrence ID** on this character; a stable native **source definition ID**, with its original source occurrence/path and parameter/choice bindings where applicable.
- An **acquisition/completion event ID** or other independently authenticated proof of entitlement. A persistent supernatural influence is a source fact, not proof that every available progression node was earned.
- A **grant kind/source** (inherent Species/Ancestry, Class/Background/Upbringing or other independent advancement, supernatural-acquired, and temporary form) without forcing all into biological inheritance semantics.
- Separately evaluated **current applicability** (applicable, suppressed, or unresolved), with supported native policy or scoped DM decision and exact provenance.
- Stable resource/feature identities independent of whether the form currently permits use. Do not delete earned progression, counters, inventory ownership, or history to simulate suspension.

Pillar 2's `NativeOriginEffectiveGrant` is already an excellent model for **manifested Origin-derived** characteristics with `Identity`, `SourceCandidateIds`, `EffectiveValue`, `Parameters`, and `ResolvedChoices`. **Do not lose Phase 3's resolved `originBindings` or source-dependent choice identities** when the shared effect layer consumes a grant. A separate minimal typed grant envelope/adapter may be necessary for Class and supernatural progressions; do **not** make the Species deconstructor authorize arbitrary class abilities.

### 2. Authoritative transitions

- Preserve Pillar 2's **controlled post-origination target path** `EvaluateReconstructionTarget`. Initial creation must not authorize the `rite-of-rebirth` or `supernatural-reconstruction` method by ID alone.
- The **method-neutral replacement operation** should validate a separately authorized completion event, reconstruct its exact target, apply any supported retain/remove rules, append a completed historical event, and publish `Underlying` only on whole-transition success.
- Keep each method's distinct acquisition and authorization contract. The existing Rite path is presently **DM-only because no individual selected-native Rite procedure exists**; this must not become a universal gate for first-party supernatural reconstruction.
- Separate temporary-form activation/reversion (Pillar 4 execution using Pillar 2's Origin records) from permanent baseline replacement (Pillar 2). Form **entry and exit** are not origination events or `supernatural-reconstruction`. Effective `Current` is derived from a verified source-specific form profile, never taken as client-authoritative gameplay.
- A completed permanent transition **during an active form** must have an explicit interaction policy (supported first-party rule or scoped DM override). On eventual form exit, re-evaluate the **latest `Underlying`**; do not restore an activation-time snapshot.
- Repeated transition IDs, stale state, incomplete targets, unresolved interactions, and conflicting grant identity must fail closed **without publishing a partial new baseline**. Retained, dropped, or re-keyed grants preserve source/history audit. API persistence later must provide optimistic concurrency or equivalent atomicity against the actual current saved character revision.

### 3. Effective mechanics outside Origin

The shared effective-character evaluator, not the Origin constructor, should apply **source-qualified** operations affecting abilities, Class-feature availability, creature type, attacks/actions, movement, speech, equipment usability, spellcasting, resources, conditions, HP, and vulnerabilities. Per-form rules specify **which** scopes retain, modify, suspend, or replace. Persisted resources and owned equipment are separate from transient ability to use them. Unknown overlaps remain unresolved unless rules or a trusted DM decision settle them.

Pillar 4's prototype effect authorizer in PR #149 introduces `effectiveEffectDefinitions` as a candidate native grammar. It is **not an approved extension** and must be reconciled with existing `originBindings`, source-specific parameters and dynamic/level-dependent mechanics. Its first-party `AuthorizesForm` currently requires a DM form decision; that is a **Pillar 4 prototype defect** to correct in the effect/form authority, not a reason to force native form execution to become DM-only in Pillar 2.

### 4. Host trust and future API boundary

Before adding public Origin construction/reconstruction routes, distinguish three layers:

1. **Trusted saved character state:** The host authenticates the caller, campaign/table scope, character ownership, current saved revision, authoritative `ExistingProfile`, and earned non-Origin grants/effects. Never use submitted `ExistingProfile`, independent grants, history or pending DM decisions as trusted facts.
2. **Rules Core mechanical authority:** The catalog's current first-party definitions (with preserved source/parameter identities) and appropriately scoped, *authenticated* DM modifications determine what is allowed. Missing native rules yield incomplete/unresolved, not permission or a prohibition inferred from a missing creature-type default.
3. **Consumer result:** Preview can report candidate/partial/unresolved state, but only a completely authorized transition result can be saved as new authoritative state; persist a completed event **once**, preserving unique occurrence IDs, scope and version. Character Sheet consumes the result and never reconstructs an alternate Origin locally.

The current `NativeOriginConstructionResult.IsMechanicallyAuthorized` is not sufficient to prove an arbitrary **caller-provided** `ExistingIndependentGrants` or `PersistentInfluences` list is authentic. A future server-backed trust adapter, or a construction request contract that distinguishes verified host context from user proposals, must enforce that boundary.

## Pillar 2 pre-API corrective work — prioritized handoff

| Priority | Request to Pillar 2 owner | Acceptance evidence |
| --- | --- | --- |
| **Blocker** | Establish a trusted **existing-profile/character-state** handoff for construction and Rite, including real ownership, campaign scope, earned independent grants, persistent influences and current state. Existing constructor inputs must not be client-authoritative simply because `Status == Authorized` | Tests that tampered old state, injected Class/supernatural grants, forged influences and wrong table/character scope cannot create an authorized committed profile |
| **Blocker** | Define a **shared method-neutral permanent reconstruction boundary** around the merged atomic Rite executor, preserving Rite-specific requirements. Avoid a second Species replacement path for `supernatural-reconstruction` | Initial creation rejected for every post-origination method; method-specific completed event reconstructs an authorized target; repeated/stale requests cannot duplicate history; unsupported method cannot silently execute |
| **Blocker** | Specify an **authored first-party completion authorization pathway** alongside scoped DM overrides. Do not mistake a method listing for authorization; do not make DM-only execution permanent policy | Native-authored allowed/denied and DM-modified cases remain distinct; absent individual native procedure remains unresolved rather than automatically permitted |
| **Blocker** | Define the **cross-pillar form/rebirth interaction** and return-status contract now, without requiring Pillar 2 to implement the entire temporary-form executor | During-form Rite is handled only by explicit rule/DM policy; preserved form maintains its provenance; exit uses latest underlying baseline; conflicting concurrent forms unresolved |
| **Important** | Define how **Origin-derived** grants map into the shared earned-characteristic vocabulary without reinterpreting Class/Background/Upbringing/Supernatural content | Exact native source, occurrence, manifestation, parameter/choice results and stable grant IDs survive multiple origin/rebirth operations; unrelated entitlements remain separate |
| **Important** | State API preview/commit and idempotency boundaries for the coming Phase 5; avoid exposing synthetic authority callbacks as production authorization | Distinct preview, incomplete, unresolved, denied and committed states; atomic persisted transition with expected revision and unique completed event |
| **Engineering** | Resolve post-merge `NativeOriginRebirthEngine.cs:195` nullable warning and investigate the broader native/working consistency check failure before claiming complete CI readiness | Focused tests stay green; broader suite regression diagnosed without changing data merely to satisfy a count or snapshot |

**Scope constraint:** Pillar 2 is **not** being asked to implement vampire/lycanthrope progressions, form triggers, Class suppression, equipment morphing, native-effect directive grammar, HP/concentration policy, or Character Sheet UI. These remain Pillar 4/shared-character or later consumer work. The pre-API boundary requires clearly defined interfaces and safety tests, not all supernatural gameplay to be completed.

## Gate for resuming Pillar 4 prototype implementation

- Agree the common trusted-source/earned-grant contract, including what the Origin engine does **not** authenticate.
- Review merged Pillar 2 Phase 4 transitions and its planned method-neutral extension; preserve its history and atomicity guarantees.
- Resolve which native authoring contract supports existing embedded trait parameters and source-specific effects; correct first-party form permission in the Pillar 4 prototype.
- Rebase the three experimental PRs on the reconciled Pillar 2 baseline **only after** this review. They remain drafts until tested end-to-end against a genuinely constructed Origin. Do not merge the design branch or modify `main` without authorization.

## Current validation snapshot

Pillar 2 post-merge targeted CI [43184621839](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43184621839) passed 186 Mechanics, 111 Host, and 345 focused Python tests with one nullable-reference warning in the Rite executor. The broader run [43184631143](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43184631143) failed the existing `test_current_native_and_working_contracts` native/working contract check. **This document records an integration review, not an executable validation or proof that these missing contracts are now implemented.**
