# Rules Core Code Rewrite handoff — continuous content releases and experiment separation

**Owning project:** Rules Core Code Rewrite (not Rules Content)  
**Base content PR:** `content/coherent-rule-promotion-wave-1` targeting latest `main`  
**Ruleset:** `first-party-001`  
**Status:** Native release plumbing updated on the review branch; no merge or deployment authorized.

## Release workflow — implemented pending reviewed merge

The old implementation pinned the original 35-record fingerprint in `tools/deploy_native_core.py`. Content changes made that value stale. **The release no longer maintains fingerprint constants.**

- `.github/workflows/deploy.yml` is now **manual** (`workflow_dispatch`), taking the exact 40-character `release_sha` and `review_reference`. A normal merge does not trigger deployment.
- The workflow requires `main` at exactly that SHA and an explicitly successful **main-push** CI run for that same commit, rather than treating a green PR run as sufficient.
- It calculates the semantic fingerprint using `NativeCatalogLoader` against that checked-out reviewed SHA, injects the result into the deployment environment, and requires a full 64-character lowercase digest.
- `tools/deploy_native_core.py` verifies both public and private ingress containers have the reviewed fingerprint; a missing or different value fails closed. A built image is accepted only with a matching reviewed commit label and an unprivileged runtime user.
- Existing network isolation, no-host-port/no-volume rules, public-distribution denial by default, readiness probes and failure withdrawal remain unchanged.
- The prior release image/tag is not deleted. Existing withdrawal stops the failed replacement; **automatic restoration of the previous image is not implemented**. Operator recovery must be tested for a production release.

This mechanism makes the *approved commit* the release selector and the derived fingerprint the integrity check. A changed native JSON document automatically produces a changed expected digest. The deployment step cannot approve an arbitrary image by merely reading its own logs.

**Required operational review before first use:** Confirm the self-hosted deploy runner can reach GitHub Actions API with `actions: read`, install/use .NET 10 via setup-dotnet, resolve `main` with authenticated checkout, and access the existing protected Docker host networks. A dry-run/host preflight is not evidence that live Wiki authentication or full rollback was validated.

## Native content tests — completed

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

## Condition descriptions in the current catalog

The selected catalog also adds human-readable summaries for Deafened, Frightened, Grappled, Poisoned and Restrained, plus stable-ID links in `rule.conditions`. This changes native catalog bytes and semantic fingerprint without changing 222 entity IDs or effect rules. The current approved-review *candidate* fingerprint is `b2af2ac5b47d133e181b82604a0619593ac5c6c67ae73da6e01f96f920acfd32` (read-only C# loader workflow run 40984999799). Do not use the previous catalog fingerprint from review history. Existing authenticated private APIs should return all 30 selected condition records; no condition execution engine or new fallback may be enabled by this release. Preserve separate owner approval for the actual SHA and deployment.
