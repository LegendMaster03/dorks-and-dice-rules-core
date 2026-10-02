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
| `rules-core-db` | Aggregate Entity Framework Core database command-execution duration recorded during the request. |
| `rules-core-reference-query` | Query-stage duration reported by the Rules Core reference catalog service. |
| `rules-core-reference-docs` | Mechanical-document/materialization-stage duration reported by the reference catalog service. |
| `rules-core-reference-total` | Total operation duration reported by the reference catalog service. |

The reference metrics describe backend work performed by Rules Core for its reference APIs. They do not represent a built-in Wiki UI. Rules Wiki is a separate Tool that consumes Rules Core through the private Tool-tunnel architecture.

## Database metric semantics

`rules-core-db` is accumulated through an Entity Framework Core command interceptor and represents command execution reported by EF Core. It is intentionally not described as all data-access time: application-side object materialization, domain transformation, and any row streaming that occurs outside the command execution event can add additional request time.

The metric is request-scoped. Startup schema/bootstrap work and background jobs do not have an HTTP timing context and therefore do not contribute to a browser request's `rules-core-db` value.

## Composition

The timing state is created at the beginning of `HostedToolAuthenticationMiddleware`, which is currently the first Rules Core request middleware. A single `Response.OnStarting` callback emits the whole-request metric and the accumulated database metric.

Authentication and reference-catalog code add their component timings through the same timing helper. Existing header values are appended rather than replaced.

A request can therefore return values similar to:

```text
Server-Timing: rules-core-auth;dur=3.2, rules-core-reference-query;dur=8.7, rules-core-reference-docs;dur=1.9, rules-core-reference-total;dur=11.0, rules-core-db;dur=9.4, rules-core;dur=15.8
```

When that response reaches a client through the Site proxy, Site may additionally append its own `dnd-site` metric.

The component metrics overlap and must not be summed blindly. `rules-core` is the authoritative wall-clock request-to-headers duration for the Rules Core layer.

## Adding future metrics

Add a submetric only when it identifies a meaningful operational stage that can help distinguish a performance problem. Use the `rules-core-<component>` namespace and record low-cardinality stages rather than request identifiers or route-specific values.

Do not expose SQL, entity IDs, campaign IDs, user IDs, source names, hostnames, connection information, or other sensitive/high-cardinality data through `Server-Timing` names or descriptions.

Tests should verify metric presence, uniqueness, namespace ownership, and parseability. They must not assert exact durations.
