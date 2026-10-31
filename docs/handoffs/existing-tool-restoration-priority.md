# Existing Tools first — Rules Core omissions and restoration order

**Status:** development-only implementation triage; not deployment authorization.
**Verified default branches (2026-10-30):** Rules Core `f19f49c71bcb`, Character Sheet
`3ef460315fda`, Block Initiative `cee2e62bf104`, Rules Wiki `b49c8f6d1acb`,
Hex Crawl `4759ca402ff8`; Site `09495b48d24b`.

The previously completed **narrow implementation milestones** are not being
renamed or reopened. Their original Phase 1–4 requirements are independently
tracked as delivery gaps. Restore each Tool's actually used functionality before
completing unrelated import/conversion infrastructure. Do not introduce a Phase 5
label for omissions already assigned to Phases 1–4.

## Priority 1 — Character Sheet: restore native runtime mechanics

**Present state:** `DelegatedRulesCoreGateway` sends legacy
`/api/rules/character-mechanics/resolve`, `/api/rules/character-advancement/eligibility`,
mechanics/support, recovery, manufacturing/enchanting and campaign-scoped variants.
`CharacterRulesProjectionRequestBuilder` persists `RuleConceptKey` and builds
per-level advancement occurrences, subclasses and runtime choices. The current
native `/api/rulesets` GET-only API cannot satisfy these consumers. The local
unmerged `RulesCore.Mechanics` API implements only a partial projection, resource
transitions, condition roll modes and authenticated catalog reads.

**Implement in dependency order:**

1. Establish and test the **existing ordinary Site delegation** from Character
   Sheet to Rules Core. The local patch now mounts the authenticated, calculated
   results at `/api/mechanics/v1` on `PublicOnly` and checks trusted
   `delegatedFromToolKey = character-sheet` from target-scoped Site ticket
   introspection. This is **not** anonymous/public content distribution.
   `/api/private/mechanics/v1` remains a separately authorized private-tunnel
   surface and is not a prerequisite for Character Sheet restoration. Neither
   Site configuration, released routing nor deployment has been changed here.
   Validate a real Site delegate invocation before relying on this path.
2. Expose native option discovery through a **source-authorized** consumer
   contract. The local ordinary-delegation mechanics surface now includes a
   limited **summary-only** option lookup (`/catalog/options`), with category
   allowlisting and fingerprint checks. It contains no rule definitions. The
   indexed batch catalog that returns full definitions stays private; do not
   open it to ordinary delegation without verifying source grants and permitted
   disclosure. Summary discovery is **not** a complete builder replacement.
   Missing native IDs remain missing, never resolved by name similarity.
   Do not resolve names by similarity or read `working/`.
3. Implement the exact request/response bridge **in Character Sheet**: native IDs,
   per-level occurrence history, mandatory options, prerequisite/eligibility,
   effective campaign rules, and explicit unresolved mechanics. The provisional
   projector currently accepts cumulative level totals per class; this does
   **not** directly match the builder's per-occurrence progression model and
   must not be connected as-is.
4. Fill those **native mechanics** needed to build and run a character: derived
   ability/check/save/defense totals; class and subclass progression; feats,
   proficiencies, skills and competencies; equipment effects; spellcasting and
   separate resources; conditions and recovery. Use the established Dorks & Dice
   rulings, not edition-driven default inference.
   A confirmed data gap: the current Sorcerer native progression has no
   `proficiencyBonus` field, whereas Warlock rows do; the provisional evaluator
   must not silently substitute Warlock class values for Sorcerer or multiclass
   cases. Resolve this with the approved universal proficiency policy.
5. Preserve historical selections and resource state through a **verified native
   identity crosswalk**. Dry-run on a safe copy of Character Sheet persistence;
   unresolved choices remain visible and recoverable. Do not assume a legacy
   `RuleConceptKey` equals a native ID, or perform destructive migration without
   explicit approval.
6. Validate save/reload, level gain, ruleset fingerprint mismatch, tool/campaign
   authorization, missing definitions and latency. Only claim restored when
   real consumer tests pass end-to-end.

