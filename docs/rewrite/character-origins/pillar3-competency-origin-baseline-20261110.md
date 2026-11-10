# Pillar 3 — Character Origins Competency grant baseline

**Date:** 2026-11-10  
**Ruleset:** `first-party-001`  
**Status:** **Owner-established default, not a universal requirement.** Default effects authored in Origin JSON; exception review remains open. Character Origins only.

## Source defaults and permitted exceptions

| Source or justified exception | Competency proficiency | Competency rank contribution |
| --- | --- | --- |
| Upbringing baseline | Yes | 0 |
| Background baseline | No | +1 |
| Reviewed intensive training exception | Yes | +1 |
| Reviewed no-training exception | No | 0 |
| Other source-specific exception | As explicitly established | As explicitly established |

The Upbringing/Background split describes **the default conversion of an Origin grant that actually teaches a Competency**; it **does not require every Origin to grant one**. A Background or Upbringing can award neither, including when its significance is expressed by Skills, languages, a social feature, a contextual effect, or other approved benefits. The source type is not proof of training.

- `competencyTraining: {"ranks": 0, "proficient": true}` explicitly records the Upbringing proficiency-only default.
- `competencyTraining: {"ranks": 1, "proficient": false}` explicitly records the Background rank-only default.
- `competencyTraining: {"ranks": 1, "proficient": true}` may be authored **only** for a justified exception.
- **Neither** is represented by **no `competency-training` grant at all**, *not* by `{"ranks": 0, "proficient": false}`. The existing validated grant reducer correctly rejects a zero-effect grant. Removing a previously approved grant to make room for an exception requires a separate Origin decision; none has been removed in this pass.

Individual earned rank increments accumulate, and proficiency remains an independent Boolean. Rank progression, crafting time, newly unlocked abilities/traits, and check arithmetic are already established outside this Character Origins pass; **do not redesign them here**. Ranks are **not** treated merely as a one-to-one bonus.

The baseline and the exceptions apply at the **actual selected grant option**, not all alternatives in a mixed benefit. Thieves' Tools, specific Gaming Sets, and specific Musical Instruments remain binary equipment qualifications. Their choices do not imply an extra Competency grant.

## Authored Origin grant sites

The following **13 sites in 12 JSON records** had an existing `competency-training` grant and now carry an explicit `competencyTraining` effect. Some sites have multiple options; the effect applies only to the actually chosen Competency.

| Background | Competency outcome | Current default |
| --- | --- | --- |
| Charlatan | Fixed Disguise and fixed Forgery (two sites) | Each +1 rank |
| Entertainer | One rank in Disguise **or** one specific Musical Instrument qualification | +1 rank only if Disguise selected |
| Explorer | Navigation **or** Cartography | +1 rank in selected Competency |
| Guild Artisan | One eligible crafting Competency | +1 rank in selected Competency |
| Guild Merchant | Navigation **or** an extra commercial language | +1 rank only if Navigation selected |
| Sailor | Fixed Navigation | +1 rank |
| Spy | Disguise / Forgery / Calligraphy **or** Thieves' Tools qualification | +1 rank only for selected Competency |

| Upbringing | Competency outcome | Current default |
| --- | --- | --- |
| Aristocratic | Calligraphy / Painting / Cooking **or** one Gaming Set qualification | Selected Competency proficiency |
| General Dwarf | Smithing / Brewing / Stonemasonry | Selected Competency proficiency |
| General Goliath | Cooking / Leatherworking / Navigation | Selected Competency proficiency |
| Rock Gnome | Fixed Tinkering | Tinkering proficiency |
| Street-Raised | Disguise / Cooking / Forgery **or** Thieves' Tools qualification | Selected Competency proficiency |

No previously approved Skill, social feature, language, equipment qualification, possession, selected option, or Upbringing parent relationship was changed by this grant-effect pass. Origins without Competency training remain legitimate **neither** cases.

## Exception review, with the actual full package in mind

