# Pillar 2 — Independent WorkChat Acceptance Plan

**Date:** 2026-11-16  
**Status:** Prepared for independent testing; **not executed by WorkChat**, not acceptance evidence, and not approval to merge or deploy.  
**Rules Core:** `LegendMaster03/dorks-and-dice-rules-core`, branch `feature/pillar2-power-evaluation-prototype-20261114`, draft PR #157.  
**Character Sheet:** `LegendMaster03/dorks-and-dice-character-sheet`, branch `feature/innate-spell-ledger-persistence-20261116`, draft PR #68.  
**Site:** read-only review of relevant current authorization contracts unless a coordinated test branch is specifically approved.  
**Primary handoff:** `docs/rewrite/character-origins/pillar2-consolidated-implementation-handoff-20261116.md`.

## Purpose and independence

WorkChat must conduct an **independent, evidence-producing testing pass**, not assume the implementing chat's tests or descriptions prove correctness. Read the actual draft PR branches and supporting native data. Inspect the changed code and security boundaries; execute local tests and realistic gateway/browser flows where supported. Record the exact commit IDs checked and do not accept stale CI results as tests of a newer HEAD.

**The pass has not happened yet.** Neither user-facing "ready for review and testing" status nor a successful WorkChat outcome may be inferred from the existence of this plan or from a green hosted workflow. Keep PRs draft and main untouched.

The first-party Feat system does not exist yet. Specifically, Human Versatile's **Origin Feat choice remains unresolved**; do not invent a feat list, auto-waive this requirement, or mark a conventional Human mechanically authorized based solely on Skillful. Treat that missing dependency as a documented deferral, not a functional failure in Skillful's preview.

## Baseline gate

1. Fetch the latest commits on both exact branches and compare with main. Confirm the two PRs remain draft/unmerged. Record any new divergence from the consolidated handoff.
2. Run Rules Core structure validation, complete .NET 10 Host/Mechanics tests, native catalog and Python migration checks, and native-only Docker ingress/readiness smoke. Run Character Sheet validation, the Pillar 2 native origin contract suite, and the real native gateway smoke. Record command lines and both pass/fail counts and log links.
3. Inspect first-party data and the live API response JSON from actual native catalog loading; do not replace Rules Core with hard-coded client mocks for all tests.

## Gameplay and data acceptance matrix

| ID | Construction or effect | Required observation | Blocker if violated |
| --- | --- | --- | --- |
| G01 | Direct selectable conventional Species/Ancestry | Reconstructs its complete native traits/choices and source identity without substituting mixed assumptions | Missing or altered native effect |
| G02 | Mixed Human/Elf (biological) | One selected foundation, explicit actual offers/manifestations, resolved choices, no unselected grant; all contributor evidence retained | Automatic parental-package inheritance |
| G03 | Same canonical trait from multiple contributors | Mechanic is represented once, with provenance for every donor and no doubled resource/sense/speed | Duplicated effective benefit |
| G04 | Distinct typed senses/resistances/movement modes | Requires explicit native/verified table disposition for overlap; distinct modes remain separate and unchanged | Arbitrary stronger-mode winner |
| G05 | Dragonborn leaf ancestry | Red/Bronze selected leaf source; area and damage type remain complete from selected contributing parameters, and shared pool links remain valid | Unbound or mixed unsupported parameters |
| G06 | Native low-light/darkvision and passive physical changes | Correct selected native effect; no extra sensory capability or temporary overlay inferred | Unselected/forged effect |
| G07 | Native amphibious and altitude adaptation | Actual authored air/water breathing, no implied swim speed; altitude does not grant cold or suffocation immunity | Incorrect environmental immunities |
| G08 | Native climb/swim/burrow | Preserves complete canonical speed/terrain restrictions and source; no auto-breathing or automatic max speed | Invented traversal rules |
| G09 | Human Skillful | Verified individual native Skill ID, original source and choice; no family/category pseudo-skill or rank allocation | Unauthorized skill grant |
| G10 | Human Adaptable Learning | 4 extra ranks at level 1 and +1 at each later level as **entitlement preview only**; rank spend/caps remain unresolved | Implicit allocation or rank-cap bypass |
| G11 | Elf Trance | When selected, authored four-hour Long Rest/sleep-not-required preview; no magical-sleep immunity or rest event | Unstated immunity or resource mutation |
| G12 | Orc Relentless Endurance | When selected, trigger excludes killed outright, 1 HP instead of 0, one Long Rest use; no automatic HP mutation or recharge | Free additional uses or unconditional trigger |
| G13 | Independent choice paths / level-gated abilities | Choices, stage progression, sources and resource ownership do not turn into extra free casts or unlocked traits | Grant without correct level/source |
| G14 | Nonbiological and Restricted origin methods | Biological transmission never applies to Construct assembly, Plant reproduction, restricted Rite Ancestries or other methods without their own explicit rules/DM decision | Leaked biological permission |
| G15 | Direct Orc/Elf when other features are unimplemented | Unreviewed biological effects remain conventional native package; not automatically offered for mixed inheritance | Unsupported first-party inheritance |

