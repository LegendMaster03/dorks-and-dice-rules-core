# Source acquisition API notes

Source acquisition endpoints are current-account, Dev-only Source Administration operations. They require a redeemed Tool Host identity in `dorks-and-dice` mode.

`GET /api/source-admin/acquisitions` returns the current account's acquisition history, including voided records.

`POST /api/source-admin/packages/{sourcePackageId}/current-user-acquisitions` appends a provenance record. The request accepts `acquisitionKind`, optional `reference`, and optional `acquiredAt`.

`POST /api/source-admin/acquisitions/{sourceAcquisitionId}/void` marks one current-account record void by appending separate revocation provenance. The request accepts an optional `reason`.

These endpoints never create or remove `user_source_grant`. Acquisition history is not consulted by source read authorization.
