# Source administration

Source ingestion and source-access administration are control-plane operations. They are deliberately separate from Rules Layer authoring, and source-content access remains an explicit entitlement rather than an implication of Dev or Rules Lawyer authority.

## Authority

Source administration endpoints require a redeemed Dorks & Dice Tool Host identity with:

- active site mode `dorks-and-dice`; and
- effective global role `Dev`.

The main site remains the authority for that role. Owner inherits the site's top-level global roles and therefore receives effective Dev authority through the existing host role hierarchy.

`Rules Lawyer` alone is not sufficient. Rules Lawyer authority governs rule concepts, bindings, decisions, and publication; it does not make a user a Source Layer administrator and it does not grant restricted source-content access.

## Import contract

`POST /api/source-admin/import` accepts the existing `Import5eToolsDocumentRequest` contract:

- package key, display name, provider, license, and public/restricted flag;
- work key and display name;
- edition key and display name;
- a 5e.tools-shaped JSON document.

Package, work, and edition identity metadata is immutable under its key. Conflicting metadata is rejected rather than silently changing provenance.

Import preserves complete entity objects in immutable Source Layer revisions. Canonical SHA-256 fingerprints make reimporting semantically identical JSON idempotent; changed source content creates the next revision instead of overwriting the previous one.

## Source-access administration

A Dev can inspect Source Layer package metadata through `GET /api/source-admin/packages`. This control-plane catalog includes public and restricted packages, their provenance metadata, and whether the current authenticated account has an explicit grant. It does not return source entity payloads or raw source documents.

Restricted source access for the current authenticated account can be changed explicitly with:

- `POST /api/source-admin/packages/{sourcePackageId}/current-user-grant`
- `DELETE /api/source-admin/packages/{sourcePackageId}/current-user-grant`

The target user ID is never accepted from the browser. Rules Core always derives it from the redeemed Tool Host identity, so this first grant-management slice can not be used to mutate another account's entitlement.

Grant and revoke operations are idempotent. Granting an already-granted package or revoking an already-absent grant succeeds without creating duplicate state. Public packages reject grant mutation because public content does not require a per-user entitlement.

## Access isolation

Import authority and source-read authority remain independent.

A Dev may import a restricted package, but the import does not create a `user_source_grant` for the Dev or for anyone else. The restricted package remains unavailable through normal source search/read APIs until the Dev separately chooses to grant the current account. Revoking that grant immediately returns the current account to normal restricted-source hiding behavior.

The self-grant control is therefore an explicit entitlement operation, not an automatic consequence of Dev authority. A Dev who never grants the current account still can not read restricted source content. Likewise, a source grant does not grant Dev, Rules Lawyer, or campaign DM authority.

PostgreSQL integration tests cover the boundary: anonymous, Rules Lawyer-only, and wrong-mode requests can not use the administration catalog; a Dev can see package metadata but can not read a restricted entity before granting the current account; explicit grant unlocks normal source reads; repeat grant is idempotent; public grant mutation is rejected; explicit revoke hides the restricted entity again; repeat revoke is idempotent.

## Embedded UI

Users with effective Dev authority in Dorks & Dice mode receive a `Source Administration` view in the embedded Rules Core module. The ingestion section exposes package/work/edition metadata, public/restricted selection, and raw 5e.tools-shaped JSON ingestion. The browser performs basic JSON syntax validation, but the backend importer remains authoritative for schema/provenance validation.

A separate `Current account source access` card lists package metadata and allows explicit grant/revoke only for the signed-in account. Public packages are shown as available without a grant. Restricted packages show whether the current account is granted and expose `Grant my account` or `Revoke my account` accordingly.

Large imports are processed normally, while the result UI renders only the first 100 entity results to avoid creating an unnecessarily large browser DOM.

## Deliberate omissions

This slice does not expose arbitrary other-user source-grant mutation or acquisition tracking. Supporting another account's entitlement requires an explicit administrative workflow with recipient resolution and appropriate audit semantics; it is not inferred from the current Dev's authority.

Source administration also does not automatically create Rules Layer concepts from imported source entities. Normalization and adjudication remain explicit Rules Layer operations.
