# Pillar 3 — Historical Origin training conversion scope

**Date:** 2026-11-10  
**Ruleset:** `first-party-001`  
**Status:** Approved conversion *guidance* for historical benefits, not a rule granting training to every Background or Upbringing. See the specific Origin JSON for what is actually authored.

## Correct default and conversion guideline

**Neither Competency proficiency nor a rank is the default for an Origin.** The existence of a Background or Upbringing does not entitle the character to an unrelated Competency. An Origin may instead offer only Skills, equipment qualifications, languages, social/contextual features, possessions, or other approved benefits.

**Only when converting a previously existing training benefit to the current Competency model**, use the source type as a starting point:

| Earlier training benefit | Starting conversion candidate |
| --- | --- |
| Upbringing's relevant earlier craft/tool training | Proficiency in the matching native Competency |
| Background's relevant earlier craft/tool training | One rank in the matching native Competency |
| Source-specific exception established by balance and source review | Both, neither, or another explicitly supported result |
| No earlier training entitlement to convert | **No newly inferred Competency grant** |

A source that has neither has **no `competency-training` grant**; do not author an empty `{"ranks":0,"proficient":false}` effect. Even where a native `competency-training` choice already exists, the fact that it is inside an Upbringing or Background alone does **not** settle its rank/proficiency allocation. Preserve approved choice identities pending that separate decision.

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

## Independent native choices without a source-approved Competency amount

The following **five approved training-choice sites** were introduced or materially reallocated during the native Origin reconciliation. Their alternatives remain, but generic source-type conversion does not authorize an independent numeric/binary effect:

| Origin | Retained options | Why automatic allocation is inappropriate |
| --- | --- | --- |
| Explorer Background | Navigation or Cartography | New independent Background allocated from an Outlander split, not direct preservation of one historical training choice with the same current outcomes |
| Spy Background | Disguise, Forgery, or Calligraphy **or** Thieves' Tools | The three ranked Competency alternatives are independently designed. **Thieves' Tools alone is already a direct binary conversion.** |
| Aristocratic Upbringing | Calligraphy, Painting, or Cooking **or** a Gaming Set | New cultural Upbringing; no old source defining the three selected Competency grant amounts |
| General Goliath Upbringing | Cooking, Leatherworking, or Navigation | A reviewed cultural training choice without direct evidence for a specific rank/proficiency conversion amount |
| Street-Raised Upbringing | Disguise, Cooking, or Forgery **or** Thieves' Tools | Newly generalized mixed choice: three Competency alternatives need source-specific allocation, but **Thieves' Tools is already a direct conversion of its older proficiency** |

No `competencyTraining` amount was assigned to the three Competency alternatives in Spy or Street-Raised merely because the binary Thieves' Tools outcome is settled. **Choice existence**, **equipment identity**, and **rank/proficiency amount** are distinct authoring facts.

## Exceptions for Origins with substantial specialized training

The conversion guideline is **not a ceiling**. A craft-centered Upbringing that is also weaker when compared against complete conventional and cross-cultural character packages may justify **both** proficiency and a rank. A historical training benefit may warrant neither if reviewed source semantics support that treatment; removing an already approved grant still requires a specific decision.

- **General Dwarf** is a candidate for source-specific review. Its Dwarvish, one craft choice, and narrow Stonecunning should be compared with other complete packages before adding a rank.
- **Rock Gnome** is craft-centered but already grants Tinkering, Artificer's Lore, clockwork creation, Prestidigitation, and Magical Tinkering. Its thematic crafting emphasis alone does not justify an extra rank.
- **Guild Artisan** can be evaluated for a proficiency-plus-rank exception if its entire Background, rather than the profession label alone, supports it.
- **General Goliath, Aristocratic, and Street-Raised** retain their existing approved Competency alternatives. Their non-equipment training amounts are still open decisions, not automatically Proficient because they are Upbringings.
- Origins with no existing native `competency-training` grant (for example Acolyte, Criminal, Noble, Soldier, Sage, Wilderness-Raised, and Giant Foundling) remain valid **neither-Competency-grant** cases. Criminal still has **Thieves' Tools equipment proficiency**; neither refers only to ranked Competency effects.

## Combined-source examples without inventing unfinished allocations

- General Dwarf selects Smithing and Guild Artisan selects Smithing: the authored direct conversions yield Smithing proficiency from Upbringing and +1 rank from Background.
- Rock Gnome Tinkering and an eligible Guild Artisan Tinkering choice likewise combine proficiency with +1 rank.
- Spy or Street-Raised selects Thieves' Tools: grant the **binary item-tool proficiency only** from that selected option, with no extra rank or Competency training.
- Spy or Street-Raised selects one of its *different* Competency alternatives: the option is approved, but its independent rank/proficiency contribution remains to be allocated source by source.
- Criminal plus Rogue repeats the **binary** Thieves' Tools qualification with independent provenance; duplicate equipment proficiency does not create a Competency rank.

The current source JSON is **not** an executable Origin selection authority. The native Competency reducer accepts trusted `NativeCompetencyTrainingGrant(SourceId, OccurrenceId, CompetencyId, Ranks, Proficient)` inputs, and the equipment evaluator also requires selected/authorized source occurrences. Neither should infer a grant from mere source names.

**Next step:** Verify the seven authored historical Competency conversions and the three direct Thieves' Tools cases with data-focused tests; separately review exception candidates and the five unallocated independent native choices without applying a universal Origin conversion rule.
