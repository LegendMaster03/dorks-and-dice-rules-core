# Pillar 2 — proposed choice instances, synthetic catalog evidence, and nested Heritage history

**Date:** 2026-11-11  
**Status:** Source-neutral, read-only contract experiments. No first-party selection policy, catalog entitlement, DM authority, persistent player data, grant, or actual inheritance rule is approved.

**Tools:**
- `tools/character_origins_choice_instances.py`
- `tools/character_origins_nested_choice_history.py`

**Tests:**
- `tests/test_character_origins_pillar2_choice_instances.py`
- `tests/test_character_origins_pillar2_nested_choice_history.py`

## Why this boundary exists

The [native choice-schema audit](pillar2-native-choice-schema-audit-20261111.md) identifies authored choices, and the applicable-choice projection associates them only with **tentatively retained characteristics**. Neither answers whether a player may select a particular skill, feat, spell, size, or governing ability. Nor does the old experimental nested profile retain a complete verifiable record of independent choices made by that parent.

The next general-system boundary therefore separates **the original entitled choice definition**, **one candidate-bound proposed selection**, **external catalog eligibility evidence**, **historical choice state**, and **permission to actually grant or transmit** that choice. These cannot be collapsed into one source-qualified ID, and none of the new studies creates an authorized current Origin.

## Proposed choice instances

An explicitly proposed choice record contains:

| Field | Role |
| --- | --- |
| `candidateId` | Exact selected characteristic offer in the fingerprint-bound preview; still ephemeral, not a persistent grant |
| `choiceEvidenceId` | Exact authored native choice occurrence, associated with that candidate |
| `selectedOptionIds` | Explicit chosen values, with exact declared count and no duplicates |
| `proposedAuthorityRef` | Attribution of the proposal; **not** verification that a rule or DM authorized the choice |

An experimental reviewer verifies that the candidate and authored choice are actually related. A record cannot claim the Human Skillful catalog merely by naming the trait in a custom proposal or by choosing an unmanifested parent Species. It also rejects duplicate selections for the same choice occurrence, invented option IDs, incorrect cardinalities, unselected characteristic references and stale candidate projections.

**Choice-kind distinctions:**

- **Finite authored options** (Human Small/Medium, Goliath governing ability): offered option set is checked directly. The selected intrinsic value must match the actual proposed property.
- **Open catalog entitlements** (Human Skillful or Versatile; High Elf Arcane Cantrip): require a separately supplied `synthetic-catalog-evidence-only` snapshot containing the **exact referenced catalog**, candidate option IDs, and evidence reference. An arbitrary or missing catalog cannot silently satisfy the choice.
- **Persistent timing:** explicitly authored creation/ancestry selections retain their timing. A list with unresolved timing (such as the High Elf spell-list choice) remains unverified even when a catalog option matches.
- **On-use options:** Metallic Breath Weapon's Repulsion/Enervating options remain runtime choices; **no permanent origin choice instance is allowed** for them.
- **Dependent bindings and player allocations:** `spellcastingAbilityFrom`, `allocation: player-choice`, and unmodeled choice fields remain unresolved; they are not reinterpreted as independent fixed-menu choices or arbitrary rank allocations.
- **Source-level selection controls:** Tiefling's required Ancestry selection and an Ancestry's source-wide spellcasting ability selection are not granted as traits in a mixed Heritage. Those need a future direct-standard-preset policy versus mix policy distinction.

All proposals preserve original contributor and authored occurrence identity. For testing, an instance snapshot fingerprint binds a complete proposed choice set to the precise candidate review. This is **not** an immutable, approved grant ID.

### Important catalog limitation

The experimental catalog snapshot answers only: *Does the proposed value appear in the particular list supplied to the reviewer?* It does **not** validate the actual first-party skill/feat/spell catalogs, level qualification, duplicates against other character components, proficiency/rank ceilings, entitlement balance, or DM authorization. A user-supplied `proposedAuthorityRef` is not trusted approval. No actual Rule Core entitlement is issued.

## Previously mixed parents and prior choices

The existing `draft-origin-profile` model conserves a parent's **manifested characteristics and parameters**, and the generic nested-pool verifier reconstructs those characteristics from earlier sources. However, its current schema **does not persist independent resolved choices** (for example, which Skillful proficiency that character received). It would be incorrect to infer that a nested parent must choose that skill again, or that a descendant automatically inherits the same skill.

The separate `nested_choice_history.py` study tests a possible **version-bound, non-authoritative sidecar**:

- References one **actual manifested characteristic** in a recorded parent profile.
- Includes the exact parent `profileId` and content fingerprint, preventing a historical selection claim from silently following an edited parent profile.
- Associates an authored choice occurrence and explicit option values with that characteristic, using the same native choice evidence and synthetic catalog validation.
- Rejects absent or mismatched parent traits, duplicate or fabricated choice records, stale profile snapshots, unresolved allocations, on-use options and derived bindings posing as persistent choices.
- Identifies missing recorded choice history, rather than fabricating a value.

A structurally matched sidecar remains `source-shape-matches-proposal-not-authorized`. **The fingerprint does not prove that the earlier player actually selected the option**; it proves only that a proposed historical record matches one exact draft profile and a synthetic authoring snapshot. It is not a signature or access-control mechanism. An authorized future profile format must persist trusted choices *when they are made*, not retroactively certify a proposed sidecar.

Historical **recording**, inheritance **eligibility**, and the descendant's eventual **manifestation** remain three different decisions:

1. The parent possessed a particular manifested trait with a recorded optional value.
2. The mixed-Heritage policy establishes which properties the parent can offer or transmit. Latent transmission is not resolved.
3. The descendant selects or manifests its own configuration under that policy, potentially with its own permitted choice where the definition allows one.

None of the three implies the others. Retaining source and choice provenance lets us implement varied future policies, including **fully custom character construction**, without pretending every choice has biological parentage.

## Current remaining work — before trusted standard round trips

1. **Authoritative generic choice definition and execution contract:** Convert current source-specific formats into unified, versioned choice requirements, including timing, replacement, embedded spell choices, per-character governing ability, and catalog selection. Keep the original authored definition available for lossless reconstruction.
2. **Actual catalog adapters and eligibility:** Resolve skill, feat and spell availability from authoritative Rules Core catalogs (including level and skill-rank limits); experimental catalogs cannot serve as validation.
3. **Integrated choices within character profiles:** Persist chosen values on the final current grant/slot with stable identity, complete source provenance and an authoritative record of the approving action. Do not rely on ephemeral `evidence-N` identifiers or optional historical sidecars.
4. **Nested profiles and transmission:** Require an actually verified profile carrying its prior selections; decide which retained values are transmissible, configurable for the child, or history only. Mixed parents should not require re-running all of their ancestors' selection interfaces.
5. **Standard direct Species/Ancestry reconstruction:** Round-trip each current standard source through the same general choice/manifestation/reconstruction path; unresolved source bindings and differences in trait semantics must be reported, not silently dropped.
6. **Policy/authority and balance:** Establish standard versus mixed versus partially/fully custom eligibility, general duplicate and intrinsic-property rules, DM exceptions, and a separate playtest balance assessment. None of these policies is invented by the choice evidence prototypes.

The three public workflow stages remain **Deconstruct/Assemble → Reconcile/Manifest → Reconstruct**. The five distinct internal operations remain available for implementation and validation. All content here stays on the draft Pillar 2 feature branch; no selected-native gameplay rules or `main` branch files are changed.
