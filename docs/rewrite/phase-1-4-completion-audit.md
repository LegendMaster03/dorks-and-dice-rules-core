# Original Phases 1–4 — completion reconciliation (2026-10-30)

Base verified: Rules Core `main` `f19f49c71bcbd0b0fafd0ad202a6239a69e0a409`.
`feature/rules-core-rewrite` is absent from the current remote branch list. This audit
uses the original phase meanings in `foundation-and-cutover-gates.md`, not the
narrower similarly numbered implementation milestones.

**Delivery order correction:** use `docs/handoffs/existing-tool-restoration-priority.md`
to restore current Tool dependencies first. Source conversion and import are
still assigned to original Phases 2–3, but are not prerequisites for Character
Sheet native mechanics or optional Block Initiative numeric lookup.
The narrow reported Phase 3A/3B milestones remain complete; this audit only
assesses omitted requirements against the original broader plan.

## Gap matrix

| Original requirement | Existing implementation | Missing work / dependency | Completion test | Work to close |
| --- | --- | --- | --- | --- |
| P1: Independently executable native rule kernel | Native catalog, stable IDs, atomic immutable fingerprinted snapshot, host | Typed actor/effect rules, condition application, campaign-effective composition and persisted authorized scopes; requires reviewed typed effects and policy decisions | Deterministic numeric roll/check/effect tests; homebrew and campaign isolation; missing-rule outcomes | Started `RulesCore.Mechanics` with roll selection, conditional roll-mode predicate matching and typed character calculation; finish native effect vocabulary and effective-scope resolver |
| P1: Ruleset identity/version/authorization | `NativeCatalogSnapshot` and Site-ticketed Wiki API | No effective mechanics version resolution or authenticated general-Tool API before this work | Version mismatch, source-grant denial and unauthorized scope tests | Introduced version-pinned request and separate Character Sheet source-ticket gate; Site route/grant coordination remains |
| P2: Skills and competencies | Native skills, independently ranked children, composite metadata, separate competency facets; conversion-name reference | Complete ranks/training/proficiency/class-skill grants, prerequisite profiles, source conversion execution | Ranked vs proficient, composite arithmetic, facet isolation, 3.x vs 5.x fixtures, denied unavailable profiles | Partial native skill and competency calculations; mixed profiles and missing governing abilities are explicit unsupported states |
| P2: Derived statistics and defenses | Native defense rule and monster mechanics specifications | Full equipment grants, condition interactions, HP/proficiency policy for each class, movement, all saves, effects | Negative Dex/Touch/Flat-Footed/condition cases; HP advancement | Four approved AC calculations, a typed condition subset (Blinded defense and speed), six ability modifiers, basic saves, initiative and explicit HP gain path; untrusted equipment contributions flagged |
| P2: Class/advancement | Native class tables and separated Warlock Patron/Pact/Invocation records | General advancement choices, class-specific effects, prestige and multiclass combination, Level Band transitions, feat legality | Per-level grants, choice conflicts, multiclass and prestige fixtures | First class-table projector with fail-closed duplicate/gapped advancement validation; limited Warlock prerequisites; multiclass proficiency remains unavailable |
| P2: Casting and resources | Sorcerer slot/point rows, Warlock pact slot rows, feature-specific recovery definitions | Casting mode selection, spell-point conversion, mixed class slots, granting spell eligibility, comprehensive spending/recovery | Independent Sorcery Points/Pact Magic/slots; short vs long rest; spell points | Distinct trackers and long-rest Sorcery Point recovery; missing recovery and mixed caster rules explicitly unsupported |
| P2: Source conversion | Historical source/working reference and mapping documentation | Versioned interpreter, accepted mappings, source evidence, conversion regression fixtures and grants | Mixed native/imported replay, 3e/3.5e/5e/5.5e differences, unsupported cases | Not implemented in this patch; source-shaped data is never asserted converted |
| P3: Unified stat blocks | Native bestiary and reference catalog search/detail | Native-authoritative typed attack/action/effect projection shared by authored and imported creatures; provenance and grants | One native-authored and one converted monster with equivalent typed projection; Block Initiative monster and condition lookup | Preliminary numeric creature projection marks actions/effects and source initiative proficiency unresolved; exposes CR and direct numeric fields without guessing |
| P4: Authenticated mechanics contracts | Public read catalog + separate private Wiki API; legacy Character Sheet delegated gateway | Site-issued authorized Character Sheet path, deployment configuration, contracts for options/advancement and effective campaign scopes | Wrong-tool ticket forbidden; version-pinned POST; complete character save/reload | Authenticated fingerprint metadata, projection, condition roll-mode, resource transitions and creature facts are on a delegated `PublicOnly` calculator route; raw indexed native option discovery and batch definitions remain `PrivateOnly`. Real Site routing and Character Sheet adaptation remain |
| P4: Native identity migration | Persisted `RuleConceptKey` strings in Character Sheet | Versioned crosswalk verified by identity and semantics; unresolved selections retained in storage | Migration dry run, no selection loss, rollback and read-after-write | Strategy documented in Character Sheet handoff; migration NOT performed |
| Performance and operations | Immutable indexed snapshot; no request-time catalog rebuilding | Realistic benchmarks, effect graph, request caching/contexts, observed Site roundtrip | Representative P50/P95/P99, concurrency, cancellation, Server-Timing | Projection reads snapshot once, indexed lookup, cancellation and diagnostic timing added; measurements still required |

