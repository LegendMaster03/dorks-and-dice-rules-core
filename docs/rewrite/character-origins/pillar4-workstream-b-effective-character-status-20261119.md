# Pillar 4 — Workstream B: effective-character mechanics

**Branch:** `feature/pillar4-effective-character-mechanics-20261119`  
**Baseline:** `main` at `e4f5a279d786ef56063364d62284da8c897ea80a` (2026-11-19)  
**Scope:** Shared Rules Core mechanics and synthetic verified inputs. No first-party supernatural content, production authentication, Character Sheet persistence, or UI.

## Baseline findings

The starting `main` already contains `NativeCharacterInfluenceResolver`, `NativeCharacterManifestationResolver`, `NativeCharacterManifestationSlotComposer`, `NativeCharacterManifestationTransitionPlanner`, `NativeCharacterManifestationProjector`, and `NativeOriginPermanentReconstructionEngine`. Their executable capabilities must not be counted as outstanding Pillar 4 work. The three synchronized reference branches were consulted as design/regression sources, not merged indiscriminately.

The source-qualified characteristic model already supports independently owned persistent influences, expression and earned-progression gates, reversible numeric and condition changes, alternate-sheet overrides, disjoint partial-form slots, authenticated form entry/exit and composite interactions, and a read-only policy view of resources. A form reads the latest authoritative parent state, not a clone captured at activation. Class levels and earned grants survive form termination.

The original influence resolver correctly left multiple active writers of the same property unresolved. This workstream adds a narrowly scoped, **verified single-writer selection** for an exact active collision set, without implicit stacking or last-wins semantics.

## Implemented extension: exact writer selection

- `NativeCharacterState.InfluenceOverlapSelections` is an optional list of `NativeInfluenceOverlapSelection` decisions. Each lists stable effect instance IDs, exactly one selected ID, a source rule occurrence, decision basis, and receipt.
- `INativeCharacterInfluenceOverlapAuthority` is a separate **optional** trust interface. Existing Workstream A and Character Sheet implementers of `INativeCharacterInfluenceAuthority` are not forced to adopt it. If any decision exists and the authority cannot independently verify it, resolution is **invalid**.
- Structural validation checks all supplied decisions, including dormant decisions, before requesting authority callbacks. Missing or forged effect IDs, mixed-property selections, malformed sources, duplicate IDs, and invalid selected writers fail closed.
- A selection applies only when its complete listed effect ID set equals the **currently active collision** for that one characteristic and sheet. Missing, partial, or multiple matching decisions yield `influence-overlap-unresolved`, independent of input order.
- Selecting one writer never acquires another feature, changes `CurrentResources`, revokes an earned progression, deletes a suppressed effect, or mutates authoritative state. The result retains the selected effect's original influence/source occurrence.
- The existing alternate-sheet collision rule is unchanged: a selected writer colliding with a local authored sheet override is still unresolved. Independent form interaction decisions are still required for coexisting manifestations.
- Deep-copying character state now preserves and detaches the optional overlap decision sets.

**Contract coordination:** Workstream A owns acquisition/earned progression and must not treat overlap choices as entitlement grants. A trusted Character Sheet host must own/persist exact decision identities and verify source occurrences, table scopes, rule authority, evidence, current character revision, and the actual permitted writer selection. A client-supplied `InfluenceOverlapSelections` list cannot authorize itself. The optional interface makes this a compatible additive contract rather than a change to Workstream A's existing interface.

## Mechanical subsystem support matrix

| Subsystem | Status | Concrete execution boundary / remaining gap |
| --- | --- | --- |
| Persistent effect acquisition/source tracking | **Supported as verified input** | Resolver requires source/version, evidence, per-effect authorization; acquisition itself belongs to Workstream A |
| Characteristic value replacement/adjustment | **Supported** | Ability scores, training properties, defense contributions, Hit Point gain inputs, walk speed, typed conditions; bounded native inputs |
| Characteristic granting/suppression | **Partial** | Source-authorized condition add/suppress and typed values; arbitrary trait/feature grant acquisition is not implemented here |
| Conditions and expression dependencies | **Partial** | Condition-ID presence/absence, exact and numeric expression gates, locally source-qualified earned prerequisites; duration/exposure, source-owned condition history and cross-influence dependencies need shared policy |
| Two independent influences | **Supported** | Disjoint writers compose; exact verified selection resolves collision to one writer; without a unique exact rule collision remains unresolved |
| Multiple Wolf/Hybrid-like forms | **Supported for scoped numeric composition** | Distinct authored partial slots compose with a verified Coexist decision; full overlapping replacements fail closed |
| Temporary activation, suppression and reversion | **Supported** | Ledger revision/event checks, host proof, lifecycle, persistent progression, and latest-primary inheritance; storage commits are external |
| Movement | **Partial** | Walk speed is typed; flying, climbing, swimming, burrowing and other physical/movement modes are not in `NativeCharacterRequest` |
| Creature type and targeting classification | **Unresolved for transformed effective character** | Pillar 2 has native intrinsic source projectors; general effective targeting type is not composed from active character influences/forms |
| Attack/action/speech capability | **Unresolved** | No generally authorized typed form action or speech applicability contract; no monster action copying |
| Class features and spellcasting | **Partial / withheld in form** | Latest Class numeric advancement projects; unverified form feature IDs/Class resources are intentionally withheld, not automatically retained |
| Hit Points | **Partial / withheld in form** | Authored `HitPointGains` affect normal numeric input; form-specific maximum/current transfer, healing and recovery policy unresolved |
| Resources and quantities | **Partial** | Ownership, live/captured/adjusted maxima, read-only quantities, and private activation pools supported; spend/recover persistence and form-specific Class resource use are external/undefined |
| Equipment and inventory usability | **Unresolved** | Existing qualification contracts do not establish transformed shape, worn state, item ownership or item usability |
| Persistent weaknesses and exposure effects | **Partial** | Source-gated condition and numeric inputs supported; damage/exposure/periodic triggers and source-specific duration unresolved |
| Permanent reconstruction | **Partial** | Existing atomic engine validates target, separately verifies completion, retains independent grants and history, and can complete without a fixed stage count. The current `supernatural-reconstruction` native method lacks an executable complete-replacement operation and selected-native completion procedure; this is **not** implicitly authorized |
| Origin/class/history retention across reconstruction | **Partial** | Origin engine preserves prior independent grants and history; host must coordinate independent Class, inventory, resources, current manifestations and persisted completion receipt |

