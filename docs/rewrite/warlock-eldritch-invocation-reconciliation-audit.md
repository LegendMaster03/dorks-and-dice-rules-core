# Warlock Eldritch Invocations — source inventory and reconciliation decision map

**Date:** October 25, 2026  
**Branch:** `feature/rules-core-rewrite`  
**Status:** **SOURCE REVIEW / OPEN DECISIONS. NO NEW OWNER RULINGS OR NATIVE EXECUTABLE MECHANICS.** Source values below are taken from the imported SRD 5.1 and SRD 5.2.1 records, not from speculative 3.5e catalog imports. The prior approved Pact/Patron architecture controls all future native allocations.

## Work order revised by owner — Pact-specific options first

**Current first pass completed:** [Pact-specific Invocation codification](warlock-pact-specific-invocation-codification.md) and [its machine-readable 16-record manifest](../../data/reference/warlock-pact-specific-invocations.json) cover **nine already-imported SRD options plus seven previously identified published non-SRD options** across Blade (5), Chain (4), Tome (4), and Talisman (3). All nine existing native SRD optional-feature files now carry explicit **required Pact-type attachment metadata** while preserving their original candidate definitions and `pending-native-reconciliation` status. Seven non-SRD options are independently paraphrased **reference-only**, not SRD imports or activated native abilities. **No additional Invocation budget was adopted.**

**Owner-directed order:**

1. **Codify** known Pact-specific Invocations (completed).
2. **Resolve** cross-edition mechanics, duplicate effects and outdated prerequisites **before** any general Invocation selection-budget decision (next work).
3. Decide between **further 3.5e Invocation research** and **native Invocation progression** **after conflict reconciliation**; do not silently choose one in advance.

This **supersedes** the earlier recommended ordering that began with Invocation entitlement progression. The existing 39-SRD-plus-3-Talisman first-pass catalog counts remain intact: indexing the other four non-SRD Pact-specific names **does not silently expand the playable 42-option first pass**.

## Current binding agreements (not up for silent reinterpretation)

1. **Patron, Pact and Invocation are separate pillars.** A Warlock acquires one **dedicated first Pact at level 1** without consuming an Invocation selection; base Pact acquisition is *not* selectable again as an ordinary Invocation.
2. **Every Invocation attaches to exactly one actual Pact agreement**, including generic Invocations with no Blade/Chain/Tome/Talisman-specific prerequisite. Options may additionally require a specific Pact, the **Patron of that Pact**, class features, character levels and accepted Patron terms. Do not test some other Pact/Patron's eligibility instead.
3. **Pact severance deactivates attached Invocations** but preserves historic acquisitions; any reinstatement/transfer requires valid receiving-Pact/Patron restrictions and terms. Automatic refunds and restoration timing remain unapproved. Additional simultaneously active Pact acquisition gates are deferred.
4. **Four active Pact choices:** Blade, Chain, Tome, Talisman. **Star Chain and Seeker remain disabled and archived**.
5. **Pact-level intrinsic grants already approved:** Blade gets a second Pact-weapon attack at Warlock 5; Chain familiar has its own extra attack at 5 (command semantics unresolved); Tome gets three specifically named slot-free level-3 spells at 5; Talisman gets Shared Ward at 5 and the nonstacking guidance/correction base at 1. None consumes Invocation selection.
6. **Fiend is the only in-scope runnable Patron**, though Invocation eligibility must support future Patron-restricted combinations without implementing those other Patrons. Hellfire Shield remains unresolved as a possible Fiend-related Invocation candidate, **not an automatic Patron grant**.
7. The **historical 3.5e Warlock Invocations** (Least/Lesser/Greater/Dark, at-will spell-like, blast shapes and essences) are **historical reference only** in this first SRD-first slice, not active additions to the Invocation catalog.

## Source counts — crucial distinction

