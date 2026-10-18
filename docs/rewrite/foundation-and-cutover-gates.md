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

Inventory each actual consumer contract before implementing that consumer's integration. **First build the native foundation and per-subsystem conversion**, then verify Block Initiative's narrow converted stat-block read contract when the stat-block pipeline is ready. Character Sheet and Wiki contract inventories can follow before their respective dev-only integrations.

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

## Incremental implementation sequence — native foundation, one complete system at a time

**Owner sequencing decision (2026-10-18):** Build the **general Dorks & Dice rules foundation and basic operational functionality first**, then implement **one native rules subsystem at a time together with its imported-content conversion**. Do **not** begin by implementing Block Initiative's lookup or a monster converter ahead of the mechanics that give converted stat blocks their meaning. Do **not** require the entire game's final rules to be written or implemented before the first kernel code. This explicitly **replaces the earlier full-game-before-any-engine-code Gate 0 constraint**. The replacement is still one in-place service, with no dual old/new engine or service cutover.

The principle is **native rule + conversion + tests per subsystem**, all based on a shared, already functional foundation. A subsystem is only considered integrated when an imported implementation can be interpreted and executed (or explicitly marked unsupported) using the same native rules as internally authored content.

**Early tester feedback (owner direction, 2026-10-18):** A reviewed, working **subset of native mechanics should become available to specifically authorized testers before the full game is finished**, rather than treating tester access as a final release milestone. Progressively enabled features use the same native Rules Core as development and eventually normal play; there is no separate tester-only rules engine. Protect the intentionally hidden global Rules Core and other dev-only tools: **enabling access to one tested feature is not blanket re-enablement**. Preserve a way to submit, classify and reproduce tester feedback and refine native rules/conversion fixtures before locking subsequent dependent systems. This is a development feedback loop, not authorization to deploy or change access settings now.

**Customizability preserved from the existing model:** Authorized DMs can specify **campaign-scoped changes to effective rules**; users can **upload homebrew content** for private use/sharing and users or authorized campaign managers can **explicitly select eligible homebrew packages or rules**. The same native rules engine resolves these, with provenance and scope; selecting/accessing a source and *activating* it are separate operations. No upload silently changes the global ruleset or any other campaign. User-uploaded homebrew and DM campaign overrides must not require Rules Lawyer approval by default. Rules Lawyers handle globally published reconciliation or genuinely new shared conversion disputes, not ordinary campaign choices.

### Phase 0 — design and approve the *foundation*, not the complete rulebook

Recover existing approved Dorks & Dice decisions and agree on the smallest **coherent shared vocabulary and basic playable behavior** needed for engine implementation:

- Stable native mechanic/concept identifiers and semantic types: character/creature, ability, roll/check/save, defense, condition/effect, damage, action/resource, feature/grant, progression track and source provenance.
- A usable ordinary roll/effect-resolution foundation (including approved Normal, Advantage, Disadvantage and **verbatim canonical Emphasis** rule), basic ability/check semantics, simple action/effect results and state boundaries; preserve established Touch/Flat-Footed/ordinary AC distinctions where applicable.
- Ruleset ownership/versioning, basic eligibility and precedence concepts, source-native versus effective-native representation, user/source grants, restricted content and explicit unresolved/unsupported states.
- **Customization extension points from the start:** user-uploaded homebrew ownership/versioning, share/access grants and explicit opt-in adoption; DM-authorized campaign rule changes, with isolation from the global system and other campaigns. Decide effective precedence and applicability before enabling each kind of override, but do not hard-code a future fixed list of every homebrew mechanic.
- First foundation acceptance cases and deliberately deferred systems; do not pretend that provisional advanced combat, spellcasting or Level Band edge cases are already fully specified.

**Approval checkpoint:** Owner reviews the **foundation design and its initial acceptance examples before native engine code**. That is the limited design prerequisite. Subsequent subsystems receive their own focused rule/conversion review **before those subsystem mechanics are implemented**; a finished standalone rulebook is an eventual outcome, **not the prerequisite for creating the foundation**. Separate authorization still applies to merges, deployments, access changes and destructive data changes.

### Phase 1 — create the executable native foundation

Build a single **Dorks & Dice-first** kernel, basic ruleset identity, versioning, typed mechanism/effect evaluation, canonical source evidence/provenance model, and persistence that can evolve. Begin with basic characters/creatures, ordinary checks and core rule outcomes sufficient to prove executable native mechanics. Keep the player/DM table rules independent from any imported source edition or adapter schema. Use disposable/sanitized data and regression fixtures. **Do not create a second legacy runtime or require a full import corpus yet.**

**The foundation must support layering without building every authoring screen now:** A published native baseline, eligible selected homebrew/other source contributions, and authorized campaign modifications compose into the same reproducible effective ruleset. Preserve the distinction between access to a package, selecting it for use, and publishing/activating the resulting rule. Early regression cases should prove that one campaign's DM override and one user's private homebrew do not affect another campaign or user.

