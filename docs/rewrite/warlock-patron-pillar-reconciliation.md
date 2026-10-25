# Warlock Pillar 1 — Patron: SRD Fiend deconstruction and reconciliation

**Stage:** source-complete Patron analysis and automated conversion crosswalk; **not yet an owner-approved reconciled native Fiend**, playable subclass, or runtime implementation. This pass intentionally resolves **identity** (already approved), inventories and compares **all Fiend mechanics** in SRD 5.1/5.2.1, and identifies remaining rulings. It does not silently choose an edition where rules differ.

## Structural decision applied

Warlock has **three class-specific pillars: Patron, Pacts, and Eldritch Invocations**. Their interaction produces progression, and guided subclass presets are another means of selecting the same abilities, *not* an independent pillar.

- **A Patron is required for Warlock class identity at level 1.** Warlock power derives from a Patron even if the character has no personal contract, communication, knowledge of the Patron's name, or ongoing relationship. Direct and inherited Patron connections both satisfy the requirement. Fiend is the initial SRD example.
- **Patron features are separate from identity and can be acquired later.** The 2014 Fiend presents subclass features at 1/6/10/14, and 2024 at 3/6/10/14; those are source projections, not two gameplay engines. The 2024 subclass first appears at 3 in source, but native Patron identity remains known from level 1.
- **Deconstruction yields five coherent part IDs plus identity**: spell access, Dark One's Blessing, Dark One's Own Luck, Fiend damage resistance, and Hurl Through Hell. These names identify rule capabilities, not necessarily five independent player purchases. Prerequisites, packaging, feature timing, and advancement entitlements remain to be reconciled.
- **Guided source projections** use the same part IDs and grant each once per progression. They do not provide duplicate subclass or part entitlements. The actual **single authoritative native Fiend preset** cannot be finalized until the open rulings are approved; SRD51/SRD52 projections are comparison and compatibility fixtures.

The machine-readable source-preserving crosswalk is `data/reference/srd/warlock-fiend-patron-crosswalk.json`. Imported source records remain untouched at `data/reference/srd/structured/class/class-warlock.json` and staged native candidates remain `pending-native-reconciliation`.

## Patron relationship and cross-pillar eligibility — owner design direction (2026-10-24)

**A Patron does not require the Warlock to have personally entered a contract.** A Warlock who inherits a connection to a supernatural being still has a **Patron**: the relationship and its consequences can be inherited, even if the Warlock has never met, communicated with, or agreed to serve that being. The distinction is **how the Patron connection was acquired**, not whether the Patron exists.

- **Direct Patron connection:** the Warlock personally makes a bargain, receives a blessing, is chosen, or develops another relationship with the power-granting being.
- **Inherited Patron connection:** an ancestor's bargain, a family legacy, or supernatural ancestry links the Warlock to the Patron or Patron type. No personal agreement, knowledge of the being's name, or ongoing contact is inherently required.
- **Other indirect Patron connection:** supernatural forces designate the Warlock as a conduit or pass along the effects of a Patron's power without the character personally negotiating for it.

**Do not misclassify inherited power as Patronless.** The Patron's power/source must exist in the character concept, but a Warlock need not know or identify the individual being; the level-1 build can record a Patron type or hidden/unrevealed Patron reference. Discovery of the Patron's name, motives, or role can remain narrative. These cases do not remove the required Patron power source.

**A genuinely Patronless Warlock is not a valid native Warlock concept.** Owner ruling: active **Patron-sourced Warlock power** defines Warlock class identity, while **self-sustaining innate magic with no operative Patron power connection** fits Sorcerer, even if that power originated in an ancestor's Warlock bargain. A Warlock may be unaware of, estranged from, or unable to contact their Patron; such cases are **not** Patronless. Contractual obligation, awareness, communication, and named NPC identity are not prerequisites. **An ancestor's Patron or contract alone does not determine a descendant's class**; the setting and original bargain control what power and Patron connection, if any, transfer.

**Narrative relationship is separate from mechanical entitlement.** The manner of receiving power does not automatically add or remove Fiend, Archfey, or other Patron features. A guided Fiend preset establishes a Fiend power-source relationship but does not mandate a personally signed deal. Patron feature acquisition still follows its approved level/selection/compatibility rules.

