# Character Sheet → Rules Core native mechanics v1 (partial integration handoff)

**Target repositories:** `LegendMaster03/dorks-and-dice-rules-core` and
`LegendMaster03/dorks-and-dice-character-sheet`.

## Contract status and transport

This increment supplies a **partial** native mechanic projection, not a working
Character Sheet migration. It supports the existing Site **ordinary Tool
 delegation** on the Rules Core `PublicOnly` ingress:

- `GET /api/mechanics/v1/ruleset`
- `POST /api/mechanics/v1/characters/project`
- `POST /api/mechanics/v1/resources/resolve`
- `POST /api/mechanics/v1/conditions/roll-mode`
- `GET /api/mechanics/v1/creatures/{entityId}` (partial numeric facts)

Every route requires target-scoped Site ticket introspection, the actual Site
user ID, Site mode `dorks-and-dice`, and trusted
`delegatedFromToolKey = character-sheet`. A Wiki ticket, a Block Initiative
ordinary delegation, anonymous traffic, or a private-tunnel ticket on the
public ingress is rejected. This does **not** require changing Character Sheet
or Site to use an unconfigured new private-tunnel relationship. No bypass by
invented headers or browser-supplied identity is allowed.

The separate `PrivateOnly` ingress retains
`/api/private/mechanics/v1/*`, which checks the distinct Site
`privateTunnelSourceToolKey = character-sheet` authorization. It additionally
exposes private indexed catalog search and bounded batch definition lookup.
Neither ordinary delegation nor the private Wiki tunnel authorizes those private
Character Sheet operations. **The private Character Sheet tunnel is not
currently established by this patch**, so the private discovery operations
remain inaccessible to the existing Character Sheet deployment.

The ordinary authenticated API deliberately does **not** expose entire native
JSON definitions; source-grant and rights-based authorization for general-Tool
option discovery has not yet been implemented. The anonymous
`/api/rulesets` catalog keeps its independent approved-fingerprint distribution
gate. The new ordinary mechanics routes return projected numerical/typed
outcomes only, not raw definitions or historical RuleConcept DTOs.

No code in this patch changes Site registration, deployment, campaign authority,
Character Sheet persistence, or the existing Rules Wiki private boundary.

### Native option discovery and batch identity resolution

When a separately authorized **Character Sheet private tunnel** is established,
Character Sheet can request indexed native
option summaries through `GET /catalog/rules?fingerprint=<value>&category=class&q=warlock&limit=50&offset=0`.
The response carries `rulesetId`, `fingerprint`, `contractVersion: 1`, `total`,
`offset`, `limit`, `categoryFacets` and `rules: [{id, name, kind}]`. Use the
exact native `kind` (`class`, `patron`, `pact`, `invocation`, `condition`,
`leveled-condition`, etc.). Search results never contain source file paths.
`limit` is bounded to 1–100; input and category filters have strict limits.

`POST /catalog/resolve` accepts `{ "rulesetId": "first-party-001",
"fingerprint": "<value>", "ids": ["class.warlock", "pact.warlock.talisman"] }`.
It returns a bounded list of `{id, name, kind, definition}` from the same
snapshot and explicitly returns `missingIds` and `status: "partial"` for
unavailable native IDs. No fuzzy concept mapping, `working/` fallback, source
history, campaign-effective overrides or unrelated Tool auth is provided.
The batch is limited to 1–100 **distinct** valid native IDs and exact ruleset
fingerprint. Mismatch returns 409; malformed input returns 400.

This supports **option lookup only**. It is not the complete rule-mechanics
projection consumed by the existing Character Sheet and does not authorize
replacing live API calls before the private tunnel and consumer migration are
implemented and tested.

### Character projection request