## Verified limitations of this code increment

- The library is **not** a full authoritative character engine. Class table values
  can be projected when explicitly represented. Effects whose semantics exist only
  in description text are not executed.
- Source conversion and campaign/homebrew scope composition are not implemented.
- Numeric condition evaluation supports only explicitly typed effects (for example, Blinded AC and half-speed). Known event predicates may be supplied for conditional roll mode, defense, and speed. Unknown or absent event facts and unimplemented effect types remain unresolved; spell and general feature effects are not interpreted.
- Current native class JSON contains some grants to IDs that are not present in
  `native/`; `working/` remains excluded, so those are unresolved.
- Current equipment contributions are supplied typed facts, not yet verified
  against equipped native item identities. The projection flags this limitation.
- Rejected or incomplete advancement histories now withhold class-derived grants,
  resources, choices, saves, and training outcomes. Duplicate/gapped native rows
  return issues instead of being silently combined. Multiclass proficiency is
  unavailable rather than inferred from class-local bonuses.
- Condition roll modes can now be matched by roll subject and a whitelisted
  subset of native `when` clauses, with explicit event facts and unknown values
  preserved. The authenticated roll-mode endpoint exposes that narrow evaluator;
  it does not resolve automatic saves, all numeric roll modifiers, all condition
  restrictions, or arbitrary native effects.
- Multiclass casting and extended saving-throw combination remain unsupported
  until their approved native profiles are represented.
- Spell points, prestige acquisition, feats, feature-specific conditions, and
  external-source permission filtering are still incomplete.
- The ordinary Character Sheet Site delegation can now be distinguished from
  private-tunnel credentials at the API boundary; this is uncompiled local code.
  The first authenticated calculator route is **not yet an end-to-end Character
  Sheet integration**. Source-authorized native option discovery, campaign rules,
  request/response migration, persistence and real Site routing remain untested.
- No Source/Rules Layer legacy compatibility adapter is introduced.

## Phase completion recommendation

**Phase 1: Incomplete.** Complete and test effect evaluation and effective-scope
composition, beyond the demonstrated arithmetic/roll kernel.

**Phase 2: Incomplete.** Require an end-to-end native character projection including
validated features, complete stats, skills/competencies, independent resources,
recovery, prerequisites, and representative versioned source conversion.

**Phase 3: Incomplete.** Require identical authoritative converted projections of
native-authored and permitted imported creatures. Partial bestiary lookup alone
is insufficient.

**Phase 4 original-requirement gaps: Still open despite completed narrow milestones.** Require a deployed *authorized development-only* Site
path, updated Character Sheet consumer, tested persisted-identity migration and
save/reload acceptance. This is independent of full optional monster coverage.

The completed narrower catalog/transport milestones should remain complete.

## Reproducible validation

When a full repository checkout and .NET 10 SDK are available:

```sh
dotnet restore RulesCore.slnx
dotnet build RulesCore.slnx --no-restore
dotnet test RulesCore.slnx --no-build
python -m pytest -q tests
```

Security checks should verify that Wiki tickets, Block Initiative tickets,
wrong-target tickets, missing tickets, disabled Site mode, and unavailable Site
all fail for Character Sheet mechanics. The ordinary public ingress **does**
serve authenticated mechanics POST but must reject unauthenticated and non-Character
Sheet requests. Ensure public ingress has no private catalog-definition endpoints,
and private-only ingress rejects ordinary Site delegation.

Capture at least 100 representative project/advance calls using several character
sizes, with native content preloaded. Record P50/P95/P99 wall time, per-stage
Server-Timing, Site delegation overhead, GC allocation and concurrent request
latency. Do not assert a performance regression is fixed until measured.

**Execution status in the isolated patch environment:** `git diff --check` is
available; .NET SDK and a full upstream checkout are not available, so build,
unit, HTTP integration, concurrency and performance tests have not run.

## Native advancement eligibility and consumer interface — incremental update

- A dedicated native `NativeAdvancementEligibility` evaluator now supports
  selected base-class level advancement and constrained Warlock Patron, Pact,
  and Invocation eligibility. It records satisfied, failed and unresolved
  requirements separately, with explicit rejection of duplicate/missing
  progression, unknown native selections, and version mismatch.
- A Character Sheet-authenticated **summary-only** option search on the
  ordinary-delegation mechanics ingress provides bounded native ID/name/kind
  discovery without returning full definitions. The private Wiki tunnel and
  raw native content authorization are unchanged.
- The separate Character Sheet client branch adds typed v1 API requests and
  snapshot validation, but the existing persisted `RuleConceptKey` workflows
  still require a tested migration and runtime presenter integration.
- C# HTTP and unit tests were authored but **not executed** in this environment;
  the .NET 10 SDK is unavailable. The five Node mock-transport tests for the
  Character Sheet native client did execute and pass with a temporary
  TypeScript 5.8.3 compilation stub rather than the full upstream repository.
