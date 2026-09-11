# Source acquisition provenance

Rules Core can record how the current authenticated account obtained a source package. Acquisition records are historical provenance only. They do not prove entitlement and they never grant, revoke, or otherwise alter restricted source-content access.

## Model

The first acquisition slice uses two append-only PostgreSQL records:

- `source_acquisition` identifies the source package, current Dorks & Dice user, acquisition kind, optional reference, optional acquisition time, and record provenance.
- `source_acquisition_revocation` marks one acquisition record void without deleting or rewriting the original record.

One acquisition can have at most one revocation. Package deletion cascades to acquisition history because a source acquisition has no meaning after its package identity is removed.

Supported acquisition kinds are:

- `physical-copy`
- `digital-copy`
- `subscription`
- `licensed-access`
- `other`

The optional reference is capped at 500 characters. It is intended for ordinary non-secret provenance such as a provider, order label, library reference, or descriptive note. License keys, passwords, payment-card information, access tokens, and other secrets must not be stored there.

## Authorization boundary

The current implementation exposes acquisition management only to an effective `Dev` identity while the Tool Host reports Dorks & Dice mode. The user ID is always taken from the redeemed Tool Host context. The browser can not choose another target user.

A Dev therefore can list, record, and void only the signed-in account's acquisition records. An acquisition belonging to another account behaves as not found when addressed through the current-user void endpoint.

This is intentionally narrower than a future administrator-facing acquisition workflow. Cross-user recording or correction would require explicit recipient resolution and audit semantics rather than trusting a browser-provided user ID.

## Acquisition is not entitlement

`source_acquisition` and `user_source_grant` answer different questions:

- acquisition: how did this account say it obtained the package?
- grant: may this account read restricted source content through Rules Core?

No operation bridges those tables automatically.

Recording a restricted physical or digital copy leaves the package unreadable until an explicit source grant exists. Voiding an acquisition does not revoke a source grant. Granting or revoking source access does not change acquisition history.

This is deliberate. Rules Core does not currently verify ownership, subscription state, receipts, licenses, or external-provider entitlements. A self-recorded acquisition is useful provenance but must not silently become an authorization claim.

## API

`GET /api/source-admin/acquisitions` returns the current account's acquisition history, including voided records.

`POST /api/source-admin/packages/{sourcePackageId}/current-user-acquisitions` appends a record. The request contains an acquisition kind plus optional reference and acquisition time.

`POST /api/source-admin/acquisitions/{sourceAcquisitionId}/void` appends the void marker. A repeated void request is idempotent and returns the already-voided record without changing its original void reason or time.

All responses are `no-store`. Anonymous calls are unauthorized. Authenticated non-Dev or wrong-mode calls are forbidden.

## Schema initialization

The acquisition service currently creates its two tables and indexes through idempotent PostgreSQL DDL before acquisition operations. This allows the slice to coexist with the existing central Source/Rules schema initializer without database migrations. A later persistence-model consolidation can move these records into the normal EF model without changing their external semantics.

## Embedded UI

The Source Administration view contains a `Current account acquisition history` card. It allows the current Dev to select an imported package, record the acquisition kind and optional provenance, review prior records, and void an incorrect record.

The acquisition controls are presented separately from `Current account source access`. The UI explicitly warns that acquisition history does not grant or revoke content access and that secrets must not be entered into the reference field.
