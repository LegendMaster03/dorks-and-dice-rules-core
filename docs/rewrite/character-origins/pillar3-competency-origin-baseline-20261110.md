# Pillar 3 — Historical Origin training conversion scope

**Date:** 2026-11-10  
**Ruleset:** `first-party-001`  
**Status:** Approved conversion *guidance* for historical benefits, not a rule granting training to every Background or Upbringing. See the specific Origin JSON for what is actually authored.

## Correct default and conversion guideline

**Neither Competency proficiency nor a rank is the default for an Origin.** The existence of a Background or Upbringing does not entitle the character to an unrelated Competency. An Origin may instead offer only Skills, equipment qualifications, languages, social/contextual features, possessions, or other approved benefits.

**Only when converting a previously existing training benefit that was retained in the approved native Origin**, use the source type as a starting point. An earlier edition's tool grant is source evidence, not an entitlement that overrides the approved native redesign. Folk Hero and Hermit deliberately omitted historical tool training; Gladiator deliberately replaced it with a narrow weapon qualification; Explorer's split from Outlander is not a mechanical one-to-one tool import. See [source conversion exceptions and whole-package review](pillar3-origin-training-exceptions-review-20261110.md) for the explicit comparisons.

| Earlier training benefit | Starting conversion candidate |
| --- | --- |
| Upbringing's relevant earlier craft/tool training | Proficiency in the matching native Competency |
| Background's relevant earlier craft/tool training | One rank in the matching native Competency |
| Source-specific exception established by balance and source review | Both, neither, or another explicitly supported result |
| No earlier training entitlement to convert | **No newly inferred Competency grant** |

A source that has neither has **no `competency-training` grant**; do not author an empty `{"ranks":0,"proficient":false}` effect. Newly authored choices receive only their specifically approved effects; their Origin type alone does not supply a generic grant.

**Thieves' Tools is a direct conversion, not a candidate ranked Competency conversion.** A previously granted Thieves' Tools proficiency directly becomes the **same narrow binary equipment proficiency**, `equipment.tool.thieves-tools`. It grants neither a Competency proficiency nor a Competency rank; that does **not** make it an exception or a missing benefit. Criminal's fixed grant, Spy's selectable grant, and Street-Raised's selectable grant all use this exact canonical equipment qualification. A redesigned surrounding one-of choice does **not** change the meaning of a chosen Thieves' Tools proficiency. The conditional alternatives still require ordinary choice authorization so the character receives only what they selected.

Other explicitly preserved equipment qualifications (individual Gaming Sets and Musical Instruments) likewise remain equipment, rather than fabricated ranked Competencies. The nature of the source benefit determines its target system; the Upbringing/Background conversion guideline never converts *every* tool into a Competency.

Ranks and proficiency are independent. Existing Competency rank progression, crafting time reductions, unlocked abilities/traits, and check arithmetic are established elsewhere and are **not** reconsidered here.

## Reviewed historical Competency conversions already authored

The following **seven Origin records, containing eight Competency-grant sites**, have direct older training counterparts. Their current effects are authored as `competencyTraining` on the existing source benefit, not in a parallel tool/Skill container.

| Origin | Prior training relationship | Authored conversion |
| --- | --- | --- |
| Charlatan Background | Disguise Kit and Forgery Kit | Fixed Disguise +1 rank; fixed Forgery +1 rank |
| Entertainer Background | Disguise Kit | One selected Disguise +1 rank **or** one Musical Instrument equipment proficiency |
| Guild Artisan Background | One chosen artisan-tool trade | One rank in eligible chosen native craft Competency |
| Guild Merchant Background | Navigator's Tools **or** extra commercial language | Selected Navigation +1 rank **or** the language |
| Sailor Background | Navigator's Tools | Fixed Navigation +1 rank, separate from Water Vehicles |
| General Dwarf Upbringing | Selected traditional craft/tool training | Chosen Smithing/Brewing/Stonemasonry proficiency |
| Rock Gnome Upbringing | Tinker's Tools | Fixed Tinkering proficiency |

The above values are **current source-level authoring decisions for conversion**, not a universal algorithm to run when additional records are added. The original options, independently learned Skills, social features, languages, and equipment remain unchanged.

### Direct equipment proficiency conversions

