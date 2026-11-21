# Homebrew Conversion: Repository Capabilities, Gaps, and Self-Review

**Baseline reviewed:** \`9f50ab70a37be69a6748603d210fa9406ba7ea59\` on \`feature/biological-combine-gene-offers-20261121\`  
**Documentation branch:** \`docs/pillar2-homebrew-conversion-framework-20261121\`  
**Review scope:** Manual homebrew Species conversion; no new mechanics APIs, default prices, inheritance-combination rules, or WorkChat acceptance in this track.

**Labels:** **Supported** = observed relevant capability at the baseline; **Partial** = real implementation with bounded semantics or separate host work; **Not supported** = manual procedure or future integration required. These labels describe repository capabilities, **not** whether a custom Species is permitted at a particular table or has been balanced.

## Manual procedure versus actual software

| Conversion step | Baseline status | Grounding / executable boundary | Manual author / future gap |
| --- | --- | --- | --- |
| Inventory built-in Species/Ancestry and canonical traits | **Supported** for selected native content | \`native/origination/biological/species/\` and \`genes/\`; \`NativeOriginSourceDeconstructor\` obtains source-qualified candidates | A third-party PDF or homebrew text is not automatically parsed or admitted |
| Distinguish biological characteristics from cultural Background/Upbringing | **Partial** | Separate native source catalogs and source concepts exist; selected Species/Ancestry and cultural records have explicit parent links | DM must decide whether unfamiliar text is inherent, cultural, or independent |
| Capture intrinsic type, size, walking speed and existing mechanical traits | **Supported** for recognized, valid offers | \`NativeOriginSourceDeconstructor\`, \`NativeOriginContracts\`, foundation checks in \`NativeOriginConstructionEngine\` | Novel intrinsic kinds and unknown effect fields need authored interpretation |
| Find mechanically equivalent canonical definitions | **Partial** | Canonical \`origin-trait\` IDs and exact source occurrences exist; 85 normalized biological genes are first-party content | No semantic search that proves a homebrew definition equals a native mechanic; similar names are insufficient |
| Create a new homebrew canonical definition | **Partial** for manually authored native/table data; **not supported** as arbitrary executable conversion | Native catalog loads authored definitions; table rules can authorize bounded effective outcomes; unknown fields are not a universal effect interpreter | Author a valid identity, mechanical schema, executable semantics, grant eligibility, version and trustworthy source |
| Configure source-specific choices and parameters | **Partial** | Native choices, bindings, dependent-selection evaluators, parameter reconciliation and authored choice validation exist | Some dynamic/open choices and configuration shapes remain unresolved; do not assume any custom field is bindable |
| Combine contributor offers and retain gene instances | **Supported** for the bounded biological method | \`NativeOriginBiologicalCombination\`, \`NativeOriginBiologicalInheritanceEvaluator\`, transmission evaluator and scoped authorization | No inference from historical ancestry, unknown homebrew sources, or arbitrary parent counts; provenance must be verified |
| Derive a new stronger effect from **inherited gene instances** | **Not supported at this baseline** | Universal synergy already consumes **effective** grant inputs, not latent/inherited gene pairs | Parallel inherited-gene-combinations track must specify its own explicitly authorized bridge; do not treat prospective code as merged |
| Reconcile choices, mutual exclusion, dependencies, effective grants | **Supported with coverage limits** | \`NativeOriginConstructionEngine\`, \`NativeOriginReconciler\`, \`NativeOriginConstraintEvaluator\`, dependent/resource binders, canonical authoring | Unsupported rules, missing choices and cross-system semantics remain unresolved |
| Derive a fixed trait from multiple **effective** characteristics | **Supported with restrictions** | \`NativeOriginSynergyEvaluator\` with reviewed native or authenticated table rule, fixed canonical output, supported selectors and input matching | No arbitrary rewriting of AC/trait values, resource-bearing/level-gated synergy outputs, or arbitrary operating conditions |
| Review whole biological foundation and grant authorization | **Supported with coverage limits** | Construction statuses \`Invalid\`, \`Incomplete\`, \`Unresolved\`, \`Conflicting\`, \`Authorized\`; \`IsMechanicallyAuthorized\` only for Authorized | An authorized profile only proves implemented rules accepted it; it does not prove every feature in external prose was faithfully modeled |
| Inspect innate-spell unlocks and source-bound ability | **Partial** | \`NativeOriginInnateSpellProgressionProjector\` handles supported native spell courses, future unlocks, and ownership | No arbitrary spell authoring, unsupported open-catalog choices, spell casting, or general slot accounting in this projector |
| Track innate free-casting use counts | **Partial** | \`NativeOriginInnateSpellResourceLedger\` can compute candidate updates, Long Rest recovery and reviewed advancement transitions | Host must authenticate ownership, persist exact ledger revision atomically, integrate actual spell actions and any table capacity decision |
| Model permanent/conditional flight and unusual sensory effects | **Partial** | Existing movement traits and projectors cover specific authored shapes, such as \`trait.spectral-flight\`; supported canonical senses are projected | A general editor/interpreter for every flight restriction, water-connected detection rule or dynamic environment dependency is not established |
| Apply a complete numerical initial biological creation budget | **Partial engine, unconfigured first-party rules** | \`NativeOriginAllocationPreview.AssessCreation\` invoked by trusted construction host for direct/mixed/custom biological intent; opt-in \`RequireCreationAllocation\` and policy | No approved first-party allowance or characteristic prices; configuring per-value price distinctions is a parallel development track |
| Price same canonical trait differently for distinct configurations | **Not complete at baseline** | Existing \`NativeOriginAllocationPrice.MemberKeys\` primarily identifies intrinsic slot or canonical trait; \`InteractionPrices\` adds authored premiums | Do not pretend existing key matching implements the pending configuration-aware price rules |
| Value complete Species balance | **Not supported as a verdict** | Provisional allocation/power research and per-grant ledger do not measure actual play balance | Later quantitative comparison and playtesting, outside this documentation track |
| Publish a newly reconstructed individual as a reusable Species preset | **Not automatic** | A reconstructed Origin profile retains current inherent grants and source evidence | Authored/selectable reusable templates require separate review, release, authority and migration behavior |
| Convert a Construct, magical creation, or transformation using the same traits | **Partial and method-specific** | Construct source/service/operating session adapters exist in bounded form; the universal Origin construction and state contracts are shared | Each procedure supplies its own costs, acquisition, resource and applicability rules, not biological genes or allocation by default |
| Run a universal homebrew converter/editor in a player-facing UI | **Not supported** | Manual documentation, validation contracts and specific native tooling are available | No general third-party ingestion, canonical match review, effect authoring, end-to-end character UI, or assured runtime execution |

### Critical interpretation of status

A \`Profile\` object can exist for an **incomplete, unresolved, conflicting, or invalid** proposal. Consumers must check \`NativeOriginConstructionResult.Status\`, \`Issues\`, and \`IsMechanicallyAuthorized\`, rather than using non-null profile data as character permission. Likewise, candidate eligibility, inherited gene presence, a manifest attempt, or a projected spell preview is not proof of an effective, usable trait.

The **initial creation gate is host-supplied and opt-in for compatibility**. A missing required schedule reports \`not-configured\` or unresolved; a malformed schedule reports \`invalid-policy\`. Under a complete *hypothetical* schedule it may report \`within-allowance\` or \`over-allowance\`. Approval of prices or a complete first-party budget is not implied by the existence of these status strings. An explicit authenticated \`dm-waived\` result represents a table exception, not a universal free allocation.

The \`NativeOriginAllocationPreview.Assess\` public historical preview is narrower in application (mixed biological intent); the enforced construction assessment additionally covers direct and custom biological initial creation. Older documents describing the preview should not override the new engine behavior.

## Specific integration requirements discovered

These are **requirements to review**, not authorized implementation tasks in this branch.

1. **Homebrew intake/authority.** Design a safe path to create a source-qualified custom Species/Ancestry record with editor identity, campaign/ruleset scope, licensing/attribution, revision, and separate authoring versus acquisition permissions. The host must not trust arbitrary client trait payloads, rule claims or source IDs.
2. **Canonical match review.** Support side-by-side mechanical diff against exact native definitions. Accept an explicit reviewer decision (equivalent, configured variant, distinct, unresolved); a keyword classifier must not silently collapse mechanically different features.
3. **Extensible semantic definitions.** Maintain a registry/schema of supported effect types plus required runtime adapters. Capture unfamiliar clauses without discarding them, and fail closed when a new effect cannot actually run.
4. **Compound authoring.** Support independent and linked traits, choices at creation versus on use, source-scoped parameter bindings, exclusions, stable resource owners, pool sharing, advancement unlocks, and effects whose constraints are inseparable from their benefits.
5. **Configured-value valuation integration.** After the parallel pricing track lands, verify how distinct configurations map to price records, how conflicts/overlap are rejected, and how entire courses and bundles are charged. Do not publish a preliminary numerical budget.
6. **Inherited-gene combination integration.** After that parallel track lands, verify that a genuinely new phenotype is produced **only** by explicit permitted instance combinations, with exact source provenance, correct effective result, non-stacking behavior, and a valuation disposition.
7. **Nonbiological adapter boundaries.** Define how a valid authored characteristic can be acquired by Construct Assembly, magical creation, or a transformation without importing biological contributor rules, latent gene capacities, or creation prices.
8. **State and resource integration.** Add host-owned audit storage and revision-aware spell/resource updates, actual gameplay effect consumption, state-based applicability, and eventual reconstruction/migration for changed authored definitions.
9. **Whole-profile diagnostics.** Expose original text and mapped row, canonical/configured ID, remaining unresolved field, missing cost, prerequisite, authored DM ruling, and final grant status. Do not mislabel a partial projection as success.
10. **Calibration and test workflow.** In a *separate approved workstream*, compare published Species and new conversions at common levels, evaluate unusual synergies, then conduct playtesting. No automated "balanced" badge based solely on a ledger total.

### Possible automated converter: minimum useful product

A future tool should accept **source text and provenance**, produce a **draft characteristic worksheet**, present **canonical candidates with mechanical diffs**, and allow a human author to accept, edit, or reject each match. It should emit explicit **missing definition, runtime, source permission, dependency, choice, progression, and price** diagnostics before constructing a preview. An authenticated author may then submit complete native/table data for trusted review and authorize the resulting **whole** Origin. Its output should retain a reproducible mapping from each original feature to every effective characteristic.

Automation should **not** assume two donors, assign arbitrary zero cost, infer that matching words indicate identical mechanics, auto-upgrade duplicate traits, synthesize native source identity, or treat a text-to-JSON conversion as a rule executor. A usable first release could be a guided checklist and comparison editor; full automatic conversion is not a prerequisite to the manual framework.

## Self-review: baseline and decision consistency

| Check | Result |
| --- | --- |
| Preserves Deconstruct → Combine → Reconcile ↔ Manifest → Reconstruct | **Pass.** Reconciliation/manifestation remain iterative; source offers and effective grants are not conflated |
| Separates biological inheritance from general Origination | **Pass.** The guide reuses mechanical identities but keeps Construct and transformations' separate acquisition, power and state rules |
| Preserves Background and Upbringing boundaries | **Pass.** Cultural and learned traits are not automatically treated as hereditary Species features |
| Reuses native identities without assuming all similarly named content is identical | **Pass.** Real \`trait.fire-bolt\`, \`trait.spectral-flight\`, \`trait.amphibious\`, \`trait.coldwater-swimmer\`, and \`trait.low-light-vision\` checked against catalog |
| Does not claim Natural Armor is already first-party | **Pass.** The baseline gene catalog does not contain Natural Armor; C1/C2 are explicitly hypothetical; flat AC 17 is not approved |
| Does not promise unfinished inherited-gene combinations | **Pass.** Distinguishes gene instances from effective-input synergy and delegates the new combination rules to parallel track |
| Does not promise unfinished configured pricing | **Pass.** Current key-based ledger is described separately; price variations are integration questions, not enforced rules at baseline |
| Covers entire spellcasting and flight lifecycle | **Pass as a manual example.** Ability, spell reference, unlock, uses, slot permissions, duration, armor and source dependencies are stated; runtime limits remain visible |
| Treats unknown effects as unresolved rather than free | **Pass.** The unfamiliar Species contains a new effect with manual DM disposition, explicitly unpriced and not machine-authorized |
| Avoids balance conclusions and approval drift | **Pass.** No point totals or universal admission rules; mechanical completeness and later testing are distinct |
| Avoids shared engine files and parallel edits | **Pass by scope.** Four new documentation files only; no new native rules or engine mutations |
| Independent executable test coverage | **Not run in this track.** Documentation review does not constitute WorkChat acceptance, gameplay validation, or .NET integration testing |

### Evidence inspected

- \`src/RulesCore.Mechanics/NativeOriginContracts.cs\`, \`NativeOriginConstructionContracts.cs\`, \`NativeOriginConstructionEngine.cs\`, \`NativeOriginSourceDeconstructor.cs\`, \`NativeOriginBiologicalCombination.cs\`, \`NativeOriginBiologicalInheritanceEvaluator.cs\`, \`NativeOriginAllocationPreview.cs\`, \`NativeOriginSynergyEvaluator.cs\`, \`NativeOriginInnateSpellProgressionProjector.cs\`, \`NativeOriginInnateSpellResourceLedger.cs\`, and the dependent grant/intrinsic interpreters.
- Native \`rules/creature-origination.json\`, \`native/README.md\`, the 85-file biological gene directory and selected concrete gene files.
- Related Origin design records: \`pillar2-biological-combine-gene-offers-20261121.md\`, \`pillar2-provisional-allocation-preview-20261115.md\`, \`pillar2-universal-characteristic-synergy-20261119.md\`, \`pillar2-native-innate-spell-progression-preview-20261115.md\`, \`pillar2-construct-assembly-first-increment-20261119.md\`, and \`pillar2-universal-origin-model-first-20261111.md\`.

**Integration readiness:** Documentation can be reviewed and merged separately **only when approved**. The two ongoing development branches should be reconciled before using this as definitive implementation/authoring UI guidance. A final manual authoring workflow can exist before all optional automation, while the software must still fail closed on unsupported mechanics.