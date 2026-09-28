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

## Historical browser links

Some downstream persisted state can contain a previously materialized absolute
browser href such as `/tools/rules-core/conditions/exhaustion` instead of the
structured `browserLink` contract. New responses do not emit that destination,
but deployment must preserve those historical links without requiring downstream
Tools to understand the split.

Before the split is deployed, the Site must provide a legacy browser-route alias
or redirect from `/tools/rules-core/{**toolRelativePath}` to
`/tools/rules-wiki/{**toolRelativePath}`, preserving the trailing path and query
string. This compatibility route is a Site deployment concern; it must not turn
the headless Rules Core service registration back into a navigable UI Tool.
