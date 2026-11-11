# Pillar 3 — Guild Artisan and Guild Merchant Source Reconciliation

> **Current native decision (2026-11-10):** Both Guild Artisan and Guild Merchant are now selected-native independent Backgrounds. Guild Artisan grants one rank only in one guild-appropriate Competency from the authoritative shared `competency-category.crafting-method` list in `native/competencies.json`, referenced by `choiceCategoryId`. Prior statements below calling Guild Merchant unapproved or the category unresolved describe an older review stage. See [current training scope](pillar3-competency-origin-baseline-20261110.md).

**Status:** Guild Artisan approved and authored in the native catalog; Guild Merchant remains a separate unapproved candidate. Earlier proposal and approval-boundary sections below are historical analysis, superseded by the final decision.

**Ruleset:** `first-party-001`  
**Branch:** `feature/character-origins-pillar3-life-experiences`

## Purpose and established principles

Reconcile the 2014 Player's Handbook Guild Artisan Background against the native Skills and Competencies without automatically importing the published Guild Merchant as an inherited Background variant.

Project principles already established:

- **Upbringing** describes the character's formative community and learned cultural practices; **Background** describes meaningful experiences, institutional roles, and acquired training. They may overlap in a character's story without being the same grant.
- Distinct Backgrounds should stand on their own even where they share a generic organization/contact feature. A published label of *variant* is source evidence, not an instruction to build a subtype.
- Native Background selection does not automatically add 2024 ability score bonuses or Origin feats.
- The independently authored text belongs in native content only after owner approval; PHB expressive passages are not assumed CC-BY merely because they can be researched publicly.
- Starting equipment is ordinary inventory; no extra special container or separate equipment mode is invented.

## 2014 baseline

| Source element | Guild Artisan (2014) |
| --- | --- |
| Skills | Insight, Persuasion |
| Tool | One type of artisan's tools |
| Language | One additional language of choice |
| Feature | Guild Membership |
| Starting possessions | One artisan's tool set, a letter introducing the member, traveler's clothes, pouch containing 15 gp |

**Guild Membership** functionally supplies potential assistance among guild members (including temporary food and lodging), professional introductions and contacts, conditional institutional backing and access to influential figures, and related support in suitable circumstances. Original source establishes a **5 gp monthly guild due** and good-standing requirements; membership is not an unrestricted entitlement to goods, legal immunity, or privileged audiences.

