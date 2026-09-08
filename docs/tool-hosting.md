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
- `/tool-host/{slug}/context` provides host context; existing authenticated APIs also expose session/campaign reads.

Because WebSocket proxying is not part of the contract, Rules Core does not use Blazor Server for its hosted UI. The initial integration target is an ES module at `/app.js`, with standard HTTP APIs behind explicit authentication contracts.

## Rules Core endpoints

Initial endpoints:

- `GET /health` - application liveness.
- `GET /ready` - PostgreSQL-aware readiness.
- `GET /app.js` - Embedded Module entry point.
- `GET /` - standalone service metadata.
- `GET /api` - API-surface metadata.

Planned endpoint families:

- `/api/rules/...` - resolved rules/content consumption.
- `/api/sources/...` - source connections/imports and source availability.
- `/api/global/rules/...` - global Rules Layer adjudication; requires Rules Lawyer authority.
- `/api/campaigns/{campaignId}/rules/...` - campaign Rules Layer; requires campaign-scoped authority.
- `/api/integration/...` - explicitly trusted Dorks & Dice service-to-service operations.

## Authentication work still required

The current Tool Host API proves authenticated identity and read-only campaign membership to the browser, but it does not yet provide the complete trusted write/authentication exchange Rules Core needs for its own APIs. That contract must be added deliberately. Rules Core must not infer identity from forwarded/browser-controlled headers.

Standalone development will use a local development identity adapter only after the production contract is defined; it will not create a second production account system.
