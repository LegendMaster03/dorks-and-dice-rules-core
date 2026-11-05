# Pillar 3 — Urchin Source to Street-Raised Upbringing Reconciliation

**Status:** Final selection-count proposal for owner review only. Earlier Street-Raised scope, language, training pools and Urban Navigation are **already approved**; the selection counts, native name/ID, and complete selectable package are **not yet approved**. No native Upbringing or Urchin Background is added by this design note.

**Ruleset:** `first-party-001`  
**Development branch:** `feature/character-origins-pillar3-life-experiences`

## Previously approved boundaries — retain unchanged

The 2014 *Player's Handbook* Urchin source is assigned **entirely to an independent Upbringing**, not to a separate native `background.urchin`. Its defining circumstance—learning ordinary practices and movement through the streets and informal networks of a substantial settlement—is **formative experience**. A later occupation is independently selected as Background. No orphanhood, poverty, criminality, abandonment, Species, age cutoff, or fixed profession is implied. Merely having a city address is not sufficient; the environment should genuinely have shaped the character's formative practices.

Already approved in `docs/rewrite/character-origins/pillar3-2014-background-intake.md`:

1. **One learned community language**. The language must have been used by the household or formative community. Common is eligible where appropriate, but not universally automatic.
2. **Skill choice pool (exactly five):** `skill.stealth`, `skill.sleight-of-hand`, `skill.perception`, `skill.insight`, `skill.survival`.
3. **Competency choice pool (exactly four):** `competency.disguise-kit`, `competency.thieves-tools`, `competency.cooking`, `competency.forgery`.
4. **Urban Navigation** — already approved wording, preserve rather than silently balance-nerf:

   > **Urban Navigation.** When traveling outside combat through a substantial settlement, you can identify efficient routes through its accessible streets, alleys, and passages. You and companions following your guidance can travel between locations in half the usual time. This benefit does not bypass physical barriers or grant additional movement during combat.

5. **No Upbringing starting equipment**, mandatory fixed occupational training, or 2014 Urchin pet mouse/parent token/childhood gear. Ordinary starting inventory comes from the selected Background and other ordinary sources.
6. **Independence:** Can combine with Criminal, Spy, Charlatan, Sage, Soldier, or any other Background. The specific grant duplication/replacement policy is **deferred**; do not invent a special Street-Raised stacking rule.
7. **No separate Urchin Background.** The original source's equipment is historical reference, not a second mechanical entitlement.

The feature has an actual **half-time noncombat traversal result** and applies to companions guided by the character. It does not automatically reveal hidden entrances, grant passage through guarded property, change travel speed in wilderness or combat, grant teleportation, or require a separate urban pathfinding subsystem. Resolve unusual obstructions and access restrictions normally. Do not impose a new home-city-only or studied-map-only condition that would narrow the approved rule.

## Verified 2014 mechanical source — historical reference, not target rules

2014 Urchin provided fixed **Sleight of Hand and Stealth**, **Disguise Kit and Thieves' Tools**, **no language**, the *City Secrets* half-time urban travel benefit for companions guided outside combat, and **small knife, home-city map, pet mouse, token remembering parents, common clothes, and 10 gp** as starting items. This source explicitly assumed childhood deprivation and orphanhood; those are deliberately **removed** as mandatory Street-Raised narrative facts.

Evidence:
- 2014 Urchin historical transcription: https://dnd5e.wikidot.com/background:urchin
- 2014 feature and items reference: https://dnddata.wikidot.com/backgrounds
- 2014 source pointer: https://dnd-wiki.org/wiki/Urchin_(5e)
- Prior decision record and approved original Dorks & Dice feature expression: `docs/rewrite/character-origins/pillar3-2014-background-intake.md`.

Mechanic-only comparison to the PHB is appropriate; independently author native prose. Urchin is not one of the SRD 5.1 Background entries and the proposal does not import the PHB's narrative tables.

## What remains: the number of training choices

**Do not confuse the 2014 Background grant count with a Dorks & Dice Upbringing baseline.** The original 2 Skills + 2 tools accompany a full **Background**. Here Street-Raised is one formative selection **in addition to** an independently selected Background, which may itself supply two Skills, tools, contacts, and equipment. Transplanting all four original training grants would stack an entire source Background on top of a native Background.

### Comparisons with existing native Upbringings

