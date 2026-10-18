# Rules Core rewrite — foundation and in-place integration checkpoints

Status: **rewrite planning / corrected operational baseline**, 2026-10-18. The filename is retained for existing documentation links; **no old/new service cutover is planned**.

The owner confirms the **old Rules Core has been hidden and its public/tester access disabled for approximately one month**. This is **not** a live public service that must be preserved alongside a replacement. Work on `feature/rules-core-rewrite` is one **in-place service rewrite** in the existing repository, not dual production engines, a temporary parallel deployment, or a service-switch/cutover project. Branch-based code review and tests do not constitute parallel live operations. Do not merge, deploy, change access settings, re-enable integrations, or reset stored data without separate explicit authorization.

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

The rewrite is a **new native implementation in the existing Rules Core service/repository**, not an obligation to preserve old code paths, a legacy-compatible second engine, or a prolonged staged refactor. Author the Dorks & Dice rules kernel from its own reviewed gameplay model; source imports, interfaces and persistence serve that model rather than dictate it. Reuse an isolated helper only for a concrete reason. Since the old service is not in public/tester use, **there is no requirement to operate legacy and replacement systems concurrently or perform a routing cutover**. Keep development changes controlled on the rewrite branch and run tests on disposable or appropriately protected data.

The Dorks & Dice game-design decisions already established through years of compatibility/reconciliation are **not** being discarded. Recover their intended semantics as a reviewed specification and tests, then reimplement those rules in the new engine. Treat the old code and API responses as **evidence for behavior, existing data and consumer contracts**, not as automatically correct gameplay rules or a requirement for backward-compatibility shims. Distinguish deliberate mechanics decisions from old implementation compromises.

The independently authored core can use properly attributed material from **SRD 5.1** and **SRD 5.2.1**, released under **CC BY 4.0**, without assuming every D&D publication is CC-licensed or that the SRDs are public domain. Game procedures themselves are not protected by U.S. copyright, while copyrightable expression still is. Track source, license, attribution, and modifications for incorporated material. Keep **3e/3.5e OGL** materials and independently licensed third-party source content separately identified; do not convert their licenses to CC BY or publish restricted text merely because its mechanic can be modeled.


## Operational boundary

**Owner-verified accessibility (2026-10-18):**
- **Rules Core:** Hidden; public/tester access disabled for roughly a month. The old implementation is not an exposed service requiring seamless replacement.
- **Block Initiative:** The **only public/tester-accessible Tool** relevant to this rewrite. Its **core turn/block and encounter functionality is independent** of Rules Core; its optional integration fetches monster/stat-block data for auto-fill, and the current code also supports **condition search/autocomplete**. This is a **small read-only consumer dependency**, not a dependency on a complete native combat adjudicator.
- **Character Sheet:** Development access only, deliberately restricted when the old Rules Core was shut down. It requests the largest share of Rules Core mechanics but **does not set public/tester availability requirements** during development.
- **Rules Wiki and other Tools:** No additional public/tester integration dependency is assumed. Review their actual contracts when their development milestones begin.

The first integration must preserve Block Initiative's **manual encounter entry and normal initiative operation** even if Rules Core lookup is unavailable. Do not enable the hidden Rules Core for everyone merely to make optional auto-fill work; when authorized, expose only the reviewed lookup/detail scopes and verify grants, permission filtering and source visibility. Remember that monster and condition lookups may include restricted content and must not leak it across grants.

- Do not merge, deploy, alter Tool registrations/access controls, re-enable exposed endpoints, or modify live persistence without explicit owner authorization.
- Keep Rules Wiki's private server-to-server delegation separate from externally available consumer APIs; no public route must silently authorize private bodies.
- Do not put restricted source contents, credentials, unsanitized data dumps or user identifiers in fixtures, logs, traces or review artifacts.
- Use disposable/sanitized datasets for destructive tests; **keep backups and deliberate data-change reviews** if existing dev records need preservation. This is ordinary data safety, **not** a mandatory old-service migration or cutover rehearsal.
- Routine game Tools should receive effective authorized lookup data/mechanics, not arbitrary source alternatives or restricted authoring content.

## Compatibility to inventory, not assumptions to inherit

Inventory each actual consumer contract before implementing that consumer's first replacement capability. **Start with the small Block Initiative read-only contract; the complete Character Sheet and Wiki contract inventories can follow before their respective integrations.**

