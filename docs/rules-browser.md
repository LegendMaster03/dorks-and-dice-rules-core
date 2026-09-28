# Browser-link contract

Rules Core remains authoritative for canonical rule identities and for the API
data that describes those rules. Human-readable browser pages are owned by Rules
Wiki.

API responses that expose `browserLink` continue to return the same three-part
contract:

- `toolSlug`: now `rules-wiki`;
- `toolRelativePath`: the existing stable path such as
  `/monsters/ancient-red-dragon`, `/conditions/prone`, or
  `/rules/{conceptKey}`;
- `routeIdentity`: the canonical Rules Core concept key.

Changing `toolSlug` does not change concept keys, source IDs, rule IDs, API routes,
or Rules Layer semantics. Consumers should continue treating `browserLink` as an
opaque navigation target instead of constructing `/tools/...` URLs themselves.

Legacy route parsing remains in Rules Core because it is part of the stable link
contract and regression tests. The presentation and Embedded Module route state
live in Rules Wiki.
