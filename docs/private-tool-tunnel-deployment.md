# Private Tool tunnel deployment

Rules Core supports a split-ingress deployment for private first-party Tool integrations.

The generic private-tunnel security model and creation procedure are documented in the Site repository at `docs/private-tool-tunnels.md`. This document covers the Rules Core-specific split ingress, database dependency, and deployment behavior.

This deployment model is opt-in at the Compose level. `docker-compose.yml` describes only the normal public Rules Core service. Production deployments that use the Rules Wiki -> Rules Core tunnel must include `docker-compose.private-tunnel.yml` on every recreate.

## Topology

```text
shared Tool backend network
        |
        v
rules-core                RulesCore:ApiSurface=PublicOnly

Rules Wiki
        |
        | pair-specific private network
        v
rules-core-private        RulesCore:ApiSurface=PrivateOnly
        |             \
        |              \
        |               +-- restricted database network --> PostgreSQL
        |
        +-- restricted control-plane network --> Dorks & Dice Site
```

The private instance is **not** attached to `dorks-and-dice-backend`. Ordinary Tool containers therefore do not receive network reachability to the private ingress.

The public instance can not serve private API routes, even if a private target credential is accidentally presented to it. The private instance can not serve the stable public API. Health and readiness endpoints remain available because they are outside `/api`.

## Required networks

The production split requires three external Docker networks:

1. a source/target pair network for Rules Wiki and the private Rules Core ingress;
2. a restricted Tool control-plane network for private Rules Core -> Site ticket introspection; and
3. a restricted Rules Core database dependency network for private Rules Core -> PostgreSQL.

Current production names are:

```text
pair network:       dorks-and-dice-private-rules-wiki-rules-core
control plane:      dorks-and-dice-tool-control-plane
database network:   dorks-and-dice-rules-core-db
```

Create missing networks idempotently:

```bash
docker network inspect dorks-and-dice-private-rules-wiki-rules-core >/dev/null 2>&1 \
  || docker network create dorks-and-dice-private-rules-wiki-rules-core

docker network inspect dorks-and-dice-tool-control-plane >/dev/null 2>&1 \
  || docker network create dorks-and-dice-tool-control-plane

docker network inspect dorks-and-dice-rules-core-db >/dev/null 2>&1 \
  || docker network create dorks-and-dice-rules-core-db
```

Expected membership:

```text
dorks-and-dice-private-rules-wiki-rules-core
  dorks-and-dice-rules-core-private
  dorks-and-dice-rules-wiki

dorks-and-dice-tool-control-plane
  dorks-and-dice-rules-core-private
  dorks-and-dice-site

dorks-and-dice-rules-core-db
  dorks-and-dice-rules-core-private
  PostgreSQL
```

Ordinary Tools join none of these networks unless they are explicitly a peer in a separately authorized tunnel.

## Database isolation

Both public and private Rules Core instances use the same Rules Core database, but the private ingress must not join `dorks-and-dice-backend` merely to reach PostgreSQL.

`docker-compose.private-tunnel.yml` therefore attaches `dorks-and-dice-rules-core-private` to the restricted database network selected by `RulesCorePrivate__DatabaseNetwork`. When that setting is omitted, the overlay uses the production-safe default `dorks-and-dice-rules-core-db`.

The PostgreSQL peer is managed outside this repository. On the current TrueNAS deployment, the PostgreSQL container serves multiple applications and remains attached to `dorks-and-dice-backend` for those existing consumers. Adding the database dependency network gives PostgreSQL a second network interface; it does not move the container or remove the existing backend attachment.

A one-time command such as:

```bash
docker network connect \
  --alias ix-dorks-and-dice-postgres-postgres-1 \
  --alias postgres \
  dorks-and-dice-rules-core-db \
  ix-dorks-and-dice-postgres-postgres-1
```

is only a runtime attachment. If TrueNAS recreates PostgreSQL, that attachment may disappear. The PostgreSQL side of the network must therefore be persisted through TrueNAS configuration when possible, or through an idempotent TrueNAS startup/init task that reconnects the managed PostgreSQL container after recreation.