### Phase 2 — implement rules systems as vertical slices, with conversion in each slice

For **each** subsystem:

1. Recover approved Dorks & Dice rules, settle only material open behavior, and record plain-language rules and examples.
2. Implement the **native** procedure, effects, constraints, and deterministic tests for that system.
3. Implement/version relevant **3e, 3.5e, 2014 5e, 2024 5e** and eligible third-party **source-to-native mappings**, including source-specific differences, conditions and permissions. PF1e is optional until adopted; do not require a converter for sources that are not in scope.
4. Test native authoring, a representative import from each supported format, mixed imported/native behavior, idempotent replay, unauthorized access, uncertain/unmappable input, and how a source rule becomes **available versus adopted/active**.
5. Publish approved **reusable mapping decisions** as conversion rules; send genuinely ambiguous or contradictory cases to a bounded **Rules Lawyer review queue** with context and suggested alternatives. Approved rules are reused on later imports, with provenance and versioning, rather than requiring per-monster/per-book decisions.
6. Mark what the slice **does not yet support**. Later imported content can remain source-native/preserved and pending translation, but must **not** silently acquire guessed native outcomes.

Candidate order follows dependencies rather than publication/edition: abilities and competencies/roll modes; defenses/conditions/damage; action economy, attacks, maneuvers and criticals; equipment/creature traits; spellcasting/powers; classes, features and Level Bands; travel/environment and other modules. Use dependencies and tests to adjust order. **Imported stat blocks are composite content:** their attacks, traits, spells and conditions become convertible as those subsystems mature.

### Continuous tester feedback — after each usable subsystem, not only at full-system completion

For every completed and reviewed native subsystem, make a **separately authorized, limited tester-facing workflow** available when practical, without waiting for all later systems or a converted monster catalog:

1. Confirm the slice's supported behavior, deterministic tests, relevant conversion cases, known omissions and source permissions.
2. Expose **only the approved slice** to the intended tester accounts through an appropriate Tool/test harness; the full Rules Core remains hidden by default and Character Sheet need not become public.
3. Record test cases and feedback tied to a **ruleset version, converted source revision, homebrew/campaign configuration and reproduction steps**. Distinguish native rule defects, mapping/conversion defects, authorization issues, and usability feedback.
4. Resolve issues in the owning subsystem and update its documentation/tests/mapping rules before depending on it in later slices; if source semantics genuinely conflict, use Rules Lawyer review. A tester cannot modify the global published baseline merely by reporting a preference.
5. Allow optional tabletop/manual play tests as well as digital integration tests; early testing must not force use of Character Sheet or Block Initiative.

**This feedback loop operates throughout Phase 1–4.** It is not a request for a second service, public access to undeveloped systems, or general release of the previous Rules Core.

### Homebrew and campaign-specific rules — progressive capability

- **Author/upload:** Accept user-created rules/content as separately attributed, versioned homebrew packages or entries (subject to safe import formats, source ownership/licenses and normal access checks). Preserve original artifacts and mechanical interpretation separately. Homebrew can extend existing concepts or introduce new ones through declared, reviewed extension points; **do not permit unchecked arbitrary executable code** as a rule definition.
- **Share/access:** The uploader controls permitted sharing within the authorized product model. A homebrew package can be private, offered to other users, or eligible for selection by a campaign. Access to a package is **not** an effective-rules activation or a grant to re-share restricted material.
- **Select/adopt:** A user may explicitly enable eligible homebrew in their own applicable rules context; a DM/campaign owner chooses **shared campaign-effective rules** for the table. A personal selection must not override shared campaign mechanics or another user's state without appropriate authority.
- **Modify:** An authorized DM can change, disable, replace or supplement applicable rules **within their campaign**, while the global Dorks & Dice baseline, other campaigns, imported source truth and original homebrew remain unchanged. Treat changes as scoped rules and record their version, precedence and provenance.
- **Resolve:** The normal native evaluator composes versioned baseline rules, accessible activated contributions and authorized campaign choices, then returns deterministic effective behavior and why it applies. Conflicts are explicit; a rule must not become active only because someone uploaded it, and missing permission must not leak its contents.
- **Review:** Routine homebrew creation, selection and DM campaign overrides **do not require a Rules Lawyer**. Rules Lawyer input applies to global/common adjudication, complex source normalization or genuinely ambiguous shared conversions—not as a general gate for tables playing their own homebrew.
- **Test early:** Include private homebrew, optional shared homebrew, campaign override, unchanged other-campaign result, revoked access and conflicting selected rules among foundational/component acceptance cases; richer authoring/editor UI can follow as a later integration.

### Phase 3 — unified converted stat-block catalog and source access

Once the **needed stat-block rule families** have converters, implement the shared stat-block resolution/query path. **Both** (a) Dorks & Dice's **internal database of authored or previously imported stat blocks** and (b) **external source lookups/imports, as currently supported**, must reach the **same authoritative native stat-block projection**. Native-authored facts do not need an unnecessary external-edition translation; imported entries (whether cached internally or retrieved externally) pass through source interpretation and the appropriate **versioned Dorks & Dice conversions** before use.

