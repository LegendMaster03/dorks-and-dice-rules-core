# Warlock modular class — Alpha 0.3.1 comparative review

**Classification:** Selected experimental native rules (Alpha 0.3.1), subject to revision; the reference record below remains historical comparison material.  
**Owner-selected test baseline:** October 25, 2026.  
**Scope:** Band 1 levels 1–20, Fiend Patron, four active Pacts, and a provisional 12-Invocation hybrid.  
**Native authority:** `data/rulesets/first-party-001/native/classes/base/warlock/warlock.json` plus the separately authored class features and one-entity-per-file Patron, Pact, and Invocation records in `classes/base/warlock/{patrons,pacts,invocations}/`. Global acquisition and selection rules are part of `warlock.json`; individual options declare their own prerequisites and mechanics.  
**Historical machine-readable comparison:** `data/reference/experiments/warlock-modular-alpha-0.3.1.json`.

## A/B question

The seven other classes promoted in the earlier snapshot use one coherent, selected class-level progression with attached features. Warlock deliberately tests a different way to organize one class: **Patron** (power source), **Pact** (manifestation/terms of the relationship), and **Invocations** (extra clauses bound to a specific Pact). These are **three complementary pillars of one Warlock**, not alternative source-edition versions. The first Warlock Pact is independently granted at level 1 and costs no Invocation choice.

The aim is to determine whether independently developed and specialized Pacts meaningfully improve build choice and mechanical identity without making complexity or total power disproportionate to the existing unified-class approach. No outcome has yet been selected. Do not replace the seven unified classes with a new modular pattern, and do not replace the modular Warlock with an imported combined source-class record.

The experimental packet is meant for a normal **5e or 5.5e host game**: replace that game's Warlock for the test; do not combine both Warlocks. Shared unfinished Dorks & Dice mechanics are not secretly filled in by this package. Host-game sources should not silently become authoritative first-party rules.

## What the test actually selects

| Feature or entitlement | Selected experimental model |
| --- | --- |
| Patron | A real supernatural Patron at level 1, including an inherited or concealed source; no direct contact or signed contract required |
| Pact | One of Blade, Chain, Tome, or Talisman, granted separately at level 1 |
| Invocations | One elective at level 1; attach each to an actual Pact with required qualifications and Patron acceptance |
| Elective Invocation acquisitions | Levels **1, 2, 4, 5, 7, 9, 11, 13, 15, 16, 18, 20** |
| Eldritch Blast | Automatic level-1 Warlock class feature; does not consume a known-cantrip choice |
| Pact development | Intrinsic Deepened Pact at level 5, without an Invocation tax; optional enhancements remain elective |
| Pact Magic | Short/Long Rest Pact slots; Mystic Arcanum remains a separately tracked higher-level entitlement |
| Fiend | Level-1 Dark One's Blessing and power source, level-6 Dark One's Own Luck, level-8 Infernal Retaliation with level-13 and -18 scaling, level-14 Hurl Through Hell |
| Fiend spell access | Default automatic merged 13-spell list, first spells at level 3; elective expanded-choice alternative remains a test counterfactual |
| Epic material | No Band 2 features or level-19 automatic Epic Boon |

The `warlock.json` progression is the authority for Invocation acquisition counts; the option directories are discovered by `parentClass` and entity `kind`, without manually repeated active-ID lists. Fiend milestones and Patron spell access are defined by `patrons/fiend.json`, not duplicated on each base-class level. The selected Invocation pool reconciles 5.5e shared options with retained 5e-only options; source-edition variants do not grant duplicate Invocations.

The level-by-level 20-row table in the machine-readable review file reflects the actual Alpha 0.3.1 playtest packet. Level-20 counts: 12 elective Invocations, four Pact slots at spell level 5, four ordinary cantrips (plus automatic Eldritch Blast), and 15 ordinary known spells (plus separate Patron and Pact grants).

### Four Pact roles and their level-5 improvement

- **Blade:** Pact weapon, with two attacks as part of its own Attack action at Warlock 5.
- **Chain:** Pact familiar, with two familiar attacks in its one commanded Attack action at Warlock 5. Companion command semantics are provisional relative to the host game.
- **Tome:** Book of Shadows, ritual/cantrip access, plus one slot-free cast per Long Rest of each of Animate Dead, Haste, and Call Lightning from Warlock 5.
- **Talisman:** Wearable/grantable aid to skill checks, with Shared Ward at Warlock 5. Shared Ward is a Reaction consuming an existing Talisman charge to reduce one damage event by 2d6 + Charisma modifier.

The broader Invocation ecosystem matters when evaluating a Pact, not just its automatic features. Several optional Pact-specific clauses are not in the present alpha packet and must not be interpreted as rejected.

### Fiend timing boundary