**Meaning of “supported”:** executable for an authenticated **synthetic** input under the stated limited contract, not proof of production authorization or approved game content. “Partial” is not a fully playable transformed character.

## Synthetic acceptance coverage

| Requested case | Test evidence |
| --- | --- |
| 1. Persistent modification without transformation | `SeparateVampireExpressionDecisionsRevealOnlyAuthoredEffects` |
| 2. Dormant effect becomes applicable | `EarnedProgressionSurvivesExpressionLossAndUseRequiresAuthoredDecision`, `DormantChoiceNeverChangesAnUncontestedWriter` |
| 3. Independent compatible influences | `DisjointNumericChangesComposeWithoutInventingPrecedence` |
| 4. Incompatible effects and authored collision choice | `OverlapDoesNotUseInputOrderAsAStackingRule`, `ExactAuthorizedOverlapSelectsOneWriterRegardlessOfInputOrder`, `PartialOrAmbiguousOverlapSelectionsRemainUnresolved` |
| 5. Activation and reversion | `AtomicCoexistActivationAndReversionUseOneTransitionEventEach`, `ReversionAndReactivationPreservePersistentProgressionAndHistory` |
| 6. Different Wolf/Hybrid scopes | `SwitchingToHybridDoesNotClaimWolfOwnedProgression`, `PartialFormKeepsItsScopeWhenAloneAndWhenAnotherFormEnds` |
| 7. Simultaneous partial forms | `CoexistingPartialTransformationsCombineDisjointSlotsOnLatestBase`, `CoexistFailsClosedWhenTwoFormsWriteSameSlot` |
| 8. Parent changes while transformed | `CurrentClassAndUnderlyingOriginChangesFlowIntoActiveFormAndReversion` |
| 9. Class progression while active | `NativeProjectionKeepsUpdatedClassAndSourceDerivedValuesWithoutSnapshotRollback` |
| 10. Resources across forms | `ResourceOwnershipIsStableAndMaximumChangeNeverRefills`, `CapacityReductionDoesNotClampRecordedCurrentQuantity` |
| 11. Unsupported policies fail closed | `NativeProjectionWithholdsUnverifiedFormFeaturesButPreservesNumbers`, `CompositeNativeProjectionWithholdsUnverifiedClassUseButKeepsVerifiedSlots` (HP/Class withheld; equipment applicability remains unimplemented) |
| 12. Authorized permanent completion | `RiteAndMethodNeutralReconstructionShareTheSameAtomicOperation`, `SharedExecutorRejectsDuplicateCompletionEvents`, `SupernaturalMethodListingDoesNotAuthorizePermanentReconstruction` (generic boundary only; no native supernatural completion enabled) |
| 13. Source tampering/unauthorized transitions | `InvalidOrUnauthorizedOverlapFailsClosedBeforeApplication`, `VerifiedPrimaryWriterDecisionSurvivesFormEntryAndReversion`, `UnauthorizedInheritedProgressionAndGrantBlockTransitions` |

All named cases are library-level synthetic tests. Their presence is not a claim that the full list of unresolved gameplay policies has an executable implementation.

## Integration dependencies and decisions deferred

**Workstream A outputs:** Authenticated influence occurrence, definition version, acquisition and expression receipts, independently earned progression/grants and source/occurrence-qualified applicability decisions. An exact overlap decision additionally requires source rule proof; no general progression stages or acquisition formulas are created here.

**Character Sheet integration:** Revision-current primary mechanics, character/sheet/manifestation ownership, verified decision storage and history, atomic event/revision commit, resource balances, authoritative `NativeOriginProfile`, Class advancement, conditions and inventory. The preview result is not a persisted mutation; an optional verified overlap is not a client permission shortcut.

**First-party content decisions deferred:** Which supernatural effects/forms exist and how they are earned; vulnerability/feeding/exposure triggers; creature-type targeting with multiple types; each form's movement modes, actions, speech, Class feature use and spellcasting restrictions; HP current/maximum transfer and over-cap/recovery; equipment shape/merge/drop/use; ongoing-condition duration; resource spending/recovery; specific terminal reconstruction target/retention/override policies and native completion rule. No default is inferred from a monster stat block.

**Further shared contract work:** Typed effective-character domain projections for creature classification, movement modes, actions, equipment and spellcasting; authored source-qualified condition histories; owner/occurrence-aware cross-influence dependencies; authorized class-feature applicability; and a supported HP policy vocabulary. These require explicit common semantics and consumer coordination before interface expansion. Do not implement them as a second supernatural character engine.
