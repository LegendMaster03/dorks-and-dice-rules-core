# Pillar 2 — Consolidated Biological Origination Implementation Handoff

**Updated:** 2026-11-16  
**Status:** Both cross-repository PRs remain **draft and unmerged**. No deployment or change to `main` is authorized.  
**Rules Core:** `LegendMaster03/dorks-and-dice-rules-core`, branch `feature/pillar2-power-evaluation-prototype-20261114`, [PR #157](https://github.com/LegendMaster03/dorks-and-dice-rules-core/pull/157).  
**Character Sheet:** `LegendMaster03/dorks-and-dice-character-sheet`, branch `feature/innate-spell-ledger-persistence-20261116`, [PR #68](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/pull/68).

## Verified resumption baseline (before this continuation)

- Rules Core: `0376a6b97b152cdc5ebb5ec5e3ef504c9dcc0ec9`; 170 commits ahead and 38 behind `main` at inspection; 77 PR changed files. Both Actions runs succeeded: [structure #43925395815](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43925395815) and [complete public native API/C#/Python/Docker #43925395770](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43925395770).
- Character Sheet: `69cd7591cf09b01b152e632789c3f816c6c53b10`; 58 commits ahead, none behind `main`, 22 PR changed files. [Validation #43931185553](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43931185553), [real Rules Core contract #43931185596](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43931185596), and [native contract smoke #43931185560](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43931185560) all succeeded.
- On 2026-11-16, the 38 newer upstream Rules Core commits affected Character State manifestation/influence documents, contracts, resolvers, projectors, and their tests. No upstream paths overlapped this continuation's new physiological projector/test; cross-pillar integration still requires review. **Do not rebase or merge upstream automatically.**

## Architectural and authorization decisions to preserve

- Shared construction is **Deconstruct → Combine → Reconcile → Manifest → Reconstruct**. Biological Origination is one method-specific procedure; construct, plant, spawning, magical, and supernatural methods are separate. Both conventional Species/Ancestry and mixed backgrounds remain supported.
- Player direction, selected offers, complete physical foundation, verified native contributor eligibility, source-native choices, dependencies, and full trait progression are authoritative. There is no approved dominant Species, two-parent ceiling, random inheritance rule, or universal numerical budget.
- A previously manifested mixed contributor can transmit **only the verified current underlying configuration**, with immediate-parent identity, authoritative profile revision, and characteristic provenance. `INativeOriginManifestedContributorAuthority` and `NativeOriginManifestedContributorVerifier` implement validation but the **production authenticated profile lookup is not connected**. The in-process self-override remains test-only.
- Biological opt-in is an explicit finite first-party list, not automatic eligibility for the entire native catalog. Additional trait permissions and exact allocation prices require independent review/approval. Read-only projections do not grant traits.

## Existing implemented increments (pre-continuation)

1. Nine-Species/34-Ancestry native audit, biological contributor method eligibility, physical foundation selection, explicit six-trait opt-in, Dragonborn complete Breath Weapon parameter composition, dependent/linked choices, resource-owner reconstruction, sense/resistance overlap, Drow sensory link, and level-gated Stone Goliath sight.
2. Authenticated in-memory manifested descendant verifier, revision/lineage serialization, multigeneration cases, source-neutral resource closure, downstream intrinsic/vision/Darkvision projection, and advisory provisional allocation preview.
3. Source-neutral Dwarf HP progression, passive overlap, Human Adaptable Learning rank entitlement, Drow and Tiefling source-bound spellcasting ability, level- and proficiency-dependent innate spell preview.
4. Pure native innate spell free-cast ledger: initialization, spending, short/long rest behavior, advancement, stale-state checks, and exact table-authorized changed-capacity outcomes. These calculations are **not** production event authentication.
5. Rules Core authenticated, read-only conventional Direct-Origin `POST /api/mechanics/v1/origins/innate-spells/ledger/initialize-preview`; no mixed-Origin authorization or persistence.
6. Character Sheet PostgreSQL immutable ledger receipt/CAS store, Site-authorized actor/character coordinator, narrowly scoped Direct-Origin native bootstrap, exact catalog/Origin identities, monotonic `CharacterSheetRoot.MutationRevision`, database trigger and guarded resource SQL updates. Concurrent independent Feat additions still succeed. No public ledger mutation route is enabled.

Relevant detailed records are in this directory: `pillar2-authenticated-mixed-descendant-transmission-20261115.md`, `pillar2-biological-inheritance-native-catalog-audit-20261115.md`, `pillar2-provisional-allocation-preview-20261115.md`, `pillar2-human-skill-rank-entitlement-projection-20261115.md`, `pillar2-drow-innate-spell-source-binding-20261115.md`, `pillar2-tiefling-descendant-spell-binding-20261115.md`, and the five `pillar2-native-innate-spell-*-2026111*.md` reports. Character Sheet's authoritative record is `docs/innate-spell-ledger-persistence-boundary-20261116.md`.

## Continuation increment: physiology projection

**Commits:** `c257f71a6e090de6348b16359bde3f80ff4089d7` (projector), `5eed4cd95f85340e78e8b9b6119562c4016b243e` (regressions), `e3752f242b09f693cf58236442735716b7a10ed8` (canonical source expectation correction).

- New `NativeOriginPhysiologyProjector` is a read-only consumer of exactly two **already approved opt-in** native traits: `trait.shared.amphibious` and `trait.goliath.altitude-acclimation`. A completed, verified conventional Direct Origin or explicitly authorized mixed Origin must select the original trait. Nothing is auto-inherited.
- Matches the existing direct and approved-mixed intrinsic foundation verification. Every selected adaptation must retain native source, candidate identity, individual contributor eligibility, exact original canonical payload, and existing authorization. Duplicate grants, wrong mode, active transformations, unauthorized or malformed profiles fail closed.
- Amphibious records the authored ability to breathe air and water **without** a swimming speed. Altitude Acclimation records its unbounded natural acclimation above 20,000 feet and the native exclusion of cold resistance and suffocation immunity; it does not imply unrelated environment protection.
- Both views are explicitly `PreviewOnly`. They do not perform movement execution, hazard adjudication, event authorization, or state persistence. This is an executable projection of selected authored mechanics, **not** a new Species balance/admission or inheritance policy.
- `NativeOriginPhysiologyProjectorTests` uses actual Bronze Dragonborn and Fire Goliath donor offers and both traits together; checks direct Orc absence, serialization round trip, a missing selection, duplicated grants, false candidate evidence, payload tampering, incomplete origin, and direct/mixed mode isolation.

**Verification after continuation:** The intermediate test commit `5eed4cd95f85340e78e8b9b6119562c4016b243e` passed [structure #43948308534](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43948308534) but failed [full native #43948308571](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43948308571) on **two incorrect source-ID test expectations only** (495 of 497 Mechanics tests passed). The actual authorized Goliath trait is sourced from canonical `species.goliath` even though the playable parent contributor is `ancestry.goliath.fire`. No effect/eligibility implementation was wrong. The corrected test commit `e3752f242b09f693cf58236442735716b7a10ed8` passed **both** [structure #43948712120](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43948712120) and [full native API / C# / Python / native catalog / Docker #43948712162](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43948712162). The intermediate failure is not the current branch result. No local .NET runtime was available; the full hosted CI provided build/test verification. The source-ID distinction is independently asserted against the selected `fire` contributor candidate.

### Final verification and integration regression

**Further code/test commits:**
- `f7f50539e8d7c5e20199227249d846ab744f5997` — reject nonnumeric authored altitude bounds rather than letting an invalid native shape reach numeric parsing.
- `32255ad24b878a8f39d842842e35912794932adb` — verify a conventional, direct Bronze Dragonborn activates only its actually selected canonical Amphibious grant. Both [structure #43949543704](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43949543704) and [complete native #43949543729](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43949543729) passed.
- `caa841752bf8a6adfda3e53af68f4659b084b319` — regress selected physiological grant projection through a verified **previously mixed** parent → child transmission, including exact native source, immediate-parent profile revision and contributor ID. The resolver remains a test-only in-process `INativeOriginManifestedContributorAuthority`; it adds no production trust shortcut or inheritance permission.

**Final verified code-head CI:** at `caa841752bf8a6adfda3e53af68f4659b084b319`, [structure #43949908774](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43949908774) **passed** and [complete native API / C# / Python / native / Docker #43949908672](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43949908672) **passed**. The full workflow reports **120/120 Host tests and 499/499 Mechanics tests passing**, plus successful Python, native catalog validation, and Docker smoke. Documentation-only commits after this verification must not be confused with additional gameplay changes.

**Changed-file scope of this continuation:** new `src/RulesCore.Mechanics/NativeOriginPhysiologyProjector.cs`, new `tests/RulesCore.Mechanics.Tests/NativeOriginPhysiologyProjectorTests.cs`, and this new consolidated handoff. No Character Sheet files or Site contracts were changed; neither PR was merged, deployed, or marked ready.

## Existing integration trust boundaries

- Rules Core native result/projection fingerprints and optimistic revisions are **not** signatures or actor authentication. The Site host must attest the actor, saved character ownership, actual Origin underlying profile and authoritative revision, native catalog, and event evidence; the database must atomically compare the source revisions when committing.
- Character Sheet's registered native ledger evaluator permits only verified source-matched conventional Direct-Origin `Initialize`. Spending, Short Rest, Long Rest, Advance, transformation/form applicability, mixed-Origination, DM capacity rulings and caller-visible mutation remain unavailable. Do not allow submitted resource balances or scoped authority claims to become durable state.
- The Character Sheet root revision and PostgreSQL lock close ordinary character aggregate mutation races during a remote bootstrap, without EF blanket concurrency tokens. Independent direct SQL bypass, Site lifecycle/authorization changes, future transformation revisions, and unimplemented runtime event recording are not thereby secured.

## Remaining technical work and explicit blocks

**Biological construction and actual consumers (primary):**
- Production authenticated lookup of already-mixed underlying profiles at the saved revision, with actual character/campaign scope. The current test-only resolver does **not** supply that production proof; enabling the feature requires a trusted integration owner.
- Full native characteristic coverage: Human Skillful/Versatile feat selection and ranked allocation; remaining Elf/Gnome/Tiefling spell-course creation/replacement choices; additional movement modes, speed overlays, conditional physiological effects; Goliath and other higher-level traits; and cross-trait dependencies. Do not treat read-only entitlements as allocated ranks or spell actions.
- End-to-end conventional and mixed constructions, source identity in Character Sheet, real player choices, multigenerational saved-profile resolution, and full conventional preset round trips. In particular, the Wood Elf 35-foot/Elf 30-foot walking speed conflict remains intentionally unresolved without an authored relationship rule.
- A configurable provisional protection against uncontrolled accumulation of independent benefits, subject to owner approval for actual effective allowance and price taxonomy.

**Ledger integration (secondary):**
- Trusted spell-use and rest event evidence; current stored ability/class progression and Origin validation; full advancement and resource capacity change adjudication; runtime spell slots versus Origin free casts; mixed Origin, scoped DM authority and transformation applicability. Do **not** expose client mutation or invent a capacity-refill default.
- Character Sheet's Direct-Origin bootstrap runs under existing PostgreSQL source revision checks. New actions need their own source/event proofs and replay protection rather than copying the preview endpoint.

**Gameplay decisions still pending:** exact biological allocation safeguard and allowances; policy for latent/unmanifested characteristic reexpression, broadened first-party trait eligibility, per-level skill rank allocation/retraining and Origin Feat source, Wood Elf speed reconciliation, temporary-form overlay applicability, default resource-capacity growth. Exact scoped DM outcomes are supported where the existing engine already requires them.

## Next executable increment

After confirming both CI runs for the physiology increment, inspect existing native movement-mode authoring and source deconstruction, then implement a source-neutral, **read-only** mode projection and tests for selected, authorized complete Origin grants only where native executable definitions already exist. Do not make unreviewed movement traits first-party inheritable or force Wood Elf base/leaf speed precedence. If existing contracts cannot safely express a movement mode without approving a new game rule, document that precise gap and prioritize trusted mixed-contributor lookup planning or another previously authorized trait consumer instead.

For later independent validation, compare the actual two PR branches and their contracts, not just green CI. Validate production mixed-profile authentication, exact source/choice lineage, Character Sheet mutations and source revisions, direct Species compatibility, duplicate-benefit handling, progression, and temporary/permanent form applicability. Neither PR is ready to be marked non-draft merely because read-only projections pass.
