# Pillar 2 Phase 5 — Future partial character-state slot compatibility audit

**Recorded:** 2026-11-12  
**Status:** Future architecture candidate; **not approved for implementation**.  
**Scope:** Review of existing Rules Core contracts only. No changes to executable Phase 5 mechanics, schema, or milestones.

## Proposal to retain for later review

Pillar 4 has proposed independently activated **partial character-state slots**:
each slot declares only the characteristics it overrides; other
characteristics resolve against the latest authoritative base character
and other separately authorized state. Potential uses include supernatural
forms, Wild Shape, Polymorph, and other temporary states. A later review may
consider its relationship to permanent reconstruction and the selection of
which hereditary contributor state is mechanically relevant.

This is an architectural hypothesis, **not** a Phase 5 rule or an approved
state format. In particular, it does not authorize automatic trait inheritance,
a universal form precedence rule, class replacement mechanics, or a choice of
which of a parent's states contributes to a descendant.

## Existing contracts that support the direction

- `src/RulesCore.Mechanics/NativeOriginContracts.cs` distinguishes
  `NativeOriginProfile.Underlying` (latest authoritative baseline)
  from `Current` (effective evaluated configuration). Its contract expressly
  says reversion uses the **latest** `Underlying`, never an activation-time
  baseline. Preserve this distinction even if the representation of the
  effective state eventually becomes derived rather than stored.
- `NativeOriginEffectiveGrant` retains grant/manifestation identity,
  `SourceCandidateIds`, source-qualified `NativeOriginCharacteristicIdentity`
  (including `NativeOriginSourceOccurrence`), actual value, parameter/choice
  bindings, and authorization basis. Candidate IDs are local offers; they are
  **not** durable grant identities.
- `NativeOriginActiveTemporaryTransformation` already identifies a form,
  effect source, activation evidence, persistent influence, optionally its
  effective grant IDs, and authorization. Other concurrent temporary
  transformations and their precedence evidence are represented separately.
  These are evidence and identities, **not** an implemented overlay engine.
- `NativeOriginHistoryEvent` records method, contributor IDs, event stage,
  prior configuration where applicable, resulting profile, and table decision.
  Its prior-configuration snapshot is **audit evidence, not a restoration
  target**. `NativeOriginPersistentInfluence` and
  `NativeOriginIndependentGrantApplicability` remain distinct from inherent
  baseline grants.
- `NativeOriginPermanentReconstructionEngine` replaces `Underlying` after
  validating an authorized target and retains independent grants, relevant
  influence/form evidence, and history. Active-form interactions explicitly
  require authorization; there is no implicit rule about how a Rite changes an
  already active form.

## Present assumptions that would need adaptation

| Existing contract or implementation | Current requirement | Implication for later partial slots |
| --- | --- | --- |
| `NativeOriginCurrentConfiguration` and `NativeOriginProfile` | `Underlying` and `Current` are complete `SpeciesId` / `AncestryId` / `InherentGrants` configurations. | A future slot should be able to specify only overridden characteristics; an effective complete configuration may remain a **derived output**, but the contract does not currently accept the sparse input as `Current`. A versioned adapter/contract would be needed. |
| `NativeOriginProposal.ExistingConfiguration` and `ExistingEffectiveConfiguration`; `NativeOriginContractValidator` | Active-form proposals require both complete baseline and effective configurations. Profile validation also requires a populated `Current` grant collection. | Cannot substitute an incomplete overlay for `ExistingEffectiveConfiguration` without validator and API changes. Preserve the checks until a trusted resolver exists. |
| `NativeOriginConstructionEngine` | Construction produces a materialized current configuration. Temporary-form evidence does not independently execute transitions or derive field-by-field overrides. | Future execution must resolve effective state from authoritative baseline plus separately verified overlays, rather than treating the old full `Current` as an immutable source. |
| `NativeOriginPermanentReconstructionEngine` | When an authorized permanent baseline replacement happens under an existing temporary form, it may carry `existing.Current` forward while installing a new `Underlying`; the interaction itself is separately approved. | A later resolver must distinguish a form's explicit overrides from fields inherited from the **new** baseline. Reusing an old complete effective snapshot indefinitely would freeze unspecified characteristics incorrectly. |
| `NativeOriginContributor.ManifestedProfile` and `NativeOriginSourceDeconstructor` | Nested or manifested contributors supply a complete `NativeOriginCurrentConfiguration` and its grant occurrences; the contract does not select between a creature's baseline and its temporary effective form. | A future, authenticated hereditary-state selector and resolver would be needed. Do not infer that the visible/effective form is necessarily heritable. |
| `NativeOriginIntrinsicProjector`, `NativeOriginVisionProjector`, and the approved-mixed equivalents | Strictly validate a complete, currently authorized profile, source occurrences, and supported adjudication. Some initial-state checks require `Current` to be the same baseline instance. | Preserve fail-closed attribution, but allow future interpreters to validate a **derived** effective result and its overlay provenance. Do not weaken these initial-Origin guards during Phase 5. |
| `NativeOriginActiveTemporaryTransformation.EffectiveFormGrantIds` and precedence evidence | Refer to grants in the materialized `Current`; concurrency/precedence may remain unresolved without a specific decision. | Future slot identity, ordering, overlap, suppression, activation/reversion, and effective-grant attribution require an explicit resolution contract. None is agreed here. |

**Hard distinction:** requiring a **complete evaluated output** for a specific
mechanics calculation is not the same as requiring a **complete independently
persisted copy** of each temporary character state. The former can be retained
while changing how the latter is represented.

## Guardrails for continuing Phase 5 unchanged

1. Keep `Underlying` authoritative and `Current` explicitly effective; do
   not collapse them into a single Species/Ancestry selection.
2. Preserve source-qualified grants, acquisition/activation evidence, DM
   rulings, persistent influences, independent entitlements, and completed
   historical reconstruction events.
3. Treat full effective configurations as **current computed projections or
   compatibility records**, not a newly approved permanent storage mandate.
   Do not add a new per-form full-character snapshot requirement.
4. Do not use a historical `PreviousConfiguration` or the `Current` present
   at form activation as the implicit reversion source. Reversion refers to
   the latest authorized baseline subject to separately approved interactions.
5. Preserve the distinction between origination process eligibility,
   resulting creature classification, and actual manifested traits. Keep
   parent-character identity verification and choice of heritable state
   outside the current Phase 5 Human–Elf source-reference slice.
6. Continue rejecting unverified temporary state, parent profiles, grant
   applicability, table authority, and client-authored history at current
   API trust boundaries.

## Questions deferred to the separate Pillar 4 review

The future design must establish slot identity and lifetime, exact
characteristic selection/replacement semantics, collisions and precedence,
class/feature behavior, which grants are suppressed versus retained, how
`Underlying` changes during an active slot, state reconstruction and
versioning, and which biologically relevant state a contributor exposes.
It must also identify any migration of persisted full profiles and
consumer projections. **No answer to those questions is approved by this
audit.**

**Phase 5 completion remains governed by**
`pillar2-implementation-roadmap-20261111.md`. No runtime refactor,
migration, new form executor, new requirements, or delay is authorized by
this compatibility note.
