# Bard — Band 1 base-class reconciliation (four-way proposal)

**Scope:** Unified Dorks & Dice Bard class levels 1–20, before subclasses, prestige classes, and Band 2. **Status:** complete four-way *base-class proposal*, pending rulings on the limited genuine conflicts identified below. This does **not** authorize activating `data/native/classes/base/bard.json` or any individual Bard feature records.

## Approved foundational identity (2026-10-19)

**Bardic magic is derived from art, creativity, and expressive practice rather than limited to music.** Singing, instruments, recitation, poetry, storytelling, dance, drama, drawing, painting, calligraphy, sculpture, and comparable media may serve as a Bard's artistic expression. Music remains valid and common, but the class may not require musical performance merely because of historical feature names.

The proposed umbrella label is **Bardic Expression**; historical **Bardic Music** and source-specific feature names remain identifiable for reconciliation and provenance. The umbrella is vocabulary only until feature entitlement, effect, and resource decisions are made.

- **Class access:** A nonmusical Bard must have access to the class's core magical identity and abilities. No musical-instrument proficiency, singing, or audible performance is a universal prerequisite.
- **Feature execution:** Keep the feature's meaningful requirements: activation and action economy, resources, concentration/maintenance, range, targeting, artistic training, and ability-specific perceptibility. An audible spell/song does not automatically become an unrestricted visual effect; a visual work should not be subjected to an irrelevant hearing requirement. Decide any needed medium-specific transmission rules when reconciling the feature.
- **Casting and implements:** Musical instruments can be focuses, but the class must support suitable alternatives. Do not grant every artwork or item focus functionality without the ordinary magic-component/focus rules.
- **Skills and competencies:** Use the existing Skills and Competencies system for artistic training; preserve relevant historical Perform rank requirements via native training/conversion. Replace mandatory music-only class grants with an appropriate eligible artistic choice without inventing an extra bundle of free proficiencies.
- **No automatic audience:** Expressive origin of magic does not impose audience/perception on all Bard spellcasting. Only effects explicitly requiring observation, hearing or another sense impose that condition.

## Relevant source evidence and class-pass work

- **3.x:** Historical *Bardic Music* encompasses multiple performances and abilities and often references Perform ranks and hearing. Preserve unique effects and costs while generalizing their permitted art form.
- **SRD 5.1:** Bardic Inspiration supports inspiring words or music. Instrument proficiency/focus and Song of Rest require conversion to nonmusical media.
- **SRD 5.2.1:** Bardic Inspiration explicitly supports **words, music, or dance**; some grants accept a recipient who can *see or hear*. This is evidence that music is not an essential universal medium.
- **Established native magic:** Spell preparation has been removed; slots and points are alternatives under the approved framework. The Bard's spell progression and distinct performance resources still need reconciliation.
- **Shared class infrastructure:** Reuse the Barbarian Band 1 advancement, feats, native skill/competency and proficiency distinctions, cross-edition feature reconciliation, and source-neutral class/feature definition patterns.

## Source coverage and scope

- **3.0 Bard:** https://www.dragon.ee/30srd/bard.htm (partial independent SRD mirror; the Bard page is available and compared, but no full 3.0 source pack has been imported). 3.0 has medium and light armor, shields, and one selected historical martial weapon; Inspire Courage grants **+2 saves against charm/fear, +1 attacks and damage**. Bardic Music also includes Countersong, Fascinate, Inspire Competence, Suggestion and Inspire Greatness. Level 1 Bardic Knowledge; BAB 3/4; good Reflex and Will saves.
- **3.5 Bard:** `data/reference/3x/band1-baseclasses/srd35/classes-i.md` in `feature/band1-baseclass-srd-sources` (unmerged source-only branch, with separate OGL notice). Adds Inspire Heroics, Song of Freedom, Mass Suggestion and Inspire Courage progression (+1 at 1, +2 at 8, +3 at 14, +4 at 20); its weapon list includes additional fixed weapons, while medium armor proficiency is not retained.
- **SRD 5.1:** `data/reference/srd/structured/class/class-bard.json`. Bardic Inspiration, Spellcasting, Jack of All Trades, Song of Rest, Expertise, Font of Inspiration, Countercharm, Magical Secrets, Superior Inspiration, advancement feats.
- **SRD 5.2.1:** Same staged class source; revised Bardic Inspiration, revised Countercharm, earlier Expertise and Superior Inspiration, different spell-access/known-count mechanics, spell-slot-to-Inspiration conversion, Words of Creation, Bard level-19 Epic Boon.
- **Exclusions:** Neither published Bard subclasses nor *Heliana's Guide* subclasses are part of the Band 1 **base class**. Copyrighted third-party subclass design is illustrative context only, not a source to copy into native.

