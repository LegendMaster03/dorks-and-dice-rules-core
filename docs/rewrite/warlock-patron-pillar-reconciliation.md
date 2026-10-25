# Warlock Pillar 1 — Patron: SRD Fiend deconstruction and reconciliation

**Stage:** source-complete Patron analysis and automated conversion crosswalk; **not yet an owner-approved reconciled native Fiend**, playable subclass, or runtime implementation. This pass intentionally resolves **identity** (already approved), inventories and compares **all Fiend mechanics** in SRD 5.1/5.2.1, and identifies remaining rulings. It does not silently choose an edition where rules differ.

## Structural decision applied

Warlock has **three class-specific pillars: Patron, Pacts, and Eldritch Invocations**. Their interaction produces progression, and guided subclass presets are another means of selecting the same abilities, *not* an independent pillar.

- **Patron identity or power-source connection belongs at Warlock class level 1.** The character may have a direct or inherited connection to a Patron; an inherited Patron is still a Patron, even without a personal agreement or a known individual name. Fiend is the initial SRD example.
- **Patron features are separate from identity and can be acquired later.** The 2014 Fiend presents subclass features at 1/6/10/14, and 2024 at 3/6/10/14; those are source projections, not two gameplay engines. The 2024 subclass first appears at 3 in source, but native Patron identity remains known from level 1.
- **Deconstruction yields five coherent part IDs plus identity**: spell access, Dark One's Blessing, Dark One's Own Luck, Fiend damage resistance, and Hurl Through Hell. These names identify rule capabilities, not necessarily five independent player purchases. Prerequisites, packaging, feature timing, and advancement entitlements remain to be reconciled.
- **Guided source projections** use the same part IDs and grant each once per progression. They do not provide duplicate subclass or part entitlements. The actual **single authoritative native Fiend preset** cannot be finalized until the open rulings are approved; SRD51/SRD52 projections are comparison and compatibility fixtures.

The machine-readable source-preserving crosswalk is `data/reference/srd/warlock-fiend-patron-crosswalk.json`. Imported source records remain untouched at `data/reference/srd/structured/class/class-warlock.json` and staged native candidates remain `pending-native-reconciliation`.

## Patron relationship and cross-pillar eligibility — owner design direction (2026-10-24)

**A Patron does not require the Warlock to have personally entered a contract.** A Warlock who inherits a connection to a supernatural being still has a **Patron**: the relationship and its consequences can be inherited, even if the Warlock has never met, communicated with, or agreed to serve that being. The distinction is **how the Patron connection was acquired**, not whether the Patron exists.

- **Direct Patron connection:** the Warlock personally makes a bargain, receives a blessing, is chosen, or develops another relationship with the power-granting being.
- **Inherited Patron connection:** an ancestor's bargain, a family legacy, or supernatural ancestry links the Warlock to the Patron or Patron type. No personal agreement, knowledge of the being's name, or ongoing contact is inherently required.
- **Other indirect Patron connection:** supernatural forces designate the Warlock as a conduit or pass along the effects of a Patron's power without the character personally negotiating for it.

**Do not misclassify inherited power as Patronless.** A specific Patron relationship can exist without a character knowing the Patron's identity; the level-1 build records the relevant power-source identity or type, while discovery of an individual name may remain narrative.

**A genuinely Patronless Warlock is a separate, unresolved design question.** The fact that 3.5e allows inherited or involuntary power does **not** by itself prove a Warlock lacks a Patron or any supernatural power source. Do not introduce a Patronless mechanical option or declare it universally valid from these examples. Do not require a named NPC object, active contact, or signed contract when validating a legitimate inherited connection.

**Narrative relationship is separate from mechanical entitlement.** The manner of receiving power does not automatically add or remove Fiend, Archfey, or other Patron features. A guided Fiend preset establishes a Fiend power-source relationship but does not mandate a personally signed deal. Patron feature acquisition still follows its approved level/selection/compatibility rules.

