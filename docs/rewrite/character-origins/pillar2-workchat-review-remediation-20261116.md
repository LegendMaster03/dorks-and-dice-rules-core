# Pillar 2 — Independent WorkChat Review Remediation and Retest Handoff

**Date:** 2026-11-16  
**Review source:** [independent WorkChat review](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/docs/pillar2-independent-review-20261116/docs/rewrite/character-origins/pillar2-independent-review-and-testing-20261116.md). Its **NOT READY** verdict was based on Rules Core `0ac60f80a330777b4e1a26e1e4718a2923610bfb`, Character Sheet `69cd7591cf09b01b152e632789c3f816c6c53b10`, Site main and separately examined Site DM authority. These changes do **not** retroactively change that independent verdict.

**Implementation results:** Rules Core source HEAD `dbe86e42a0fda03c234210052ed76ef23e40f27f` (**code**); Character Sheet source HEAD `2d3e1379afba56a7efde90a09f7ef1a76fd8552f`. Both draft PRs remain unmerged; no Site source change, main modification, or deployment occurred. This document may itself advance the Rules Core documentation-only HEAD.

## WorkChat finding reconciliation

| Finding | Status after implementation | Reproducible evidence | Remaining qualification |
| --- | --- | --- | --- |
| **F01 — no pinned present-head Core/Sheet test** | **Addressed for tested pair** | Sheet adds `.github/workflows/pillar2-current-branch-cross-repo.yml`, which verifies exact source SHAs, runs native consumer probe, real Core Host HTTP and PostgreSQL mixed Origin save/reload. [Run 43988098956](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43988098956): **14 passed, zero failed**, including the new Drow case. It printed `SHEET_SOURCE_SHA=2d3e1379afba56a7efde90a09f7ef1a76fd8552f` and `CORE_SOURCE_SHA=dbe86e42a0fda03c234210052ed76ef23e40f27f`. | Workflow pins Core intentionally, and requires updating when Core advances. Existing main-compatibility and historical Phase 5 workflows remain unchanged. The joined test still uses fixture Site DM authority. |
| **F02 — malformed nested Origin data raised before validation** | **Code and HTTP test corrected** | Core `NativeOriginApiEndpoints.HasMalformedOriginNestedInputs` rejects invalid contributor Role/IDs, candidate IDs and manifestation requirement IDs **before** native evaluator; real authenticated HTTP regression covers null candidate key, null requirement key, null biological role with HTTP 400 and no effects. [Core CI 43986682615](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43986682615): **125 Host, 527 Mechanics tests**, zero failed; Python/content/Docker successful. [Structure CI 43986682682](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43986682682) successful. | This validates the specific WorkChat inputs and shared guard at both entry points. An independent follow-up should also exercise approved-mixed HTTP null negatives and general fuzzing. |
| **F03 — current Site DM scope not validated in genuine three-service use** | **Still blocked** | The WorkChat report identified Site feature `feature/pillar2-origin-dm-campaign-authority-20261112` as the existing scoped authority contract; current Sheet still rejects missing scope. No permissive fallback or Site change was made. | Need **isolated nonproduction three-service** actual Site-issued DM/owner permissions, revocation/archive, unrelated-account denial, draft activate/reload and browser checks. Synthetic in-process Site actor in the paired test is **not** this evidence. Do not claim production is broken or ready. |
| **F04 — absent positive HTTP projections and native nonempty durable ledger** | **Addressed for tested subset** | New real-first-party Elf Trance, Orc Relentless Endurance (direct and scoped mixed) authenticated HTTP tests pass at Core HEAD above. Character Sheet's conditional `RealDrowCourseInitializesNonemptyNativeLedgerAndReloadsFromPostgres` builds a real Drow direct Origin with explicit `species.elf`/Drow identity and Wisdom, commits via normal Direct activation, advances Warlock Class from level 1 through 5, requests authentic Core HTTP ledger bootstrap, persists two exactly source-grant-keyed free casts (Faerie Fire and Darkness) in PostgreSQL, checks create-once event and denies SpendFreeCast, reopens a new DB context and recovers both balances. [Pinned joint run 43988098956](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43988098956): **14/14 passed**. | Site identity is test-only. No casting/resting/level-up ledger mutation was enabled. This is a real Core/Sheet/DB positive path, **not** a browser or genuine Site ticket test. |

## Additional defect discovered and corrected by pinning the pair

The first current-head probe demonstrated that Core projects the canonical `trait.shared.low-light-vision`, while Character Sheet's `VerifiedOriginVisionReader` recognized only obsolete `species.orc#trait:low-light-vision`. The reader now recognizes the canonical ID while preserving narrow support for existing persisted legacy ID, exact source/occurrence and strict rule-effect payload checks. New negative unit coverage rejects altered source, definition and unexpected effects. That prior integration failure disappeared on [current-head joint CI](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43988098956). No broader optical mechanic or grant was added.

## Test-harness diagnostics during remediation

The new Drow test initially failed Direct activation because its draft recorded only Drow Ancestry, while native deconstruction correctly derived `species.elf` and Sheet required a match with the saved selected Species. The test now supplies both selected IDs explicitly, without loosening the activation verifier.

The next run initialized a correctly empty level-one ledger because the test incorrectly supplied `5` to `ApplyClassAdvancementAsync` as the *Hit Die gain* rather than actually advancing Class levels. The test now applies the real Class advancement to levels 2–5 and verifies level 5 before requesting free-use pools. This corrected fixture proves two granted native free-use resources without inventing any entitlement.

