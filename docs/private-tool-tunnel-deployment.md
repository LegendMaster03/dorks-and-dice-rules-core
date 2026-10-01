# Private Tool tunnel deployment

Rules Core supports a split-ingress deployment for private first-party Tool integrations.

This deployment model is intentionally opt-in. `docker-compose.yml` by itself retains the current single-service deployment. The split is activated only when `docker-compose.private-tunnel.yml` is included.

## Topology

```text
shared Tool backend network
        |
        v
rules-core            RulesCore:ApiSurface=PublicOnly

private source/target network
        |
        v
rules-core-private    RulesCore:ApiSurface=PrivateOnly
        |
        | restricted control-plane network
        v
Dorks & Dice Site     ticket introspection only
```

The private instance is not attached to `dorks-and-dice-backend`. Ordinary Tool containers therefore do not receive network reachability to the private ingress.

The public instance can not serve private API routes, even if a private target credential is accidentally presented to it. The private instance can not serve the stable public API. Health and readiness endpoints remain available because they are outside `/api`.

## Required networks

The split deployment requires two external Docker networks:

1. a source/target private network chosen for the particular private relationship; and
2. a restricted Tool control-plane network used by the private target to reach Site ticket introspection.

For the Rules Wiki -> Rules Core relationship, a deployment can use names such as:

```bash
docker network create dorks-and-dice-private-rules-wiki-rules-core
docker network create dorks-and-dice-tool-control-plane
```

The exact private-network name is deployment configuration rather than application identity.

The source Tool and `dorks-and-dice-rules-core-private` join the source/target private network. Site and `dorks-and-dice-rules-core-private` join the restricted control-plane network. Ordinary Tools join neither network.

## Core configuration

The deployment environment must provide:

```text
RulesCorePrivate__Network=<private source/target Docker network>
```

Optional overrides are:

```text
RulesCorePrivate__ToolHostBaseUrl=http://dorks-and-dice-site:8080
ToolHosting__ControlPlaneNetwork=dorks-and-dice-tool-control-plane
```

`RulesCorePrivate__ToolHostBaseUrl` must resolve from the restricted control-plane network. It is used only for Site ticket introspection.

The private instance sets `RulesCore__BootstrapBaseline=false` because the public and private instances share the same Rules Core database and baseline ownership remains with the normal deployment instance. Schema initialization remains safe when both instances start together because Rules Core already coordinates startup schema work with a PostgreSQL advisory lock and persisted schema revision.

## Starting the split deployment

Once the external networks and peer attachments exist, validate and start both compose files together:

```bash
docker compose \
  --project-name dorks-and-dice-rules-core \
  --env-file /mnt/HDDs/www/dorks-and-dice-rules-core/.env \
  -f docker-compose.yml \
  -f docker-compose.private-tunnel.yml \
  config

docker compose \
  --project-name dorks-and-dice-rules-core \
  --env-file /mnt/HDDs/www/dorks-and-dice-rules-core/.env \
  -f docker-compose.yml \
  -f docker-compose.private-tunnel.yml \
  up -d --force-recreate --remove-orphans
```

Do not activate the overlay until the private source Tool is configured to use the private target URL. Enabling the overlay changes the normal `rules-core` service to `PublicOnly`, so an old source that still attempts to reach private endpoints through the ordinary public/delegation path will fail closed.

## Site control plane

Site private-tunnel authorization is independent of Docker network membership. The deployment must also configure the allowed source/target pair under:

```text
ToolHosting:PrivateTunnels:{sourceToolKey}:{index} = {targetToolKey}
```

For example, the Rules Wiki relationship is `rules-wiki -> rules-core`.

The source uses Site only to exchange its short-lived source capability for a target-scoped Rules Core ticket. The actual Rules Core API request travels directly over the source/target private network.

Private target tickets use the target's stable key-scoped introspection path, for example:

```text
/tool-host/registrations/rules-core/api/introspect
```

That path is intentionally independent of any historical application slug or UI integration metadata.

## Security invariants

- `DelegationTargets` does not grant private API access.
- private tunnel configuration does not grant ordinary Tool delegation.
- the public Rules Core ingress can not serve private API routes.
- the private Rules Core ingress can not serve public API routes.
- ordinary Tool containers do not join the source/target private network.
- private target authentication must carry Site-issued `PrivateTunnelSourceToolKey` provenance.
- Site is a control-plane participant; it is not the private API data-plane proxy.
