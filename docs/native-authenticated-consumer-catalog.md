# Live native catalogs for authenticated tools

The authoritative native ruleset is continuously edited and is served to authorized Site users without a per-content-update approval.

## Authentication and access

The browser calls the existing Site registration gateway at:

`/tool-host/registrations/rules-core/api/upstream/api/consumer/v1/rulesets`

The Site gateway already issues a user-scoped, short-lived target ticket for authenticated users who can access the Rules Core service in the Dorks & Dice mode. Rules Core redeems that ticket through the existing Site introspection contract. Anonymous requests, mismatched Site modes, missing users, and tickets issued for a different purpose are rejected. There is no bypass to the private Wiki ingress and no new frontend secret. Access is governed by the existing Site account and Tool visibility controls.

`GET /api/consumer/v1/rulesets`, `GET /api/consumer/v1/rulesets/{id}/rules`, and `GET /api/consumer/v1/rulesets/{id}/rules/{ruleId}` use the same search, validation, pagination, and data DTOs as the native read-only public catalog, but require the authenticated Site ticket instead of anonymous distribution authorization. They serve only the authoritative `native/` snapshot; `working/`, `reference/`, and `conversions/` are not loaded.

The same package-level reference policy applies to the separate private Wiki reference endpoints after their own Site ticket validation. First-party access is preserved. `RulesCore:AuthenticatedReferenceAccess:AdditionalRulesetsCsv` explicitly grants additional package IDs to authenticated reference users, independently of catalog registration and fingerprint changes. The shipped Compose configuration sets `RULES_CORE_AUTHENTICATED_REFERENCE_RULESETS` to `dnd-5e,dnd-5-5e,fate-condensed`; set it to an empty string to retain first-party-only grants. Unknown, unregistered or unavailable packages are not served. Future registered packages without a reference grant remain denied unless separately approved for public distribution.

The public anonymous `/api/rulesets` endpoints retain the independent explicit distribution gate. Publicly approved packages remain readable through authenticated routes. Internal reference access does not imply public API redistribution approval, mechanics authorization or account-specific entitlements.

## Fingerprints

The catalog fingerprint remains a version identifier. It is returned in responses to support caching, diagnostics, encounter provenance, and explicit version-dependent mechanics calculations. It does **not** authorize access. An ordinary edit to native JSON changes the fingerprint but does not disconnect authorized tools.

Clients loading ordinary monster and condition references should accept the current definition by native ID rather than rejecting a recently updated fingerprint. Operations that require repeatable, exact-snapshot calculations may still use version checks, but those checks must not be confused with permissions.

## Scope and rollout

This change makes native monster and condition reference lookup work for authenticated Block Initiative users through an existing Site route. It does not authorize anonymous publication or change Site ticket authentication, mechanics authorization, or campaign writes.

Rollout order: Rules Core first, then Block Initiative. Verify a signed-in tool gateway request, an anonymous denied request, a current native monster and a condition, and a lookup after a content-only update. No Site database migration is required.