The request uses native identifiers and the exact ruleset fingerprint from
`GET /api/mechanics/v1/ruleset` on ordinary Site delegation (or the matching
private route when an explicit private tunnel exists). The public catalog may be unavailable
until approved for distribution:

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<current validated fingerprint>",
  "abilityScores": {
    "strength": 10, "dexterity": 16, "constitution": 12,
    "intelligence": 8, "wisdom": 14, "charisma": 16
  },
  "advancements": [
    { "classId": "class.warlock", "level": 1, "occurrenceId": "stable-character-entry-id" }
  ],
  "selectedOptions": ["patron.warlock.fiend", "pact.warlock.talisman"],
  "skills": { "skill.arcana": { "ranks": 0, "proficient": true } },
  "currentResources": { "resource.pact-magic": 1 }
}
```

`advancements` refer to independent class-level tracks; selection rows use
native option IDs and stable occurrence identifiers. Character Sheet owns the
character, roll history, inventory and persisted resource state. Rules Core
returns computations and proposed consequences only; it does not write the
Character Sheet database.

Fields currently accepted: ability scores, class advancements, ranked or
proficient skills, independently faceted competencies, typed defense
contributions, selected native option IDs, remaining resources, condition IDs,
base walking speed, and per-level hit point gains. Approved typed condition
operations such as Blinded AC suppression and half-speed are evaluated, but
unsupported conditional effects remain explicit. Supplied defense contributions
**do not** imply equipment grant authorization.

### Character projection response

`contractVersion` is `1`, with `rulesetId`, exact `fingerprint`, `status`,
`values`, `featureIds`, `resources`, `requiredChoices`, and `issues`.
Each numeric value is `{ "status": "resolved", "value": 3 }` or an
explicit `{ "status": "unsupported" | "input-required", "value": null,
"reason": "..." }`. Full projection status is `resolved`, `partial`,
or `invalid`. Do not present a `partial` result as complete.

Native resources have keys, maximum, optional current value, recoverability,
and optional spell level. Sorcery Points are separate from Pact Magic, and
leveled slots are separate pools. A noncaster receives none unless a specific
native rule grants a resource. `recovery: "unsupported"` means an unimplemented
recovery schedule, not Long Rest by default.

Version mismatch returns HTTP 409 (no implicit upgrade), while missing source
content returns HTTP 503. Invalid resource actions return 400/422. The server
emits per-request `Server-Timing` for projection and resource calls. Auth failure
codes are 401/403/502/503/504, as appropriate. No resource endpoint accepts a
caller-defined maximum/recovery: it recalculates them from a native projection
before considering the requested transition.

### Integration requirements in Character Sheet

Review and replace the following existing consumers after the endpoint is
available through an independently authorized Site route:

- `src/CharacterSheet.Web/DelegatedRulesCoreGateway.cs`: replace old
  `/api/rules/character-mechanics/resolve` and per-concept N+1 lookups with one
  version-pinned native projection; maintain cancellation and downstream timing.
- `src/CharacterSheet.Web/Client/src/rules-core-api.ts`: remove
  `ResolvedRuleCatalogItem`, `ResolvedRuleDetail`, `RuleConceptKey` based
  endpoint assumptions from the new path; preserve independent public browsing.
- `src/CharacterSheet.Application/Characters/Projection/CharacterRulesProjectionRequestBuilder.cs`:
  create native IDs and stable occurrence identifiers without mixing in
  untranslated legacy string keys.
- Character-building, advancement, spellcasting, condition, inventory and
  persistence workflows: use only `resolved` mechanics; display unsupported
  results as unavailable rather than synthesizing local rules.

Advancement validation is fail-closed: missing or duplicate progression rows, repeated
class occurrences, duplicate occurrence IDs, or unavailable class definitions withhold
all class-derived resources, features, and choices. Affected saves and training totals
are marked unsupported. Multiclass proficiency and class saving-throw proficiency
are not calculated from the highest class-local bonus. Existing ability modifiers
remain independently available. Callers must treat any `partial` projection as
non-authoritative for character advancement, resource spending, or saves.

The standalone native condition evaluator supports typed advantage and
Disadvantage, distinguishes attacks **by** and **against** a creature, and
cancels opposing modes. Character Sheet can call the version-pinned
`POST /conditions/roll-mode` endpoint with explicit event facts:

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<current validated fingerprint>",
  "conditionIds": ["condition.wet"],
  "roll": { "appliesTo": "saving-throw", "subject": "affected-creature" },
  "facts": {
    "qualifiers": { "sourceKind": "spell", "purpose": "ordinary-save" },
    "sourceDamageTypes": ["cold"]
  }
}
```

The response has `status`, nullable `mode` (`Normal = 0`, `Advantage = 1`,
`Disadvantage = 2`), and `issues`. HTTP 200 means the typed calculation is
resolved; HTTP 422 means at least one matching conditional effect is unsupported
or required event facts are absent. Invalid roll contexts return 400; ruleset
or fingerprint mismatch returns 409. A return of `Normal` is authoritative
only when **all** applicable condition predicates were evaluated, not merely
because no unconditional advantage was found.

`facts.flags` supports `fearSourceWithinLineOfSight`, `anchored`,
`attackerCanSeeAffectedCreature`, and `targetCanSeeAffectedCreature`.
`facts.qualifiers` supports `purpose`, `incomingAttackType`,
`attackerRelationToGrappler`, `attackerRelationToPinner`,
`targetRelationToGrappler`, and `sourceKind`. `sourceDamageTypes` is an explicit
set for the source event, used to evaluate native Wet cold, lightning, and fire
predicates. When an effect specifies an `ability`, the roll must provide that
ability (for example, `"ability": "dexterity"` for a Dexterity save); an omitted
ability is unresolved, not treated as a different saving throw. Unknown predicate keys, absent facts, or malformed clauses fail
closed; the evaluator does not interpret native description text as code.
Condition AC and speed facts can be evaluated in the standalone library, but
only roll mode has an independently exposed HTTP route in this increment.
The native effect vocabulary remains incomplete.