## Reconciliation without new class identity or duplicate feature grants

Use **one native Bard** and the established approved native rules:

1. **Class identity:** Bardic magic derives from art and expression. Music is neither mandatory nor the baseline prerequisite. Cooking is a legitimate example of art, not a required subclass choice. Preserve effect-specific range, transmission, target perception, training, and action requirements. The term *Bardic Expression* is a provisional umbrella name, not a new approved feature or resource.
2. **Hit Die and defenses:** Use modern Bard d8 Hit Die, Dexterity and Charisma saving throw proficiencies, plus the historical 3.x 3/4 BAB and good Reflex/Will progressions through existing compatible native tracks. Do **not** invent duplicate saving-throw grants or BAB-derived attacks on top of a separate attack procedure. 3.0's medium armor and shields, 3.x named weapon proficiencies, and modern tool/skill grants can be retained as compatible proficiencies; older arcane spell failure restrictions need their shared rule, not an arbitrary Bard-only percentage.
3. **Training:** Preserve 3.x class-skill eligibility and ranked skill points (**6 + Intelligence modifier per level; first level x4 in the legacy rank model**), including Perform/Craft/Profession. Retain 5.x three starting skill proficiency choices and three artistic training/tool choices using the universal Skills/Competencies system, with equal eligibility for nonmusical Bards rather than requiring three instruments. Proficiency/ranks/Expertise are separate mechanical concepts, not automatic free rank bonuses.
4. **Bardic Knowledge (L1):** Retain the special historical Intelligence + Bard level lore check, with History rank synergy and DC examples 10/20/25/30. Preserve no take-10/take-20 rule. Jack of All Trades is not automatically a second full competence grant on this special procedure.
5. **Bardic Music performances (L1 onward):** Restore unique 3.x effects, training requirements and resource: one performance use per Bard class level per day, independent of modern Bardic Inspiration. A use is paid on starting a qualifying performance; maintaining an effect follows that effect's Action/concentration and duration rules. Suggestion activated on an already Fascinated target does not consume another performance use where the original says so. The 3.x source calls the family *Bardic Music*; the expression medium is not restricted to music in native. Evaluate perceptibility per feature rather than automatically treating all expressions as sound.
6. **Bardic Inspiration (L1):** Separate Charisma modifier (minimum 1) resource; d6 at 1, d8 at 5, d10 at 10, d12 at 15. Bonus Action, one die per eligible recipient, 60 feet, expression perceived by sight/hearing/appropriate specified sense. **Proposal:** retain 5.2.1's hour-long duration and recipient use *after a failed d20 test* instead of the older ten-minute/before-outcome procedure. Long Rest recovery becomes Short/Long Rest at L5 via Font of Inspiration. Font additionally allows spending a spell slot to recover one use, without a new independent pool.
7. **Song of Rest (L2):** Restore 5.1's 1d6/1d8/1d10/1d12 healing on qualifying Short Rest hit-die expenditure at levels 2/9/13/17. It is independent of Bardic Inspiration and Bardic Music and may use nonmusical expression appropriate to the rest.
8. **Expertise and Jack of All Trades (L2):** Two Expertise selections at L2, two more at L9, retaining the 5.2.1 earlier schedule and no duplicates at older L3/10; Jack of All Trades adds half proficiency bonus to otherwise unproficient eligible skill checks. Do not double-add it to already proficient checks or mistake rank training for modern Expertise.
9. **Countercharm (L6 and L7):** Retain the 5.1 Action-based short protective performance from level 6, **and** gain 5.2.1 Reaction-based reroll with Advantage from level 7. Treat them as two uses of the **same Countercharm feature**, each with its actual action cost, rather than stacking free Reactions or two separate class grants. Historical *Countersong* stays a distinct anti-sonic/language-dependent performance, not a renamed Countercharm.
10. **Magical Secrets (L10):** From Bard 10, support 5.2.1's continuing spell choices/replacements from Bard, Cleric, Druid and Wizard lists; additionally, the specific 5.1 grants at L10/L14/L18 allow **two spells chosen from any class list** each time. Those six spells are selected **within** the Bard's allowed spells-known capacity; do not silently create six extra known spells, spell slots or spell points. After the native removal of preparation, interpret `prepared spells` references as spell-access/known choices, not daily preparations.
11. **Superior Inspiration (L18):** Use 5.2.1 minimum-two-uses-on-initiative result from L18. Do not separately grant older L20 one-use-when-empty feature.
12. **Words of Creation (L20):** Retain the two named Power Word spell grants, allowing the two-target use under its source conditions. Since native spell preparation is removed, treat the source's `always prepared` as **always accessible extra known/available Bard spells**, with its associated feature cost and targeting rules; do not create prepared-list infrastructure solely for this feature.
13. **Advancement:** ASI or eligible ordinary feat at levels 4/8/12/16/19. Epic Boon access starts only at **total character level 21**, not class level 19. No permanent ability-score maximum, no alignment-based class restriction or loss of advancement.
14. **Spellcasting:** Full modern Bard 1st–9th level slot progression is one class progression; player selects slots or spell points by existing native rules. The 3.x sixth-level maximum is not a second parallel caster track. **3.x also knows four cantrips at level 1, five at level 2, and six from level 3; modern 5.x knows two initially, three at level 4 and four at level 10.** This is a separate genuine known-cantrip-count conflict; see below. Retain class-eligible spells of other source eras through provenance/conversion rather than silently deleting them. Spells have their **actual components** under native spellcasting; generic descriptions of music and 3.x universally verbal Bard spells do not unilaterally waive or add components.

