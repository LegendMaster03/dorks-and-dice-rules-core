# Pillar 4 — Pillar 2 dependency review and revised development plan

**Review date:** 2026-11-12  
**Status:** **Proposed execution plan for owner review**, not authorization to merge or approve individual supernatural mechanics.  
**Rule set:** `first-party-001`  
**Design branch:** `feature/pillar4-supernatural-nature-design-20261111`  
**Pillar 2 reviewed:** `feature/pillar2-characteristic-inventory-20261111` at `ad23a720f4aab7af97b8934a0be545a4ac48e437` (PR #141, draft against `main`).  
**Parallel Pillar 2 work inspected:** `feature/pillar2-phase4-origination-rebirth-20261112` at `f2a8fb9bb2` (not merged at review).  
**Pillar 4 prototype implementation reviewed:** draft PRs [#146](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/146), [#148](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/148), [#149](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/149). All remain separate from `main`.

## Executive decision

**Pause expansion of the three Pillar 4 implementation PRs while reconciling them with the evolving Pillar 2 implementation.** Retain them as tested, restricted technical experiments; do not merge them into the Pillar 2 development branch without a deliberate contract-alignment pass.

Pillar 4 should not become a second Origin constructor, supernatural-specific characteristic inventory, Class entitlement source, or DM authorization service. It should supply separately authored supernatural **source/progression**, **persistent effects**, **form profiles**, and **permanent transformation completion criteria** to the existing Rules Core character systems. The Pillar 2 engine owns authoritative Species/Ancestry construction, its inherent grants, and historical reconstruction. A shared Rules Core effective-character evaluator, not the Character Sheet client or Pillar 4-specific copy, handles cross-domain applicability and mechanical outcomes.

**Owner-selected design constants:** three behavior categories (modification-only; modification plus temporary replacement; modification progressing to permanent reconstruction), four equal primary test cases (Draconic Bloodline, full vampire, lycanthropy, ceremorphosis), individually authored progression/trigger/agency rules, authoritative underlying state versus temporary effective state, reversion from the latest underlying state, and authorized DM overrides at every gameplay level. This document does not approve numerical source content, universal stages, balance, or a new native schema.

## Verified Pillar 2 implementation progress

| Area | Verified status at snapshot | Significance for Pillar 4 |
| --- | --- | --- |
| **Phase 1** (PR #143; temporary-form PR #144 included) | Merged into Pillar 2 branch. `NativeOriginProfile.Underlying` vs `Current`, `PersistentInfluences`, `ActiveTemporaryTransformation`, concurrent-form precedence evidence, independent entitlement/applicability, history, method references, and scoped table decisions | **Do not reproduce** these representations or invent a temporary Species/Ancestry contract |
| **Phase 2** (PR #145) | Merged. `NativeOriginConstructionEngine` performs catalog-backed deconstruct/combine/reconcile/manifest/reconstruct for direct and selected mixed Origins, source-qualified grants, issue statuses and scoped DM checks. Partial results are not mechanically authorized | Consume actual authorized results, not arbitrary profiles with `Status=Authorized`; do not use unresolved grants for gameplay |
| **Phase 3** (PR #147) | Merged. Full native ancestry parent chains, explicit `originBindings` for Dragonborn parameters, `NativeOriginDependentBinder` for Elf spellcasting choice, provenance-aware nested contribution, real-catalog tests | Use resolved, **parameterized** `NativeOriginEffectiveGrant` data; avoid rebuilding lineage, child-leaf binding, or ancestral feature eligibility in Pillar 4 |
| **Phase 3 limits** | General mixed-transmission policy, direct descendant intrinsic precedence (e.g., Wood Elf speed), shared resource interaction, several cross-Ancestry cases, latent trait transmission and reproduction during a temporary form are still **unapproved**. Human–Elf coherent case is **table-authorized**, not a general first-party recipe | Do not assert that all mixed Origins or form-induced inheritance cases are supported by first-party defaults |
| **Phase 4 in progress** (`feature/pillar2-phase4-origination-rebirth-20261112`) | Early commits add native creature-type method-default evaluation and distinct Rite/process acquisition consideration. At review, these commits do **not** execute Rite, ceremorphosis, form activation/reversion, or complete reconstruction | Coordinate the shared replacement/transition mechanism with this branch; do not race it with a separate Pillar 4 replacement executor |
| **Phase 5** | Rules Core Origin persistence/public API and Character Sheet integration remain in the Pillar 2 roadmap, not completed by these merges | Do not wire temporary local state directly into Character Sheet as a bypass of Rules Core authority |

**CI note:** Pillar 2's focused Origin workflow passed on `ad23a720` (run [43179614478](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43179614478)). Its broader public-native-API workflow run [43179623337](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43179623337) failed in the existing Python native/working boundary check (`test_ruleset_split.NativeWorkingBoundaryTests.test_current_native_and_working_contracts`). This does not demonstrate a Pillar 4 regression; it means overall repository readiness must not be inferred from only the focused workflow. Verify the cause with the owning workstream before promotion.

## What the existing Pillar 4 PRs prove and what they do not

| Draft PR | Tested prototype | Missing production guarantee or design risk | Recommendation |
| --- | --- | --- | --- |
| **#146 — Effect composition** | Source-linked ability-score add/replace and temporary independent-grant suspension; conservative unresolved output | `IndependentGrants` are passed through the Origin construction request, **not earned by the Origin engine**. A synthetic `NativeOriginConstructionResult` and fixture authorization do not establish real Class/Bloodline entitlements. No effect progression, transition executor or cross-domain availability policy | **Retain as a spike; revise source/entitlement input contracts** to accept trusted common character grant provenance rather than relying on Origin grant-shape coincidence |
| **#148 — Numeric projector bridge** | Reuses `NativeCharacterProjector` for resolved ability scores/modifiers; withholds unsupported cases | Restricts output to ability subset. It deliberately cannot yet execute feature, equipment, spellcasting, HP, action or condition state; Constitution changes require a resource/HP policy. Separate character-input authority callback is currently only an interface | **Keep bounded**, but integrate only after common trusted-character state and native effect definitions are verified |
| **#149 — Catalog effect authorization** | Checks exact authored effect IDs, source occurrence, source mechanical definition, operation, value, target, form scope; permits authenticated scoped DM decisions | Proposed `effectiveEffectDefinitions` array is a **new authoring convention**, not yet reconciled with existing embedded Species/Ancestry mechanics, `originBindings`, choice parameters and level-scaled effects. Its current `AuthorizesForm` requires a verified DM-form override even where a future native first-party rule would permit the form. Host grants and Origin authenticity remain unimplemented | **Keep the exact-source security tests, but redesign/approve native authoring and the normal first-party form path before adoption** |

**Branch integration:** The #149 stacked head and Pillar 2's latest branch diverge after shared commit `1d6d59903c`. Pillar 2 has Phase 3 additions absent from these Pillar 4 spikes. Reconcile current implementation and tests against **newer Pillar 2**; passing the prior CI runs does not certify today's cross-branch integration.

**Avoid accidental duplication:** `INativeOriginAuthority`, the proposed effective-effect authority, and the proposed character-input/host trust providers should draw on one authenticated campaign/character authorization context. They can expose separate *typed authorizations* but must not create three incompatible concepts of earned grants or DM permission. Serialized caller data alone must not confer entitlements.

## Responsibility and dependency boundary

| Shared mechanic or action | Primary owner | Pillar 4 role |
| --- | --- | --- |
| Species/Ancestry deconstruction, inherent grant identity, dependent choices, original and transformed Origin profiles | **Pillar 2 / shared Origin engine** | Reference actual `Underlying`, `Current`, selected source occurrences and existing transformation records |
| Authorized permanent Species/Ancestry reconstruction, replacement history, Rite execution | **Pillar 2** | Define what a given supernatural progression's completion event requests and which effects should remain |
| Persistent supernatural acquisition and advancement, specific stage choices, triggered/unlocked forms, optional progression paths | **Pillar 4 content + shared progression infrastructure** | Author source-specific conditions and grants; do not invent Class levels or automatic inheritance |
| Cross-domain effect application and composition | **Shared Rules Core character/mechanics layer** | Author individual effective operations/policies; do not copy class/equipment/condition projectors |
| Class features and earned advancement | **Shared Class/character advancement** | Request source-qualified temporary *applicability*, never delete earned Class levels or history through a form |
| Inventory/equipment state, ownership, usability, merging/drop/transform behavior | **Shared inventory/equipment mechanics** | Supply the form's specific policy; not a universal vampire/werewolf equipment default |
| Creature type and targeting effects | **Shared creature/targeting evaluator** | Specify effective type per condition/form and its scope; do not import monster statblocks |
| HP/ability scores, spellcasting, resources, concentration, conditions, lifecycle | **Shared character/resource/condition engines** | Supply bounded form/source rules, with persistent amounts and exact reversion interactions |
| Campaign table decisions and authority | **Common authenticated host/Rules Core policy** | DM can change any particular rule/effect/transition in scope, without bypassing account/character access checks |
| API persistence and consuming character UI | **Pillar 2 Phase 5 / Rules Core API / Character Sheet** | Consume effective Rule Core output; no separate client-side supernatural resolver |

## Revised execution sequence

### Track A — Align before coding (immediate; no dependency on unfinished Pillar 2 Phase 4)

1. Freeze new mechanical surface area in PRs #146, #148 and #149; leave drafts intact for review. Do not merge or discard their regression coverage.
2. Review **latest Pillar 2 Phase 3/4 commits** and agree a precise common contract for a grant **source occurrence**, independent Class/Background/Upbringing entitlements, supernatural-acquired grants, and form-only grants. Preserve `NativeOriginEffectiveGrant` only where it actually describes the grant; avoid making all powers look like Species traits.
3. Decide whether effective directives are native source-local definitions, canonical shared mechanics, or a compatible combination. Reuse validated `originBindings` parameter results; do not require unchanging literal numeric values for level-dependent or form-dependent rules. New schemas are **proposals** until approved.
4. Correct the design defect in PR #149 that makes first-party form authorization effectively DM-only. The default path must accept an executed, supported native form rule; DM alteration remains optional and universal.
5. Review all three effect/character/Origin trust contracts against Pillar 2's real authentication flow. Choose **one authenticated provenance model** and rebase the combined spikes on a stable Pillar 2 integration commit.

**Exit:** Written common contract and owner review of unresolved semantics; no duplicate Origin constructor, no unsupported grants, and no mandatory DM ruling for a supported native form. Previous tests still run once rebased.

### Track B — Prove one real persistent-effect vertical slice (can advance alongside Pillar 2 Phase 4)

1. Use a **real, completely authorized** direct Origin from the actual selected native catalog and source it through `NativeOriginConstructionEngine`, not a hand-constructed test result with `Authorized`.
2. Feed one genuinely earned, host-verified supernatural grant into the common entitlement model. It is **not** automatically granted just because a character has a supernatural influence.
3. Apply one expressly approved **modification-only** effect with the shared effect evaluator and the existing projector. Check correct source/parameter binding, a normal non-affected character, and a scoped DM replacement; preserve the original Origin/advancement and spent resources.
4. Use the **Draconic Bloodline** as the primary category-1 design test; until a specific numerical feature is approved, keep exact synthetic effects in test-only data. Full-vampire normal-form effects remain an additional demanding acceptance fixture.

**Exit:** One full source-to-projection test with authenticated-authority substitute exercising the real construction result, and independently verifiable provenance. This is a test milestone, **not** a production authentication claim.

### Track C — Define and execute temporary form profiles (coordination gate: Pillar 2 transition support)

1. Agree with Pillar 2 exactly how an **authorized form activation** produces `Current` without changing `Underlying`; never let clients supply an invented active form and full species package as if already executed.
2. Reuse Pillar 2's temporary Species/Ancestry contract. The **shared effective-character layer** handles independently scoped modifications, suspensions, replacement of action sets, creature type, movement, spellcasting and equipment usability.
3. Explicitly define interaction of form entry/reversion with current HP/max HP, resources, concentration, conditions, equipment state, and active effect duration. Each source/form chooses the policy; no universal reset.
4. Test one reversible form with differing baseline and effective Species, Class grant applicability, latest-baseline reversion, a changed underlying baseline **during** the form, simultaneous forms and DM precedence.
5. Use **lycanthropy** for maximal temporary replacement and **vampire bat/mist** to show that two forms of one supernatural nature can have *different* restrictions. Trigger, agency and stage progression remain individually authored.

**Exit:** Authorized entry, evaluation, and exit all execute through Rules Core; independently earned entitlements survive and the latest underlying Origin is reconstructed on exit. No snapshot restoration or automatic monster CR/Hit Dice/progression.

### Track D — Complete permanent supernatural reconstruction (coordination gate: Pillar 2 completed reconstruction/Rite)

1. Consume Pillar 2's **completed and authorized** `supernatural-reconstruction` or applicable Rite method, baseline replacement, and historical audit rules. The authoritative change occurs **at a completion event**, not merely from infection, a power unlock, or an optional intermediate stage.
2. Test optional independent progression, partial modification, and direct terminal completion without a mandatory partial stage (BG3 comparison is illustrative, not canonical D&D mechanics).
3. Reconcile affected inherent grants and current form profiles, and preserve/alter independently earned Class features **only according to explicit authored rules**. Preserve spent resources and inventory histories unless independently authorized effects change them.
4. Determine which variants of **full vampirism** are persistent modifiers and which, if any, include reconstructive replacement. Do not force all vampirism into type 1, 2, or 3 solely to match the case matrix.

**Exit:** A completed, validated permanent transition updates `Underlying`; the former configuration persists as history, not an active grant source; DM-authorized variations remain explicit.

### Track E — Consolidate four-case rules and consumer integration (gate: shared mechanics and Pillar 2 Phase 5 API)

1. Convert design fixtures into **executable integration tests** for Draconic Bloodline, full vampire, lycanthropy and ceremorphosis, including absent/deferred effect coverage and negative/unauthorized cases.
2. Author individual first-party content **only after** their progression, form policies, source/stacking rules, balancing and trigger behavior are reviewed. Do not use selected-native monster statblocks as PC grants by resemblance.
3. Integrate the authoritative API/persistence and Character Sheet; ensure normal, active, reversion, suspended grant, source-replacement and permanent reconstruction states survive save/reload and use **current native ruleset content** without a new fixed-fingerprint publication gate.
4. Verify repeated evaluation, native content changes, live DM overrides, cross-tool effects, and playtest feedback. Keep rules authority in Rules Core.

**Exit:** A user can construct, transform, save, reload, reverse or permanently reconstruct an authorized character in the development environment; the same results are independently visible to all consuming tools.

## Near-term priority order

**Now:** Review this plan and freeze PRs #146/#148/#149 as non-mergeable implementation experiments. **Next:** synchronize Origin/effect grant identities, native effect authoring, and shared trusted-source authorization with Pillar 2 Phase 3/4; issue a corrected integration specification. **In parallel:** prepare Draconic Bloodline and full-vampire normal-state test data with no unapproved numerical benefits. **After the Pillar 2 transition boundary is executable:** advance form-profile and reconstruction runtime behavior.

Do not require all of Pillar 2 Phase 5 to be completed before running a Rules Core library-level persistent-effect integration test. Conversely, do not treat a passing synthetic host authority as production-ready character permission.

## Open decisions requiring explicit design review

1. Which common character-grant abstraction can represent **inherent**, **Class/Background**, **supernatural-acquired**, and **form-only** entitlements without losing their different provenance and revocation/availability semantics?
2. How does a native supernatural definition express a **dynamic** value, authored precondition, target and acquisition choice while reusing the selected-native content grammar and Origin parameters?
3. What is the exact source-of-truth/evidence for **first-party active form authorization** as opposed to a DM override?
4. What are the policies for independent and overlapping **resources**, modified ability scores/HP, concentration, equipment state, and conditions during transformations?
5. When may persistent physical changes such as vampirism change effective creature type or baseline Origin, and when must an explicit permanent reconstruction occur?
6. How will P2 Phase 4 Rite events and P4 completion events share an authorized transition executor without a second Origin implementation?
7. How will existing `NativeCharacterProjector` feature IDs be mapped to independently **earned source occurrences** needed for reliable form-dependent applicability?

**No individual supernatural progression, new first-party trait, merge, or gameplay API change is approved by this plan.**
