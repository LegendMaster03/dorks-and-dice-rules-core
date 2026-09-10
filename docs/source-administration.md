# Source administration

Source ingestion, source-access administration, and acquisition provenance are control-plane operations. They are deliberately separate from Rules Layer authoring, and source-content access remains an explicit entitlement rather than an implication of Dev or Rules Lawyer authority or of an acquisition record.

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

## Acquisition provenance

A Dev can record how the current authenticated account obtained a source package without changing that account's source-content grant. This is deliberately historical/provenance data rather than authorization evidence.

Current endpoints are:

- `GET /api/source-admin/acquisitions` - lists only the signed-in account's acquisition records.
- `POST /api/source-admin/packages/{sourcePackageId}/current-user-acquisitions` - appends an acquisition record for the signed-in account.
- `POST /api/source-admin/acquisitions/{sourceAcquisitionId}/void` - appends a revocation/void marker to one of the signed-in account's records.

Supported acquisition kinds are `physical-copy`, `digital-copy`, `subscription`, `licensed-access`, and `other`. A record can also contain an optional non-secret reference and acquisition time. The reference field is for ordinary provenance such as a provider, order label, library reference, or similar note; passwords, license keys, payment information, and other secrets must not be stored there.

Acquisition records are append-only. A mistaken or superseded record is not deleted or rewritten; a separate one-to-one revocation record marks it void and can retain an optional reason. Repeating a void operation is idempotent and preserves the original void provenance.

Recording or voiding an acquisition never creates, deletes, or changes `user_source_grant`. Likewise, granting or revoking source access does not create, delete, or reinterpret acquisition history. See `docs/source-acquisitions.md` for the detailed model and invariants.

## Access isolation

Import authority, acquisition provenance, and source-read authority remain independent.

A Dev may import a restricted package, but the import does not create a `user_source_grant` for the Dev or for anyone else. The restricted package remains unavailable through normal source search/read APIs until the Dev separately chooses to grant the current account. Revoking that grant immediately returns the current account to normal restricted-source hiding behavior.

The self-grant control is therefore an explicit entitlement operation, not an automatic consequence of Dev authority or acquisition provenance. A Dev who never grants the current account still can not read restricted source content even after recording that the account owns or otherwise acquired the package. Likewise, a source grant does not grant Dev, Rules Lawyer, or campaign DM authority.

PostgreSQL integration tests cover the boundary: anonymous, Rules Lawyer-only, and wrong-mode requests can not use source administration; a Dev can record only the signed-in account's acquisition provenance; another Dev can not read or void that record; acquisition recording does not unlock a restricted entity; explicit grant does unlock it; voiding the acquisition does not revoke that grant; and only explicit grant revocation hides the entity again.

## Embedded UI

Users with effective Dev authority in Dorks & Dice mode receive a `Source Administration` view in the embedded Rules Core module. The ingestion section exposes package/work/edition metadata, public/restricted selection, and raw 5e.tools-shaped JSON ingestion. The browser performs basic JSON syntax validation, but the backend importer remains authoritative for schema/provenance validation.

A separate `Current account source access` card lists package metadata and allows explicit grant/revoke only for the signed-in account. Public packages are shown as available without a grant. Restricted packages show whether the current account is granted and expose `Grant my account` or `Revoke my account` accordingly.

The `Current account acquisition history` card records package, acquisition type, optional reference, and optional acquisition time. Existing records remain visible after being voided. The UI repeatedly states that acquisition provenance does not grant or revoke source-content access so it can not be mistaken for the entitlement control.

Large imports are processed normally, while the result UI renders only the first 100 entity results to avoid creating an unnecessarily large browser DOM.

## Deliberate omissions

This slice does not expose arbitrary other-user source-grant or acquisition mutation. Supporting another account requires an explicit administrative workflow with recipient resolution and appropriate audit semantics; it is not inferred from the current Dev's authority.

Acquisition records are self-recorded provenance. Rules Core does not currently verify receipts, subscriptions, licenses, ownership, or legal entitlement against an external provider, and acquisition records are intentionally not used as an authorization decision.

Source administration also does not automatically create Rules Layer concepts from imported source entities. Normalization and adjudication remain explicit Rules Layer operations.