**Cross-pillar prerequisites are supported by design.** A Pact choice or improvement may require an eligible Patron category/trait/feature, an Invocation may require a specific Pact or Patron part, and a Patron feature may depend on an existing Pact/Invocation. At the shared Rules Core level, store these as explicit **typed prerequisites and dependency relationships** rather than hard-coded names or impossible one-way pillar hierarchies. The character builder must explain incompatible or unavailable choices and preserve the same restrictions for guided presets and custom builds.

- Prerequisites may reference **source traits** (e.g., a fiendish or otherworldly source), **specific unlocked parts**, or Warlock class levels. A source trait does not imply a particular named NPC or a contract.
- Cross-pillar restrictions should be attached **only when mechanically or narratively justified for the specific option**. Do **not** restrict all Blade/Chain/Tome Pacts to particular Patrons merely because the architecture permits dependencies.
- Never silently waive a prerequisite for guided presets. If an option changes later, validate acquired dependent parts and provide a clear replacement or grandfathering rule rather than creating an invalid invisible state.
- The first SRD Fiend slice must show at least one **synthetic, non-playable compatibility fixture** exercising both an allowed and a denied Patron-dependent Pact/Invocation option. This tests generic eligibility without authoring an unsupported new playable Pact or Patron.

**Hexblade and Vestige remain open base-class-versus-Warlock classifications.** Their historical equivalents—the 3.5e Hexblade and Binder—were separate base classes, while 5.x treats the concepts as Warlock Patrons. Evaluate *each* for its distinct gameplay loop, specialized resources, progression independence, overlap with existing Warlock Pacts/Invocations, and ability to reconstruct its published Warlock preset. Neither is pre-committed to class status. This review is **separate from prestige classification**: these are base-class-versus-Warlock membership questions, not automatically 3.5e prestige cases.

**Why Fiend is the correct first Patron case:** Fiend has a named 2014/2024 SRD subclass with a strong historical 3.5e Warlock fiendish-origin/heritage precedent. It can validate direct feature reconciliation without needing to invent modern subclasses for historical sources. Historical non-SRD Fiendish Heritage and Hellfire options remain **reference-only**, not automatically available selectable content.

## Cross-edition Patron landscape beyond SRD Fiend

A separate [comparison of the ten published 5.x Patrons with 3.5e Warlock power sources, heritage feats, Binder, Hexblade, and related class material](warlock-patron-3x-5x-comparison.md) is recorded as **reference research**. 3.5e did **not** offer a ten-Patron subclass menu. This compatibility survey does not expand the current playable SRD-only Fiend implementation. Especially important future tests include Hexblade–Blade Pact overlap, Genie variable elemental subtypes, the Vestige companion, and distinguishing Undead from Undying.

## Verified source feature inventory

| Mechanic | SRD 5.1 (2014) | SRD 5.2.1 (2024) | Native reconciliation issue |
| --- | --- | --- | --- |
| Patron identity | Fiend selected at level 1 | Fiend subclass selected at level 3 | **Identity at level 1 is approved; early mechanical grant is open** |
| Fiend spells | Ten additions to options for Warlock spells (not automatically known) | Ten automatically available spells, granted at Warlock levels 3/5/7/9 | Grant semantics and lists differ, native no-daily-preparation policy applies |
| Dark One's Blessing | Level 1; Warlock reduces hostile creature to 0 HP | Level 3; also triggers when another defeats enemy within 10 feet | Trigger and first-grant level |
| Dark One's Own Luck | Level 6; +1d10 ability check or saving throw; 1 use per Short/Long Rest | Level 6; +1d10; Charisma-modifier uses (min 1) per Long Rest, once per roll | Recovery and number of uses |
| Fiendish Resilience (Patron) | Level 10; type selected on rest; magical/silvered weapons bypass | Level 10; selected type cannot be Force; no magical/silvered bypass | Damage restriction/bypass; **not** the historical base-class healing feature |
| Hurl Through Hell | Level 14; attack hit, no save; 10d10 Psychic on return for non-Fiends, Long Rest recharge | Level 14; Charisma save; 8d10 Psychic on failure for non-Fiends and Incapacitated until return; expenditure of Pact slot restores use | Save, damage/timing, status and recovery |