1. For **Block Initiative**, verify the existing `/tool-host/registrations/rules-core/api/upstream` gateway paths and effective `/api/rules?entityType=monster|condition&q=...&limit=...` search and `/api/rules/{conceptKey}` monster detail shape. Current client code projects HP, initiative modifier, ordinary/Touch/Flat-Footed AC, challenge rating and optional combat statistics. Record expected permission behavior, unavailable-service manual fallback and required provenance; do not mistake this for core combat rules execution.
2. When work begins on **Character Sheet / Rules Wiki**, inventory their actual external/internal routes, authorization and consumer data contracts independently; these are dev-only consumers and need not be public-ready for Block Initiative.
3. Record read/write paths for source import, content blobs, per-user registrations/grants, canonical entities, Rules Lawyer decisions, campaign overrides, and publications.
4. Capture deterministic fixtures and assertions from the existing implementation and real-world source *shapes*, including negative access cases.
5. Establish representative timing/load targets for the endpoint being rebuilt; old `Server-Timing` samples, if available, are evidence rather than mandatory live-service performance baselines.
6. Review open, unmerged work as reference material; do not assume it exists on `main` or silently transplant it.

Because **Block Initiative is exposed**, coordinate its lookup/detail contract changes and test its manual fallback before changing accessible behavior. Character Sheet and Rules Wiki are development-only and may adopt intentionally improved contracts without compatibility adapters. Do not accidentally conflate public Block Initiative lookup permissions with private Wiki authoring/delegation.

## Behavioral invariants to carry into a replacement

**Source truth.** Original imported bytes and immutable source-native versions survive translations and parser upgrades. Reprocessing without an upstream change must not invent new native revisions.

**Identity.** Package/distribution, physical representation, source-native entity, publication, canonical entity, source occurrence, and user-facing Rules Layer concept answer different questions. Shared canonical recognition never grants access to restricted source bodies. Name similarity alone never proves cross-edition mechanical equivalence.

**Rules.** The Dorks & Dice core ruleset is first-class, authoritative, versioned, and ultimately independently playable. It defines how imported rules can contribute, override, interact, or remain unresolved. Global and campaign choices, publication, and source acquisition are distinct operations. Existing adjudication and historical provenance remain inspectable after migration. 3.x mechanics are not reinterpreted as 5e mechanics, or vice versa, merely to fit a common schema.

**Authorization.** Site identity and role/campaign authority remain separate from Rules Core source-content grants. Access filtering must apply at catalog, counts/facets, detail, history, comparison, resolution, and export boundaries. The internal Wiki delegation is not a substitute for per-user checks.

**Idempotence.** Re-import, refresh, normalizer replay, bootstrap, and restart converge without duplicate canonical identities, fake upstream revisions, or overwritten deliberate decisions.

**Performance.** Expensive requests need explicit budgeted stages, batched reads, suitable indexes, bounded work queues, safe concurrent worker ownership, and observability. Avoid hiding costly synchronous source reconciliation inside hot read paths.

## Incremental implementation sequence — single in-place rewrite, consumer-specific integration

The checkpoints below are **logical work/validation phases**, **not separate live Rules Core instances**, deployment lanes, or a fixed service switch. Work may be organized by the smallest actual consumer capability, and completed parts are verified independently before the next consumer is introduced. Existing documentation has a stronger **full native game-design approval condition before implementing the core engine**; the owner has not revoked that design-approval decision by removing the need for a cutover. **Documentation, contract inventory and acceptance tests for the Block Initiative lookup can be prepared without the entire combat engine being implemented.** Authorize engine code separately.

### Gate 0 — native game design and minimum consumer contract approval (no engine code)

**0A — Recover established decisions.** Inventory and verify gameplay choices from competencies, composite skills, advancement, combat, spellcasting, source adjudication and house rules. Identify decisions separately from bugs, old fallbacks and implementation-only details.

**0B — Complete the native independent rules specification.** Maintain [native-rules-design-specification.md](native-rules-design-specification.md) as the owner-reviewed source of truth for character creation, advancement, action economy, combat, maneuvers, spellcasting, equipment, conditions, recovery, exploration and GM procedures. Preserve generalized **Level Bands**, with independently evaluated total-character and class/prestige tracks.

**0C — Define source/content compatibility.** Cover native preservation, automated 3e/3.5e/2014 5e/2024 5e conversion, competing contributions, provenance, safe unresolved decisions and explicitly optional Pathfinder 1e scope. Rule/content conversion is different from automatic character or saved-encounter migration.

