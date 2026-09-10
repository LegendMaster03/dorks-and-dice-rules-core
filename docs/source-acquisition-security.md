# Source acquisition security boundary

Acquisition provenance is intentionally non-authoritative. The security boundary is defined by these properties:

- Only a redeemed Tool Host identity with effective `Dev` authority in Dorks & Dice mode can reach acquisition administration endpoints.
- The target account is always the authenticated Tool Host user; acquisition APIs accept no arbitrary subject user ID.
- A user can not list or void another account's acquisition records through the current-user endpoints.
- Acquisition data is never consulted by restricted source read authorization.
- Recording, voiding, or retaining an acquisition does not mutate `user_source_grant`.
- The optional reference field is ordinary provenance text and must not contain credentials, license keys, access tokens, payment data, or other secrets.

This separation is required even if a future workflow verifies acquisition evidence. Verification state and source-content entitlement must remain explicit decisions rather than an implicit consequence of storing provenance.
