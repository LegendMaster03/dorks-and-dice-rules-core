# Phase 3B — Native public consumer API contract (v1)

Status: **development only** on `feature/rules-core-phase3b-public-native-api`. No deployment, production routing change, tester access, or ruleset distribution approval is granted by this document. Core Phase 3A integration base: `4bf95dce56c98dc9975aef41696d55dbfb72dae0`. The coordinated, unreleased Wiki Phase 3A integration reference is `ab56a5659dbb533097e094fd71f02245a983def3`.

## Transport boundary and authority

The following GET routes exist **only** with `RulesCore:ApiSurface=PublicOnly`; they are absent on `PrivateOnly` (404). `PrivateOnly` continues to serve authenticated `/api/private/rulesets` as Phase 3A defined, and public traffic must not traverse the private Wiki/Core pair network. No compatibility endpoints or legacy `/api/rules` are created. The API is public/anonymous only for **explicitly distribution-approved package snapshots**. It never evaluates mechanics, performs edits or resolves campaign-specific rules.

The only authoritative runtime content root is `data/rulesets/<rulesetId>/native/`. RulesCore.Content owns immutable validated catalogs and indexes. Each handler captures a single `NativeCatalogStore.Snapshot`. Catalog definition, count, facets and fingerprint are derived from that exact snapshot; request handling does not reload JSON or construct indexes. The current one-loaded-ruleset host can gain additional public rulesets when multi-ruleset hosting is designed separately; the public contract already uses stable ruleset-scoped paths and IDs.

## Public distribution gate — fail closed

Native directory promotion approves **gameplay authority**, not **anonymous distribution**. The public ingress requires ALL of these *package-level* configuration settings for the requested ruleset:

```text
RulesCore:PublicDistribution:Packages:<rulesetId>:Enabled=true
RulesCore:PublicDistribution:Packages:<rulesetId>:ApprovedFingerprint=<64-character SHA-256 catalog fingerprint>
RulesCore:PublicDistribution:Packages:<rulesetId>:RightsReviewReference=<recorded ownership/license review>
RulesCore:PublicDistribution:Packages:<rulesetId>:AttributionStatement=<reviewed attribution obligations>
```

No such settings ship in application defaults or Compose, and a partial/invalid/mismatched policy publishes **nothing**. The review must establish ownership or distribution rights for **every definition and embedded asset in the complete native snapshot**, check underlying SRD/third-party licenses and attribution obligations, verify there are no secrets, source/candidate excerpts or internal references in JSON definitions, and record the actual attribution requirements. A free-form review reference is an operator attestation, not automated proof of copyright clearance; access controls do not substitute for review. Rights review for the current `first-party-001` package is **not evidenced** here, so it remains publicly unavailable. In tests only, the entire synthetic fixture is explicitly approved with its computed fingerprint and fixture review statement.

Any native definition change modifies the canonical catalog fingerprint and **automatically revokes** public visibility until the package is reviewed and the approved fingerprint is updated. There is no per-rule approval detector, individual-rule allowlist, implied approval from `native/`, or fallback into `working/`, `reference/`, or `conversions/`. Request/response DTOs omit `NativeRuleEntity.SourcePath`; nested definition contents remain part of the approved native content and must be reviewed before approval.

Unknown, non-distributable or fingerprint-mismatched rulesets are indistinguishable from an unknown ruleset on search/detail (404); public listing omits them entirely, including counts, fingerprints and facets. Routes do not accept Site tickets as public access grants; such headers have no effect. Public responses use `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`. No file paths, rights-review records, internal credential details, or private metadata appear in response envelopes.

## Version 1 routes and DTO examples

All successful responses are JSON with camel-case properties. Field names and numeric pagination values are stable in v1. IDs are **native entity IDs**; `conceptKey`, historical source revisions and campaign-effective projections are not supported.

### GET /api/rulesets

```json
{
  "rulesets": [
    {
      "id": "first-party-001",
      "entityCount": 35,
      "fingerprint": "<sha256-of-exact-approved-native-snapshot>",
      "capabilities": { "read": true, "write": false }
    }
  ]
}
```

This is a *shape example for a future approved package*, **not** an assertion that `first-party-001` is currently publicly distributable. Without approval the response is `{"rulesets":[]}` (200) when a valid catalog exists. With no usable catalog, return 503.