## Proposed unified Band 1 base-class grant table

| Bard class level | Base-class grants/improvements; no subclass grants |
| --- | --- |
| 1 | Bardic Expression (artistic identity), Bardic Inspiration d6, Bardic Knowledge, Bardic performances: Countersong, Fascinate, Inspire Courage +1 to attack/damage, Spellcasting |
| 2 | Expertise (2 choices), Jack of All Trades, Song of Rest d6 |
| 3 | Inspire Competence performance |
| 4 | ASI/qualifying feat; modern cantrip progression milestone (the final native known-cantrip count depends on the quantitative decision below) |
| 5 | Bardic Inspiration d8; Font of Inspiration (Short/Long Rest restoration and spell-slot exchange) |
| 6 | Suggestion performance; Countercharm (Action protective form) |
| 7 | Countercharm (Reaction reroll form) |
| 8 | ASI/qualifying feat; Inspire Courage improvements |
| 9 | Expertise (2 additional choices); Inspire Greatness performance; Song of Rest d8 |
| 10 | Bardic Inspiration d10; modern cantrip progression milestone; Magical Secrets (ongoing eligible-list expansion plus first 2 unrestricted selections) |
| 11 | Existing spell/ability advancement, no redundant base feature |
| 12 | ASI/qualifying feat; Song of Freedom performance |
| 13 | Song of Rest d10 |
| 14 | Inspire Courage improves; 2 additional unrestricted Magical Secrets selections |
| 15 | Bardic Inspiration d12; Inspire Heroics performance |
| 16 | ASI/qualifying feat |
| 17 | Song of Rest d12 |
| 18 | Mass Suggestion performance; 2 additional unrestricted Magical Secrets selections; Superior Inspiration |
| 19 | ASI/qualifying ordinary feat; not an automatic Band 1 Epic Boon |
| 20 | Inspire Courage improves; Words of Creation |