**Cross-pillar prerequisites are supported by design.** A Pact choice or improvement may require an eligible Patron category/trait/feature, an Invocation may require a specific Pact or Patron part, and a Patron feature may depend on an existing Pact/Invocation. At the shared Rules Core level, store these as explicit **typed prerequisites and dependency relationships** rather than hard-coded names or impossible one-way pillar hierarchies. The character builder must explain incompatible or unavailable choices and preserve the same restrictions for guided presets and custom builds.

- Prerequisites may reference **source traits** (e.g., a fiendish or otherworldly source), **specific unlocked parts**, or Warlock class levels. A source trait does not imply a particular named NPC or a contract.
- Cross-pillar restrictions should be attached **only when mechanically or narratively justified for the specific option**. Do **not** restrict all Blade/Chain/Tome Pacts to particular Patrons merely because the architecture permits dependencies.
- Never silently waive a prerequisite for guided presets. If an option changes later, validate acquired dependent parts and provide a clear replacement or grandfathering rule rather than creating an invalid invisible state.
- The first SRD Fiend slice must show at least one **synthetic, non-playable compatibility fixture** exercising both an allowed and a denied Patron-dependent Pact/Invocation option. This tests generic eligibility without authoring an unsupported new playable Pact or Patron.

**Hexblade and Vestige remain open base-class-versus-Warlock classifications.** Their historical equivalents—the 3.5e Hexblade and Binder—were separate base classes, while 5.x treats the concepts as Warlock Patrons. Evaluate *each* for its distinct gameplay loop, specialized resources, progression independence, overlap with existing Warlock Pacts/Invocations, and ability to reconstruct its published Warlock preset. Neither is pre-committed to class status. This review is **separate from prestige classification**: these are base-class-versus-Warlock membership questions, not automatically 3.5e prestige cases.

**Why Fiend is the correct first Patron case:** Fiend has a named 2014/2024 SRD subclass with a strong historical 3.5e Warlock fiendish-origin/heritage precedent. It can validate direct feature reconciliation without needing to invent modern subclasses for historical sources. Historical non-SRD Fiendish Heritage and Hellfire options remain **reference-only**, not automatically available selectable content.

## Intergenerational bargains: power and Patron connection can inherit separately (owner clarification, 2026-10-24)

**The original agreement and setting determine what passes to descendants.** Do not automatically inherit a Warlock class, a subclass, its abilities, or an obligation merely because an ancestor was a Warlock. Model these separately:

1. **Inherited magic / power:** Does a magical capability transfer to the descendant, and if so, is it a self-sustaining innate gift or an ongoing manifestation of a Patron's power?
2. **Inherited Patron connection:** Does the original Patron's source relationship, claim, or influence pass to the descendant? A descendant can have a Patron without having made or even knowing about the original deal.
3. **Inherited contractual obligations:** Does any term bind descendants? The answer is setting/contract specific. A Patron connection and binding obligations are **not synonymous**, and neither should be assumed solely from ancestry.

**Examples, not mandatory outcomes:**

| Transfer under the original arrangement | Character consequence |
| --- | --- |
| **Power and Patron connection** | A descendant whose abilities still operate through that Patron may become a **Warlock** without making a personal deal. |
| **Power, but not an operative Patron power connection** | If the magic becomes an **autonomous inherited innate ability**, the descendant can be a **Sorcerer**, even though the family history includes a Warlock and a Patron. |
| **Patron connection or obligation, but no magical power** | The descendant has a supernatural relationship or inherited obligation but **does not become a Warlock automatically**; a class requires the appropriate power and progression. |
| **Neither** | Descent alone confers neither the Patron's power nor Warlock class identity. |

**Classification tests the descendant's current power source, not the genealogy of that power.** A family could have Warlock and Sorcerer members. Having a Patron somewhere in the family tree does not automatically make all magical descendants Warlocks. Conversely, an unknown or inherited Patron can still actively supply a Warlock's power without a fresh contract. A contract can grant power without transferring an obligation, or create obligations without automatically granting class features.

**Rule implementation boundary:** distinguish `historicalPowerOrigin`, `currentPowerSource`, `patronConnection`, and `inheritedObligation` at the conceptual level when conversion needs them. These are **design distinctions, not approved mandatory JSON field names**. The character's actual source/feature entitlements follow approved class rules; setting and GM determine contract terms. Do not encode a universal inheritance rule into Patron eligibility or auto-grant abilities to descendants.