**General Dwarf — strongest candidate to review for both:** It has Dwarvish, one of three chosen craft proficiencies, and narrow Stonecunning. Compared with General Goliath (Gol-Kaa, a chosen Skill, and a chosen Competency), General Dwarf may have less broadly usable learned training. Its emphasis on making and material craft provides a source-based reason to consider **proficiency +1 rank**. This is **not yet evidence of a net full-character imbalance**: compare the complete Species–Ancestry–Upbringing–Background package and the actual campaign's utility of Stonecunning. No extra rank is granted now.

**General Goliath — comparative review:** It has a learned language, a chosen Skill, and a chosen practical Competency without another major Upbringing feature. Compare complete conventional and substituted packages, especially Giant Foundling. A particular Goliath clan might warrant intensive craft training, but the baseline General Goliath grant does not assert that intensity; no automatic extra rank.

**Rock Gnome — intensive craft identity, but substantial other benefits:** Tinkering, Artificer's Lore, clockwork devices, Prestidigitation, and Magical Tinkering form a strong package with its General Gnome parent language. A craft emphasis alone does **not** establish that the package is underpowered. Keep proficiency-only pending package review.

**Aristocratic and Street-Raised — no automatic crafting upgrade:** Their possible craft Competency is one option within larger packages (language, chosen Skill, and cultural or travel feature), not a categorical promise of advanced crafting. Preserve the selected-Competency proficiency default or their distinct equipment alternative.

**Backgrounds:** Guild Artisan is an obvious place to evaluate a possible proficiency-plus-rank *exception* because of sustained craft work, but its additional Skills, language, guild feature, and inventory count toward the whole Background. Charlatan already grants **two separate Competency ranks**, and Spy/Guild Merchant/Entertainer contain cross-domain alternatives. No additional proficiency or rank is assumed to balance display counts.

**Neither cases:** Acolyte, Folk Hero, Hermit, Sage, Noble, Soldier and other Backgrounds/Upbringings with no existing native `competency-training` remain without one. Their other approved benefits are not deficient merely because no Competency is present. A future source may replace a prior Competency training benefit with a feature-only outcome if its own Origin pass authorizes the change; this migration does **not** revoke existing grants.

## Consequences under current defaults

- General Dwarf (Smithing) + Guild Artisan (Smithing): **proficient Smithing with one rank** from distinct granted sources.
- General Goliath (Navigation) + Sailor (Navigation): **proficient Navigation with one rank**; Sailor's Water Vehicles qualification is separate.
- Street-Raised (Forgery) + Charlatan: **proficient Forgery with one rank**; Charlatan also contributes its separate Disguise rank.
- Rock Gnome Tinkering + Guild Artisan choosing Tinkering, if eligible: **proficient Tinkering with one rank**.
- Guild Artisan without a matching proficiency source: **one rank**, with proficiency determined separately.
- Spy choosing Thieves' Tools instead of a Competency: **neither Competency rank nor proficiency from that specific choice**; only the canonical binary equipment qualification.

These examples describe selected source effects. The future Origin selection authority must still validate actual characters, choices, and source occurrences before the existing native reducer combines them.

## Implementation and review boundary

`competencyTraining` is authored data, not a new class or separate container; it is two independent effect fields on the existing **single** `competency-training` benefit. The runtime currently accepts `NativeCompetencyTrainingGrant(SourceId, OccurrenceId, CompetencyId, Ranks, Proficient)` from **already-authorized** inputs. This pass does not implement the Origin grant projector, alter rank progression, or infer extra equipment qualifications.

A future approved exception is a **local change to the justified Origin**. Do not update every Background or Upbringing when one exception is approved. The regression tests should enforce explicit well-formed grant effects and the approved current defaults, without imposing a fixed total number of Origins or a permanent global ban on exceptions.

**Next review:** Evaluate General Dwarf against complete conventional/cross-cultural alternatives, then General Goliath and Guild Artisan as needed. Do not apply the prospective both/neither exceptions without the specific Origin's balance decision.