Rules Core deployment should fail rather than attach the private ingress to the shared Tool backend as a shortcut.

## Core configuration

The production environment file must provide the pair-network identity:

```text
RulesCorePrivate__Network=dorks-and-dice-private-rules-wiki-rules-core
```

The current optional/defaulted settings are:

```text
RulesCorePrivate__DatabaseNetwork=dorks-and-dice-rules-core-db
RulesCorePrivate__ToolHostBaseUrl=http://dorks-and-dice-site:8080
ToolHosting__ControlPlaneNetwork=dorks-and-dice-tool-control-plane
```

`RulesCorePrivate__ToolHostBaseUrl` must resolve from the restricted control-plane network. It is used only for Site ticket introspection.

The private instance sets `RulesCore__BootstrapBaseline=false` because the public and private instances share the same Rules Core database and baseline ownership remains with the normal deployment instance. Schema initialization remains safe when both instances start together because Rules Core coordinates startup schema work with a PostgreSQL advisory lock and persisted schema revision.

## Starting the split deployment manually

Once the external networks and peer attachments exist, validate and start both Compose files together:

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

## Automated deployment

`.github/workflows/deploy.yml` is part of the production contract. It must deploy both:

```text
docker-compose.yml
docker-compose.private-tunnel.yml
```

The workflow validates that the private pair-network setting is present, recreates the public and private services together, and verifies readiness on both network surfaces. The database dependency network uses the overlay default unless deployment configuration overrides it.

A deployment workflow that uses only `docker-compose.yml` would remove `rules-core-private` with `--remove-orphans` and revert the public service to the unsplit configuration. That is why overlay use belongs in CI/CD rather than an operator-only command sequence.

## Site control plane

Site private-tunnel authorization is independent of Docker network membership. The deployment must configure the allowed source/target pair under:

```text
ToolHosting:PrivateTunnels:{sourceToolKey}:{index} = {targetToolKey}
```

For Rules Wiki:

```text
ToolHosting__PrivateTunnels__rules-wiki__0=rules-core
```

The source uses Site only to exchange its short-lived source capability for a target-scoped Rules Core ticket. The actual Rules Core API request travels directly over the source/target private network.

Private target tickets use the target's stable key-scoped introspection path:

```text
/tool-host/registrations/rules-core/api/introspect
```

## Verification

Public readiness:

```bash
docker run --rm \
  --network dorks-and-dice-backend \
  curlimages/curl:8.12.1 \
  -fsS http://dorks-and-dice-rules-core:8080/ready
```

Private readiness:

```bash
docker run --rm \
  --network dorks-and-dice-private-rules-wiki-rules-core \
  curlimages/curl:8.12.1 \
  -fsS http://dorks-and-dice-rules-core-private:8080/ready
```

Network membership:

```bash
docker network inspect dorks-and-dice-private-rules-wiki-rules-core \
  --format '{{range .Containers}}{{println .Name}}{{end}}' | sort

docker network inspect dorks-and-dice-tool-control-plane \
  --format '{{range .Containers}}{{println .Name}}{{end}}' | sort

docker network inspect dorks-and-dice-rules-core-db \
  --format '{{range .Containers}}{{println .Name}}{{end}}' | sort
```

Readiness proves network and process health. Complete verification also requires an actual Rules Wiki operation so the Site capability exchange, target-scoped ticket, private Core introspection, and private API authorization are exercised end to end.

## Security invariants

- `DelegationTargets` does not grant private API access.
- private tunnel configuration does not grant ordinary Tool delegation.
- the public Rules Core ingress can not serve private API routes.
- the private Rules Core ingress can not serve public API routes.
- `dorks-and-dice-rules-core-private` is not attached to `dorks-and-dice-backend`.
- database access for the private ingress uses the restricted dependency network.
- ordinary Tool containers do not join the source/target private network.
- private target authentication must carry Site-issued `PrivateTunnelSourceToolKey` provenance.
- Site is a control-plane participant; it is not the private API data-plane proxy.