**Historical Guild Merchant variant:** Guild involvement in trade, shopkeeping or goods movement rather than practicing a craft. Source allows a substitution for the artisan-tool proficiency (navigator's tools or an additional language) and a possible mule-and-cart starting package. The historical text shares Guild Membership, but the project has rejected automatic variant inheritance as a design principle.

Sources:
- Official D&D Beyond 2014 background index: https://www.dndbeyond.com/backgrounds
- Secondary 2014 Guild Artisan breakdown: https://dnd5e.wikidot.com/background:guild-artisan
- Historical Guild Merchant mechanical comparison: https://planar-invasion.neocities.org/compendium/backgrounds/guild-artisan-guild-merchant-variant
- Guild Membership details and 5 gp dues: https://dnd-5e.fandom.com/wiki/Guild_Artisan

The source text is *non-SRD PHB expression*. Native wording must be independently written; mechanical distinctions and general vocational concepts may inform the design.

## Guild Artisan — independent Background proposal

### Identity

You have practical experience in a craft or trade and belong, or previously belonged, to a professional guild or similar organized association of practitioners. This need not mean hereditary membership, ownership of a business, a monopoly over the trade, or a prescribed childhood apprenticeship. Characters may have learned the craft later in life or worked in an artisan household while growing up; that history should not force the same Upbringing.

The **guild** should be a real association in the character's world if the Guild Membership feature is selected, rather than a fictional global credential recognized everywhere. Where a setting has trade associations with different names, an equivalent institution may perform the same function.

### Skills — retain the 2014 baseline

- **Insight:** `skill.insight`
- **Persuasion:** `skill.persuasion`

These reflect evaluating transactions and interacting within professional networks. They remain Background training independent of a character's upbringing. No requirement to transfer either to an Upbringing.

**Unapproved alternative:** Later assess Appraise or another commerce-related skill as a possible replacement only if the native skill system's conceptual distinction warrants it. Do not silently substitute a 3.5e Skill merely to be different.

### One selected native craft Competency — major reconciliation point

The 2014 source grants one type of artisan's tools. The current Dorks & Dice catalog defines more complex native **Competencies** with associated crafting skills and/or tool proficiencies. They are not perfectly interchangeable with a 2014 tool-only grant.

**Proposed native rule:** Choose **one existing native craft Competency** corresponding to the guild trade. Examples include:

- `competency.smithing` or `competency.blacksmithing` — metalworking (these are separate native definitions; their differences must not be silently flattened).
- `competency.leatherworking`
- `competency.cooking`
- `competency.carpentry`
- `competency.weaving`
- `competency.pottery`
- `competency.calligraphy`
- `competency.cartography`
- `competency.brewing`
- `competency.alchemy`

This list is **illustrative, not an approved exhaustive eligibility registry**. Other craft Competencies in `native/competencies.json` are eligible if they correspond to a suitable trade; combat or covert tool training such as Disguise Kit, Thieves' Tools and Forgery are not assumed to be artisan trades solely because those records are also called Competencies. Further eligibility and power comparisons are necessary before selecting a closed list.

**Important balance question:** Some native Competencies have **crafting-skill and tool facets**, while others have only one. Granting a full native Competency is not always equivalent to the original 2014 tool proficiency. This is an intentional subject for later corpus review, not evidence that all native trades must be reduced to tool proficiency.

The 2014 equipment should provide tools appropriate to the selected trade **only where corresponding tools exist**. A full workshop, materials, recipes, and bulk resources are not automatically granted; tool-to-inventory identity resolution remains deferred.

### Language — proposal

Learn **one language of choice associated with the guild's work, members, clients, or trade relationships**, including Common where it is used in that context. This retains the 2014 one-language amount while respecting the project pattern of experience-linked language choices. No secret professional language is assumed.

### Guild Membership — proposed original native wording

> **Guild Membership.** Your experience and standing with a recognized craft guild or comparable trade association can provide introductions to other members, access to professional contacts, and modest food or lodging when the organization can reasonably assist you. The guild may offer institutional support, including advocacy or introductions to influential people, when your interests and its own align. Such help depends on the organization's reach, resources, and your standing with it; it does not guarantee legal immunity, unrestricted aid, or access to every guild. Maintaining good standing ordinarily requires **5 gp in monthly dues**, as in the 2014 source, with specific circumstances resolved by the DM.

**Approval question:** Preserve the source's explicit **5 gp/month dues** in native mechanics, or replace it with an institution-dependent customary obligation? The source value is meaningful (a recurring cost compared with the 15 gp starting purse), and should not be changed without a stated system/balance reason. Recommended initial baseline: **retain 5 gp/month**, without building a dues collection, subscription, or recurring automation system. Missing dues can affect standing as the source describes, under ordinary DM adjudication.

**Distinctions from existing social features:**
- `background.acolyte`: religious community support, rites and care, not a professional trade network.
- `background.noble`: social access due to recognized noble status, not guild membership.
- `background.soldier`: service standing and qualified requisitions within an acknowledged force.
- `background.spy`: obtaining leads from personal intelligence sources rather than guild-wide trade support.

A formerly active member retains learned Skills/Competencies even if current guild privileges are diminished or unavailable. No universal substitute patron or new guaranteed membership is invented.

### Starting equipment — source-faithful candidate

- Tools appropriate to the chosen craft Competency, where a suitable tool set is defined.
- Letter of introduction or other evidence of recognized professional guild connection.
- Traveler's clothes.
- Pouch with **15 gp**.

Equipment is ordinary inventory. Membership dues are a separate ongoing institutional obligation and are **not** automatically prepaid or deducted from the initial gold unless the player and DM establish that as part of play.

### Narrative and variants

The profession is a **choice of trade**, not a separate formal Background variant for each of the 2014 Guild Business examples. A brewer, mason, and cartographer may use Guild Artisan and choose different craft competencies.

An **Artisan Household / Craft-Raised Upbringing** is a possible future formative option, **not required to use Guild Artisan**. Being raised around artisans does not automatically confer a guild membership privilege; membership in later life need not imply such upbringing.

Guild Merchant should be **reconciled as a separate Background candidate**, not an inherited Guild Artisan variant. It may share a bounded institutional feature because merchant associations can provide similar aid, but its vocational competence, skills, language and equipment must be considered independently. Merchant is not automatically a craftsman or automatically a guild member. A later generic Merchant Background is a separate scope decision.

## Tests for the future corpus balance review

1. **General Dwarf Upbringing + Guild Artisan:** Dwarf crafting tradition may grant an overlapping craft Competency. Establish general duplicate semantics before giving extra ranks or replacement grants.
2. **General Goliath Upbringing + Guild Artisan:** Clan Craft may overlap Cooking or other native craft choices.
3. **Aristocratic Upbringing + Guild Artisan:** Social training and trade experience can coexist; Persuasion or language may overlap without automatically stacking.
4. **Street-Raised + Guild Artisan:** A street-raised craft worker should not have to take Criminal to enter a guild.
5. **Guild Artisan versus Guild Merchant:** Can the character earn money and institutional recognition through trading without learning to manufacture goods?
6. **Guild Artisan versus Sage:** Calligraphy, Cartography, and scholarly work can be trade skills or academic experiences; the Background feature should distinguish craft association from research access.
7. **Membership standing:** Consider whether missing dues, loss of contact, limited institutional reach, or an incompatible local guild affect the feature's availability without erasing trained competencies.
8. **Economic balance:** The monetary value of selected tool sets and the 5 gp/month obligation may matter more than the 15 gp cash award; compare with other Background starting inventories and organizational benefits.

## Approval boundary

Nothing in this document authorizes an active `native/backgrounds/guild-artisan.json` file yet. Before promotion, approve the fixed skills, selected-craft Competency principle and eligibility, trade-related language, scope and dues of Guild Membership, and starting-equipment description. Guild Merchant remains a separate, unapproved follow-up; there is no native Background inheritance or generic guild-management implementation.


## Approved Guild Artisan native allocation — 2026-11-04

**Owner decision:** Approve Guild Artisan with the 2014 **Insight and Persuasion** skills; **one** native craft Competency appropriate to the character's trade; **one** language associated with the guild, members, clients or trade; **Guild Membership** with the original **5 gp monthly dues**; and **one relevant tool set where defined, guild introduction evidence, traveler's clothes and 15 gp**. This approval authorizes the native Guild Artisan record, but does not authorize Guild Merchant.

**Native record:** `data/rulesets/first-party-001/native/backgrounds/guild-artisan.json` (`background.guild-artisan`, `selected-native`, independently selectable).

**Selected native craft Competency:** One training choice from the existing native crafting catalog corresponding to the guild trade. The native record uses `choiceCategory: crafting-competency`, `choiceCount: 1`, and `choiceConstraint: appropriate-to-character-guild-trade`, with illustrative examples rather than a closed list. Some crafting Competencies include skill-rank facets, some include tool proficiency, and some both; **this mechanical inequality is intentionally preserved for the corpus-wide review**. The category resolver is deferred, and unrelated competencies such as Thieves' Tools are not automatically valid guild crafts.

**Trade language:** A language the character learned through guild work or its commercial relationships, including Common when appropriate. This is a Background grant, independent of Upbringing.

**Guild Membership:** Provides conditional professional contacts, introductions, modest hospitality and institutional advocacy where reasonable and within guild reach. **5 gp/month** is an ordinary dues obligation affecting standing under DM adjudication. Do not build an automated recurring-payment mechanism or apply an automatic deduction from starting gold. Training persists even if guild ties or good standing change.

**Starting possessions:** Appropriate trade tools when the selected craft has corresponding equipment, an introduction letter or evidence of membership, traveler's clothes, pouch and **15 gp**, all ordinary inventory. No new workshop, recipes, materials, bulk resources or fictitious tool kit is granted. Item identity and conditional matching to the chosen native craft are deferred to normal equipment materialization.

**Guild Merchant:** Still requires its own source and grant reconciliation as an **independent Background**, not a variant or automatic inheritor of Guild Artisan. No Merchant content has been promoted.

**Outstanding general work:** Resolve semantic `crafting-competency` eligibility, item-tool mapping, multi-facet benefit balance and duplicate training from Dwarf/Goliath or future non-Species Upbringings through the planned corpus-wide review. No executable C# or consumer API changes are authorized by this content promotion.

This approved allocation supersedes the earlier unapproved options and approval boundary in this document.