**Shared facts do not automatically approve numerical reconciliation.** In both sources Luck modifies an ability check or save by 1d10; the Patron resistance is selected after a rest; Hurl Through Hell temporarily sends a creature away. Differences in costs and timing alter gameplay, so a native ruling is required.

### Spell lists are not identical

- **Shared across both lists:** Burning Hands, Command, Scorching Ray, Fireball, Stinking Cloud, Fire Shield, Wall of Fire.
- **2014-only:** Blindness/Deafness, Flame Strike, Hallow.
- **2024-only:** Suggestion, Geas, Insect Plague.

Thus there are **13 distinct spell names** across the two sources, but it would be incorrect to grant all 13 automatically without an explicit ruling. In 2014, Fiend spells are *eligible additions*; in 2024 they are source *always-prepared grants*. Native Dorks & Dice has removed daily spell preparation, but still distinguishes learned/access entitlement and class-specific known counts. This must be reconciled against the native spell-access policy, not solved by blindly adding a second list.

## Approved historical interaction

The already approved 3.5e Warlock **base-class Fiendish Resilience** heals 1/2/5 HP per round at class levels 8/13/18 using a one-Long-Rest recharge. The Fiend **Patron feature** called Fiendish Resilience is a resistance to a selected damage type from level 10. These are **two separate mechanics** despite the same display name: never merge their IDs or make one upgrade the other.

The 3.5e Warlock is not built on modern named Patron subclass progressions. Do not retroactively mandate choosing a modern Fiend subclass for historically derived features. Non-SRD heritage feats and prestige classes can be researched as future compatibility cases without importing them into this slice.

## Working definitions and decision gate

The source crosswalk records **one identity, five feature-part identities, two historical guided projections, and seven explicit open decisions**. These are grounded source mappings, **not extra granted powers**.

Before promoting a native Patron rule to authoritative data, decide:

1. **Level-1 Fiend mechanical grant**: identity only, 2014 Blessing, or another approved entitlement. A non-zero starting benefit may be desirable but is not yet a decision.
2. **Fiend spell access**: how both source lists and their different acquisition semantics translate into one native entitlement without granting unjustified bonus spells.
3. **Blessing**: initial level and whether another creature's defeat within 10 feet triggers it.
4. **Luck**: Short-Rest single use versus Charisma-modifier Long-Rest uses.
5. **Patron damage resistance**: Force selection and magical/silvered weapon bypass.
6. **Hurl Through Hell**: hit and save procedure, timing/dice, status, Pact-slot use recovery.

**Deferred beyond one-Patron SRD proof:** Whether and how advanced Warlocks can mix different Patrons' abilities; those choices cannot be proven from SRD Fiend alone. Minimal synthetic fixtures may exercise the generic prerequisite/selection contract without pretending to be playable Patrons.

## Implementation sequence after reconciliation

1. Approve the six Patron rulings and encode **one native set** of part definitions with stable IDs, referenced by an authoritative Fiend progression/preset.
2. Implement/validate feature triggers and recovery with shared Rules Core mechanics, including appropriate source conversion mapping. No Fiend-specific runtime engine.
3. Validate guided versus custom acquisition against the same entitlement ledger, testing level-1 identity and the 1/3/6/10/14 milestone conflicts.
4. Continue to **Pacts** next, then **Eldritch Invocations**. Validate the integrated class at levels 1–20 after all three pillars are complete.

The source crosswalk tests already verify both projections, all feature identities, and source spell lists, without making unapproved mechanical promises.