- Preserve original external content and source identity/provenance; do not make external lookup a shortcut that bypasses conversion.
- Preserve source grants, licenses and appropriate stat-block-level authorization. A source's presence in a database does not automatically make it public or active.
- A converted stat block contains supported numeric/structural data plus **native-qualified actions, defenses, conditions, damage, features and effects**. Unknown mechanics remain explicitly unresolved or omitted from executable behavior; never pretend the raw text was fully converted.
- Support deterministic repeatable projection and **reconversion when a conversion rule improves**, without manufacturing new upstream source revisions, erasing deliberate Rules Lawyer decisions, or duplicating one physical source across users.
- Query details needed for Block Initiative include authorized monster/condition search, HP, initiative modifier, standard/Touch/Flat-Footed AC, CR (when sourced) and other supported combat statistics. Keep manual entry and graceful fallback when lookup is unavailable.

**Block Initiative integration occurs after this conversion-backed lookup is ready**, not as the first implementation phase merely because it is the sole tester/public Tool. Its turn/block engine already works independently, and native stat-block conversion can be built/tested behind dev access until appropriately complete.

### Phase 4 — progressively integrate deeper consumers

**Character Sheet** stays developer-only by default and can integrate with completed native slices without waiting for every optional module. **Rules Wiki** authors/displays reviewed native rules and provenance through its own private delegation boundary; it does not become a second mechanics executor. Consumer access may be granted **selectively to authorized testers for completed slices**, without exposing the entire tool or lifting other dev-only restrictions. Homebrew upload/selection and campaign-authorized rule editing should progress through the same effective-rules API without requiring global Rules Lawyer publication. Other Tool integration is driven by real contracts and need, not by an artificial global cutover.

### Phase 5 — verification and selectively restore access (no cutover)

Run component-level and cross-system gameplay acceptance, mixed-edition conversion/round-trip fixtures, **tester-feedback regressions**, campaign override/homebrew isolation and opt-in tests, authorization/visibility tests, bounded import/replay, performance and persistence tests. Re-enable only approved APIs/Tools when their specific required behavior is ready. This is **not** a parallel deployment, old-to-new routing switch, mandatory live data migration or wholesale restoration of public Rules Core.

## Rules Lawyer workload and conversion acceptance

- **No review needed for tested, unambiguous mappings:** The converter can handle routine 3.x/5.x equivalents, source variants and already-approved substitutions automatically, with traceable mapping rule/version and diagnostic output.
- **Review only meaningful exceptions:** Conflicting definitions or effective rule choices, unsupported new mechanics, materially different costs/procedures, low-confidence matches and novel source relationships require Rules Lawyer/DM attention as appropriate. A name match or a model confidence score alone does **not** justify auto-adoption.
- **Reusable outcomes:** An authorized Rules Lawyer decision can become a reusable scoped policy where its evidence and applicability justify that; record exception scope, source/revision and overrides. Subsequent imports must not recreate the same question for every source instance.
- **Safe defaults:** Preserve and index source-native truth, retain available-but-not-adopted content and show an unresolved conversion status. Do not execute guessed semantics or silently grant restricted source abilities.

## Evidence before an exposed integration or data change

- For incremental tester access: scoped entitlement, a documented working feature and its limitations, repeatable feedback captures and ruleset/source versions; **no automatic exposure of all Rules Core or Character Sheet features**.
- For homebrew/campaign customization: opt-in grants distinct from content access, authorized DM edit/publish scope, effective precedence/reproducibility, protection of restricted sources, and no cross-campaign effects.
- Foundation kernel native behavior, stable mechanic identities, normalized/effective source distinction and owner-approved examples.
- Per-system native rule tests **and conversion fixtures together**, including source-specific differences, mixed characters and denial cases.
- For Block Initiative: converted internal/external monster lookups; condition search; ACL-filtered projections; correct stats; persisted template identities; manual-entry fallback; independent initiative operation.
- Immutable provenance, proper duplicate-source treatment, replay/idempotence, explicit unresolved handling and reusable Rules Lawyer outcomes; no unauthorized source content exposure.
- Appropriate backups/recovery and schema tests for actual data changes; **no implicit database reset** or invented old/new cutover rehearsal.
- Relevant diagnostics/response budgets and explicit owner approval for merges, deployments, restored access or destructive persistence changes.

## Decisions deliberately not made in Gate 0

The replacement language/runtime, relational schema, DTOs, normalization vocabulary, canonical match thresholds, search/index strategy and worker topology follow the reviewed source evidence and native design. Reusing an old table/type is not a requirement. **Do not invent an old/new service migration or routing cutover.** Actual schema/data changes still require appropriate tests and owner authorization. New features in other Tools are out of scope except integration changes explicitly authorized for a consumer.