Infernal Retaliation is one level-8 Fiend feature with two alternative modes, consuming the same single Reaction activation and shared one-use-per-Long-Rest entitlement. At levels 8 / 13 / 18, fast healing is 1 / 2 / 5 HP per round; Flamewreath is 1d6 / 2d6 / 5d6 Fire damage per qualifying hit. The effect lasts 20 rounds. It does not revive an unconscious Warlock; when activated in advance, the healing mode may stabilize once at 0 HP but does not heal while at 0.

**Alpha procedural interpretations, not permanently approved shared rules:** healing at the start of the Warlock's turns (first on next turn; no healing at 0 HP), and Flamewreath damage after each resolved melee hit from within 5 feet. The approved high-level Fiend feature and selected numeric values persist while precise event timing is tested.

The separate 5.x Fiend level-10 damage resistance was retired, not duplicated. Hellfire Shield is not a free Fiend feature. Eldritch Blast's Patron imprint and later specialization should not be confused with an automatically selected invocation.

## Preserved alternatives and limitations

The 3.5e, 2014 5e, raw 2024 5.5e, Pact-adjusted 2024, and selected hybrid Invocation curves remain preserved in `data/reference/warlock-invocation-progression-band1-comparison.json`. A zero-Invocation-level-1 candidate is **only** an alternate balance probe. The approved default is one elective at level 1 plus a free Pact. A matching number of choices across editions does not establish equal power.

Alpha 0.3.1 omits **seven elective Invocations**, including the three dedicated Talisman options. The resulting playtest is informative about the core Talisman and generic clauses, but insufficient to conclude that the completed Talisman ecosystem is weak or strong. Star Chain/Seeker are disabled/deferred, not deleted. Additional Pact acquisition entitlement is not selected. Other pending shared mechanics include familiar behavior, crafting/feat interactions, spell resource integrations, and exact Fiend event timing.

## Approved feedback and rule restoration (October 30, 2026)

The approved feedback revision separates Talisman's Untrained Guidance and Fortunate Correction into paragraphs and adds Psychic alongside Necrotic and Radiant to Investment of the Chain Master's damage conversion. Animate Dead remains selected for Tome. This restoration includes those decisions from `content/warlock-early-playtest-feedback`; it does not revert to the annotated packet's older Investment text.

Native descriptions retain the packet's complete play procedures rather than abbreviated previews: weapon bonds, familiar command limits, book replacement and ritual access, talisman resources, Patron timing and spell-choice replacement, and Invocation procedures. The selected numerical progression and catalog remain unchanged.

Progression `improvements` describe changes to existing features. `relatedFeatures` entries contain a display name and `ruleId` linking to a separately owned rule, such as a Fiend milestone or spell grant. They are references, not extra base-class grants, and do not select a character's options. The Wiki presents both alongside the existing `featureGrants`.

Tester questions, comparisons, and illustrative builds remain separate from normative rule text. The complete operating procedure for the current alpha remains in each rule even where that procedure is provisional.

## Evaluation and acceptance

Compare representative builds at levels **1, 2, 4, 5, 7, 9, 11, 13, 15, 16, 18, and 20**, with Fiend and each of Blade/Chain/Tome/Talisman, against similarly equipped, single-progression reconciled classes in comparable encounters. Record action/reaction contention, spell resources, feature-combo viability, options actually worth selecting, and the time required to make meaningful character decisions. Ask for concrete examples of over- or underperformance and mechanics that needed adjudication. Do not collect a compulsory form.

A valid architecture decision must compare play outcomes and actual build incentives, not just the number of Invocations, intrinsic Pact features, or assumptions about a future completed ecosystem.

## Source and distribution boundaries

The owner's **`Dorks_and_Dice_Warlock_Alpha_Playtest_Packet.pdf`**, Alpha 0.3.1 (October 25, 2026, 19 pages), is the authoritative *playtest package* for this experiment, not an authoritative native rule catalog. The repository documents below govern the approved design directions and preserved decisions:

- `docs/rewrite/warlock-pact-pillar-reconciliation.md`
- `docs/rewrite/warlock-fiend-patron-consolidated-decision.md`
- `docs/rewrite/warlock-invocation-progression-band1-test-baseline.md`
- `docs/rewrite/warlock-pact-specific-invocation-conflict-resolution-proposal.md`
- `data/reference/warlock-invocation-progression-band1-comparison.json`

Warlock Alpha 0.3.1 is the **current selected native Warlock** even while its design is being tested. Imported edition candidates stay in `working/`; the historical experiment record stays in `reference/` with a distinct `experiment.*` ID, and that reference record itself is never runtime authority. The native Warlock entities are included in the catalog and its semantic fingerprint. The Wiki should display the selected native alpha as the current Warlock, with a clear playtest/version label, not as a finalized architecture decision.

The playtest packet contains CC-licensed SRD sources, historical non-SRD research references, and original first-party adaptations. Do not treat every historical non-SRD contribution as CC-licensed or freely distributable. Perform source-rights review separately before any external release.

