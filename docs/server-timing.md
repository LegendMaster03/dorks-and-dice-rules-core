# Rules Core Server-Timing instrumentation

Rules Core is a headless Dorks & Dice service. It emits standard HTTP `Server-Timing` metrics so requests can be decomposed across the Site platform and Rules Core without coupling either service to a separate telemetry backend.

The authoritative cross-Tool contract is maintained by the Site repository in `docs/server-timing.md`. This document records the Rules Core-specific implementation and metric semantics.

## Metric namespace

Rules Core owns the `rules-core` Tool key and therefore the `rules-core` / `rules-core-*` timing namespace.

Rules Core must not emit `dnd-*`; that namespace is reserved for the Site platform.

## Metrics

| Metric | Meaning |
| --- | --- |
| `rules-core` | Whole Rules Core request duration from the earliest Rules Core request boundary until response headers are committed. |
| `rules-core-auth` | Time spent redeeming the Site-issued Tool authentication ticket for the request. |
| `rules-core-db` | Aggregate Npgsql database-operation duration recorded during the request before response headers are committed, excluding physical connection-open spans. |
| `rules-core-reference-query` | Query-stage duration reported by the Rules Core reference catalog service. |
| `rules-core-reference-materialize` | Reference materialization stage, including document loading/projection and effective-rule resolution where the requested reference requires it. |
| `rules-core-reference-total` | Total operation duration reported by the reference catalog service. |

The reference metrics describe backend work performed by Rules Core for its reference APIs. They do not represent a built-in Wiki UI. Rules Wiki is a separate Tool that consumes Rules Core through the private Tool-tunnel architecture.

## Database metric semantics

Rules Core contains both Entity Framework Core queries and infrastructure code that executes PostgreSQL commands directly. `rules-core-db` therefore uses Npgsql's built-in `ActivitySource` rather than an EF-only interceptor so both paths are represented.

The listener requests propagation-only activity data. Rules Core does not request or expose SQL text, query parameters, database names, connection identifiers, or other Npgsql enrichment data for this metric. Physical connection-open activities are excluded; command/COPY-style database operations completed while the Rules Core request timing state is active contribute their elapsed duration.

Each Npgsql activity retains the request timing state under which it started, so an activity completing through an asynchronous continuation can not be attributed to a different concurrent request. The database accumulator is frozen when response headers are committed, matching the point at which `Server-Timing` itself becomes immutable. Database work still in flight after that boundary is not included.

The metric is request-scoped. Startup schema/bootstrap work and background jobs do not have an active Rules Core request timing state and therefore do not contribute to a browser or Tool request's `rules-core-db` value.

`rules-core-db` still must not be interpreted as all time attributable to persistence. Application-side materialization, domain transformation, connection acquisition, and other non-command work can contribute to `rules-core` without appearing in `rules-core-db`.

## Composition

`RulesCoreServerTimingMiddleware` is the first Rules Core request middleware. It creates the request timing state before authentication or endpoint work begins. A single `Response.OnStarting` callback freezes the request-scoped database accumulator and emits the database and whole-request metrics.

Authentication and reference-catalog code add their component timings through the same timing helper. Existing header values are appended rather than replaced. Timing lifecycle ownership remains separate from authentication so replacing or reordering authentication does not silently remove whole-request instrumentation. The timing middleware also clears its ambient request state when the request pipeline exits, including paths that never start a response.

A request can therefore return values similar to:

```text
Server-Timing: rules-core-auth;dur=3.2, rules-core-reference-query;dur=8.7, rules-core-reference-materialize;dur=1.9, rules-core-reference-total;dur=11.0, rules-core-db;dur=9.4, rules-core;dur=15.8
```

When that response reaches a client through the Site proxy, Site may additionally append its own `dnd-site` metric.

For Rules Wiki traffic, Rules Core is reached over the private Tool tunnel data path rather than through the Site HTTP proxy. Rules Core therefore makes the timing information available to its direct caller; exposing those downstream timings on a Rules Wiki browser response is a separate Rules Wiki propagation concern rather than something the Site can infer from the private data path.

The component metrics overlap and must not be summed blindly. `rules-core` is the authoritative wall-clock request-to-headers duration for the Rules Core layer.

## Adding future metrics

Add a submetric only when it identifies a meaningful operational stage that can help distinguish a performance problem. Use the `rules-core-<component>` namespace and record low-cardinality stages rather than request identifiers or route-specific values.

Do not expose SQL, entity IDs, campaign IDs, user IDs, source names, hostnames, connection information, or other sensitive/high-cardinality data through `Server-Timing` names or descriptions.

Tests should verify metric presence, uniqueness, namespace ownership, and parseability. They must not assert exact durations.