| Imported source | Count | Meaning |
| --- | ---: | --- |
| 2014 SRD 5.1 | **32** | Eldritch Invocation entries; the three Pact Boons were **separate**, not among these 32 |
| 2024 SRD 5.2.1 | **28** | **25 ordinary Invocations + three original Pacts**, which source 2024 now calls Invocations |
| Named overlap | **18** | Names present in both SRD Invocation catalogs; **same name does not guarantee same effects or prerequisites** |
| 2014-only | **14** | Present in SRD 5.1 but absent as an Invocation in SRD 5.2.1 |
| 2024-only | **7** | Present in SRD 5.2.1 but absent in SRD 5.1 |
| Distinct SRD Invocation names, excluding base Pacts | **39** | 18 + 14 + 7 |
| Additional Talisman-specific published options | **3** | Rebuke, Protection, Bond, from non-SRD *Tasha's Cauldron of Everything* (2020), previously approved **to remain optional clauses**, but exact native texts/implementation still need review |
| **Total immediate named candidate pool** | **42** | **39 imported SRD + 3 Talisman**, NOT 42 approved/native-working mechanics |

**Do not add the 2014 + 2024 source row totals together:** shared names are reconciled to one native choice. The source inventory's 42 `options` historically include three **base Pacts**, while the **candidate Invocation** total of 42 **excludes base Pacts** and adds Talisman's three non-SRD options. These are two different 42-item sets.

## Invocation selection budget — unresolved, not silently assigned

The imported source-level tables provide the following **maximum Invocation selections total** before any native conversion:

| Warlock level | 2014 Invocation capacity | 2024 Invocation capacity (includes any Pact taken as Invocation) |
| --- | ---: | ---: |
| 1 | 0 | 1 |
| 2 | 2 | 3 |
| 5 | 3 | 5 |
| 7 | 4 | 6 |
| 9 | 5 | 7 |
| 12 | 6 | 8 |
| 15 | 7 | 9 |
| 18 | 8 | 10 |
| 20 | 8 | 10 |

**Decisive interaction with approved architecture:** In 2024, the Pact can consume an Invocation entitlement. In native Dorks & Dice, the **first Pact never does**. Simply copying the 2024 column as the number of **ordinary** Invocations in addition to the separately granted Pact would give more total benefits than a typical 2024 build that purchased a Pact with one of its ten selections. This might be acceptable given the deliberate three-pillar redesign, but **the exact native Invocation counts and any compensating entitlement changes are not yet approved**. In 2014 Invocations first arrive at class level 2 (two selections); 2024 makes one available at level 1, which is consistent with the owner's desire to preserve **early Invocation access**.

**Potential choices for owner review later:** retain the 2024 Invocation-count table with the free dedicated Pact (max 10 ordinary Invocation choices), adapt a reduced budget to approximate 2024 total options (e.g. 9 ordinary at level 18 with an explicit new level-1 treatment), or establish a custom schedule after comparative build testing. **This audit does not select any schedule, grant extra options or change class data.**

## Exact 39-name SRD catalog and explicit Pact-type prerequisites

¹ `Shared eligibility` means the **source entry does not require a particular Pact type**. In native Dorks & Dice it **still must attach to a specific Pact** and satisfy the Patron's terms and any other prerequisites. `1*` means **no numeric Warlock-level prerequisite in that edition's source entry**, not that the 2014 class could actually choose Invocations at level 1 (its first selections occur at level 2). Historical spell/cantrip and feature prerequisites still apply.

