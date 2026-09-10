# Source acquisition review checklist

Before merging source acquisition provenance, verify the following invariants remain true:

- Acquisition endpoints require a redeemed Dorks & Dice identity with effective `Dev` authority.
- The subject user ID always comes from the redeemed Tool Host context rather than browser input.
- Acquisition history is scoped to the current authenticated account.
- Recording an acquisition does not create a `user_source_grant`.
- Voiding an acquisition does not delete the original record and does not revoke a `user_source_grant`.
- Source-grant changes do not create, void, or reinterpret acquisition history.
- Another account's acquisition behaves as not found through current-user mutation endpoints.
- Optional acquisition references are length-limited and documented as unsuitable for secrets, credentials, license keys, or payment data.
- Integration tests cover authorization, account isolation, grant independence, append-only void semantics, and idempotent repeated voiding.
- Browser modules and the production container build pass validation.
