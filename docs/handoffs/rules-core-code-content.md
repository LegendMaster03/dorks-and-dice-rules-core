# Rules Core Code Rewrite handoff — ongoing content content release and experiment separation

**Owning project:** Rules Core Code Rewrite (not Rules Content)  
**Base content PR:** `content/coherent-rule-promotion-wave-1` targeting latest `main`  
**Ruleset:** `first-party-001`  
**Status:** **Handoff only. No Core Code changes or deployment authorized by the present content PR.**

## Requirement 1 — reviewed SHA + fingerprint, not the original pinned catalog

`tools/deploy_native_core.py` currently hardcodes `FINGERPRINT = "870ca5e89a97b9176f0a3b55fb6cb9467227d13562a83dd7d72ecee5a57ecda6"`. That value reflects the original 35-entity catalog. The reviewed content snapshot expands the catalog to a verified **222** entities and exact native fingerprint **`b2af2ac5b47d133e181b82604a0619593ac5c6c67ae73da6e01f96f920acfd32`**, computed by `NativeCatalogLoader` in the content digest PR workflow. Continuing to require the original digest will correctly reject the update, but ignoring the check would be unsafe.

Implement a reviewed release input containing **(a) exact release Git SHA, (b) exact C# loader-computed native SHA-256 fingerprint, (c) content-review reference, and (d) rollback image/SHA**. Verify the image provenance against the reviewed release SHA and verify **both private and public ingress instances** loaded the exact approved fingerprint. Do not accept a runtime-generated fingerprint as approval by itself. A manifest containing the reviewed expectation is not a per-rule publication allowlist and must not relax native authority, signing/review or isolation checks.

Affected components: `tools/deploy_native_core.py`, release workflow(s) invoking it, `src/RulesCore.Content/NativeCatalogLoader.cs` (digest source, should not change solely for this task), `src/RulesCore.Host/NativeReferenceEndpoints.cs` (private fingerprint/readiness), public-distribution policy and configuration. Preserve commit-pinned image reuse, no image-building during deploy, controlled network membership and rollback safety.

**Acceptance criteria:** old fingerprint does not authorize updated content; reviewed SHA+new fingerprint passes; mismatched fingerprint fails closed before or during readiness verification; both ingress containers agree; missing/corrupt native fails with no `working/` fallback; prior image is recoverable without restoring any obsolete runtime. No deployment without owner authorization.

## Requirement 2 — dynamic native content tests (completed on the content branch)

`tests/RulesCore.Host.Tests/NativeCatalogTests.cs`, `tests/NativeCatalogFingerprint/Program.cs` and the Docker API smoke test now derive entity counts and category facets from the current catalog, rather than the initial 35-record dataset. The persistent test contract covers successful loading, exact identity uniqueness, paging, references, fingerprint format, failed invalid input and no `working/` fallback. Future valid content additions/removals must not require changing source-code counts.

**Acceptance criteria:** all C# unit/integration tests build under .NET 10 and pass with arbitrary valid catalog sizes; native content remains the sole source; semantic fingerprint changes for content edits but not JSON whitespace or file moves; private API can find `class.rogue`, `class-feature.rogue.sneak-attack-level-1`, `monster.aboleth`, and `monster-family.goblin`.

## Requirement 3 — preserve public-distribution restrictions

The host has a separate public-consumer API `src/RulesCore.Host/NativePublicEndpoints.cs` with fingerprint- and rights-review based distribution gating. A changed catalog fingerprint must **not** implicitly inherit public approval from the prior one. Verify `GET /api/rulesets` and `GET /api/rulesets/first-party-001/rules` return only records authorized for the new digest, and the private API continues requiring its private reference ticket. The Wiki must not be granted public distribution rights merely because a class entered `native/`.

**Acceptance criteria:** old fingerprint authorization does not unlock new definitions, and public/private API surface isolation and scoped access tests continue to pass.

## Requirement 4 — separate opt-in Warlock experiment contract

The review data is **not native**: `data/reference/experiments/warlock-modular-alpha-0.3.1.json`, ID `experiment.warlock-modular-alpha-0-3-1`. The staged `working/classes/base/warlock.json` remains an unresolved imported candidate. Neither file may be parsed by the ordinary authoritative native catalog.

Design an **explicit, access-controlled experimental content endpoint or reviewed release-attached experiment artifact**. Selection must require a distinct experiment/version identifier and explicit user/DM opt-in. Keep experiment identity, provisional status, authorizations and provenance in the returned payload. Keep its cache and response models separate from `NativeRuleEntity` or otherwise guarantee that an experimental object cannot overwrite an authoritative `class.*` ID or affect the fingerprint. Never load arbitrary paths under `working/` via API, never expose an unrestricted file endpoint, and do not implement a per-native-rule approval detector.

**Acceptance criteria:** native `GET` does not return `class.warlock` or the `experiment.*` record; a positively permitted experiment request retrieves Alpha 0.3.1 with clear status; unauthorized or unknown versions fail closed; changing experimental JSON alone cannot change the authoritative native catalog fingerprint or public-distribution eligibility.

## Deployment verification after owner approval

1. Record reviewed PR/commit and exact new native fingerprint computed by the native C# loader.
2. Build **one** commit-pinned embedded-content image; validate catalog count, no duplicate identities and readiness.
3. Confirm authorized private API returns class progression, selected feature, and representative monsters/families with the expected fingerprint.
4. Confirm Wiki renders new categories after its UI handoff is implemented.
5. Assert `working/` is never used as a runtime fallback, and public access still honors rights/fingerprint restrictions.
6. Simulate failure/rollback or conduct controlled recovery test to the **prior content image** and its corresponding original fingerprint.

These checks are separate from owner approval to **merge** and from owner approval to **deploy**.

## Condition-description update (same review branch)

continuous content now also adds human-readable summaries for Deafened, Frightened, Grappled, Poisoned and Restrained, plus stable-ID links in `rule.conditions`. This changes native catalog bytes and semantic fingerprint without changing 222 entity IDs or effect rules. The current approved-review *candidate* fingerprint is `b2af2ac5b47d133e181b82604a0619593ac5c6c67ae73da6e01f96f920acfd32` (read-only C# loader workflow run 40984999799). Do not use the earlier continuous content fingerprint from review history. Existing authenticated private APIs should return all 30 selected condition records; no condition execution engine or new fallback may be enabled by this release. Preserve separate owner approval for the actual SHA and deployment.