**The contracts currently lack** a fully validated advancement/feat decision
endpoint, equipment and condition authorization, spell-point conversion,
multiclass spellcasting composition, effective campaign mechanics, and complete
feature-specific resource recovery. Do not treat their absence as success.

## Persisted identity migration (not yet executed)

Persisted legacy `RuleConceptKey` is **not** a native ID. Migration must:

1. Make a versioned, provenance-preserving inventory of legacy selections,
   occurrence IDs, class/subclass parent links, resources and applied grants.
2. Create explicit old-key → native-ID mappings with evidence of mechanical
   equivalence (not name matching), source edition, ruleset fingerprint and
   mapping revision. Support one-to-many, many-to-one and unresolved mappings.
3. Dry-run on a protected Character Sheet database copy. Preserve unresolved
   selections and original identifiers verbatim, label them for review, and
   never delete or remap to a nearby option automatically.
4. Migrate atomically and idempotently, retaining original keys for audit and
   rollback. Do not silently change an existing character's power, class levels,
   grants, or historical resource expenditures.
5. Verify selection roundtrip, save/reload, advancing one level, class options,
   valid resource trackers, no data loss, authorization scope and rollback.

Migration requires an approved target schema and verified crosswalk; this patch
contains neither a crosswalk nor a database migration.

## Acceptance and performance gate

Character Sheet can legitimately resume full mechanics testing only when:

- A representative native character can create, project, advance, validate all
  mandatory selections and prerequisites, expend/recover resources, and save and
  reload without identifier loss.
- Source-only content remains unsupported until converted, including supplied
  historical monster attacks or unreviewed prestige prerequisites.
- Direct, Wiki, unauthenticated and wrong-scope calls are denied, and private
  source content never travels over public catalog endpoints.
- The one-call projection meets measured concurrency and P95 latency budgets
  after Site Tool Host delegation, with cancellation and diagnostic evidence.

**Do not turn on global access or deploy without separate authorization.**

## Character Sheet native option discovery and advancement eligibility (additional increment)

The authenticated **ordinary delegation** surface now exposes **summary-only**
option discovery and explicit candidate advancement eligibility:

- `GET /api/mechanics/v1/catalog/options?fingerprint=<sha256>&category=class&q=warlock&limit=50&offset=0`
- `POST /api/mechanics/v1/advancements/eligibility`

The source-catalog categories allowed on the ordinary delegated surface are
`class`, `subclass`, `patron`, `pact`, `invocation`, `condition`, and
`leveled-condition`. A category is required. Only `{id, name, kind}` is returned;
raw definitions, source paths, and the private full catalog remain unavailable
on this route. Results use the captured immutable catalog snapshot and require
its exact fingerprint. The Site must authenticate trusted ordinary
`character-sheet` delegation before any route handler executes.

Advancement eligibility accepts the same version-pinned `NativeCharacterRequest`
as the projection API plus `candidateId` and reports `eligible`, `ineligible`,
`unresolved`, or `invalid`. It currently handles native base-class next-level
rows and a conservative subset of Warlock Patron/Pact/Invocation prerequisites.
It checks eligible native class level, exclusive Patron/Pact choices, invocation
level and Pact requirements, elective costs and duplicates. It returns
`unresolved` for unsupported new requirements, repeatable invocation target
identity, unverified known-cantrip prerequisites, multiclass policy, missing
content and ambiguous advancement history; it never guesses an approval.

Example request:

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<current 64-character SHA-256 fingerprint>",
  "candidateId": "invocation.agonizing-blast",
  "character": {
    "rulesetId": "first-party-001",
    "fingerprint": "<same fingerprint>",
    "abilityScores": {
      "strength": 10, "dexterity": 14, "constitution": 12,
      "intelligence": 10, "wisdom": 10, "charisma": 16
    },
    "advancements": [
      { "classId": "class.warlock", "level": 2, "occurrenceId": "stable-class-occurrence" }
    ],
    "selectedOptions": ["pact.warlock.chain"]
  }
}
```

`invocation.agonizing-blast` has a known-cantrip selection prerequisite in
native content; this input does not include that cantrip identity and must be
reported `unresolved` instead of automatically `eligible`.

An accompanying Character Sheet TypeScript client (`rules-core-native-api.ts`)
implements these operations and native character projection, with five passing
isolated mock-transport tests. The **existing** `RuleConceptKey` workflows,
per-occurrence advancement schema, campaign scope and save/reload paths remain
unmigrated. Neither this new endpoint nor the test client completes the
Character Sheet integration.
