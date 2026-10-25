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

The historically approved, **now reopened for owner reconsideration**, 3.5e Warlock **base-class Fiendish Resilience** heals 1/2/5 HP per round at class levels 8/13/18 using a one-Long-Rest recharge. The Fiend **Patron feature** called Fiendish Resilience is a resistance to a selected damage type from level 10. These are **two separate mechanics** despite the same display name: never merge their IDs or make one upgrade the other.

The 3.5e Warlock is not built on modern named Patron subclass progressions. Do not retroactively mandate choosing a modern Fiend subclass for historically derived features. Non-SRD heritage feats and prestige classes can be researched as future compatibility cases without importing them into this slice.

## Owner review: Fiend mechanics and 3.5e fast healing (2026-10-24)

The historical **3.5e Warlock base-class fast-healing grant is REOPENED FOR REVIEW**. The 3.5e feature provides fast healing 1/2/5 at levels 8/13/18 for up to 20 rounds, once per Long Rest (the original 3.5e rule said once per day). The earlier approval survives in the decision history, but this feature is **not a finalized grant for the restarted Warlock**. Other historical rulings (Damage Reduction, Deceive Item, Energy Resistance, Imbue Item, and optional-only Eldritch Sight) are unaffected. Do not silently delete, retain, or convert healing before the owner decides.

**Why reopen:** healing restores lost HP while 2014/2024 Dark One's Blessing and Fiendish Vigor provide **Temporary Hit Points** and Patron Fiendish Resilience provides **damage resistance**. They are not exact substitutes. However, giving every Warlock extra 20/40/100 potential HP per daily activation could displace Pact/Invocation choices and dilute Patron-specific defensive identity. Evaluate **automatic grant versus optional Invocation versus removal**, comparing all three pillars; an old base feature must not be automatically retained merely because it was approved before the restart.

**Native Patron acquisition decision 1 — approved:** adopt **2014 Fiend level 1** for the Patron's first mechanical milestone, including **Dark One's Blessing and Patron spell access**. The 2024 source's subclass level-3 grant remains preserved in the comparison preset for conversion/provenance; it is not the native first grant. The Blessing **trigger** and the spell grant's **learning/list** still need approval.

**Native Luck decision 4 — partly approved:** the level-6 **Dark One's Own Luck** has a number of uses equal to the Warlock's **Charisma modifier, minimum one**, with one use per roll. The user selected the scaling count, **not** the recharge frequency. 2024 Long Rest recovery is recommended; 2014 instead provided one use per Short/Long Rest.

### Detailed comparisons remaining for owner decision

| Mechanic | 2014 SRD 5.1 | 2024 SRD 5.2.1 | Recommendation (not yet approved) |
| --- | --- | --- | --- |
| **Fiend Spells (decision 2)** | Ten spells added to available **choices**, consuming normal Warlock learned-spell entitlements; acquired as a class-level-1 Patron list | Ten spells automatically **prepared** at Warlock levels 3/5/7/9, in addition to normal learned spells; native no-daily-preparation needs conversion | Consider **automatic learned/access grants** separate from Warlock's regular choices, with 2014 initial availability; first decide spell roster and exact level grants. Avoid awarding all 13 distinct names automatically. |
| **Dark One's Blessing (decision 3)** | Temporary HP **max(1, CHA modifier + Warlock level)** when the Warlock personally drops a hostile creature to 0 HP | Same Temporary HP, also triggers when **another creature** drops an enemy within **10 feet of the Warlock** to 0 HP | Adopt the **2024 cooperative trigger** with the already approved level-1 acquisition; Temporary HP do not stack. |
| **Patron Fiendish Resilience (decision 5)** | Level 10, choose any one damage type after a Short/Long Rest; damage from **magical or silvered weapons** bypasses the resistance | Level 10, choose any one damage type **except Force**; no bypass for magical or silvered weapons | Adopt the **2024 simpler resistance**. This is independent of the reopened historical base fast healing. |
| **Hurl Through Hell (decision 6)** | Level 14, on hit immediately removes target until end of next turn, **no saving throw**; non-Fiend target takes **10d10 Psychic on return**; Long Rest restore | Level 14, hit then **Charisma save**; on failure deals **8d10 Psychic immediately** to non-Fiends, removes and **Incapacitates** target until return; Long Rest restore **or expend Pact Magic slot** to restore use | Favor 2024 **saving throw and slot recovery**, then explicitly choose damage amount/timing. 10d10 averages 55; 8d10 averages 44 before saving-throw probability. |

