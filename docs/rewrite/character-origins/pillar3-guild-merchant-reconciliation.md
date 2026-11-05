# Pillar 3 — Guild Merchant Background Reconciliation

**Status:** Guild Merchant approved and authored as an independent native Background. The earlier proposal and unapproved alternatives are preserved as historical analysis; the final approved allocation appears below.

**Ruleset:** `first-party-001`  
**Branch:** `feature/character-origins-pillar3-life-experiences`

## Scope and project decisions

Guild Merchant is a candidate for a **new, independent Background**, not a `Guild Artisan` variant, inherited specialization, or automatically derived package. The owner approved independent Guild Artisan earlier; that approval does **not** settle Guild Merchant's training and inventory.

**Guild Merchant** specifically represents a merchant's experience with a recognized guild, consortium or equivalent professional trading association. A merchant who has never belonged to such an organization may justify a later independent `Merchant` Background, but granting everyone guild benefits by renaming ordinary commerce would be incorrect.

Upbringing remains a separate, formative cultural choice; the merchant could have been raised in a city, village, aristocratic household, on the streets, among traveling peoples, or in another community. Guild membership is **not inherited by birth** and does not automatically grant a craft-producing Competency.

## Source comparison

The 2014 *Guild Merchant* is published as a **variant of Guild Artisan**. It maintains the two Skills (Insight and Persuasion), one general learned language, and Guild Membership (including **5 gp/month guild dues**). Unlike a craftsperson, a merchant could exchange artisan-tool proficiency for Navigator's Tools **or another language** and could substitute **a mule and cart** for the artisan's tool set. It remains subject to the source Background's other starting inventory, including introduction letter, traveler's clothes and 15 gp.

- Legacy source comparison: https://dnd5e.wikidot.com/background:guild-artisan
- D&D Beyond forum documenting the PHB's Guild Merchant replacement choice: https://www.dndbeyond.com/forums/d-d-beyond-general/bugs-support/29921-guild-merchant-variant-proficiencies-and-equipment
- Guild Artisan approved source reconciliation: `docs/rewrite/character-origins/pillar3-guild-artisan-reconciliation.md`
- Native Guild Artisan: `data/rulesets/first-party-001/native/backgrounds/guild-artisan.json`

The published mechanical choice is evidence, not authorization to copy non-SRD PHB expressive language into our native content.

## Proposed independent Guild Merchant package — NOT YET APPROVED

### Character identity

You have experience buying, selling, transporting, procuring, assessing, or brokering goods in a trading organization. You may have been a shopkeeper, traveling trader, caravan organizer, purchasing agent, merchant-factor, shipper, or broker. Guild membership may have been obtained after childhood; it does not require an inherited business, production craft, a specific Species or Upbringing, continuing profitable trade, or ownership of a ship or caravan.

### Skills — recommended targeted departure from 2014

- **Fixed:** `skill.persuasion`, representing the human and institutional negotiation side of commerce.
- **Choose one:** `skill.insight`, `skill.appraise`, or `skill.gather-information`.
  - Insight: assessing partners, negotiations and incentives.
  - Appraise: judging goods and their worth.
  - Gather Information: discovering market opportunities and commercial contacts.

These IDs exist in the native Skill catalog. Unlike the 2014 variant, this is a **genuine independent merchant training choice**, not simply inheriting the two Guild Artisan skills. Selecting Insight still permits the original 2014 pair when appropriate.

**Caution:** The seeded `skill.appraise` and `skill.gather-information` definitions do not yet have the same depth of rule metadata as `skill.insight` or `skill.persuasion`. Their availability is confirmed, but consumer skill-action execution and balance need checking during the common skill integration pass; do not fabricate missing DCs or retroactively award ranks.

**Alternative to decide:** Keep Insight + Persuasion fixed exactly as published, and distinguish Merchant entirely through navigation/language options and equipment. That maximizes source consistency but reduces the value of defining Merchant independently. **Recommendation: fixed Persuasion with one of the three commercial Skill choices.**

### Qualification — Navigator's Tools or a second learned language

**Recommended choice:** Select exactly **one** of the following, reproducing the functional 2014 choice without inheriting the Guild Artisan crafting grant:

1. `competency.navigation` — proficiency with Navigator's Tools, matching the established native Competency. It provides **Navigation**, not a new crafting trade, cart driving qualification, unrestricted vehicle operation or magical routefinding.
2. One **additional language**, learned through commercial dealings.

This is **one alternative choice**, not Navigation plus another language. The choice may require a native grant option group able to express different result types; author the meaning explicitly and defer generic projection to the intended integration work. Do not invent a single `competency.merchant` to hide this distinction.

### Baseline commercial language

Retain the **one** 2014 Background language, chosen from languages reasonably used among the guild's members, partners, clients, markets or trade routes; **Common is eligible**. This language is granted in addition to the optional extra language from the choice above, if that option is selected. A character could therefore gain **one or two Background languages**, not automatically two.

**Power comparison:** Navigation versus a second language can have very different mechanical value depending on the campaign. This is an intentional source-derived alternative for the corpus review, rather than a claim of strict equivalence. Duplicate-language choices are handled by the eventual general duplicate-grant rule.

### Defining feature — Guild Membership (shared functional rule, independent Background)

Recommend **the same functional feature and monthly dues** as approved Guild Artisan, rewritten for commerce rather than a craft:

> **Guild Membership.** Your previous experience and recognized standing within a traders' guild, merchant association, or comparable commercial organization can provide introductions to fellow members, professional contacts, and modest food or lodging when the organization can reasonably help. Your association may advocate on your behalf or arrange introductions to influential figures when it serves the group's interests. This aid depends on its reach, resources and your standing; it does not provide guaranteed credit, favorable prices, unrestricted access to goods, legal immunity, or universal help from every merchant. Maintaining good standing ordinarily requires **5 gp in monthly dues**, with missed payments and circumstances handled by the DM.

**Why a shared feature is appropriate:** Guild Merchant and Guild Artisan derive network support from the *same kind of institutional relationship*, even though their vocational training differs. The earlier objection was against inheriting a complete unrelated Background package by narrative variant, **not against two independent Backgrounds using a legitimately shared rule**. Do not invent a separate market-network superpower merely for mechanical novelty.

A former member keeps acquired Skills, languages and qualifications when privileges lapse, though the availability of contacts remains contextual. A generic non-guild Merchant, if subsequently authored, should not receive Guild Membership automatically.

### Starting equipment — 2014-derived candidate

Recommended initial package:

- One **mule and cart** instead of a craft tool set, matching the source option.
- A letter of introduction or similar record of membership in the merchant association.
- Traveler's clothes.
- A pouch containing **15 gp**.

No artisan tool set, free cargo inventory, starting merchandise stock, guaranteed trade credit, shop or warehouse, or automatic trading profit. Neither ownership of a vehicle nor purchase of animals grants a Land Vehicles qualification by itself.

**Open concern:** A mule and cart are materially different from a handheld artisan tool set, and particularly unsuitable for a sedentary shopkeeper or sea-based trader. Two ways to resolve this without inventing a merchant-management subsystem:

- **A — Retain the mule-and-cart source package** for the initial native corpus (recommended), with ordinary in-game disposal/replacement or a case-by-case DM agreement for unsuitable campaigns. This gives a concrete historical baseline for the balance review.
- **B — Offer a mutually exclusive merchant-inventory choice** of mule/cart or a defined comparable trading kit. This requires specifying the alternate kit and its value before native approval.

The suggested initial approval should select **A**, but the owner may prefer B to make Guild Merchant less transport-focused. Ordinary travel and animal logistics remain table adjudication; no new caravan ownership, inventory economics, trading rules or follower systems are authorized.

## Comparison with other native Backgrounds

| Dimension | Approved Guild Artisan | Proposed Guild Merchant |
| --- | --- | --- |
| Skills | Insight + Persuasion | Persuasion + one of Insight/Appraise/Gather Information |
| Vocational training | One native craft Competency | Navigation **or** one additional language |
| Baseline language | One trade-linked language | One trade-linked language |
| Feature | Guild Membership, 5 gp/month | Guild Membership, 5 gp/month, independent record |
| Ordinary inventory | Craft tool set when defined, introduction, clothes, 15 gp | Proposed mule/cart, introduction, clothes, 15 gp |
| Upbringing link | None required | None required |