**0D — Critically compare alternatives.** Use Pathfinder and other sources only after recording the owner's native decisions, especially maneuver design.

**0E — Approval and acceptance examples.** Separate what is settled from what is optional/open, with ordinary gameplay and cross-edition acceptance cases. The existing **Gate 0 owner sign-off before the full native rules kernel** remains the agreed governance boundary unless explicitly revised. This does **not** make every full-game mechanic a prerequisite for documenting or testing Block Initiative's small monster/condition lookup contract.

**Gate 0 exit:** Explicit owner review and approval before starting the **native gameplay engine**, as already established. This is **not** a cutover/migration/dual-deployment gate and does not by itself authorize merging, deployment or re-enabling public access.

### Gate 1 — authoritative Dorks & Dice kernel and data model

Implement a single, first-class, versioned Dorks & Dice mechanics/ruleset model and persistence in Rules Core, without retaining a second legacy runtime. Design schema and services from accepted native rules rather than reproducing obsolete DTOs. Verify behavior using disposable or sanitized storage; preserve existing records only where the owner needs them and approves the specific data treatment.

### Gate 2 — source ingestion, resolved lookup and Block Initiative integration

Implement the **minimum real source identity, permission-filtered catalog/search, and monster/condition read projections needed by Block Initiative**. Prioritize its actual query and detail contract; test imported stat-block variants, distinct ordinary/Touch/Flat-Footed AC fields, authorized source display, manual-entry fallback, missing data and permission failures. The standard encounter/initiative engine is *not* a Rules Core feature to rebuild. Source import/reconciliation can then expand by actual format/consumer need rather than all sources becoming a precondition for the first useful lookup.

### Gate 3 — effective rule composition and deterministic mechanics

Add native rules evaluation, source-to-native semantic adaptation, publication/Rules Layer decisions, conflicts, campaign overrides and repeatable context-specific results incrementally. Preserve immutable source truth, grants and provenance. Complete the tested engine behavior that actual consumers request without creating an edition-specific second combat runtime.

### Gate 4 — development-only Character Sheet, Rules Wiki and further consumers

Integrate progressively based on the distinct mechanics required by **Character Sheet** (largest consumer) and the internal/reference requirements of **Rules Wiki**. Verify permission isolation, typed effective mechanics projections, and consumer-owned character/encounter state. Each integration can remain development-only until separately authorized for broader access.

### Gate 5 — validation and controlled access restoration (no cutover)

Once the relevant capabilities pass tests, consider **whether to re-enable only the appropriate Rules Core routes and permissions** for the applicable Tool(s). This is **not** an old-to-new engine cutover, parallel runtime migration or mandatory wholesale public release. Record the reviewed API surface and access grants, test backup/recovery for any data changes, verify service health and relevant performance, and obtain explicit owner authorization for deployments/access changes. Existing development-only consumers may stay development-only.

## Evidence before an exposed integration or data change

- **Block Initiative:** automated/manual stat-block lookup behavior, monster and condition search, ACL-filtered source data, correct read projection, stored template handling, independent initiative/encounter operation and manual fallback on Rules Core errors.
- **New native mechanics:** tests of accepted rule procedures and pure/mixed source interactions **as each capability is built**, including predictable conflicts and source provenance.
- **Security:** permissions and restricted source visibility at lookup, detail, list/count, Wiki/private delegation and export boundaries wherever implemented.
- **Data safety:** backups, idempotent imports/reprocessing, explicit treatment of existing records, tested recovery and **no implicit database reset**; use migration tests for schema changes where genuinely needed, **not** a mandatory old-service cutover rehearsal.
- **Quality:** endpoint-specific response budgets, diagnostics, failure handling, and bounded import work; full-scale performance tests as the affected consumers/volumes warrant them.
- **Approval:** no merging, deployment, access restoration or destructive persistence changes without explicit owner approval.

## Decisions deliberately not made in Gate 0

The replacement language/runtime, relational schema, DTOs, normalization vocabulary, canonical match thresholds, search/index strategy and worker topology follow the reviewed source evidence and native design. Reusing an old table/type is not a requirement. **Do not invent an old/new service migration or routing cutover.** Actual schema/data changes still require appropriate tests and owner authorization. New features in other Tools are out of scope except integration changes explicitly authorized for a consumer.
