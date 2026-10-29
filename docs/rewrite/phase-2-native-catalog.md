# Phase 2 — Native JSON Catalog

## Runtime authority

The initial ruleset identity is first-party-001. The runtime selects the configured
ruleset directory under Rulesets:ContentRoot, reads its manifest.json, and loads
only that package's native/ subtree. No working/, reference/, conversions/, or
source-import content can be loaded or used as a fallback. Promotion into
native/ is the approval boundary; no status-based approval detector or
publication allowlist is involved.

The loader supports a standalone entity object, an array of entities, or
consolidated documents with named entity arrays. Document properties beginning
with an underscore and records of kind metadata are not playable entities.
Stable IDs, names, kinds, uniqueness, manifest identity and native authority,
duplicate JSON keys, known condition relationships, safe paths, and symlink
exclusion are validated. Any failure aborts the complete load.

## Immutable catalog contract

RulesCore.Content provides NativeCatalogLoader and NativeCatalogSnapshot. The
latter contains cloned structured JSON definitions with ruleset identity,
source path provenance, stable ID, display name and category; an immutable
ID lookup; category indexes and counts; prebuilt three-character postings for
case-insensitive substring searches over ID and name; deterministic sorting;
bounded pagination (limit 1–100); and a SHA-256 content fingerprint.

The fingerprint is computed over ruleset identity, manifest schema version,
and canonicalized playable definitions ordered by ID. Changing JSON whitespace,
object property order, or the location of a definition does not change its
fingerprint. Modifying the definition does.

Per-request catalog queries do not read files, deserialize JSON, or rebuild
indexes. For search queries shorter than three characters, an existing
immutable list or category index is scanned.

NativeCatalogStore owns the currently published snapshot and swaps references
atomically. Readers should take a single Snapshot reference and use it for
the entire operation. A failed load does not publish a partial catalog.
Explicit replacement and invalidation exist for future integration; no
filesystem watcher or automatic reload is implemented.

## Host and deployment behavior

At startup, NativeCatalogInitializer loads and validates the catalog, then
publishes it through dependency injection. A successful initialization makes
GET /ready return HTTP 200. A missing, invalid, duplicate or unsafe package
makes GET /ready return HTTP 503 and a non-sensitive reason code. GET /health
continues to return HTTP 200 independently of content and of PostgreSQL.
No /api endpoints are mapped. Both PublicOnly and PrivateOnly modes retain
their Phase 1B API restrictions.

From the repository root, the host can run with:

    dotnet run --project src/RulesCore.Host

For another working directory, configure Rulesets__ContentRoot as an absolute
path to the repository's data/rulesets directory. The Dockerfile copies the
manifest and approved native/ definitions into /app/data/rulesets, setting
Rulesets__ContentRoot to that location. Working, reference and conversion data
are excluded. A content-only Docker rebuild reuses the cached .NET build
layer when source files have not changed. A mounted native package can be
replaced and activated by restarting the service without recompiling .NET.

## Validation

Run the C# project and prior Python suites from the repository root:

    dotnet restore RulesCore.slnx
    dotnet build RulesCore.slnx -c Release --no-restore
    dotnet test RulesCore.slnx -c Release --no-build
    python3 -m unittest discover -s tests -p 'test_*.py'
    python3 tools/validate_ruleset_split.py
    docker build -t rules-core-phase2:local .

The Phase 2 CI workflow also tests ready/unready Docker instances and verifies
that consumer APIs remain absent. Existing Phase 1A migration evidence, data,
validators and tests are unchanged.

At the Phase 1A baseline, six authoritative JSON files yield 35 playable
entities: 29 ordinary conditions, one leveled condition, four rules and
one rule chapter. The native catalog intentionally does not ingest the
unfinished thousands of definitions under working/.

## Phase 3A handoff

1. Inject NativeCatalogStore, retrieve one Snapshot reference per operation,
   and fail closed when it is null. Do not perform new file reads or inspect
   working/ from a request handler.
2. Use the immutable entity definitions, ID lookup, category counts and
   search operations to build explicit API DTOs. Preserve ruleset ID and
   fingerprint semantics; do not expose internal filesystem paths in APIs.
3. Implement Site ticket redemption, exact rules-wiki provenance, domain
   authorization and distinct private/public access before mapping Wiki
   or private endpoints. Phase 2 grants no API authentication shortcuts.
4. Keep gameplay evaluation, source conversions, imports, campaign overrides
   and PostgreSQL persistence outside the Phase 3A transport integration.
5. Design snapshot refresh, invalidation and deployment rollout explicitly
   when needed. Phase 2 supports atomic replacement but not hot reload.
