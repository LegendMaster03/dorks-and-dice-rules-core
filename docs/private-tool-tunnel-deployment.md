# Private Rules Core tunnel — native deployment

Rules Wiki reaches Rules Core through a separately authorized, private first-party Tool tunnel. This file describes the **current native host** and the Compose overlays; the retired database-backed Rules Core deployment and its PostgreSQL network are not part of this service.

## Services and networks

The two current Compose definitions are `docker-compose.yml` and `docker-compose.private-tunnel.yml`. A production deployment that includes the private instance must use both.

| Container | API surface | Docker networks |
| --- | --- | --- |
| `dorks-and-dice-rules-core` | `RulesCore__ApiSurface=PublicOnly` | `dorks-and-dice-backend` only |
| `dorks-and-dice-rules-core-private` | `RulesCore__ApiSurface=PrivateOnly` | Pair-specific private tunnel and restricted Site control plane only |

The private instance has **no PostgreSQL dependency or database network**. Both processes validate their embedded native JSON catalog independently. Neither instance serves the other's reference API surface; both can expose authenticated native mechanics on their own surface.

The required pair-network configuration is:

```text
RulesCorePrivate__Network=dorks-and-dice-private-rules-wiki-rules-core
```

The overlay also accepts `ToolHosting__ControlPlaneNetwork` (default: `dorks-and-dice-tool-control-plane`) and `TOOL_HOST_BASE_URL` (default: `http://dorks-and-dice-site:8080`). Both networks must exist before deployment. The allowed pair members are Rules Wiki and private Rules Core. The restricted control plane connects private Rules Core and the Site for authentication introspection. The ordinary backend network must **not** be connected to the private instance.

Check the effective configuration, without starting containers:

```bash
docker compose --project-name dorks-and-dice-rules-core \
  --env-file /mnt/HDDs/www/dorks-and-dice-rules-core/.env \
  -f docker-compose.yml -f docker-compose.private-tunnel.yml config
```

The paths above are the established production deployment layout; verify the environment before using them on another host.

## Authorization

Site private-tunnel approval and Docker network isolation are independent requirements. The allowed Rules Wiki → Rules Core pair is configured at the Site control plane (`ToolHosting__PrivateTunnels__rules-wiki__0=rules-core`). Rules Core redeems Site-issued, target-scoped tickets through `/tool-host/registrations/rules-core/api/introspect`. The private reference validator requires `rules-wiki` as the private source Tool and the `dorks-and-dice` Site mode. An ordinary Tool delegation ticket is insufficient.

The private HTTP API is at `/api/private/rulesets`, with additional authenticated `/api/private/mechanics/v1` operations. Public catalog distribution remains separately gated. See [architecture](architecture.md) and [native mechanics](native-mechanics-api.md).

## Deployment and verification

Use the manually authorized [GitHub Actions deployment](../README.md#manual-deployment). The current deployment utility validates the two-overlay service configuration, expected networks and members, readiness, and API-surface isolation. Do not recreate containers from only one Compose file with `--remove-orphans` or weaken the private network to make authentication pass.

After an approved deployment, verify `/ready` on both ingresses and perform one authenticated Rules Wiki reference request. Liveness/readiness alone do not prove private ticket authorization. The Site repository owns the general private-tunnel architecture, registration, and source/target pairing policy.
