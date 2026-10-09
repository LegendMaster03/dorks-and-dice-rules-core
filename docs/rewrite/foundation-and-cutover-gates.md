# Rules Core rewrite — foundation and cutover gates

Status: **rewrite baseline / implementation planning**, 2026-10-09. This document is not a claim that the new service or migration exists.

The rewrite starts on `feature/rules-core-rewrite`, independent of deployed `main`. The existing Rules Core remains available until a separately reviewed and authorized cutover. Do not deploy this branch, alter live registrations, or migrate/reset the production database as an incidental consequence of starting the rewrite.

## Why this is a rewrite

The current service already completed the headless Rules Core / Rules Wiki split. This effort is **not** another frontend migration and not merely a faster resolver. **Rules Core is becoming the authoritative host and execution engine of the Dorks & Dice custom TTRPG system.** Its own versioned ruleset defines primary mechanics, their applicability, and how imported external rules interact with the core and with each other. D&D 3e, 3.5e, 5e, 5.5e, and future third-party sources remain compatible without becoming the engine's hidden base ontology. The architectural definition is in [ruleset-first-target-architecture.md](ruleset-first-target-architecture.md). Existing code is evidence about required behavior **and about the Dorks & Dice game-design decisions already made through cross-edition reconciliation**; it is not the architecture specification for the replacement. Begin from those existing design decisions, not a blank list of mechanics.

Known investigation areas:

- translating materially different source formats without treating 5e.tools JSON as a universal rules ontology;
- keeping source-native truth, source revisions, translated mechanics, canonical recognition, and adjudicated rules distinct;
- avoiding incorrect canonical merges and duplicated SRD identities;
- making 3e/3.5e, 5e, and 5.5e mechanics independently correct where they differ;
- bounded import/replay operations, parallelizable maintenance, and efficient read projections;
- avoiding request-time whole-corpus scans or repeated catalog construction, especially on Character Sheet and Wiki paths;
- preserving source licenses, grants, immutable history, and existing user data.

These are subjects to verify against fixtures and current code before selecting replacement structures.

## Greenfield implementation policy

The rewrite is a **new implementation**, not a staged refactor of existing Rules Core classes, schema, and modules. Build the authoritative Dorks & Dice system engine from a new native domain model with independently designed persistence, evaluation, and integration boundaries. Reuse of a library or isolated helper requires an explicit reason; a component's presence in the legacy service is not a reason to retain it. The existing repository and branch may preserve development history without imposing a legacy code dependency. Isolate the new implementation from the currently deployed service until cutover.

The Dorks & Dice game-design decisions already established through years of compatibility/reconciliation are **not** being discarded. Recover their intended semantics as a reviewed specification and tests, then reimplement those rules in the new engine. Treat legacy code and endpoint responses as evidence and migration compatibility references, not automatically correct normative behavior. Distinguish deliberate mechanics decisions from accidentally reproduced bugs or old architecture compromises.

The independently authored core can use properly attributed material from **SRD 5.1** and **SRD 5.2.1**, released under **CC BY 4.0**, without assuming every D&D publication is CC-licensed or that the SRDs are public domain. Game procedures themselves are not protected by U.S. copyright, while copyrightable expression still is. Track source, license, attribution, and modifications for incorporated material. Keep **3e/3.5e OGL** materials and independently licensed third-party source content separately identified; do not convert their licenses to CC BY or publish restricted text merely because its mechanic can be modeled.


## Operational boundary

Hex Crawl is in human testing and, together with Block Initiative, does not require Rules Core for normal use. All other Rules Core-dependent Tools are currently restricted to development access. This reduces the number of exposed consumers during construction; it does **not** authorize production data destruction or interruption of unrelated Tools.

- Do not merge, deploy, change Tool registration, or modify production persistence without explicit authorization.
- Keep Rules Wiki's private, server-to-server delegation and Rules Core's public consumer interface as separately reviewed security and compatibility boundaries.
- Do not place restricted source contents, credentials, production dumps, or user identifiers into repository fixtures, logs, traces, or review artifacts.
- A replacement should be constructed and tested against disposable storage or appropriately sanitized copies before any production migration.
- Only effective resolved rules belong on routine game-Tool runtime paths. Rich source alternatives, historical comparisons, and authoring remain restricted internal/admin workflows.

## Compatibility to inventory, not assumptions to inherit

Before implementing the greenfield engine or replacing an external endpoint:

1. Inventory existing external routes, request/response schemas, authentication, authorization, client expectations, and error semantics. Identify actual consumers in Character Sheet, Rules Wiki, and other Tools.
2. Separately inventory private Rules Wiki Tool-to-Tool routes and the Site delegation/introspection behavior.
3. Record read/write paths for source import, content blobs, per-user registrations/grants, canonical entities, Rules Lawyer decisions, campaign overrides, and publications.
4. Capture deterministic fixtures and assertions from the existing implementation and real-world source *shapes*, including negative access cases.
5. Establish representative timing/load baselines with the existing `Server-Timing` spans. Distinguish database execution from projection, connection, network, and cache/materialization overhead.
6. Review open, unmerged work as reference material; do not assume it exists on `main` or silently transplant it.

External contracts may be deliberately improved during this rewrite, because dependent apps are development-only, but each incompatible change must be identified and coordinated before cutover. Do not accidentally change a public contract simply because an equivalent private Wiki operation changes.

## Behavioral invariants to carry into a replacement

**Source truth.** Original imported bytes and immutable source-native versions survive translations and parser upgrades. Reprocessing without an upstream change must not invent new native revisions.