For every level also apply: 3/4 historical BAB (floor(3 × class level / 4)), Fortitude poor (floor(level/3)), Reflex and Will good (2 + floor(level/2)), full modern Bard slot schedule, approved spells-known and cantrips-known count schedules (see decisions), Bardic Inspiration progression, and once/day/Bard-level historical performance uses. The proposed table is **not yet an executable class contract**.

## Detailed historical performance effects to preserve

| Feature | Acquisition | Source-specific effect/progression |
| --- | --- | --- |
| Countersong | 1 | Perform check versus sonic/language-dependent magic at 30 ft, potentially substituting check for applicable save; up to 10 rounds, original extra save for ongoing qualifying magic in 3.5 |
| Fascinate | 1 | Perform result sets Will save DC, target must perceive expression, 90 ft; target count 1 at L1 +1 per three additional Bard levels, sustained concentration up to one round/Bard level; historical reattempt/24-hour restriction on successful save |
| Inspire Courage | 1; improves 8/14/20 | 3.5 +1/+2/+3/+4 progression to qualifying attacks, weapon damage, charm/fear saves; **3.0 has different +2 charm/fear save value initially; see conflict below** |
| Inspire Competence | 3 | +2 competence bonus to ally's relevant skill check while expression is maintained, up to 2 minutes; cannot self-target under 3.x |
| Suggestion | 6 | On already Fascinated target; Will save; no separate music-performance use; use 3.5 class-level scaling DC from the source rather than 3.0 fixed 13 + Cha |
| Inspire Greatness | 9 | Eligible allies gain two temporary d10 Hit Dice (including corresponding temporary HP with Con as in source), +2 attacks, +1 Fortitude saves; target count scales by class level |
| Song of Freedom | 12 | Equivalent of break enchantment on eligible other target in 30 ft after one minute of sustained expression; cannot target self |
| Inspire Heroics | 15 | Willing ally/self receives +4 morale saves and +4 dodge AC; audience count scales by Bard class level; initial full-round transmission preserved as applicable |
| Mass Suggestion | 18 | Suggestion applied simultaneously to already Fascinated creatures, using shared save and original resource procedure |

3.x performance rank gates are real class-feature requirements: 3 Perform ranks for Countersong/Fascinate/Inspire Courage, 6 for Inspire Competence, 9 for Suggestion, 12 for Inspire Greatness, 15 for Song of Freedom, 18 for Inspire Heroics, 21 for Mass Suggestion. The native conversion must preserve training eligibility without literally requiring musical proficiency. **Do not treat a class-level grant as an automatic bypass of its rank gate** without an approved general training-conversion rule.

## Spell-count comparison and proposed handling

The two modern SRDs have identical spell **slot** tables but distinct spells-known/`prepared` counts at certain levels. Their cantrip progression is identical **to each other**, but not to 3.x. Our native rules remove *preparation*, not the concept of a finite pool of accessible Bard spells.

| Class level | SRD 5.1 known | SRD 5.2.1 prepared count (convert to accessible/known) |
| --- | ---: | ---: |
| 1 | 4 | 4 |
| 5 | 8 | 9 |
| 9 | 12 | 14 |
| 10 | 14 | 15 |
| 11 | 15 | 16 |
| 13 | 16 | 17 |
| 14 | 18 | 17 |
| 15 | 19 | 18 |
| 18 | 22 | 20 |
| 19 | 22 | 21 |
| 20 | 22 | 22 |