## First corpus tests

1. **Guild Artisan versus Guild Merchant:** Do trade production and commercial brokerage remain distinct once both use Guild Membership?
2. **Guild Merchant versus Sailor:** Navigator's Tools training does not turn merchant experience into general seafaring or a Sailor's shipboard feature.
3. **Guild Merchant versus Guild Artisan with Cartography:** Navigation and Cartography are separate native Competencies; confirm their scopes rather than substituting one for the other.
4. **Guild Merchant versus Noble or Knight:** Institutional commercial contacts do not independently confer noble privilege or retainers.
5. **Guild Merchant with Itinerant Upbringing:** A mobile childhood may teach navigation; overlapping grants use the general duplicate policy, not doubled proficiency.
6. **Guild Merchant with General Goliath:** Clan Navigation and Merchant Navigation may overlap; audit duplicates.
7. **Language and equipment:** Compare the value of an additional language with Navigation; review mule/cart and feed/transport burdens against portable craft tools.
8. **Legacy commerce Skills:** `skill.appraise` and `skill.gather-information` are present but comparatively thin; determine the intended skill-action semantics in the general skill audit without changing the approved Background's selected identity implicitly.

## Approval boundary

**No `native/backgrounds/guild-merchant.json` has been authorized.** Approval is still needed for the Skill choice, choice of Navigation or extra language, trade-associated baseline language, shared Guild Membership and 5 gp/month obligation, and mule/cart equipment choice. The record is independent of Guild Artisan, and no runtime changes or payment/trade subsystem are within scope.


## Approved native Guild Merchant allocation — 2026-11-04

**Owner approval:** The recommended independent Guild Merchant package is approved, including its fixed Persuasion, one commercial Skill choice, the Navigation-or-language alternative, one trade-related baseline language, the shared Guild Membership feature with **5 gp monthly dues**, and **mule-and-cart** starting equipment with **15 gp**.

**Native record:** `data/rulesets/first-party-001/native/backgrounds/guild-merchant.json`, selected as `background.guild-merchant`. It is **not a variant or inheriting subtype** of Guild Artisan.

### Approved mechanics

- **Skill 1 (fixed):** Persuasion (`skill.persuasion`).
- **Skill 2 (choose one):** Insight, Appraise, or Gather Information (`skill.insight`, `skill.appraise`, `skill.gather-information`).
- **Baseline language:** One language used through trade, merchants, partners, clients, markets, or trade routes. Common may qualify.
- **Additional professional choice (choose one total):** Navigation Competency (`competency.navigation`, corresponding to Navigator's Tools) **or** one **additional** learned commercial language. Both options are not granted. The native file expresses this as a `benefit-choice` with `options` and `choiceCount: 1`, whose individual option benefits have different grant types. Executable projection remains deferred to common integration.
- **Guild Membership:** The conditional trade-organization network feature already functionally approved for Guild Artisan, with **5 gp monthly dues** and ordinary DM adjudication of standing, assistance and payments. It does not create guaranteed credit, market discounts, arbitrary profits or automatic access to every merchant group.
- **Equipment:** Mule, cart, guild letter or membership evidence, traveler's clothes, pouch, and **15 gp**. No artisan tool set, shop, trading cargo, caravan inventory or automatic land-vehicle proficiency.
- **Origin independence:** Any Species and Upbringing may qualify. Membership is acquired through a trade association, not automatically inherited. A non-guild merchant remains a separately potential Background, not an entitlement to Guild Membership.
- **No extra mechanics:** No trade economy, subscription/dues tracker, caravan/follower subsystem, combat animal feature, Origin feat, fixed ability score changes, or Guild Artisan craft Competency.
- **Review boundary:** Appraise and Gather Information action metadata, mixed-result choice projection, proficiency duplication, relative value of Navigation vs language, and mule/cart value relative to craft tools require the later general review.

This final section **supersedes earlier unapproved recommendations and the approval boundary above** without erasing their source comparison.