**Not a prerequisite:** complete third-party source import, new external-edition
converters, a fully converted monster corpus or public anonymous distribution.

## Priority 2 — Block Initiative: optional native lookup

**Verified upstream state:** `integrations/rules-core/client.ts`, `conditions.ts`
and `monsters.ts` call legacy `/api/rules` and `/api/rules/{conceptKey}`.
A **separate local Block Initiative patch** now migrates optional search/detail to
`/api/rulesets/first-party-001/rules`, preserves stable `native:` identity,
handles `leveled-condition`, and refuses to invent proficiency-derived initiative.
Four isolated synthetic adapter tests pass. This is **not yet deployed** and
requires the public catalog's explicit rights-reviewed distribution approval or
a separately authorized consumer discovery API. Manual entry remains available.

1. Migrate condition search to native `condition` plus `leveled-condition`
   identities and preserve old saved references explicitly until migrated.
2. Migrate monster search and detail against native `monster` IDs and use the
   authoritative partial combat projection for numeric fields. The native
   catalog **does** contain a substantial bestiary; however, most attacks and
   some defensive/initiative fields still lack executable native conversion.
   A field marked `unsupported` is left blank, never guessed. Normalize 2014
   versus 2024 initiative only when the corresponding native rule is approved.
3. Verify encounter save/reload, manual fallback, combat-stat availability,
   condition attachments and stat-block browser links. Do not synthesize a
   legacy concept key or nonexistent source/edition data.

**Distribution/security gate:** the existing anonymous public catalog requires complete
package rights review and an **exact approved fingerprint**. No review approval
is inferred from `native/` promotion; do not disable the fail-closed public gate.
If the present tool must access nonpublic content, design an explicitly authorized
read-only Tool contract instead of widening Wiki or Character Sheet private APIs.

## Priority 3 — Rules Wiki: protect the functioning native reference

Keep its distinct Site-ticketed `/api/private/rulesets` integration, verify
class/condition/monster display and private/public isolation. Do not make Wiki
consume the character mechanics API, enable `working/` fallback or reintroduce
legacy API adapters. UI problems are handled in the separate Wiki repository.

## Priority 4 — Hex Crawl: preserve optional provider semantics

`RulesCoreTravelEnvironmentProvider` still calls legacy global/campaign
`/api/rules/travel-environment` catalog and resolution endpoints. Its interface
already distinguishes unavailable, unsupported, missing input and resolved
values, and permits independent/manual procedure execution. Once priority 1
runtime and security are stable, expose needed native travel capabilities
through an authorized consumer endpoint and adapt this provider. Do not block
basic Hex Crawl workflows on a nonessential Rules Core feature.

## Separate original Phase 1–4 obligations

Versioned source import/conversion, replay, Rules Lawyer adjudication, converted
creature attacks, broad homebrew authoring and more advanced source mechanics
remain outstanding under their original phases. They are **not** Character Sheet
or Block Initiative release gates unless a specific consumed feature requires
one of them. Do not confuse converting external rule content with migrating
persisted Character Sheet identities or saved Block Initiative monster links.

## Acceptance gate for each tool

Treat a dependency as restored only after checking its actual deployed/dev
routing, Site authorization, native ID fidelity, structured unresolved states,
persistence roundtrips and tested user-visible feature paths. A local patch or
successful static JSON lookup is not evidence of an integrated Tool.

### Update: native Character Sheet option/eligibility increment

The local public-ingress mechanics API now adds
`GET /api/mechanics/v1/catalog/options` with a mandatory category and exact
fingerprint, returning **summary-only** native IDs, names and kinds, plus
`POST /api/mechanics/v1/advancements/eligibility` for the documented limited
class/Warlock option checks. This avoids exposing full catalog definitions
through ordinary Tool delegation. It does **not** yet implement source grants,
all class prerequisites, campaign-effective rules, persisted identifier
migration, or complete Character Sheet rendering and save/reload. No public
anonymous distribution gate has been changed.