## Cross-edition Patron landscape beyond SRD Fiend

A separate [comparison of the ten published 5.x Patrons with 3.5e Warlock power sources, heritage feats, Binder, Hexblade, and related class material](warlock-patron-3x-5x-comparison.md) is recorded as **reference research**. 3.5e did **not** offer a ten-Patron subclass menu. This compatibility survey does not expand the current playable SRD-only Fiend implementation. Especially important future tests include Hexblade–Blade Pact overlap, Genie variable elemental subtypes, the Vestige companion, and distinguishing Undead from Undying.

## Verified source feature inventory

| Mechanic | SRD 5.1 (2014) | SRD 5.2.1 (2024) | Native reconciliation issue |
| --- | --- | --- | --- |
| Patron identity | Fiend selected at level 1 | Fiend subclass selected at level 3 | **Patron identity and first mechanical grant at level 1 are approved** |
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

The historically approved 3.5e Warlock **base-class Fiendish Resilience** healed 1/2/5 HP per round at class levels 8/13/18 using a one-Long-Rest recharge; **the owner has since moved this mechanic into the Fiend Patron's single Infernal Retaliation feature**, rather than retaining the universal native base-class grant. The Fiend **Patron feature** called Fiendish Resilience is a resistance to a selected damage type from level 10. These are **two separate mechanics** despite the same display name: never merge their IDs or make one upgrade the other.

The 3.5e Warlock is not built on modern named Patron subclass progressions. Do not retroactively mandate choosing a modern Fiend subclass for historically derived features. Non-SRD heritage feats and prestige classes can be researched as future compatibility cases without importing them into this slice.

## Owner review: Fiend mechanics and 3.5e fast healing (2026-10-24)

**Historical review (since resolved):** The 3.5e feature provided fast healing 1/2/5 at levels 8/13/18 for up to 20 rounds, once per Long Rest (originally once per day). The old native base-class approval remains in decision history but is **superseded for fast healing only**. Native healing now belongs to the **Fiend Patron's Infernal Retaliation** feature, with original scaling retained as reference rather than automatically established for the new Patron feature. Other historical rulings (Damage Reduction, Deceive Item, Energy Resistance, Imbue Item, and optional-only Eldritch Sight) are unaffected. Do not silently delete, retain, or convert healing before the owner decides.

**Why reopen:** healing restores lost HP while 2014/2024 Dark One's Blessing and Fiendish Vigor provide **Temporary Hit Points** and Patron Fiendish Resilience provides **damage resistance**. They are not exact substitutes. However, giving every Warlock extra 20/40/100 potential HP per daily activation could displace Pact/Invocation choices and dilute Patron-specific defensive identity. Evaluate **automatic grant versus optional Invocation versus removal**, comparing all three pillars; an old base feature must not be automatically retained merely because it was approved before the restart.

**Native Patron acquisition decision 1 — approved:** adopt **2014 Fiend level 1** for the Patron's first mechanical milestone, including **Dark One's Blessing and Patron spell access**. The 2024 subclass level-3 grant remains a source-era comparison only. The **2024 cooperative Blessing trigger is approved**; the Fiend spell grant's entitlement semantics and spell roster still need approval.

**Native Luck decision 4 — approved:** At level 6, **Dark One's Own Luck** adds 1d10 to an ability check or saving throw after the roll and before its effects, no more than once per roll, with uses equal to **max(1, Charisma modifier)**. All uses recover on a **Long Rest** (2024 recovery approved).

### Source comparison and resolved or remaining owner decisions