| Native Upbringing | Learned languages | Skills / competencies | Nontraining feature | Balance observation |
| --- | --- | --- | --- | --- |
| `upbringing.wilderness-raised` | One | Choose **one Skill**; no Competency | Wilderness Provisioning for self and up to five others when natural food/fresh water is available | Closest general-culture comparison; provisioning can be powerful in wilderness campaigns but less valuable in cities |
| `upbringing.goliath.general` | One (Gol-Kaa) | Choose **one Skill**, **one Competency** | No separate feature | Comparable number of training grants to the proposed Street-Raised option but no half-time urban travel |
| `upbringing.giant-foundling` | Giant plus one extra | **Two fixed Skills** | No separate Upbringing feature | More fixed training and language, less flexible than Street-Raised's proposed selection |
| `upbringing.elf.general` | Elvish | Choose one of three training options | No separate feature | Some Elven specializations inherit other separate benefits; do not compare a combined Elf package as if only the parent existed |
| `upbringing.dwarf.general` | Dwarvish | One crafting Competency choice | Stonework-history knowledge bonus | Narrower practical options plus a specific situational modifier |

The general-culture Upbringings do not have one normalized power budget yet, so the comparison is informative, **not proof of mathematical parity**.

### Four candidate selection-count models

| Model | Street-Raised training in addition to language + approved Urban Navigation | Assessment |
| --- | --- | --- |
| **A — recommended** | **Choose one Skill and one Competency**, independently, from the approved pools | The 2014 Skill/tool distinction survives in reduced form, preserving ordinary formative competence without granting an entire second Background. However, with the powerful travel feature this could outperform some general Upbringings in city-heavy play |
| **B — feature-weighted** | Choose one Skill, **no Competency** | Similar to Wilderness-Raised's count, but discards all four approved Competency options in the native package unless separately reintroduced later; the owner approved these as legitimate selectable alternatives |
| **C — one grant from either pool** | Choose **either** one Skill **or** one Competency (not both) | Lower baseline; makes training outcomes qualitatively unequal and can leave an Upbringing with no Skill choice at all. Requires mixed benefit-choice representation and makes pool parity difficult to judge |
| **D — high training** | Choose two distinct Skills **and** one Competency | Strong urban travel feature plus substantial flexible training on top of any Background; closest to allowing source Background benefits to accumulate twice |

**Recommendation: Model A**, one Skill plus one Competency, each exactly one selection. This is a targeted count decision that preserves the approved pools and useful freedom: for example, a street-market cook might choose Insight + Cooking; an observer of public life could choose Perception + Disguise Kit; a character familiar with illicit practices could choose Stealth + Thieves' Tools. A selection does **not** compel the character to have committed crimes or guarantee mastery of other Skills. The examples are illustrative only.

**Why not model B immediately because Wilderness-Raised has no Competency?** Urban Navigation is powerful in urban campaigns, but Wilderness Provisioning can remove food/water logistics for six people where it matters. Both are conditional and depend heavily on campaign geography. Model B is the most defensible conservative alternative if the owner prioritizes one-skill feature parity over preserving the approved Competency choices. The full 1 Skill + 1 Competency package should be checked again in the planned corpus-wide review without quietly weakening the already approved half-time feature.

## Native representation after final approval — proposed shape only

If **Model A** is approved, the complete native `upbringing.street-raised` should contain:

| Benefit | Proposed representation | Meaning |
| --- | --- | --- |
| Community Language | `extraLanguages: 1`; `choiceConstraint: spoken-in-formative-household-or-community` | A real learned language; Common eligible only where used |
| Formative Skill | `grantType: skill-training`; `chooseOne` five approved `skill.*` IDs; `choiceCount: 1` | Exactly one Skill, with `skill.perception` not independently training Listen/Spot |
| Community Competency | `grantType: competency-training`; `chooseOne` four approved `competency.*` IDs; `choiceCount: 1` | Exactly one Competency including its **existing native facets** |
| Urban Navigation | `grantType: upbringing-feature`; approved original feature text | Half ordinary noncombat settlement traversal time for the character and guided companions |

**IDs verified:** All five Skills exist under `native/skills.json`; all four Competencies exist under `native/competencies.json`. The reviewed `competency.forgery` has **both** the `skill.forgery` and Forgery Kit proficiency facets; granting it is not the same mechanical value as granting the stand-alone Disguise Kit Competency. Preserve existing semantic identities rather than fabricating a new Forgery Kit-only duplicate, and explicitly flag this imbalance for later corpus review.