### GET /api/rulesets/first-party-001/rules?q=blind&category=condition&limit=10&offset=0

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<same-approved-snapshot-fingerprint>",
  "total": 1,
  "offset": 0,
  "limit": 10,
  "categoryFacets": [
    { "kind": "condition", "count": 29 },
    { "kind": "leveled-condition", "count": 1 },
    { "kind": "rule", "count": 4 },
    { "kind": "ruleChapter", "count": 1 }
  ],
  "rules": [
    { "id": "condition.blinded", "name": "Blinded", "kind": "condition" }
  ]
}
```

The facet counts in this illustrative 35-rule package are **unfiltered**, and `total` respects search/category filters. Matching native ID or name uses case-insensitive substring search. Entries are always sorted by ordinal native ID; category facets are ordinal kind-sorted. `limit` defaults to 50 and must be 1–100; `offset` defaults to 0 and must be 0–1,000,000. Query string length is at most 200; category matches `[A-Za-z][A-Za-z0-9-]{0,79}` and a valid but unknown category produces an empty page. Unsupported or duplicate parameters and malformed numbers yield 400. Empty pages retain the total and approved package facets.

### GET /api/rulesets/first-party-001/rules/condition.blinded

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<same-approved-snapshot-fingerprint>",
  "rule": {
    "id": "condition.blinded",
    "name": "Blinded",
    "kind": "condition",
    "definition": {
      "id": "condition.blinded",
      "name": "Blinded",
      "kind": "condition"
    }
  }
}
```

The `definition` above is an **abbreviated shape illustration**. The actual response includes the entire reviewed, approved native JSON object for that rule, with no synthesized legacy fields. Ruleset IDs use lowercase alphanumeric/hyphen segments (maximum 80 characters). Rule IDs use lowercase dot-separated native segments (maximum 160 characters). Invalid IDs yield 400; valid but absent IDs yield 404. Requests for nonpublic packages yield 404 without disclosing whether the rule exists.

### Error and method behavior

HTTP 400: `{"error":"invalid_query"}` or `{"error":"invalid_id"}`. HTTP 404: `{"error":"ruleset_not_found"}`, `{"error":"rule_not_found"}`, or `{"error":"api_not_found"}` for unmatched routes. HTTP 503: `{"error":"native_catalog_unavailable"}`. Only the three named GET routes are supported; no writes, refresh, generic HTTP proxy or adapter. `/health` remains independent of catalog availability and `/ready` reports validated catalog readiness (not public-distribution approval).

## Consumer migration impact and next gate

**Character Sheet:** Replace existing `/api/rules` and campaign resolution dependencies with native ruleset-scoped list/detail reads; update rule IDs and projections. Class-specific resource and spell-slot mechanics are not supplied by this read-only API and require separately approved mechanics contracts.

**Block Initiative:** Resolve condition/statblock references from native IDs and definitions. Current legacy combat/kaiju evaluation and dynamic mechanics are not reproduced here; migrate those consumers in separate phases, without an old/new adapter.

**Hex Crawl and other tools:** Identify resolved-rule/campaign behavior that is not merely read-only catalog access; defer any required gameplay computation or campaign overrides to separate API design.

**Rules Wiki:** Remains on the Phase 3A private Site-ticketed routes, not this public API. Do not change its named-operation whitelist or deploy the Wiki.

**Phase 4/pre-deployment:** Verify live Site-issued `rules-wiki` to `rules-core` ticket redemption, correct source and target, revoked/wrong tickets and site-mode behavior; browser acceptance and private-pair/public-network isolation; record Core/Wiki SHA pairing and rollback order; conduct first-party licensing/attribution/content review before any anonymous definition distribution. Simulated-ticket CI and public API tests **do not** establish live private-tunnel acceptance. No public or tester exposure and no production deployment until separately approved.

## Verification

The Phase 3B GitHub Actions workflow runs .NET build and HTTP integration tests, all native/SRD Python validators and security tests, native authority audits, Docker image build, and PublicOnly/PrivateOnly smoke checks. Policy-enabled HTTP tests use a synthetic fixture's exact reviewed fingerprint; production package policy remains disabled.