| Mechanic | 2014 SRD 5.1 | 2024 SRD 5.2.1 | Recommendation (not yet approved) |
| --- | --- | --- | --- |
| **Fiend Spells (decision 2)** | Ten spells added to available **choices**, consuming ordinary spell choices | Ten spells automatically **prepared** at Warlock levels 3/5/7/9 | **APPROVED DEFAULT C:** 13 eligible spells at Patron level 1; ten 2024 spells automatically known at 3/5/7/9; 2014-only three available for ordinary choice. **OPTIONAL A:** 13 eligible, none automatic. **OPTIONAL B:** all 13 automatic at corresponding spell-level class milestones. Modes are mutually exclusive. |
| **Dark One's Blessing (decision 3)** | Temporary HP **max(1, CHA modifier + Warlock level)** when the Warlock personally drops a hostile creature to 0 HP | Same temporary HP, also triggers when **another creature** drops an enemy within **10 feet of the Warlock** to 0 HP | **APPROVED:** 2024 cooperative trigger at level 1; temporary HP do not stack. |
| **Patron Fiendish Resilience (decision 5)** | Level 10, choose any one damage type after a Short/Long Rest; damage from **magical or silvered weapons** bypasses the resistance | Level 10, choose any one damage type **except Force**; no bypass for magical or silvered weapons | Adopt the **2024 simpler resistance**. This is independent of the reopened historical base fast healing. |
| **Hurl Through Hell (decision 6)** | Level 14, on hit immediately removes target until end of next turn, **no saving throw**; non-Fiend target takes **10d10 Psychic on return**; Long Rest restore | Level 14, hit then **Charisma save**; on failure deals **8d10 Psychic immediately** to non-Fiends, removes and **Incapacitates** target until return; Long Rest restore **or expend Pact Magic slot** to restore use | **APPROVED:** 2024-style package at level 14, including save, timing, condition, and Pact Magic slot recovery. |

**Spell differences:** shared spells are Burning Hands, Command, Scorching Ray, Fireball, Stinking Cloud, Fire Shield and Wall of Fire. **2014 only:** Blindness/Deafness, Flame Strike, Hallow. **2024 only:** Suggestion, Geas, Insect Plague. This means 10 spells per version, **13 distinct** across both.

No native Fiend combat definition is activated by these research notes. Preserving explicit unresolved mechanics avoids inadvertently using whichever source edition appears first in the imported data.

## Working definitions and decision gate

The source crosswalk records **one identity, five source feature-part identities, two historical guided projections, and eight tracked rulings/reviews** (five approved directions, including the resolved fast-healing relocation, two remaining Fiend mechanics, and one deferred multi-Patron question). The optional Infernal Retaliation feature is additional native design, not a new SRD source part. These are grounded source mappings, **not extra granted powers**.

Before promoting a native Patron rule to authoritative data, decide:

1. **Level-1 Fiend mechanical grant — SETTLED**: use 2014 level 1 for both Dark One's Blessing and Patron spell access. The spell benefit and Blessing trigger are *separately* still open.
2. **Fiend spell access**: how both source lists and their different acquisition semantics translate into one native entitlement without granting unjustified bonus spells.
3. **Blessing — SETTLED**: 2024 expanded cooperative trigger is approved at level 1.
4. **Luck — SETTLED**: Charisma modifier uses (minimum one) and Long Rest recharge.
5. **Patron damage resistance**: Force selection and magical/silvered weapon bypass.
6. **Hurl Through Hell — SETTLED**: 2024 hit/save, 8d10 timing, Incapacitated, and Pact-slot recovery.

**Deferred beyond one-Patron SRD proof:** Whether and how advanced Warlocks can mix different Patrons' abilities; those choices cannot be proven from SRD Fiend alone. Minimal synthetic fixtures may exercise the generic prerequisite/selection contract without pretending to be playable Patrons.

## Implementation sequence after reconciliation

1. Resolve the two remaining Fiend mechanics (spell entitlement and Patron damage resistance) and the pending numerical/acquisition details for the Fiend-specific Infernal Retaliation feature; then encode **one native set** of parts with stable IDs and an authoritative Fiend progression/preset.
2. Implement/validate feature triggers and recovery with shared Rules Core mechanics, including appropriate source conversion mapping. No Fiend-specific runtime engine.
3. Validate guided versus custom acquisition against the same entitlement ledger, testing level-1 identity and the 1/3/6/10/14 milestone conflicts.
4. Continue to **Pacts** next, then **Eldritch Invocations**. Validate the integrated class at levels 1–20 after all three pillars are complete.

The source crosswalk tests already verify both projections, all feature identities, and source spell lists, without making unapproved mechanical promises.

## Owner-approved Eldritch Blast layers and Infernal Retaliation (2026-10-24)

**Approval scope:** The owner approved the three-layer Eldritch Blast architecture and the **Infernal Retaliation** feature-family reconciliation. The owner subsequently reconsidered separation and now **favors Hellfire Blast as an advanced Eldritch Blast enhancement further specialized to the Patron**, consistent with its 3.5e origin. Exact integration and the non-splitting, increasing-damage Hellfire concept remain under design; no numeric damage formula, action type, level schedule, or resource cost has yet been approved. Do not promote these candidate mechanics into playable native grants without further decisions.