## Authorization and persistence acceptance matrix

| ID | Threat or boundary | Required negative or positive test |
| --- | --- | --- |
| S01 | Caller-controlled actor, campaign, origin profile or DM ruling | Rejected without Site-issued, exact-scoped request authentication; never trust browser labels |
| S02 | Parent lookup by guessed other-owner character ID | No parent persistence read or profile disclosure until independent Site owner grants for both child and parent; campaign membership alone is insufficient |
| S03 | Untrusted previously mixed parent | Production `ManifestedProfile` resolver stays closed until full signed/server-authenticated underlying profile, event history and authorization are verified |
| S04 | Concurrent parent/child changes | Reject stale origin/draft revisions and changed event evidence; recheck ownership before mutation; never use an earlier preview as a write permit |
| S05 | Test-only self-override | Must not become a deployed generalized lookup, self-certified profile, public permission, or production inheritance route |
| S06 | Missing or altered native fingerprint | Rules Core and Character Sheet must not silently reinterpret old grant snapshots under new definitions |
| S07 | Innate spell ledger | Only currently verified Direct initializations persist; no unapproved cast, rest, level-up, mixed, or transformation mutation routes |
| S08 | Unauthorized table decisions and forged Skill choices | Fail closed; a supplied `CatalogEligibilityEvidenceId` string is not server proof by itself |

## Multi-generation and non-biological regressions

Perform a saved-profile (not just in-memory fixture) two-generation test **only if** the production authentication contract authorizes it; otherwise report **blocked/not implemented** explicitly. In the permitted test fixture, verify that an offspring has its own effective grants while ancestral `InheritedFrom` records preserve the immediate authenticated parent revision and original grant source, without recursively minting previous generations' grants.

Keep Plant, Construct, Fiend spawning and permanent Rite cases as *architecture isolation* tests. These are not mandates to implement their full player workflows in this Pillar 2 slice.

## Real consumer and UI checks

When a staging environment is available, test the actual Character Sheet through the Site host, not just unit-level application services. Verify owner/DM permissions and independently view/edit a draft, resolve selections, activate a supported Origin, reopen the saved character, and validate the exact original grant and revision. Confirm normal and archived-character restrictions, an unrelated account denial, stale revision denial, and no exposure of private parent profile data. The Character Sheet's initial Human/Elf DM-approved mixed commit remains intentionally narrower than the general native engine.

Do not mark **end-to-end** acceptance complete if only mocked gateway tests pass. State exactly which real staging scenarios could and could not be executed.

## Required WorkChat report and review threshold

Return a durable Markdown report with:

1. Both exact tested branch heads; any branch changes WorkChat made; and explicit confirmation no push to main, merge or deployment.
2. Per-ID G01–G15 and S01–S08 result: **PASS**, **FAIL**, **BLOCKED**, or **NOT RUN**; steps, observed behavior, evidence files/links, and relevant severity.
3. The exact .NET, Python, Docker, Character Sheet and real browser/gateway commands executed; CI run IDs; test counts; and whether tests were independent of prior CI.
4. Security review of source authorization, mixed-profile provenance and source revision freshness, and mechanical regression review of source-neutral traits, canonical selections, unresolved paths and duplicate handling.
5. Reproduction steps for every finding and whether it prevents ready-for-review status.
6. A definitive independent WorkChat verdict: **READY FOR REVIEW / NOT READY / INCONCLUSIVE**, explaining all blocking exceptions. A blocked Feat catalog should be identified as a known dependency, not pretended to work.

**Release gate:** no "ready for review and testing" notification until both draft branches have been verified against the intended scope, all blocking code issues are resolved, applicable CI passes at the exact code heads, and an actual WorkChat independent pass has produced a report. The implementing assistant must not claim a WorkChat test occurred if it could not invoke that environment.
