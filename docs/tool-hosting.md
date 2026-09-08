# Dorks & Dice Tool Host integration

This repository follows the Tool hosting contract implemented by `dorks-and-dice-site`.

## Existing host behavior

The main host registers already-running tools at runtime and can expose them as either an Embedded Module or a Proxied Application. It owns authentication, authorization, mode resolution, registration, gateway routing, and shared campaign/account contracts.

Important transport constraints from the current host:

- Browser Cookie and Authorization headers are stripped before proxying.
- Tool-owned cookies/sessions are unsupported.
- Arbitrary identity headers must not be trusted.
- WebSocket upgrades are unsupported.
- Redirect following/rewriting is unsupported for proxied applications.
- Embedded modules run on the main site origin and can call the host's authenticated Tool Host API as the signed-in user.
- `/tool-host/{slug}/context` provides host context; authenticated host APIs expose session/campaign reads.

Because WebSocket proxying is not part of the contract, Rules Core does not use Blazor Server for its hosted UI. The integration target is an ES module at `/app.js`, with standard HTTP APIs behind explicit authentication contracts.

## Hosted backend authentication

Authenticated Embedded Module API traffic goes through the main site's gateway:

```text
/tool-host/rules-core/api/upstream/{tool-backend-path}
```

The host authenticates the browser itself and strips browser-controlled authentication/forwarding headers. It then injects two reserved headers into the private Rules Core request:

```text
X-Dorks-Tool-Auth-Ticket
X-Dorks-Tool-Auth-Introspection-Path
```

Rules Core does not treat either value as identity. `HostedToolAuthenticationMiddleware` redeems the one-time ticket by POSTing it to the fixed Rules Core introspection path on the configured Dorks & Dice host. The production container reaches the site through the shared private Docker network using `ToolHost__BaseUrl=http://dorks-and-dice-site:8080`.

The introspection client accepts only `/tool-host/rules-core/api/introspect`; the supplied path can not redirect the server to an arbitrary target. Redirect following and cookies are disabled on the introspection HTTP client. A successful contract-v1 response must identify the `rules-core` Tool and include a stable user ID.

The redeemed context contains:

- stable user ID and display name;
- active site mode;
- effective global roles;
- enabled campaign memberships and their campaign-scoped roles.

The context is attached to the current request and projected into an ASP.NET `ClaimsPrincipal` for future policy use. `GET /api/integration/session` exists as an authenticated integration diagnostic and returns 401 when the request did not arrive with a successfully redeemed Tool Host ticket.

Standalone/direct requests remain anonymous when no Tool Host headers are supplied. Rules Core does not create a second production Identity store.

## Independent source authorization

A valid Dorks & Dice identity is not itself permission to read restricted source material. Rules Core stores per-user `user_source_grant` records keyed by the stable site user ID and the source package ID.

The source API applies both open-content and grant rules:

- public packages are readable anonymously;
- private packages are readable only when the redeemed user has an explicit Rules Core source grant;
- missing and inaccessible entities both return not-found behavior;
- a `Rules Lawyer` global role does not create a source grant;
- a source grant does not create Rules Lawyer or campaign authority.

Grant mutation is currently an internal application service. No public grant-management or source-import write endpoint is exposed yet.

## Rules Core endpoints

Current endpoints include:

- `GET /health` - application liveness.
- `GET /ready` - PostgreSQL-aware readiness.
- `GET /app.js` - Embedded Module entry point.
- `GET /` - standalone service metadata.
- `GET /api` - API-surface metadata.
- `GET /api/integration/session` - redeemed hosted identity context; requires a valid Tool Host ticket.
- `GET /api/sources` - public packages plus private packages granted to the hosted identity.
- `GET /api/sources/entities/{entityId}` - latest accessible source revision with provenance.

Planned endpoint families:

- `/api/rules/...` - resolved rules/content consumption.
- `/api/global/rules/...` - global Rules Layer adjudication; requires Dorks-mode Rules Lawyer authority.
- `/api/campaigns/{campaignId}/rules/...` - campaign Rules Layer; requires campaign-scoped authority.
- `/api/integration/...` - explicitly trusted Dorks & Dice service-to-service operations.