### Three-layer Eldritch Blast architecture — approved

1. **Base Eldritch Blast — OWNER APPROVED:** the shared Warlock attack granted **automatically as a Warlock class feature at level 1**, not requiring a cantrip selection. Exact native damage progression and cantrip count representation remain to be reconciled.
2. **Patron Imprint:** the Patron's signature manifestation or modification of Eldritch Blast, such as a damage-type conversion or conditional effect. This is part of the **Patron** pillar, not a fourth pillar and not necessarily a raw damage increase. Do not require Patrons outside the Fiend SRD slice to have executable Imprints yet.
3. **Optional Invocations:** elective Blast Shapes, Eldritch Essences, and advanced Eldritch Blast enhancements in the **Invocations** pillar. The owner favors Hellfire Blast as an advanced Patron-associated enhancement; its exact prerequisites, costs, and compatibility are still pending. Do not grant additional Invocation selections or duplicate entitlements merely for a guided preset.

A Patron's imprint should not make Pact of the Blade or other non-blast Warlock builds invalid. Implement a single authoritative Eldritch Blast capability composed of compatible modifiers rather than multiple independent edition-specific attacks. Shared resolution must eventually address number of attack rolls, hit triggers, critical hits, per-hit bonuses, damage types, targeting, resource costs, and modifier priority.

### Hellfire Blast as an advanced Patron-specialized Eldritch Blast enhancement — current direction (2026-10-24)

The owner **reconsidered an earlier standalone Hellfire Blast proposal** and favors retaining its original 3.5e identity as an **enhancement of Eldritch Blast**, specialized further by infernal Patron power. This fits the approved three-layer architecture: base Eldritch Blast (automatic level-1 class feature), Patron Imprint, and elective enhanced Invocations. The **advanced-enhancement direction is favored**, but no new numeric damage or acquisition ruling has been approved.

**Historical provenance:** *Fiendish Codex II* Hellfire Blast enhanced Eldritch Blast with 2d6 extra damage per Hellfire Warlock level (up to +6d6), at 1 Constitution damage per activation; this was not ordinary Fire damage and could interact with some Blast Shapes. Preserve this as a direct predecessor for reconciliation, not as permission to copy its additional damage onto each 5.x beam.

**Remaining decisions:** whether an enhanced Hellfire blast concentrates normal multiple beams into one stronger hit, its exact damage growth, critical-hit interactions, self-sacrifice costs, eligible Patron/Invocation prerequisites, Blast Shape compatibility, and how it interacts with the ordinary Patron Imprint. Other Patrons **may** have distinctive advanced enhancements, but need not receive equally strong damage amplifiers or identical mechanics. Hellfire must not become a mandatory ability of every Fiend Warlock.

The owner-approved **Infernal Retaliation** family is separate and unaffected.

### Infernal Retaliation — two Fiend Patron effects, owner-approved (2026-10-24)

**Infernal Retaliation is ONE Fiend Patron feature**, not a universal Warlock ability. At Warlock level **8**, it grants **two alternative effects**, Fiendish Resilience fast healing and Fiendish Flamewreath. It improves at Warlock levels **13** and **18**, without granting any extra use or feature entitlement.

| Warlock level | Fiendish Resilience | Fiendish Flamewreath |
| --- | --- | --- |
| 8 (grant) | Fast healing **1 HP per round**, for 20 rounds | **1d6 Fire** retaliation, for 20 rounds |
| 13 (upgrade) | Fast healing **2 HP per round** | **2d6 Fire** retaliation |
| 18 (upgrade) | Fast healing **5 HP per round** | **5d6 Fire** retaliation |

**Shared resource:** One activation per **Long Rest** across both alternatives. The player chooses which effect to activate; both use the Warlock's **Reaction** without requiring an external trigger under Dorks & Dice action economy. Activation consumes the one feature use. The sustained effect continues without further Reaction expenditure.