**Identity.** Package/distribution, physical representation, source-native entity, publication, canonical entity, source occurrence, and user-facing Rules Layer concept answer different questions. Shared canonical recognition never grants access to restricted source bodies. Name similarity alone never proves cross-edition mechanical equivalence.

**Rules.** The Dorks & Dice core ruleset is first-class, authoritative, versioned, and ultimately independently playable. It defines how imported rules can contribute, override, interact, or remain unresolved. Global and campaign choices, publication, and source acquisition are distinct operations. Existing adjudication and historical provenance remain inspectable after migration. 3.x mechanics are not reinterpreted as 5e mechanics, or vice versa, merely to fit a common schema.

**Authorization.** Site identity and role/campaign authority remain separate from Rules Core source-content grants. Access filtering must apply at catalog, counts/facets, detail, history, comparison, resolution, and export boundaries. The internal Wiki delegation is not a substitute for per-user checks.

**Idempotence.** Re-import, refresh, normalizer replay, bootstrap, and restart converge without duplicate canonical identities, fake upstream revisions, or overwritten deliberate decisions.

**Performance.** Expensive requests need explicit budgeted stages, batched reads, suitable indexes, bounded work queues, safe concurrent worker ownership, and observability. Avoid hiding costly synchronous source reconciliation inside hot read paths.

## Incremental implementation sequence

### Gate 0 — ruleset specification and independently verified baseline

First inventory and independently verify the Dorks & Dice game-design decisions already embodied in existing cross-edition normalization, universal mechanics/competencies, adjudication, and house-rule policy. Distinguish settled design choices from temporary implementation workarounds and genuine gaps; do not assume the game system itself starts at zero. Specify the system's known/default procedures, intentionally unresolved design choices, compatible source interactions, and core-to-import/import-to-import adjudication policy. Produce a machine-readable contract inventory and a deterministic test corpus. Capture current behavior, privacy boundaries, performance bottlenecks, dependency graph, and data counts/sizes without writing to production. Record unresolved questions and the proposed rewrite module boundaries. The acceptance suite must make regressions visible before replacement implementations begin.

After recovering the decisions already made in Rules Core, **give the owner room to establish the native Dorks & Dice system's principles and proposed mechanics without steering those ideas toward an existing game's design**. This applies both to areas that are substantially developed and to newer ideas such as combat maneuvers. Treat prior decisions as foundations that may be refined, not as immutable conclusions. Record the owner's terminology and intended procedures before applying comparison categories or suggesting implementations.

**Only after the native design is recorded**, use [pathfinder-1e-gameplay-comparison.md](pathfinder-1e-gameplay-comparison.md) as a comparative resource. Pathfinder First Edition may inspire improvements in *any* existing or new area, but it must not dictate the initial Dorks & Dice specification. Pathfinder mechanics are comparison evidence, **not automatically adopted rules** or authorization for a full content import.

### Gate 1 — Dorks & Dice ruleset kernel and persistence

Build the first-class versioned Dorks & Dice mechanics/ontology and its source/identity/rules domain model with explicit invariants and persistence ownership, **implemented independently of existing Rules Core domain types and database structures**. Use fresh disposable storage and migration tests. Do not migrate live records into a partially defined schema.

### Gate 2 — ingestion, translation, reconciliation

Introduce format adapters and lossless raw-source storage, versioned mechanical interpretation into the core ontology, publication/entity identity reconciliation, core-to-import and import-to-import semantic relationships, and resumable parallel imports/backfills. Exercise multiple representations of the same material and adversarial ambiguous matches.

### Gate 3 — adjudication and effective resolution

Implement core-system-governed composition and deterministic published Rules Layer and campaign snapshots, update review, precedence, source visibility, cross-source conflict handling, and provenance. Prove that the same revision/context yields the same effective result.

### Gate 4 — consumer and Wiki contracts

Build typed, efficient mechanics projections and the separate internal Wiki read model on the new domain kernel. Validate Character Sheet, Rules Wiki, and downstream consumer integration using representative requests and denial cases.

### Gate 5 — migration rehearsal and cutover

Rehearse a repeatable, auditable old-to-new data migration against a disposable copy. Compare entity counts, canonical links, source grants, ruleset histories, and representative resolved outputs. Run concurrency/load regression checks. Plan a reversible switch at the Site registration/routing boundary; obtain separate approval for any live migration or deployment.

## Minimum evidence before a cutover proposal

- Verified external/API and internal/Wiki contract compatibility matrix with intentional deltas named.
- Multi-format 3e/3.5e/5e/5.5e corpus tests and structured stat/mechanics assertions.
- Exact grant/authorization isolation tests (including counts, facets, exports, and error behavior).
- Idempotent ingestion, retries, failed partial import recovery, worker concurrency, and restart tests.
- Immutable provenance plus global/campaign publication replay/migration checks.
- P50/P95/P99 request timing and throughput comparisons at documented dataset sizes and concurrency.
- New deployment health/readiness and private delegation boundary checks.
- Rollback/recovery procedure for code **and** data, with tested backups and no implicit schema reset.
- Explicit owner approval before merge, deployment, or production database change.

## Decisions deliberately not made in Gate 0

The replacement language/runtime, relational schema, DTOs, normalization vocabulary, canonical match thresholds, search/index strategy, task/worker topology, and migration technique must follow the baseline evidence and proposed architecture review. Reusing a current type name or table does not by itself make it a requirement. New feature development in other Tools is out of scope for this initial branch.