**Correction of a likely mistaken shortcut:** Neither table universally dominates: SRD 5.1 has higher counts at L14–15 and L18–19; SRD 5.2.1 supplies more at numerous earlier levels. A single authoritative 1–20 native spells-known count must be chosen, without granting the sum. **Recommendation for review:** per-level greatest documented count to retain each era's acquired capacity, producing `[4,5,6,7,9,10,11,12,14,15,16,16,17,18,19,19,20,22,22,22]` at levels 1–20 (verify each number against both 5.x tables). This is a *quantitative proposal*, not an existing approved numeric-merging policy. Magical Secrets stays within this count.

## Actual owner decisions still needed

Only genuinely competing or underspecified procedures are presented for approval; most other reconciliation applies already accepted principles.

1. **Bardic Inspiration timing:** 5.1 lets the recipient choose the die after a roll but before success/failure is declared (10-minute lifespan), whereas 5.2.1 grants it after a failed d20 test (one-hour lifespan). Recommend the newer after-failure/hour-long procedure; it is simpler to apply and more permissive, not a separate resource or second inspiration.
2. **Inspire Courage charm/fear save bonus:** 3.0 grants +2 from level 1, while 3.5 begins at +1 and later scales all three numeric effects together. Recommend retain 3.0's stronger early protection against charm/fear while preserving 3.5 attack/damage scaling: charm/fear save +2 at L1–13, +3 at L14–19, +4 at L20; attack/damage +1 at L1–7, +2 at L8–13, +3 at L14–19, +4 at L20. This is one Inspire Courage feature and one existing performance use.
3. **Accessible Bard spell capacity:** Recommend the higher source documented known/available count **at each level**, rather than arbitrarily choosing an edition-wide winner or summing both tables (table above).
4. **Cantrips known:** 3.0/3.5's Bard starts with **4** cantrips known, increases to **5** at class level 2 and **6** at 3, whereas 5.1/5.2.1 starts with 2, reaches 3 at 4, and 4 at 10. **Recommendation:** retain the greater 3.x known-cantrip capacity (4/5/6 at 1/2/3 and six through L20) while applying the modern *at-will cantrip casting procedure* instead of simultaneously tracking legacy cantrip slots per day. This is a specific quantitative/procedural resolution, not an additive six-plus-four cantrip grant.

## Established conversion work, not additional class-identity decisions

- **Artistic training:** As already required by the approved expression principle, replace source musical-instrument-only training choices with **three eligible artistic training choices** and preserve any relevant historical Perform-rank gates through the native rank/competency mapping. Specific training equivalence and spellcasting-focus eligibility should be checked when the shared competency and focus rules are executable; do not assume every artistic item is a universal focus.
- **Perception:** Preserve actual target perception, range and ongoing-expression requirements by feature, allowing visual, auditory, or other appropriate artistic media without automatically changing action, duration, or target limits. A stored painting or cooked meal does not inherently grant a remote combat performance.
- **Source conventions:** Original 3.x all-verbal Bard spells, Charisma spell-level thresholds, and armor arcane-failure percentages require the already planned native spellcasting and component conversion; do not silently reinstate an older subsystem in this class file alone.

## Not blockers or permission to infer

- **No subclasses** are included; class-level subclass-feature placeholders remain staged for the later subclass pass.
- **No mandatory musical instruments**, singing, alignment requirement, daily prepared-spell selection, or permanent attribute ceiling.
- **No separate sixth-level 3.x spellcasting table** is merged additively into the full modern caster table.
- **No copied third-party subclass** from Heliana or other protected supplements.
- **No implicit extra attacks**, free actions/Reactions, spell slots, performance uses, skills or proficiencies merely because another edition describes the same entitlement.
- **No automatic source precedence.** The decisions above are scoped Bard judgments; they do not create a policy that newer source rules always win.
- **No native Bard activation until approved**, class record and referenced feature records are reconciled together, and regression validation passes. See Barbarian implementation pattern `data/native/classes/base/barbarian.json` and `tests/test_barbarian_band1.py`.

