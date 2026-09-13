# Dorks & Dice Tool Host integration

This repository follows the Tool Host contract implemented by `dorks-and-dice-site`.

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

## Embedded route ownership

The Tool Host owns the site mount point and Rules Core owns route state beneath that mount point. Current host context exposes both values explicitly:

- `toolBasePath`, for example `/tools/rules-core`;
- `toolRoute`, for example `/monsters/ancient-red-dragon`.

Rules Core consumes `toolRoute` as application route state and uses `toolBasePath` only when constructing a browser navigation target. It does not hard-code the site's MVC route or assume the mount path will never change.

Rules Core API responses that identify browser-visible rules expose a `browserLink` containing the `rules-core` tool slug, a tool-relative path, and the stable Rules Layer route identity. Other tools should give that link target back to Tool Host/navigation code instead of independently rebuilding `/tools/rules-core/...` URLs.

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
- enabled campaign memberships and their campaign-scoped role.

The context is attached to the current request and projected into an ASP.NET `ClaimsPrincipal` for future policy use. `GET /api/integration/session` exists as an authenticated integration diagnostic and returns 401 when the request did not arrive with a successfully redeemed Tool Host ticket.

Standalone/direct requests remain anonymous when no Tool Host headers are supplied. Rules Core does not create a second production Identity store.

## Independent source authorization

A valid Dorks & Dice identity is not itself permission to read restricted source material. Rules Core stores per-user `user_source_grant` records keyed by the stable site user ID and the source package ID.

The source API applies both open-content and grant rules:

- public packages are readable anonymously;
- private packages are readable only when the redeemed user has an explicit Rules Core source grant;
- missing and inaccessible entities both return not-found behavior;
- a source grant does not create Rules Lawyer or campaign authority.

The same check applies to resolved-rule provenance, semantic source comparisons, and the pinned global baseline shown in campaign scope. Campaign authority can not be used to disclose a restricted source document.

## Global rules authority

Global Rules Layer mutation is authorized from the redeemed host context. The request must be in `dorks-and-dice` mode and the user must have the effective global `Rules Lawyer` role. Rules Core rechecks this on every mutation endpoint rather than relying on frontend visibility.

## Campaign rules authority

Campaign Rules Layer reads and writes use campaign context supplied by the host. Rules Core does not maintain a duplicate campaign-membership database.

- Campaign reads require `dorks-and-dice` mode and explicit membership in the requested enabled campaign.
- An authenticated nonmember receives not-found behavior.
- The campaign-scoped `DM` role represents the campaign owner and authorizes campaign Rules Layer mutation.
- A campaign `Player` may browse published campaign rules but can not adjudicate or publish campaign rules.
- Campaign authority never implies global Rules Lawyer authority.
- Source-content grants remain independent from campaign authority.

In this architecture, “campaign owner” and “campaign DM” are the same authority concept. Rules Core therefore consumes the existing Tool Host `DM` role directly rather than defining a second ownership or adjudication capability.

## Scope contract

Rules Core models adjudication scope explicitly as either global or campaign-with-ID. The model is extensible rather than being encoded as two unrelated UI tabs.

`GET /api/workspace/scopes` reports which scopes the current identity may browse and adjudicate. The persistent workspace scope control is a convenience projection of this authority; it is not an authorization boundary.

Requests that perform scoped semantic comparison include the requested scope in the request body, and the backend independently verifies it. Existing global and campaign mutation APIs also carry scope in their endpoint identity (`/api/global/...` versus `/api/campaigns/{campaignId}/...`) and reauthorize on every request. No mutation determines its destination solely from ambient client state.

## Rules Core endpoints

Relevant browser/workspace endpoints include:

- `GET /api/rules` - published global catalog with stable browser link targets.
- `GET /api/rules/{conceptKey}` - published global resolved rule with browser link target.
- `GET /api/campaigns/{campaignId}/rules` - published effective campaign catalog.
- `GET /api/campaigns/{campaignId}/rules/{conceptKey}` - published effective campaign rule.
- `GET /api/campaigns/{campaignId}/rules/{conceptKey}/global-baseline` - exact published global baseline pinned by that campaign publication, subject to independent source access.
- `GET /api/workspace/scopes` - browse/adjudication scopes available to the current identity.
- `POST /api/workspace/comparison` - scope-authorized semantic comparison of two accessible source revisions bound to one concept.

Existing global and campaign authoring/publication endpoints remain unchanged in their persistence semantics. Publication remains explicit and append-only.
