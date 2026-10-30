# Rules Wiki UI handoff — ongoing content classes, monsters, and A/B review

**Owning project:** Rules Wiki UI (not Rules Content)  
**Content source:** `first-party-001`, branch `content/coherent-rule-promotion-wave-1`  
**Status:** Handoff only. The content change does not modify frontend code.

## Native categories currently present

At the current reviewed snapshot, the native catalog includes:

- `class` × **7** (`class.barbarian`, `class.bard`, `class.cleric`, `class.druid`, `class.rogue`, `class.sorcerer`, `class.wizard`).
- `classFeature` × **161**. Their `parentClass` relates them to a class; some are **options** rather than auto-granted features.
- `monster` × **16**, each with selected original stat-block values and `statBlockFoundationId: "rule.monster-statblock-foundation"`.
- `monsterFamily` × **3**; `memberIds` relate Goblin, Hobgoblin and Bugbear role variants.

Read via the existing authenticated/authorized native consumer or private reference contract appropriate to the Wiki. Do not point the Wiki at `working/`, GitHub raw files, or the source SRD candidate collections.

## Class presentation

Support a Band 1 **1–20 progression table** sourced from `class.definition.progression[]`, numeric and resource columns when present, and linked feature details. Resolve `featureGrants` against both standalone `classFeature` records and embedded `class.definition.inlineFeatures[]`; preserve display of features such as Bardic Knowledge, historical Druid grants and selected Cleric turning variations.

Separate **automatic** feature grants from `choiceOnlyFeatureIds`, `selectableOptionFeatures`, ordinary improvements and subclass milestones. Where the selected class omits subclass grants, indicate later work instead of linking an imported subclass candidate as granted. Do not award Epic Boons solely from class level 19. For shared-system dependencies (familiar path, feats, skill points, spell points) show that the declarative ruling exists, but do not simulate unavailable execution.

## Monster presentation

Present the selected stat block with ordinary AC, HP, speed, attributes, ability saves, skills, proficiencies, actions, bonus actions, Reactions, legendary actions, spellcasting and tactical role where defined. Where the shared monster foundation defines Touch and Flat-Footed AC or Fortitude/Reflex/Will saving throws but the individual monster lacks a derived numeric result, **show the category as uncalculated or omitted**, not as equal to the ordinary/ability save. Preserve existing attack DCs, dice, recharge and action economy. Group `familyId` variants by `monsterFamily.memberIds` and never collapse distinct Goblin/Hobgoblin/Bugbear roles into one toggle. A monster with different legal tactics but the same mechanics does not require a new identity.

Acceptance examples: Goblin Minion, Warrior and Boss appear as separate stat blocks; Hobgoblin Warrior retains Pack Tactics and Martial Advantage; Priest has its retained spellcasting effects; Aboleth and Lich display their legendary actions.

## Native conditions — always included in catalog navigation

The original catalog already contains **29** `condition` definitions and **one** `leveled-condition` (Exhaustion). These are settled, coherent rules ready for read-only Wiki review, not new imported source variants. The same content branch now includes complete descriptions for all 30 and repairs obsolete path references in `rule.conditions`.

- List the condition entities identified in `rule.conditions.conditionIds`, linking each to the canonical ID. Index ordinary conditions and Exhaustion together while respecting the distinct `kind`.
- Render the authored `description` and structured `effects` without fabricating executable outcomes. Preserve each `when` predicate, `appliesConditions` and `requiresConditions` relationship, `endsWhen` triggers, and `levelRules`/`levels` for Exhaustion.
- Link `rule.defenses` and `rule.pin-and-held` when the selected condition refers to their mechanics. Do not mistake denials of positive Dexterity/dodge defense for automatically receiving the complete Flat-Footed condition, or repeat inherited penalties.
- Preserve approved distinctions: Stunned has half Speed; Petrified does not necessarily remove awareness; Held differs from Pinned; Dazed differs from Incapacitated; Fatigued differs from Exhaustion; Shaken/Frightened/Panicked/Cowering have distinct effects; Wet retains its existing removal triggers.
- Do not display deferred concepts (Confused, Staggered, Turned, historical 3.x Exhausted) as active conditions or imply automatic fear escalation. These remain research decisions, not missing client defaults.

**Condition-specific acceptance:** All 30 linked identities have readable summaries, five newly authored summaries match their mechanical effect records, the six Exhaustion levels display cumulatively, condition dependencies are navigable without recursion or duplicate penalty application, and no condition viewer reads `working/` or public-unapproved data. The existing authenticated private reference API provides the native catalog; no new API is required solely to show these definitions. Render catalog-driven lists and category facets rather than expecting the snapshot's class, monster or condition counts to remain fixed. Verify access and entity detail behavior against the reviewed deployed SHA before concluding UI acceptance.

Detailed content-level contract: `docs/content-promotion/conditions-readiness.md`.

## Warlock experimental A/B review

The Warlock modular package is **not in the native catalog**. `docs/content-promotion/warlock-modular-alpha-0.3.1.md` and `data/reference/experiments/warlock-modular-alpha-0.3.1.json` define the selected test. A new Core Code project contract is required to serve it securely. Do **not** make the Wiki read `data/reference/` or `working/` as a runtime fallback.

When that contract exists, present a clearly distinct **Alpha 0.3.1 experiment** view: Patron / Pact / Invocations overview, table levels 1–20, separate free first Pact versus elective Invocation budget, Fiend vertical slice, four Pacts with their intrinsic level-5 upgrades, and the **five** historical/reference Invocation curves for comparison. Mark the other curves as comparisons, not simultaneous active entitlements. Mark Flamewreath/healing timing as provisional, the seven missing elective options as a limitation, and Star Chain/Seeker as deferred. A/B feedback should contrast the unified-class approach and the modular Warlock without mutating either baseline automatically.

## Acceptance criteria

- The Wiki can navigate seven 1–20 classes, resolve their features, and distinguish selectable options from unconditional grants.
- The Wiki can navigate 16 monster identities across 3 role families and render complex stat blocks without inventing derived numbers.
- The experiment cannot appear as a normal native `class.warlock`, cannot overwrite a native record or leak to public consumers.
- Wiki browser acceptance tests cover categorization, previous conditions, search/pagination, experiment labeling and absence of `working/` fallback.
- A reviewed content release displays the **new** fingerprint and SHA in diagnostics; a failed release can revert to the prior content view.