| Named Invocation | Native Pact gate in source¹ | 2014 min Warlock level | 2024 min Warlock level | Available in |
| --- | --- | ---: | ---: | --- |
| Agonizing Blast | Shared eligibility¹ | 1* | 2 | Both |
| Armor of Shadows | Shared eligibility¹ | 1* | 1* | Both |
| Ascendant Step | Shared eligibility¹ | 9 | 5 | Both |
| Beast Speech | Shared eligibility¹ | 1* | — | 2014-only |
| Beguiling Influence | Shared eligibility¹ | 1* | — | 2014-only |
| Bewitching Whispers | Shared eligibility¹ | 7 | — | 2014-only |
| Book of Ancient Secrets | Tome | 1* | — | 2014-only |
| Chains of Carceri | Chain | 15 | — | 2014-only |
| Devil's Sight | Shared eligibility¹ | 1* | 2 | Both |
| Devouring Blade | Blade | — | 12 | 2024-only |
| Dreadful Word | Shared eligibility¹ | 7 | — | 2014-only |
| Eldritch Mind | Shared eligibility¹ | — | 1* | 2024-only |
| Eldritch Sight | Shared eligibility¹ | 1* | — | 2014-only |
| Eldritch Smite | Blade | — | 5 | 2024-only |
| Eldritch Spear | Shared eligibility¹ | 1* | 2 | Both |
| Eyes of the Rune Keeper | Shared eligibility¹ | 1* | — | 2014-only |
| Fiendish Vigor | Shared eligibility¹ | 1* | 2 | Both |
| Gaze of Two Minds | Shared eligibility¹ | 1* | 5 | Both |
| Gift of the Depths | Shared eligibility¹ | — | 5 | 2024-only |
| Gift of the Protectors | Tome | — | 9 | 2024-only |
| Investment of the Chain Master | Chain | — | 5 | 2024-only |
| Lessons of the First Ones | Shared eligibility¹ | — | 2 | 2024-only |
| Lifedrinker | Blade | 12 | 9 | Both |
| Mask of Many Faces | Shared eligibility¹ | 1* | 2 | Both |
| Master of Myriad Forms | Shared eligibility¹ | 15 | 5 | Both |
| Minions of Chaos | Shared eligibility¹ | 9 | — | 2014-only |
| Mire the Mind | Shared eligibility¹ | 5 | — | 2014-only |
| Misty Visions | Shared eligibility¹ | 1* | 2 | Both |
| One with Shadows | Shared eligibility¹ | 5 | 5 | Both |
| Otherworldly Leap | Shared eligibility¹ | 9 | 2 | Both |
| Repelling Blast | Shared eligibility¹ | 1* | 2 | Both |
| Sculptor of Flesh | Shared eligibility¹ | 7 | — | 2014-only |
| Sign of Ill Omen | Shared eligibility¹ | 5 | — | 2014-only |
| Thief of Five Fates | Shared eligibility¹ | 1* | — | 2014-only |
| Thirsting Blade | Blade | 5 | 5 | Both |
| Visions of Distant Realms | Shared eligibility¹ | 15 | 9 | Both |
| Voice of the Chain Master | Chain | 1* | — | 2014-only |
| Whispers of the Grave | Shared eligibility¹ | 9 | 7 | Both |
| Witch Sight | Shared eligibility¹ | 15 | 15 | Both |

**Additional non-SRD Talisman options** (never inserted into the immutable SRD option source pack):

| Name | Pact requirement | Published minimum Warlock level | Proposed native default |
| --- | --- | ---: | --- |
| Rebuke of the Talisman | Talisman | None beyond Pact | Optional Reaction retaliation/forced movement; conflicts for Reaction with approved Shared Ward |
| Protection of the Talisman | Talisman | 7 | Optional +1d4 after a failed saving throw, with **its own** PB/Long Rest use pool |
| Bond of the Talisman | Talisman | 12 | Optional linked Warlock/wearer teleportation, with **its own** PB/Long Rest use pool |

These three are **separate elective Invocation branches** under the approved Talisman decision. No Pact of the Talisman base grants them free.

## High-priority mechanical reconciliations

### A. Core blade duplicate and Invocation prerequisites