| Origin | Source benefit | Current executable identity |
| --- | --- | --- |
| Criminal Background | Fixed Thieves' Tools training | Fixed `equipment.tool.thieves-tools` |
| Spy Background | Selected Thieves' Tools alternative | `equipment.tool.thieves-tools`, only if that option wins |
| Street-Raised Upbringing | Selected Thieves' Tools alternative from formative Urchin source | `equipment.tool.thieves-tools`, only if that option wins |

The equipment identity is already known and authored in these three files. The remaining **implementation** task is validating/materializing nested selected option grants through the Origin choice authority, not debating how to translate Thieves' Tools proficiency.

## Independently designed native choices — approved allocations

The five independently authored choice sites now have their source-specific effects in the native JSON. They were **not** assigned automatically from their Background/Upbringing type.

| Origin | Competency options | Approved effect |
| --- | --- | --- |
| Explorer Background | Navigation or Cartography | Selected Competency: **one rank, not proficiency** |
| Spy Background | Disguise, Forgery, or Calligraphy | Selected Competency: **one rank, not proficiency**; alternate Thieves' Tools remains binary equipment proficiency |
| Aristocratic Upbringing | Calligraphy, Painting, or Cooking | Selected Competency: **proficiency, no ranks**; alternate Gaming Set remains a specific equipment proficiency |
| General Goliath Upbringing | Cooking, Leatherworking, or Navigation | Selected Competency: **proficiency, no ranks** |
| Street-Raised Upbringing | Disguise, Cooking, or Forgery | Selected Competency: **proficiency, no ranks**; alternate Thieves' Tools remains binary equipment proficiency |

Every option remains mutually exclusive as previously approved. Eleven competency-training grant outcomes across the five sites now have explicit `competencyTraining` effects; none implies a separate tool entitlement.

Guild Artisan continues to grant **one rank, not proficiency** in a selected trade method. Its approved shared eligibility category is `competency-category.crafting-method` in the native Competency catalog, referenced by `choiceCategoryId` on the Guild Artisan benefit.

## Exceptions for Origins with substantial specialized training

The conversion guideline is **not a ceiling**. A craft-centered Upbringing that is also weaker when compared against complete conventional and cross-cultural character packages may justify **both** proficiency and a rank. A historical training benefit may warrant neither if reviewed source semantics support that treatment; removing an already approved grant still requires a specific decision.

- **General Dwarf** has been explicitly reviewed and remains **proficiency only**, with no starting Competency rank. Its other cultural benefits and scoped Stonecunning effect remain intact.
- **Rock Gnome** is craft-centered but already grants Tinkering, Artificer's Lore, clockwork creation, Prestidigitation, and Magical Tinkering. Its thematic crafting emphasis alone does not justify an extra rank.
- **Guild Artisan** has been explicitly reviewed and remains **one rank only**, with no automatic Competency proficiency.
- **General Goliath, Aristocratic, and Street-Raised** each grant proficiency only on their selected Competency alternatives by individual approval, not as a universal Upbringing rule.
- Origins with no existing native `competency-training` grant (for example Acolyte, Criminal, Noble, Soldier, Sage, Wilderness-Raised, and Giant Foundling) remain valid **neither-Competency-grant** cases. Criminal still has **Thieves' Tools equipment proficiency**; neither refers only to ranked Competency effects.

## Combined-source examples using approved allocations

- General Dwarf selects Smithing and Guild Artisan selects Smithing: the authored direct conversions yield Smithing proficiency from Upbringing and +1 rank from Background.
- Rock Gnome Tinkering and an eligible Guild Artisan Tinkering choice likewise combine proficiency with +1 rank.
- Spy or Street-Raised selects Thieves' Tools: grant the **binary item-tool proficiency only** from that selected option, with no extra rank or Competency training.
- Spy selects a Competency alternative: gain **one rank only**. Street-Raised selects a Competency alternative: gain **proficiency only**. Neither grants the mutually exclusive Thieves' Tools option.
- Criminal plus Rogue repeats the **binary** Thieves' Tools qualification with independent provenance; duplicate equipment proficiency does not create a Competency rank.

The current source JSON is **not** an executable Origin selection authority. The native Competency reducer accepts trusted `NativeCompetencyTrainingGrant(SourceId, OccurrenceId, CompetencyId, Ranks, Proficient)` inputs, and the equipment evaluator also requires selected/authorized source occurrences. Neither should infer a grant from mere source names.

**Current validation target:** Check the seven historical conversion records, all five explicitly allocated native choice sites, the Guild Artisan craft category, and the three direct Thieves' Tools cases. No universal Origin conversion rule is introduced.