## Retest and readiness boundary

1. **WorkChat independent retest:** rerun the original review checks against **the exact corrected paired code SHAs**, inspect negative malformed inputs, verify canonical vision interpretation and real native Drow balance. Verify run logs and draft status. The original independent review did not execute against these corrected heads.
2. **Genuine Site authorization:** identify an isolated build containing the Site DM authority feature; with current Core/Sheet, verify owner and DM session projection, campaign and target scopes, unauthorized actors, archived character, revocation/revision races, and source/commit consistency. Do not use production to obtain test evidence.
3. **Browser acceptance:** in the authorized isolated environment, create/edit/activate direct and supported Human/Elf Origins, refresh/reload, and check console/network and negative permissions. Record deployed image/commit IDs. Without this, hands-on acceptance remains **BLOCKED/NOT RUN**.
4. **Deferred mechanics are still deferred:** Human Origin Feat authoring, skill-rank spending, general production mixed-parent use permission/full provenance replay, final numeric balance allocation, new biological trait permissions, and unsupported mutation routes must not be fabricated or bypassed.

**Current verdict:** paired Core/Sheet test failures and known malformed input scenarios repaired; **not yet ready for owner-requested hands-on acceptance signoff** because genuine Site DM/owner and browser testing, and WorkChat's retest at the new heads, remain outstanding. Keep both PRs **draft and unmerged**, no production deployment.


## Follow-up: authenticated transport and Site ticket consumer validation (2026-11-16)

The previous subsection records the **earlier** remediated pair. Work continued without changing the established gameplay or DM permissions.

**Exact later tested code revisions:**
- Rules Core: `1a753073e14e04d299b348974dbddf049ca93d20` on `feature/pillar2-power-evaluation-prototype-20261114`.
- Character Sheet: `ec43be9bb8f29771c437d7007a885ea73f9557e1` on `feature/innate-spell-ledger-persistence-20261116`.
- Site: **unchanged**; independent WorkChat review identified `feature/pillar2-origin-dm-campaign-authority-20261112`, previously exercised separately. Neither Site main nor the deployed Site was inspected in this follow-up.

**New changes:** The same fail-closed nested-JSON guard now runs on **all three** authenticated native HTTP Origin paths: ordinary direct preview, DM-approved mixed evaluation, and source-matched direct innate-spell bootstrap. New real HTTP regressions cover null manifestation candidate IDs, null choice requirement IDs, malformed native contributor roles/sources, and no constructed effective result in rejected responses. Successful tests do not prove every conceivable malformed nested field is safe; additional fuzzing and Site permission races remain separate.

On Character Sheet, `ToolHostOriginDmTicketContractTests` uses the **actual** `DorksAndDiceToolHostAuthenticationClient`, `HostedToolAuthenticationMiddleware`, and `ToolHostOriginDmAccessGateway` against the contract-v1 **wire shape** published by the Site feature. It accepts only the exact current campaign DM/active character pair, fails closed when the `dmCharacterScopes` field is absent, denies empty or unrelated scopes, rejects a demoted DM, and verifies fresh introspection rather than recycling an earlier authorization. **The Site JSON in this test is authored test input**, not a response from a running Site or a three-service test.

**Verified latest CI:**

| Source/test pair | Run | Result |
| --- | --- | --- |
| Core `1a753...` rewrite structure | [43988865790](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43988865790) | PASS |
| Core `1a753...` native API + complete C#/content/Python/Docker | [43988865808](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/43988865808) | PASS, **126 Host + 527 Mechanics**, 0 failed |
| Sheet `ec43be...` + pinned Core `1a753...` | [43989241816](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43989241816) | PASS, **14 current-pair HTTP/PostgreSQL integration**, 0 failed; checked-out SHA lines verified |
| Sheet `ec43be...` + historical Phase 5 Core | [43989241827](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43989241827) | PASS (legacy compatibility, not substitute for current pair) |
| Sheet `ec43be...` + Core main consumer probe | [43989241818](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43989241818) | PASS (main compatibility) |
| Sheet `ec43be...` full release/frontend/DB/container | [43989241738](https://github.com/LegendMaster03/dorks-and-dice-character-sheet/actions/runs/43989241738) | PASS, **147 Unit + 183 Integration**, 0 failed; PostgreSQL/container smoke completed |

The current-pair workflow remains pinned to the **code SHA** `1a753073e14e04d299b348974dbddf049ca93d20`. Subsequent **documentation-only** changes to the Rules Core branch do not silently change what was tested. Any later Core **code** commit must update the explicit Sheet pin and rerun the combined workflow before a new acceptance claim.

**Remaining blocked:** No **real Site-issued ticket** has passed through a live Site + Sheet + Core integrated staging path for this corrected pair; no Site/Sheet browser creation, activation, reload and revoked/archived test has run; and WorkChat has **not independently retested these revised code heads**. The earlier independent `NOT READY` verdict remains the most recent independent verdict. The above passing CI results qualify component and paired-source behavior, **not** owner hands-on acceptance.

Keep the two feature PRs **draft and unmerged**; do not deploy to production or relax any Site authorization. Feats (including Human Versatile), general production mixed-parent permissions and other explicitly deferred mechanics are not part of this remediation.