**Spell differences:** shared spells are Burning Hands, Command, Scorching Ray, Fireball, Stinking Cloud, Fire Shield and Wall of Fire. **2014 only:** Blindness/Deafness, Flame Strike, Hallow. **2024 only:** Suggestion, Geas, Insect Plague. This means 10 spells per version, **13 distinct** across both.

No native Fiend combat definition is activated by these research notes. Preserving explicit unresolved mechanics avoids inadvertently using whichever source edition appears first in the imported data.

## Working definitions and decision gate

The source crosswalk records **one identity, five feature-part identities, two historical guided projections, and eight tracked rulings/reviews** (one approved level-1 decision, one partly approved Luck decision, four Fiend mechanics still requiring decisions, one reopened historical healing review, and one deferred multi-Patron question). These are grounded source mappings, **not extra granted powers**.

Before promoting a native Patron rule to authoritative data, decide:

1. **Level-1 Fiend mechanical grant — SETTLED**: use 2014 level 1 for both Dark One's Blessing and Patron spell access. The spell benefit and Blessing trigger are *separately* still open.
2. **Fiend spell access**: how both source lists and their different acquisition semantics translate into one native entitlement without granting unjustified bonus spells.
3. **Blessing**: initial level 1 settled; decide whether another creature's defeat within 10 feet triggers it.
4. **Luck**: Charisma modifier uses (minimum one) settled; choose recharge schedule (2024 Long Rest recommended, not yet approved).
5. **Patron damage resistance**: Force selection and magical/silvered weapon bypass.
6. **Hurl Through Hell**: hit and save procedure, timing/dice, status, Pact-slot use recovery.

**Deferred beyond one-Patron SRD proof:** Whether and how advanced Warlocks can mix different Patrons' abilities; those choices cannot be proven from SRD Fiend alone. Minimal synthetic fixtures may exercise the generic prerequisite/selection contract without pretending to be playable Patrons.

## Implementation sequence after reconciliation

1. Resolve the remaining Fiend mechanic choices, confirm Luck recovery and the historical fast-healing review, then encode **one native set** of part definitions with stable IDs, referenced by an authoritative Fiend progression/preset.
2. Implement/validate feature triggers and recovery with shared Rules Core mechanics, including appropriate source conversion mapping. No Fiend-specific runtime engine.
3. Validate guided versus custom acquisition against the same entitlement ledger, testing level-1 identity and the 1/3/6/10/14 milestone conflicts.
4. Continue to **Pacts** next, then **Eldritch Invocations**. Validate the integrated class at levels 1–20 after all three pillars are complete.

The source crosswalk tests already verify both projections, all feature identities, and source spell lists, without making unapproved mechanical promises.

## Owner-approved Eldritch Blast layers and Infernal Retaliation (2026-10-24)

**Approval scope:** The owner approved the three-layer Eldritch Blast architecture and the **Infernal Retaliation** feature-family reconciliation. The owner separately suggested a **non-splitting Hellfire Blast with increasing damage**, in the manner of 3.x; that is an endorsed direction for design comparison, not approval of a specific damage formula, action type, level schedule, or compatibility rule. Do not promote these candidate mechanics into playable native grants without further decisions.

### Three-layer Eldritch Blast architecture — approved