**0 HP stabilization rule — approved:** If the Warlock is reduced to 0 HP while Fiendish Resilience is active, it **stabilizes the Warlock once**. It does **not** restore HP at 0 HP, restore consciousness, or revive a dead Warlock. Normal consequences apply to subsequent damage at 0 HP, including breaking stabilization; fast healing **does not repeatedly restabilize** after further damage. A Warlock who is already unconscious ordinarily cannot activate the feature then; it must already be active.

**Hellfire Shield is not part of Infernal Retaliation.** It remains a **Fiend-associated Hellfire specialization** with three candidate acquisition routes: **automatically granted Patron feature**, **selectable Fiend Patron choice**, or **Eldritch Invocation with a Fiend Patron prerequisite**. None is yet selected or assigned an acquisition level/budget. A single shared native Shield effect definition must be reused regardless of route, without double-grants or extra entitlements. Its derived Eldritch Blast damage direction remains approved, but ordinary Fiend Warlocks do **not yet** receive the Shield as a committed grant. It does not consume Infernal Retaliation's once-per-Long-Rest resource. Shield remains a player-chosen Reaction without a mandatory trigger; its own costs, damage/targeting resolution, and historical Constitution damage remain under review.

**Source provenance:** *Complete Arcane* Fiendish Resilience was historically a 3.5e **base Warlock** feature and *Player's Handbook II* Fiendish Flamewreath an alternative class feature; both are reclassified as modes of one **Fiend Patron** feature in the native system. *Fiendish Codex II* Hellfire Shield retains distinct, optional-specialization provenance. The modern Fiend Patron level-10 damage **resistance** called Fiendish Resilience is a separate native feature and identity, not the fast-healing mode.

**Remaining effect-resolution details:** Healing tick timing while above 0 HP and Flamewreath's retaliatory damage targeting/application. No changes to approved levels, duration, effect numbers, shared resource, or 0 HP behavior without another owner decision.

### Scope and provenance

Mechanical reconciliation sources for the current effort are 3e/3.5e and 5e/5.5e. **4e is not a mechanical reconciliation source**, although it can be noted for historical provenance where needed. Hellfire Warlock (*Fiendish Codex II*) is a principal 3.5e comparator for the Fiend Patron, with its unique features eligible for merging or optional specialization rather than wholesale adoption of a separate three-level progression. Non-SRD abilities remain research/design candidates pending approval for native executable inclusion. Guided builds must use the same native authoritative parts and budgets as advanced character construction.

## Four further owner approvals — 2026-10-24

These supersede older open-decision wording above and in historical research notes; SRD source projections themselves remain unchanged.

1. **Base Warlock Eldritch Blast:** Automatically granted at Warlock level 1 as a class feature, without requiring a cantrip selection. The three-layer architecture remains intact. Native scaling and the accounting of other cantrip entitlements remain outstanding.
2. **Dark One's Blessing:** The 2024 cooperative trigger is approved, at the already-approved native level-1 milestone. The Warlock gains temporary HP equal to **max(1, CHA modifier + Warlock level)** when the Warlock drops a hostile creature to 0 HP, or another creature drops an enemy within 10 feet of the Warlock to 0 HP. Temporary HP do not stack.
3. **Dark One's Own Luck:** The previously established Charisma-modifier uses (minimum one), 1d10 bonus, and once-per-roll limit are confirmed, with **Long Rest** restoration now approved.
4. **Hurl Through Hell:** Approve the **2024-style** level-14 feature package: once per turn on a qualifying attack-roll hit, expend a use and force a Charisma save against the Warlock spell save DC. On failure, the target travels through the Lower Planes, a non-Fiend target immediately takes **8d10 Psychic**, and the target is **Incapacitated** until returning at the end of the Warlock's next turn. One use recovers on a Long Rest; spending a Pact Magic spell slot can restore a use. A successful save does not refund the attempt.

**Still unresolved:** Fiend spell entitlement/list reconciliation, Patron Fiendish Resilience damage-resistance rules and interactions with 3.5e resistance, and Infernal Retaliation acquisition/effect details (not its Fiend-only classification or shared Long Rest resource). These approvals do **not** make the pending Fiend native subclass or base Warlock executable.

## Latest owner ruling — two-effect Infernal Retaliation and separate Hellfire Shield (2026-10-24)

