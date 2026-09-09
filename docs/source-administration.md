# Source administration

Source ingestion is a control-plane operation and is deliberately separate from both Rules Layer authoring and source-content entitlements.

## Authority

`POST /api/source-admin/import` requires a redeemed Dorks & Dice Tool Host identity with:

- active site mode `dorks-and-dice`; and
- effective global role `Dev`.

The main site remains the authority for that role. Owner inherits the site's top-level global roles and therefore receives effective Dev authority through the existing host role hierarchy.

`Rules Lawyer` alone is not sufficient. Rules Lawyer authority governs rule concepts, bindings, decisions, and publication; it does not make a user a Source Layer administrator.

## Import contract

The endpoint accepts the existing `Import5eToolsDocumentRequest` contract:

- package key, display name, provider, license, and public/restricted flag;
- work key and display name;
- edition key and display name;
- a 5e.tools-shaped JSON document.

Package, work, and edition identity metadata is immutable under its key. Conflicting metadata is rejected rather than silently changing provenance.

Import preserves complete entity objects in immutable Source Layer revisions. Canonical SHA-256 fingerprints make reimporting semantically identical JSON idempotent; changed source content creates the next revision instead of overwriting the previous one.

## Access isolation

Import authority and source-read authority are independent.

A Dev may import a restricted package, but the import does not create a `user_source_grant` for the Dev or for anyone else. The restricted package therefore remains unavailable through normal source search/read APIs until an explicit source grant exists. Public imports are available according to the normal public Source Layer rules.

This property is covered by PostgreSQL integration tests: Dev import succeeds, Rules Lawyer-only and wrong-mode requests fail, duplicate import is idempotent, and importing a restricted package does not grant the importing Dev read access.

## Embedded UI

Users with effective Dev authority in Dorks & Dice mode receive a `Source Administration` view in the embedded Rules Core module. It exposes package/work/edition metadata, public/restricted selection, and raw 5e.tools-shaped JSON ingestion. The browser performs basic JSON syntax validation, but the backend importer remains authoritative for schema/provenance validation.

Large imports are processed normally, while the result UI renders only the first 100 entity results to avoid creating an unnecessarily large browser DOM.

## Deliberate omissions

This slice does not expose source-grant mutation or acquisition tracking. Those are entitlement operations and should not be implicitly coupled to source ingestion. It also does not automatically create Rules Layer concepts from imported source entities; normalization remains an explicit Rules Layer operation.