1. **Base Eldritch Blast:** the shared Warlock attack and scaling mechanic. Whether it is automatically acquired as a class feature rather than selected as a 5.x cantrip remains unresolved.
2. **Patron Imprint:** the Patron's signature manifestation or modification of Eldritch Blast, such as a damage-type conversion or conditional effect. This is part of the **Patron** pillar, not a fourth pillar and not necessarily a raw damage increase. Do not require Patrons outside the Fiend SRD slice to have executable Imprints yet.
3. **Optional Invocations:** elective Blast Shapes, Eldritch Essences, and advanced enhancements (including candidate Hellfire enhancements) in the **Invocations** pillar. Preserve source-inspired shape/essence distinctions and explicit compatibility rules; do not grant additional Invocation selections or duplicate entitlements merely for a guided preset.

A Patron's imprint should not make Pact of the Blade or other non-blast Warlock builds invalid. Implement a single authoritative Eldritch Blast capability composed of compatible modifiers rather than multiple independent edition-specific attacks. Shared resolution must eventually address number of attack rolls, hit triggers, critical hits, per-hit bonuses, damage types, targeting, resource costs, and modifier priority.

### Hellfire Blast single-attack proposal — under numerical review

The owner proposed that **Hellfire does not split into 5.x beams**, instead becoming a **single stronger blast whose damage grows**, analogous to 3.x. This is a compelling candidate for a Hellfire specialization mode, not a commitment to a particular damage progression. Crucially, in *Fiendish Codex II*, Hellfire Blast adds **2d6 per Hellfire Warlock level** (up to +6d6), deals damage that is **not ordinary Fire damage**, and inflicts **1 Constitution damage** per use; the original class explicitly permits the additional damage on multiple targets reached through a Blast Shape such as Chain or Cone. Thus "does not split" should distinguish the **5.x beam mechanic** from historically optional multi-target **Blast Shapes**, whose native eligibility remains to be decided.

Do **not** apply +6d6 independently to every 5.x beam. Candidate conversion: combine the base Eldritch Blast's level-appropriate beam damage into one attack and add a separately scaled Hellfire bonus. This trades several hit chances for one hit/miss outcome and changes critical-hit and on-hit interaction profiles; values, secondary target effects, shape combinations, Constitution cost, and resource gates require owner approval. Hellfire should remain an optional, costly specialization rather than automatic equipment for every Fiend Warlock.

### Infernal Retaliation family — approved concept

Unify the overlapping *Complete Mage* **Fiendish Flamewreath** and *Fiendish Codex II* **Hellfire Shield** defensive retaliation concepts under an **Infernal Retaliation** feature family with distinct selectable variants or upgrades, rather than building two unrelated combat systems.

- **Flamewreath candidate:** sustained protective retaliation without the Hellfire vitality sacrifice.
- **Hellfire Shield candidate:** stronger reactive retaliation with the historical self-sacrifice mechanic (1 Constitution damage in its 3.5e source), subject to conversion of action economy, damage, save, limits, and costs.

**Approved:** shared family / de-duplication approach. **Not yet approved:** free grants, eligibility levels, feature-cost entitlement, variant interchangeability, numerical damage, recovery, or use of Constitution damage unchanged. Reopened base-class Fiendish Resilience healing remains a separate unresolved decision. Do not equate restoration of hit points with restoration of Constitution damage.

### Scope and provenance

Mechanical reconciliation sources for the current effort are 3e/3.5e and 5e/5.5e. **4e is not a mechanical reconciliation source**, although it can be noted for historical provenance where needed. Hellfire Warlock (*Fiendish Codex II*) is a principal 3.5e comparator for the Fiend Patron, with its unique features eligible for merging or optional specialization rather than wholesale adoption of a separate three-level progression. Non-SRD abilities remain research/design candidates pending approval for native executable inclusion. Guided builds must use the same native authoritative parts and budgets as advanced character construction.