The owner approved **one Fiend-only Infernal Retaliation feature**, containing exactly **two** player-chosen Reaction effects: Fiendish Resilience fast healing and Fiendish Flamewreath. Both last **20 rounds**. One shared activation returns after a Long Rest. At class levels **8/13/18**, healing equals **1/2/5 HP per round**, and Flamewreath retaliation equals **1d6/2d6/5d6 Fire**. No Reaction trigger is needed to activate either effect.

When a Warlock with active Fiendish Resilience falls to **0 HP**, the effect **stabilizes once**, without healing at 0 HP or restoring consciousness. Subsequent damage follows normal death and stabilization procedures; no repeated automatic stabilization.

**Hellfire Shield was removed** from the feature and its progression. The Fiend-only Hellfire specialization may deliver Shield as an automatic **Patron grant**, a **Patron feature choice**, or an **Eldritch Invocation requiring Fiend**; the route is **not yet selected**. It retains **derived Eldritch Blast/Hellfire damage**, a **Reaction**, and its **own** yet-undecided cost. It does not use Infernal Retaliation's charge and is not automatically learned at level 8. Source research into Hellfire Shield's original 3.5e statistics is preserved separately. This latest decision supersedes earlier three-mode proposals without altering source-era references.

**Status:** owner-approved design directions, not an executable native Fiend definition. Native spell entitlement and Patron resistance remain unresolved.

## Hellfire Shield acquisition options — owner proposal (2026-10-24)

Hellfire Shield remains **outside Infernal Retaliation**. The owner identified three possible acquisition mechanisms for the same Fiend-specific feature; **no exclusive choice among them has been approved**:

| Path | Acquisition | Cost or condition | Status |
| --- | --- | --- | --- |
| **Fiend Patron grant** | Automatically granted at a future Fiend Patron milestone | No separate selection, but must be counted in the Patron feature budget | Proposed, level undecided |
| **Fiend Patron choice** | Offered as one selectable Fiend Patron ability | Spends the relevant Patron choice entitlement, to be defined | Proposed, level/budget undecided |
| **Fiend Eldritch Invocation** | Elective Invocation that requires a Fiend Patron | Spends an ordinary Invocation selection; prerequisites/level undecided | Proposed, not granted |

Regardless of route, implement **one native Hellfire Shield ability**, not three mechanically distinct copies. Its damage scales from eligible Eldritch Blast/Hellfire mechanics as previously directed, and activating it is a **player-chosen Reaction with no mandatory trigger**. Its resource, Constitution cost and exact derived damage calculation are still under review. It is not included in the one-charge Infernal Retaliation feature, and multiple paths should never award the same feature twice. Do not mark any of the three routes executable until the owner selects a disposition.

## Consolidated Fiend Patron decision and handoff (2026-10-24)

The latest authoritative Fiend design summary is [Fiend Patron consolidated decision](warlock-fiend-patron-consolidated-decision.md). This retains the approved Patron feature levels 1/6/8/10/13/14/18, the two-effect Infernal Retaliation progression, and the 0 HP stabilization rule. **Hellfire Shield acquisition is explicitly DEFERRED to the Eldritch Invocations pillar**; the three Fiend-only candidate routes have not been adjudicated. The other outstanding Fiend mechanic choices are **Patron spell-access entitlement/roster** and **the level-10 damage-resistance selection/bypass policy**. Historical source-era passages above remain comparison material, not operative grants. The active native Fiend file is still pending reconciliation.

## Fiend spell-access owner decision — superseding ruling (2026-10-24)

**Approved native default:** Use 2024 automatic Fiend spell grants and restore the missing 2014-only spells. Grant **Blindness/Deafness** at Warlock **3**, and **Flame Strike** and **Hallow** at Warlock **9**, alongside the original ten 2024 grants. All **13 union spells** are automatically known by level 9, distributed **5/2/2/4** at **3/5/7/9**. Patron identity/spell-access feature begins at level **1**, but no automatic Patron spell is received before level 3. These spells cost no regular spell choices, grant no free casting or slots, and require no daily preparation.

**Optional campaign rule profiles:** faithful **2014 expanded-list** mechanics with the original **ten selectable spells** and faithful **2024 auto-grant** mechanics with the original **ten automatically granted spells**; both are mutually exclusive with each other and the native default. The former C/B/A design is superseded rather than treated as three equally current options.

Refer to [Fiend Patron consolidated decision](warlock-fiend-patron-consolidated-decision.md). Level-10 resistance/replacement remains separately open.