- **Thirsting Blade (2014/2024, minimum Warlock 5):** Source two Pact-weapon attacks is **already intrinsic to native Blade at Warlock 5**; offering the unchanged Invocation consumes a slot for **zero new effect**. **Need ruling:** retire source-equivalent from native selectable pool, maintain historical projection only, or independently repurpose with *new* feature/cost. **Do not award a second extra attack or a replacement free Invocation without owner approval**.
- **Devouring Blade (2024, minimum Warlock 12):** Source requires **Thirsting Blade** to grant three total Pact-weapon attacks. If Thirsting Blade ceases to be selectable, its native requirement must be redirected to the **intrinsic Blade level-5 second attack** (or another approved prerequisite) **only if Devouring Blade itself is adopted**. The third attack remains elective/unapproved.
- **Lifedrinker (both sources):** 2014 **level 12**, extra **CHA Necrotic on each hit**. 2024 **level 9**, **once per turn** extra **1d6 Necrotic/Psychic/Radiant**, and a Hit Die-based self-healing option. These are different action/resource/damage profiles, not equivalent text changes; native choice unapproved.
- **Eldritch Smite (2024, minimum Warlock 5):** Pact Magic slot-spending one-hit burst and possible Prone; elective, **not a Pact base effect**. Clarify 3.5 touch-AC/hit/crit interactions separately before implementation.
- **Improved Pact Weapon** (*Xanathar's*, non-SRD) is referenced in external Pact research, but is **outside the current exact 42-invocation first-pass candidate pool**. Its spellcasting-focus aspect already overlaps our Blade core; no new free grant.

### B. Chain: existing core companion actions versus investment choice

- **Voice of the Chain Master (2014):** Communication/senses/remote speaking as a **Pact-restricted elective Invocation**.
- **Chains of Carceri (2014, Warlock 15):** Specialized at-will creature-control effect as a **higher-level elective Invocation**.
- **Investment of the Chain Master (2024, Warlock 5):** Packs Bonus Action familiar Attack command, alternative movement, attack damage conversion, save DC and damage mitigation. **Potential mandatory-tax conflict:** Native Chain's familiar already gains an intrinsic second attack at level 5, but the **unaugmented base familiar Attack-action/command rules are unresolved**. Confirm it has usable legal attacks *without* Investment before deciding how much remains elective. Do not double-count familiar's extra attack, grant extra player attacks, or copy an unrelated creature's action economy.

### C. Tome: intrinsic book functionality versus elective ritual mastery

- **Book of Ancient Secrets (2014):** Level-1 ritual acquisition and **higher-level discovered ritual transcription**; 2024's base Tome already grants **two 1st-level rituals**. Merely providing those same two via an elective Invocation would be redundant, but elective **advanced ritual collection** could still be meaningful. Do not auto-grant all ritual transcription without owner decision.
- **Gift of the Protectors (2024, Warlock 9):** Tome's elective names-in-book protection against reduction to 0 HP, **not** one of its three intrinsic level-5 free spells. Still independent and optional.
- **Aspect of the Moon** (*Xanathar's*, non-SRD) and **Far Scribe** (*Tasha's*, non-SRD) were identified in Pact research but are **not in the current exact first-pass source pool**. Preserve their existence for later catalog expansion.

### D. Talisman: protection without duplicate resource grants

Native approved level-1 **Untrained Guidance / Fortunate Correction** share a nonstacking check benefit; charge-limited correction and level-5 **Shared Ward** use the same Warlock-PB/Long Rest pool. **Rebuke** (Reaction retaliation), **Protection** (saving throws), and **Bond** (teleport) remain **three elective Invocation options**. A Talisman Warlock may attach generic Invocations alongside these three. Do not use a second Pact charge pool when granting an option that published an **independent** use pool.

### E. Shared 2014↔2024 effects: especially blast and cantrip targeting

| Name | 2014 source | 2024 source | Decision issue |
| --- | --- | --- | --- |
| **Agonizing Blast** | Adds CHA to damage from **Eldritch Blast** | Select an eligible damaging **Warlock cantrip**, adds CHA to its damage rolls; **repeatable** for different eligible cantrips; level 2 | Whether native broadens the target cantrip and supports repeat selection; 2024 excludes level-1 selection by prerequisite |
| **Repelling Blast** | On Eldritch Blast hit, push target up to 10 feet; no listed size cap in this Invocation | Eligible attack-roll Warlock cantrip, **Large or smaller** target, up to 10-foot push; repeatable; level 2 | Cantrip scope, creature-size restriction and per-hit push with multiple attacks/beams |
| **Eldritch Spear** | Eldritch Blast range becomes **300 feet** | Eligible damaging Warlock cantrip with >=10-foot range; range increases by **30 × Warlock class level**; repeatable; level 2 | Fixed vs level-scaled range, cantrip choice and repeatability |
| **Fiendish Vigor** | `False Life` self at will, at level 1 spell strength, roll normal Temp HP | `False Life` self, no slot, **maximize the die** | Max-Temp HP improvement, spell version interactions; distinct from Fiend's Dark One's Blessing |
| **Gaze of Two Minds** | Touch willing **humanoid**, use **Action** to project senses/extend | Touch willing **creature**, **Bonus Action** to project/maintain; other 2024 casting interactions | Wider creature scope and major action-economy change |
| **One with Shadows** | Stand in dim/dark, spend Action, invisibility breaks with moving/actions/reactions | In dim/dark, cast **Invisibility** on self without slot | Mobility and concentration/casting interactions |
| **Witch Sight** | Reveal shapeshifters/illusions within 30 feet and line of sight | **Truesight 30 feet** | True-sight scope vs narrowly targeted detection |
| **Lifedrinker** | CHA Necrotic on **each hit**, level 12 | 1d6 type choice **once per turn**, Hit-Die self-heal, level 9 | Native damage frequency and self-heal, especially two/three attacks |
| **Ascendant Step** | Self `Levitate` at Warlock **9** | Self `Levitate` at Warlock **5** | Level changes and source spell scope |
| **Otherworldly Leap** | Self `Jump` at Warlock **9** | Self `Jump` at Warlock **2** | Level changes and source spell scope |

**More shared Invocations** (Armor of Shadows, Devil's Sight, Mask of Many Faces, Master of Myriad Forms, Misty Visions, Visions of Distant Realms, Whispers of the Grave, Thirsting Blade) need a per-option semantic and prerequisite audit; do **not** assume identical texts merely because names match.

### F. Legacy 2014-only and modern 2024-only families

**2014-only SRD 14 (preserve, do not silently delete):** Beast Speech, Beguiling Influence, Bewitching Whispers, Book of Ancient Secrets, Chains of Carceri, Dreadful Word, Eldritch Sight, Eyes of the Rune Keeper, Minions of Chaos, Mire the Mind, Sculptor of Flesh, Sign of Ill Omen, Thief of Five Fates, Voice of the Chain Master.

**2024-only SRD 7 (evaluate independently):** Devouring Blade, Eldritch Mind, Eldritch Smite, Gift of the Depths, Gift of the Protectors, Investment of the Chain Master, Lessons of the First Ones.

Many 2014 spell-effect Invocations use **Warlock Pact Magic slots and/or limited once-per-Long-Rest usage**, while 2024's Invocation overhaul moves away from spending Pact Magic slots except the Eldritch Smite exception. Their name availability, spell-list changes and old slots-versus-new-at-will budgets require a dedicated family review, *not deletion by absence from 2024*. In particular, distinguish an invocation that grants a new **ability to cast a spell** from one that adds that spell to normal **Warlock spells known**, and from one that grants a **slot-free use**.

### G. 3.5e tiered invocation subsystem (deferred catalog)

Historical `Complete Arcane` Warlock invocations were **spell-like at-will** abilities with grades **Least at Warlock 1; Lesser 6; Greater 11; Dark 16**, and included **blast shapes**, **eldritch essences** and other powers. The 3.5 errata labels Eldritch Blast a *free, automatically granted* invocation, not a purchased selection. Native Eldritch Blast automatic at Warlock 1 is already approved; **the full 3.5e non-SRD invocation catalog remains outside the current first executable vertical slice**. We can design the adapter/reconciliation and research it later without silently importing full non-SRD invocations.

The `Hellfire Shield` from the consolidated Fiend decision is **also a separate non-SRD unresolved acquisition issue** (ordinary Fiend-restricted Invocation vs alternative Patron grant), **not** automatically added to the 42 candidates or to a level-8 Fiend feature.

## Suggested stepwise reconciliation, before activating any native Invocation

1. **Define the native Invocation entitlement schedule and replacement rules** in the already-approved separate-Pact economy. Decide first whether 2024's max 10 are truly **ordinary selections plus a free initial Pact**, and whether level-1 general Invocations that were previously 2014 level-less can be chosen at level 1 or should honor newer level-2 gate.
2. **Resolve obvious duplicate and prerequisites**: Thirsting Blade (now intrinsic), Devouring Blade dependency, Tome ritual duplication, Chain command baseline.
3. **Reconcile blast/cantrip modifiers together**, starting with Agonizing Blast, Repelling Blast and Eldritch Spear. Their 2024 broadening, selection repeatability, old/new level gates and multiple beams are closely related.
4. **Reconcile the 18 shared-name abilities** as a source-to-native crosswalk; prioritize mechanical forks like Lifedrinker, Fiendish Vigor, Gaze of Two Minds, One with Shadows, Witch Sight.
5. **Recover 14 removed 2014 Invocations** carefully; classify slot-spending, at-will utility, redundant ordinary spell options and compatibility with the Warlock's no-prepared-spell policy.
6. **Review the seven 2024-exclusive Invocations** (including former Pact prerequisites) and the three non-SRD Talisman elective options.
7. **Test four Fiend + Pact builds**, maintaining **identical Invocation selection budgets** across Blade, Chain, Tome, Talisman. Check level 1/2/5/9/12/15/18/20, separate source entitlements, missing prerequisites, linked-Pact break/restoration, repeated Invocation rules, and no double-grants from guided presets.
8. **Review optional non-SRD additions separately** (other published Invocations, Hellfire, future 3.5 blast shapes/essences) only after the source-backed first pass, or sooner through a specific owner exception.

**Unresolved questions that require owner approval, not automatic change:** Invocation count/levels, which 2014-only options return, which 2024 rewrites govern shared entries, non-SRD spell/feat restrictions, whether old redundant choices are removed or repurposed, and all new Pact/Patron-specific clauses.

## Sources and validation anchors

- **Imported SRD feature entries:** `data/reference/srd/structured/optionalfeatures.json`
- **Imported 20-level Warlock tables:** `data/reference/srd/structured/class/class-warlock.json`
- **Canonical source-based inventory:** `data/reference/srd/warlock-srd-slice-inventory.json` (the source inventory's 42 entries include three **Pacts**, unlike the 42 candidate Invocation names analyzed here)
- **Owner-approved Pact affiliation and severance:** `docs/rewrite/warlock-pact-pillar-reconciliation.md`
- **Four Pact depth and Talisman:** `docs/rewrite/warlock-four-pact-pillar-viability-audit.md`, `docs/rewrite/warlock-talisman-pact-strengthening-proposal.md`
- **Historical 3.5 class-only research:** `data/reference/3x/warlock-complete-arcane-research.json`
- **Fiend Hellfire disposition:** `docs/rewrite/warlock-fiend-patron-consolidated-decision.md`
- **2024 official overview:** https://www.dndbeyond.com/posts/1756-2024-warlock-vs-2014-warlock-whats-new

**No rules or test expectations are altered by creating this review. No merge is authorized.**
