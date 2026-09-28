# Rules Wiki delegation boundary

Normal authenticated browser traffic follows this path:

`browser -> Site Tool Host -> rules-wiki -> Site delegation endpoint -> rules-core`

The Site authenticates the browser request to Rules Wiki and supplies a short-lived,
server-only delegation capability when the `rules-wiki` Tool registration is
allowlisted to target `rules-core`. Rules Wiki never returns that capability to the
browser. Its backend uses the capability only to call the Site delegation endpoint.

The Site then issues the normal target-scoped authentication ticket for Rules Core.
Rules Core redeems that ticket through its existing introspection middleware, so the
established stable user ID, global roles, campaign roles, source grants, Rules
Lawyer authority, campaign DM authority, and restricted-source filtering remain
authoritative here.

Rules Wiki has no source-grant database and no parallel authorization model.
Rules Core continues using its existing external PostgreSQL database.

## Anonymous access compatibility

The current Site Tool Host can proxy an anonymous browser directly to a Tool whose
registration allows anonymous use, but its Tool-to-Tool delegation capability is
issued only after successful authenticated introspection. Therefore an anonymous
Rules Wiki request can not currently delegate to Rules Core through the required
server-to-server boundary.

If anonymous Rules Library access is to remain supported, Site must add an anonymous
first-party delegation path that carries no user identity or grants while retaining
the normal target allowlist, upstream policy, and reserved-header protections. The
Wiki must not compensate by inventing its own identity or calling Rules Core outside
the Site Tool Host boundary.

## Deployment gate

The repository split can be developed and validated before deployment, but merge and
production rollout require Site support for a headless/non-navigable `rules-core`
service registration. That registration must remain available for upstream routing,
health checks, authentication/introspection, and delegation while exposing no normal
Tool navigation/page. The `rules-wiki` registration is the public Embedded Module v2
Tool with delegation target `rules-core`.

Deployment must also preserve historical `/tools/rules-core/...` browser hrefs by
redirecting or aliasing them to the equivalent `/tools/rules-wiki/...` path, and must
resolve the anonymous-delegation gap above before anonymous access is enabled for
Rules Wiki.