**Name/ID recommendation:** `Street-Raised Upbringing`, `upbringing.street-raised`. The public-facing name describes learned formative urban experience without a required negative economic or family status. Avoid calling the selected-native package `Urchin`; retain that name only for historical-source attribution and cross-edition comparison.

**No `startingEquipment` field**, origin feat, built-in gold, fixed tool/Skill training, extra language, automatic criminal contact, automatic deception expertise, or learned child-Skill training from a composite parent. The feature's wording must remain as approved.

## Interaction examples that must remain possible

| Combination | Expected meaning | Overlaps requiring general resolution |
| --- | --- | --- |
| Street-Raised + Criminal | Learned urban environments, later illicit practice | Stealth and Thieves' Tools may both be selected twice; no double training or free re-selection is approved |
| Street-Raised + Spy | Street familiarity plus intelligence experience | Stealth, Perception, Insight, Disguise Kit, Forgery or Thieves' Tools may overlap; Spy's Intelligence Sources are not granted by the Upbringing |
| Street-Raised + Charlatan | Formative city navigation plus developed false-persona tradecraft | Sleight of Hand, Disguise Kit and Forgery overlap; Street-Raised does not automatically grant False Identity |
| Street-Raised + Sage | Formative city familiarity plus later scholarship | The Upbringing does not automatically teach Arcana/History, and Sage does not imply City Navigation |
| Street-Raised + Soldier | Formative urban experience plus military service | No automatic Land Vehicles or military standing follows from being Street-Raised |
| Street-Raised + Folk Hero | Formative urban learning plus later deeds and reputation | Urban Navigation does not grant the Community Refuge hospitality relationship |
| Street-Raised + nonhuman Species | Urban/community upbringing independent of physical ancestry | Does not change inherent Species traits, parentage or ancestry eligibility |

The coexistence of multiple features is ordinary combination, **not inheritance**. Neither the 2014 Urchin equipment nor all four Skills and Competencies are automatically added by matching an old biography.

## Balance and user-facing representation risks

1. **Urban value is geography-sensitive:** Halving ordinary noncombat travel time for guided companions may be substantial in city-heavy campaigns. Do not silently alter the approved rule to solve potential imbalance, but test its frequency and effect against wilderness provisioning and other Upbringings.
2. **Forgery grants an extra facet:** `competency.forgery` trains the Forgery Skill and Forgery Kit; the other three choices do not necessarily give a comparable second facet. This is a genuine relative-strength issue under the shared universal competency model, and a later targeted adjustment should be evidence-based.
3. **Choice overlap:** A character might receive the same Skill or Competency through the selected Background. No automatic replacement or duplicate-rank grant has been authorized; resolve once for the whole corpus.
4. **Urban route specificity:** The approved feature applies to accessible, connected passages through a substantial settlement when guiding companions outside combat; it does not create bypasses for physically blocked routes or a hidden-entrance finder.
5. **Community language:** It is a learned formative language, not an inherited Species language; the character can select Common if actually learned there. Use the same general language identity and projection rules as other Upbringings.
6. **Source biography and items:** Poverty, orphanhood, lack of guardians, a pet mouse, a knife, a city map, or personal keepsakes are **not** mandatory traits or starting grants of Street-Raised. Such items can be acquired through Background equipment or ordinary play.
7. **No new identity mechanics:** Informal city knowledge does not mechanically provide Spy's Intelligence Sources, Criminal Contact, Charlatan's False Identity, or Noble's privilege.
8. **Feature execution later:** An approved narrative feature is selected native content even if its travel-time integration and generic choice executor are pending. Do not invent travel API code as part of authoring this Upbringing.

## Approval boundary

**Already approved and not reconsidered here:** Primary conversion of the 2014 Urchin into independent Street-Raised Upbringing; one suitable community language; five-Skill pool; four-Competency pool; exact independent Urban Navigation wording and its half-time effect; absence of a second Upbringing equipment bundle; separation from any later Background.

**Owner approval requested for the remaining complete package:**

- **Choose one Skill** from Stealth, Sleight of Hand, Perception, Insight, Survival.
- **Choose one Competency** from Disguise Kit, Thieves' Tools, Cooking, Forgery.
- Confirm final native name/ID `Street-Raised Upbringing` / `upbringing.street-raised`.
- Preserve the already-approved community language, Urban Navigation, no starting equipment, independent Species/Background compatibility.

**Not approved by this document:** The numeric selection counts, native ID, and promotion of `native/upbringings/street-raised.json`. No new `background.urchin` is proposed. If the owner rejects Model A, evaluate Model B or C without undoing prior approvals.
