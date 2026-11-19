# Dorks & Dice · Quantitative Species and Ancestry evaluation

**Design review | November 14, 2026 | Reference revision `e87fd21bc5f25e79fd4068b9ac71dc26144468dd`**

## Finding

Measure the difference a manifested Origin makes to a specified character facing the same encounter, then retain the result in its natural units. A feature can change the chance of reaching an objective, the number of attacks needed, and the resources left for the next encounter in different directions. The Red Dragonborn test below changes from losing damage to gaining damage solely by changing how many targets fit the area. A single unconditional price would conceal even the sign of that comparison.

This report distinguishes **source quantities** (dice, entitlements, uses, triggers), **modeled outcomes** (conditional expectations in the fixtures below), and **design judgments** (which differences deserve playtesting). No observed table data were supplied. Numeric examples are deterministic calculations, not measured player behavior. The final inventory indexes every preset and expands the canonical mechanics, including nested options. There is no inferred common power target or exchange rate between domains.

## Source and comparison boundary

The seven references in the primary design establish the universal reconstruction sequence, source occurrences and facets, overlap adjudication, limits on manifestation, biological vertical slice, and normalization of trait definitions. Their practical consequence here is to resolve parent chains, intrinsic replacements, source-bound choices, prerequisites, and effective pools **before** comparing power. A selectable Dragonborn leaf inherits a nonselectable family. Wood Elf replaces the base speed. An offered feature, a transmitted feature, an expressed feature, and a currently unlocked feature answer different questions. The tested mixed configurations stipulate expression for comparison. They do not establish eligibility, likelihood of transmission, a capacity, or rules for latent traits. Use the same evaluator for an authored preset, a reconstructed individual, or a construct.

[Primary specification and linked architecture references](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/docs/rewrite/character-origins/pillar2-characteristic-inheritance-and-balance-design.md) · [Native catalog at the evaluated revision](https://github.com/LegendMaster03/dorks-and-dice-rules-core/tree/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native)

## Two evaluation methods

**A. Paired scenario differences.** Fix class, gear, party, targets, terrain, available information, rest schedule, and a decision policy. Compare the same character with its complete preset, with one effective characteristic removed, and with an intentionally plain reference foundation. Report enemy HP removed, own HP preserved, objective success, ally actions gained or lost, and end-of-day resources separately. Report size, type, and targeting changes explicitly. Removing a prerequisite disables dependent activations. A standalone test supplies the minimum required foundation and identifies its cost separately. Define the interaction of A and B on each endpoint as outcome(AB) − outcome(A) − outcome(B) + outcome(neither). This also works for negative effects and overlapping benefits. An observed interaction may depend entirely on the fixture.

**B. Capability and resource frontier.** Enumerate feasible action sequences on the same map and turn schedule, retaining nondominated outcomes. A sequence is dominated if another does at least as well on every selected endpoint and strictly better on one. Record hard access gates, targets affected, effective range, shareable benefits, earliest level, duration, and refresh. For Stone, allocating one use to attack moves the frontier by +3.5 outgoing damage and up to −9.5 expected mitigation in the large-hit fixture, plus a chance to push. For flight, retain reachable destinations and elapsed time. This method detects breakthroughs and competing options without pricing control in HP. Its optimistic boundary assumes informed play and enough valid triggers. Method A estimates realization under declared behavior but undersamples rare situations. Use B to find encounter pairs that distinguish configurations, then A to test those pairs. An optional weighted score would require explicit frequencies *and* preferences over endpoints. Neither the existing presets nor the candidate's experimental point schedules establish either.

## Common fixture and reproduction

Levels are 1, 3, 4, 5, 11, and 17. Model proficiency bonus (PB) as 2, 2, 2, 3, 4, and 6 respectively. One resource day begins fully rested and ends at the next Long Rest. Standard day has four three-round fights, one Short Rest after fight two, and four exploration contacts. Also test one fight and no Short Rest, or six fights with two Short Rests. Keep pre-rest unused charges, round limits, and the location of each trigger in the trace. An Elf completing its rest in four hours gives time for other activities. Extra Long Rests are not implicit.

For isolated rolls use 0.65 hit, 0.55 enemy save failure, 0.50 ordinary check/save success. Assume interior d20 thresholds, independent rolls, and stationary probabilities across levels to isolate progression. A comparison attack deals 8 on a hit, for expected value 5.2 inclusive of all damage in this simplified comparator. No extra critical term except the explicit Orc test. The martial has a weapon attack to replace, a ranged alternative, and a free bonus action unless specified. Also test a skirmisher spending the bonus action for an alternative worth 3 expected damage, a caster with existing spell access and a slotless martial. A caster who would otherwise cast a spell must debit the *whole* forgone action if choosing the Attack action solely to breathe. Record actual class progression when replacing these fixtures with player builds.

Fixed reference for isolated effects is Medium, Humanoid, speed 30, ordinary senses, same gear, same ability modifiers and no Origin features. This is a counterfactual for ablation, not a legal tenth preset. For travel tests use no mounts, group transport, artificial light, or external magic unless explicitly varied. Separate a solo passage from getting the whole party through. Non-Humanoid type changes spell eligibility in both directions. Test a Humanoid-only hostile effect and a Humanoid-only friendly effect separately. Type alone neither supplies monster statistics nor determines how often either effect appears.

**Reproduce a comparison.** Instantiate the parent chain in Appendix A, choose legal size and source-specific ability, apply replacement/upgrade semantics, and expand the referenced definitions in Appendix B. Bind leaf type/family area before rolling. Select a level and mark later abilities as committed future options with zero current activations. Write a row per event with range, light, substrate, time, trigger, eligible targets, actions available, and remaining pools. Replay both configurations in the same event order. Spend only if the activation is legal and advances the chosen endpoint. For short deterministic cases below enumerate dice or use their means. If a task can be bypassed, include its opportunity and fallback resource even if no roll occurs. Print unresolved choices and unmodeled effects alongside every comparison. Leave a whole-configuration ranking open when these can change the ordering.

## Six-level results

Each row isolates one endpoint of the named **complete conventional preset**. Other inherited endpoints still apply and are indexed below. Units differ by row.

| Preset and endpoint | 1 | 3 | 4 | 5 | 11 | 17 |
|---|---:|---:|---:|---:|---:|---:|
| Human, extra allocatable ranks |4|6|7|8|14|20|
| High Elf, distinct free leveled castings per Long Rest |0|1|1|2|2|2|
| Red Dragonborn, gross expected damage per target per ordinary breath¹ |4.26|4.26|4.26|8.53|12.79|17.05|
| Hill Dwarf, increase to maximum HP |1|3|4|5|11|17|
| Rock Gnome, eligible passive contacts in the four-contact fixture |2|2|2|2|2|2|
| Stone Goliath, expected HP preserved if all common charges defend² |19|19|19|28.5|38|57|
| Lightfoot Halfling, extra successful tests per 20 ordinary eligible rolls³ |0.50|0.50|0.50|0.50|0.50|0.50|
| Orc, maximum Rush activations with one Short Rest, before trigger/turn limits |4|4|4|6|8|12|
| Infernal Tiefling, distinct free leveled castings per Long Rest |0|1|1|2|2|2|

¹ Fractional expectation with no per-roll half-damage rounding, no resistance, and no overkill. ² Constitution +3, enough separate hits of at least 15 with reactions available. ³ Natural 1 originally fails, each reroll succeeds half the time, one reroll only. This is 2.5 percentage points per eligible roll. Advantage and disadvantage require their own reroll-order procedure.

### Conventional comparisons and marginal effects

**Human.** Skillful, Versatile, and Adaptable Learning are cumulative. Ranks equal level + 3. Twenty extra ranks at level 17 are an allocation entitlement, not +20 on one check. A one-rank increase shifting an interior threshold by one face gives 0.05 additional success per relevant roll. Four first-level ranks spread one each over four eligible checks used twice daily yield 0.40 additional successes per day. If all eight checks instead test a different skill, realized gain is zero in that fixture. An additional proficiency newly applicable to two checks gives 2 × PB/20 expected successes before caps; repeated proficiency does not double PB. The feat's marginal value requires a named eligible choice and class-specific replay. Treat its output as unresolved until that selection is verified. Rank ceilings, skill versus competency projector behavior, and the pending feat catalog prevent an upper bound on the package from the rank count alone. Record both an optimized specialist and a breadth build before tuning the progression. [Competency baseline](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/docs/rewrite/competency-baseline-v1.md)

**Elves and Tieflings.** High Elf binds Intelligence, Wisdom, or Charisma to its source. Choose a legal Wizard cantrip after a Long Rest and keep it until the next permitted replacement. One relevant scouting task and one escape per day can use Detect Magic from 3 and Misty Step from 5. A slotless martial gains up to two previously unavailable responses. A caster already able to perform both gains up to two saved slots if it would have spent both, and zero saved slots if the tasks never arise or a no-slot alternative suffices. Changing tomorrow's cantrip has option value only with information and an intervening rest. Wood trades this package for Druidcraft, Longstrider, Pass Without Trace and 35-foot speed. In a 35-foot movement gate Wood reaches the objective in one move where High's 30 feet falls short. A group stealth objective can favor Wood regardless of damage output. All Elves also bring conditional charm defense, Keen Senses, low-light perception, Trance and magical sleep protection. Two eligible Listen/Spot checks with +2 yield 0.20 extra expected successes at interior thresholds; never count the same check again as composite Perception.

A Tiefling has a Fiend targeting profile, ordinary listed biological needs, choice of size, source-bound Thaumaturgy and its selected legacy. At 20 incoming damage of the matching type, each legacy saves 10 HP even on a day with no spell demand. Abyssal adds poison, sickness and humanoid control options; Chthonic adds necrotic defense, temporary HP and weakening; Infernal adds fire defense, a ranged cantrip, reaction damage and Darkness. The level-3 free Hellish Rebuke is cast at spell level 2. Comparing free casts alone misses target restrictions, reactions, concentration and overlap with class repertoire. Drow and Infernal both offer Darkness; resolve whether distinct free uses remain authorized independently of duplicated spell knowledge. No extra cast follows merely from duplicate evidence.

**Dragonborn.** Let n be hostile targets, d the number of d10s, f the failure rate, and r the fraction of typed damage retained after defenses. Gross per activation is n × 5.5d × (0.5 + 0.5f) × r. Debit one replaced attack. At level 1, n=1 gives −0.94 expected net damage and n=2 gives +3.33. At level 5 n=2 gives +11.85. If successful saves round down, the gross formula overestimates by 0.25(1−f) per target before resistance, since a d10 sum is odd half the time. Policy can decline a losing breath. Its pool is PB per Long Rest, shared across all ordinary uses. A late arrival, ally in the area, or enemy already near zero makes nominal damage a poor estimate of realized value.

| Ordinary configuration at level 5 | Corridor, three enemies in line, one in cone | Cluster, one in line, three in cone |
|---|---:|---:|
| Red fire line |20.38|3.33|
| Mixed lightning line |20.38|3.33|
| Mixed fire cone |3.33|20.38|
| Bronze lightning cone |3.33|20.38|

Values are expected enemy damage less 5.2, rounded. A line is 30 × 5 feet, a cone 15 feet. Against a cluster with 50% fire resistance and ordinary lightning susceptibility, fire cone falls to 7.59 while lightning cone stays at 20.38. Line and cone values depend on **which creatures can actually be placed inside**. The four ordinary configurations give one breath each. For full Red add 15-foot climb and fire resistance. From 5, Warding upgrades 50% to immunity for one minute once per Long Rest, costing an action. Against 20 matching damage inside the window, marginal prevention is 10, not 20. Bronze instead breathes underwater and at 5 gains one separate Metallic Breath pool. Against two enemies and one ally with equal 0.55 failure, Enervating Breath produces 1.10 expected enemy incapacitations and 0.55 allied incapacitations before the user's next turn. Initiative determines lost turns. Its Repulsion alternative trades that for push and Prone on a different save. Appendix A distinguishes all fifteen leaves, including the five Gem affinities whose common mobility profile offers no basis for ranking their damage types without encounter frequencies.

**Dwarf and Gnome.** Hill Dwarf's extra maximum HP is a survival threshold, not daily healing. Poison saves and poison resistance may apply to the same event. For a 20-damage poison event with half on success and 0.50 base success, expected damage is 15 ordinarily, 12.5 with advantage, and 6.25 with both advantage and resistance. Total prevention is 8.75. Adding the standalone prevention of 2.5 and 7.5 would overstate it by 1.25. Stability changes qualifying push/Prone rolls only on stable ground. Rock and Forest Gnomes both have advantage on mental saves against magic. At p=0.50 this gives +0.25 success per eligible save; nonmagical mental attacks and physical saves leave that endpoint unchanged. Forest offers an at-will illusion plus PB free animal conversations and slot access. In a woodland with three distinct useful conversations the free-cast ceiling is min(3,PB); with no creatures able to help, it is zero. Small size, 25-foot movement and Fey targeting are shared by both Gnomes.

**Goliath.** All six have 35-foot movement, standing jumps, altitude acclimation, an ancestry ability choice, Powerful Build and level-5 Large Form. Large Form requires space, lasts up to ten minutes, and can bring speed to 45 feet. Test low ceilings, a 40-foot movement gate, and Strength checks independently. A size change does not grant unlisted reach. At level 4, Stone can spend two shared charges on either 19 expected mitigation or 7 expected on-hit damage plus 1.1 expected pushes, or split them for 9.5 mitigation, 3.5 damage and 0.55 push. Separately, two rock throws yield 2 × 0.65 × 5.5 = 7.15 expected damage and 0.715 expected Prone applications. Require rocks, separate bonus actions and susceptible targets. Against a 4-damage hit the expected mitigation is exactly 4 with Constitution +3. The skirmisher's 3-damage bonus action reduces the marginal damage of a throw to 0.575 before control. Sight and Throw unlock at 4, Large Form at 5.

Goliath comparisons at level 4, assuming both PB uses find triggers: Fire's Burn gives 11 on-hit damage plus independent Searing Ignition and fire defense. Frost's Chill gives 7 and two slows, or the alternate mode can attempt immobilization with those same charges; frost retaliation and cold defense are distinct. Hill can produce two automatic Prone effects on qualifying hits against Large or smaller creatures; the alternate mode instead gives 7 damage and 1.1 expected Prone against eligible creatures, including larger targets. Iron Stomach yields 5 HP per qualifying Short Rest at Constitution +3, limited by missing HP. Cloud has two Jaunts **or** two linked strikes (5 damage plus target-specific concealment opportunities), alongside two independent defensive escapes. Storm can react for 9 damage or strike for 7 with two debuff attempts; its independent aura changes attacks, damage and movement concurrently. Aggregate these as portfolios of timely options. Four identical short combats can favor different portfolios solely because of initiative and whether the character's reaction was already committed.

**Halfling and Orc.** Lightfoot combines broad natural-1 rerolls, fear defense, larger-creature movement and hiding, hearing, and three physical skill bonuses. Hiding behind a larger ally can enable an attempt that otherwise has no legal roll. On a day with no qualifying cover this benefit supplies no opportunities. The three +2 bonuses improve three separate interior-threshold checks by 0.30 total expected successes when each occurs once. Model the reroll on the modified threshold rather than adding the baseline reroll value to every modifier.

Orc Rush bundles Dash and temporary HP into one bonus action. With one Short Rest, potential THP issued equals 2PB² (8 to 72 across the levels tested), assuming every charge is used. Absorbed damage is bounded by actual damage while THP persist. Repeated refreshes while unhurt can waste almost all issued THP. With two rests and four fights where only one dash is useful per fight, useful mobility activations cap at four even when charge capacity is much higher. Relentless Endurance can preserve the next turn on a qualifying drop to zero; outright death or a follow-up attack may erase the benefit. Savage Attacks with a d12 weapon, twelve eligible attacks and 5% critical rate adds 3.9 expected damage under ordinary criticals. A ranged-only caster gains zero on that endpoint. Menacing Presence at p=0.50 gives +0.25 success when the target perceives relevant physical intimidation. Optional critical escalation requires a separate fixture.

### Dragonborn leaf comparison on a common itinerary

At level 5 replay five independent scenes with equipment, prior knowledge and objectives fixed. In the first, one character takes 20 fire damage and 20 lightning damage at different times. In the second, it must breathe underwater for longer than its ordinary breath-holding limit. In the third it must traverse 15 feet of climbable wall using an intrinsic climb speed. In the fourth it must traverse 15 feet of loose sand below the surface. In the fifth it must cross a gap in open air with no landing point below and a clear destination one move away. Report passive HP saved and whether the leaf supplies the specified means. These tests concern access to a means; ordinary climbing, ropes or another character may solve an actual adventure objective. The last column changes the itinerary to exercise otherwise invisible adaptations.

| Leaf | Passive HP saved | Underwater breathing / climb speed / sand burrow / flight | Distinguishing test or finding |
|---|---:|---|---|
| Black | 0 | yes / — / — / — | Against 20 acid instead, prevent 10 passively. Warding can prevent the remaining 10 in its window. |
| Blue | 10 | — / — / — / — | Place the apparent source of speech 30 feet away. One action gives one displaced voice, with no automatic deception success. |
| Green | 0 | yes / — / — / — | A poison exposure of 20 changes the passive result to 10; water breathing alone gives no swim speed. |
| Red | 10 | — / yes / — / — | Can climb the specified wall. Warding can add 10 prevention against the fire packet if its action and timing are available. |
| White | 0 | — / — / — / — | A 15-foot swim-speed task and an ordinary slippery-ice hazard both exercise its adaptations. Neither substitutes for breathing underwater. |
| Brass | 10 | — / — / yes / — | Changing sand to compact soil removes the burrow solution. |
| Bronze | 10 | yes / — / — / — | A cone of allies and enemies tests its independent Metallic Breath choice. |
| Copper | 0 | — / yes / — / — | Shares the climb result with Red; acid exposure and family-level control distinguish it. |
| Gold | 10 | yes / — / — / — | The two-type fixture ties Bronze. Replacing lightning with a second fire packet gives Gold 20 passive HP saved and Bronze zero. |
| Silver | 0 | — / — / — / — | On an ordinary fall that ends during Soft Landing, a free reaction can prevent the full fall damage. It does not reach the far side of the gap. |
| Amethyst | 0 | — / — / — / yes | Force packet of 20 yields 10 prevention. |
| Crystal | 0 | — / — / — / yes | Radiant packet of 20 yields 10 prevention. |
| Emerald | 0 | — / — / — / yes | Psychic packet of 20 yields 10 prevention. |
| Sapphire | 0 | — / — / — / yes | Thunder packet of 20 yields 10 prevention. |
| Topaz | 0 | — / — / — / yes | Necrotic packet of 20 yields 10 prevention. |

The five Gem leaves are temporarily nonselectable pending the psionics pass, including for ordinary mixed-Heritage contributor selection. Their evaluations exercise authored reference mechanics. All five also send one-way telepathic messages to an eligible visible creature within 30 feet. A closed door blocking sight invalidates the test; lack of a shared language alone does not. From levels 1 through 4, remove the flight solution for all five. At 5, 11 and 17 it remains once per Long Rest. For every leaf, substitute its listed affinity and family area into the breath equation at all six breakpoints. Its complete profile therefore includes a scaling offensive curve even when the itinerary gives zero passive HP saved.

## Mixed characters and exploration stress tests

Use the following explicit fixture foundation unless a row replaces it: Medium Humanoid, 30-foot walk, ordinary needs and senses, no implicit parental packages. The foundation is a stipulated custom choice for evaluation. Complete class, gear and Upbringing remain identical in paired runs.

| Manifested configuration | Counterfactual and modeled marginal result |
|---|---|
| Drow vision and sunlight sensitivity, with Dwarf vision also offered | Selecting the 120-foot sense over 60 reaches a target at 90 feet in ordinary darkness. A target at 30 feet is already in range; a target at 150 feet remains outside. Keep 120, never 180 from two ordinary range values. On eight attack rolls plus four Search/Spot checks, all at interior thresholds, Dazzled in fraction q of observations costs 0.60q expected successes, split 0.40q attacks and 0.20q checks. At q=0, 0.5, 1 the totals are 0, 0.30, 0.60. Effective eyewear or shade changes q. Removing the drawback gains exactly those opportunities in this fixture. The 90-foot detection and accuracy effects remain separate. Native Drow's three spells also matter in a complete preset. |
| Drow vision and Stone Cavernous Sight | At levels 1/3 radius is 120; at 4/5/11/17 the explicit once-per-feature upgrade can give 180 if authorized. With contacts at 30/90/150/190 feet, range-qualified counts move from two to three. In rooms no longer than 60 feet the marginal count is zero. Do not infer penetration of magical Darkness. |
| Orc and Goliath Powerful Build | Fix nominal carrying capacity C. A load between C and the one-size-larger threshold becomes portable under either standalone trait. Adding the second yields zero extra carry capacity. Add Orc to Goliath to retain explicit push/drag/lift coverage; add Goliath to Orc to gain grapple-ending advantage, worth +0.25 on one p=0.50 eligible check. A second application of the same effective trait gives neither an extra size step nor another advantage die. |
| Hill Dwarf Stonebound and Rock Gnome Earthbound | Four grounded contacts in separate windows: unanticipated enemy at 10 ft on connected earth; anticipated at 40 ft on connected stone; anticipated at 10 ft on connected stone; airborne at 10 ft. Activate Stonebound before each anticipated contact, with stone contact throughout. Dwarf detects contacts 2 and 3, Rock detects 1 and 3, union detects 1–3. Marginal gain of either over the other is one contact, compared with two standalone. At level 1 the mixed character can skip the third contact's activation and save a charge. Detection is not visual targeting. |
| High Elf and Wood Elf complete independent spell courses | One Elf foundation; stipulate two authorized source-bound courses. Choose Intelligence for High and Wisdom for Wood, keep each list and progression intact. At the six levels the combined free leveled casts/day are 0/2/2/4/4/4, plus their cantrips. With a distinct task for each spell, the second course adds up to two free responses at 5. With only one escape task satisfied by Misty Step, Wood adds no realized response. No stage-by-stage selection or second Elf foundation is needed. Honor slot, action and concentration competition when substituting actual spells. |
| Previously mixed Dragonborn, Human, High Elf | Verified parent currently expresses lightning line. Stipulate that the child expresses that ordinary breath, Adaptable Learning, and the complete High Elf course. At 1 its profile is 4 ranks, one changeable cantrip and two 1d10 breath uses. At 5 it has 8 ranks, two distinct free leveled casts and three 2d10 breaths. At 17 it has 20 ranks, two free leveled casts and six 4d10 breaths. With two lined enemies at 5, one breath adds 11.85 expected net damage over one attack. A second identical offer adds zero uses. Fire, Metallic Breath, Human's feat, and unexpressed grandparent options have no grant in this fixture. A third contributor broadens possible selections without deciding how many may transmit. |
| Gem flight and permanent custom flight | Specify same walking-speed flight and hover; synthetic permanent version available from 1, no activation, ordinary encumbrance unchanged. Three unassisted solo crossings, 20 feet each, 15 minutes apart, open air, no other route within the time limit. Gem succeeds on 0/0/0/1/1/1 by level, permanent flight on 3 throughout. At 5+ incremental success is two crossings; if all three fall within a minute it may be zero, although Gem still spends a bonus action and its use. A ten-minute uninterrupted aerial objective distinguishes the two at every level. Neither automatically moves the whole party. |
| Blueprint construct and unfamiliar homebrew trait | Supply the same expressed flight via a powered module. With charge for one minute, using the same movement and activation terms as Gem, the level-5 crossing result is one regardless of origin procedure. An additional “phase through a ward” capability with unspecified range, ward classes, frequency, and passenger rules is **unresolved**. For a one-ward scenario bound its marginal objective successes at 0–1; leave unrestricted campaign impact unbounded pending author input. No biological cost or assumption of free power follows. |

## Sensitivity, balance candidates and next review

1. **Geometry and party composition.** At 1, breathing breaks even at 5.2/4.2625 = 1.22 unresistant target-equivalents. With 50% resistance it needs 2.44. At 5, one unresistant target clears the damage comparator. With three allies in the cone or no affordable Attack action, the practical decision can reverse. Log occupied squares and the best available alternative before adjusting dice.
2. **Frequency and timing.** If ordinary success is p, advantage improves it by p(1−p), ranging from 0.09 at p=0.10 or 0.90 to 0.25 at p=0.50. Expected damage prevented by typed resistance ranges from zero without exposure to half of eligible incoming damage. Separate exposure frequency from conditional efficacy. Trance, Menacing Presence, animal speech, ice travel, and adaptation to altitude need targeted scenes as well as random encounters. Recording a zero in a particular scene is legitimate; recording zero across a campaign with unknown frequency is unsupported.
3. **Endurance versus burst.** At level 5 an Orc can present 3/6/9 Rush charges with 0/1/2 Short Rests. Stone retains three Endurance uses on each schedule. One fight can make six late-level breath uses impossible to realize; many separated fights create demand for continuous flight and passive sensing. Score deaths avoided and rounds saved separately from HP. Identical HP totals can yield different turn histories.
4. **Conventional options worth revisiting.** Human's growing discretionary ranks plus feat selection deserve the first skill-heavy comparison. Test whether a non-Human specialist retains its niche, whether rank caps constrain actual selections, and whether broad allocation still produces comparable utility. Goliath's independent level-4 pools and control deserve the first combat comparison, especially against slower defensive Species. Drow's drawback may disappear under ordinary equipment while superior range persists; test equipment availability before deciding on a link or changing range. Rock Gnome and Gem leaves should receive access-oriented tests before concluding that fewer named traits imply weakness. Orc depends strongly on realistic rest density and weapon choice. Each is a candidate for adjustment of the relevant grant, gate or progression if observed outcomes justify it.
5. **Blind spots.** The calculated endpoints do not measure table frequency, optimized feat selection, the value of social information, spell timing in every build, enemy counterplay, or player preference. Ratios such as “casts/day” count entitlements rather than spell effectiveness. Some choices change the options players even consider. Paired tactical runs estimate performance given a scene; observations across independent tables are needed to estimate scene prevalence. Avoid averaging the six progression snapshots equally unless that matches the campaign's level distribution.

**Recommended validation.** Freeze these fixtures and run paired sessions with a weapon martial, bonus-action skirmisher, and prepared caster at each breakpoint. Rotate initiative, terrain and which preset runs first. Include an ordinary campaign session alongside deliberately diagnostic maps. Log level, full choice record, matching damage, enemy/ally geometry, attack replacement, charge availability and expenditure, blocked activations, results, and the party's fallback. Use the same rolls within each pair where the procedure permits. Aggregate per encounter and per adventuring day, preserving party composition; do not treat every attack in a single fight as an independent campaign sample. For each endpoint publish paired differences, sample counts, and a table-cluster bootstrap interval when independent tables suffice. Until then, publish ranges across fixtures and individual traces. Investigate a preset if another consistently matches its useful endpoints and exceeds one across the intended settings, or if one trait bypasses a recurring adventure constraint with no practical counterplay. Neither condition alone establishes an acceptable power difference.

**Decisions for gameplay review.** Which campaign mixes and level distributions should conventional choices serve? How much within-domain disparity is acceptable in return for strengths elsewhere? Which Human rank limits and feat choices are legal in the benchmark? Which light protections are routinely available, and should Drow's range and drawback normally remain linked? How should identical spell access with separately sourced free uses reconcile? Which intrinsic compositions and permanent-flight restrictions are available to a player? Which future unlocks are committed when selected, and can a later change remove or replace them? Which unknown mechanics need designer adjudication before a comparison may inform balance? Resolve these on their own terms. Only a later allocation experiment should ask whether biological transmission uses a budget, structural constraints, or another procedure.

## Appendix A · Conventional preset inventory

The pinned source contains 43 Species/Ancestry records, 89 referenced canonical definitions and 95 direct reference occurrences. The following rows reconstruct all 33 terminal paths (28 selectable, five deferred Gem reference leaves). Each row includes its full parent chain. The parameter column lists explicit declarations in parent-to-child order, so a later ordinary walking speed replaces an earlier one. A speed repeated in a trait description explains its conditions. It is one movement mode. Default Upbringings, where present, are suggestions outside the mechanical totals.

### Dragonborn

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Chromatic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic.json) → [Black Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic/black.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "line", "lengthFeet": 30, "widthFeet": 5} → damageType = "acid" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Chromatic Dragonborn — Chromatic Warding / Black Dragonborn — Amphibious |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Chromatic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic.json) → [Blue Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic/blue.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "line", "lengthFeet": 30, "widthFeet": 5} → damageType = "lightning" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Chromatic Dragonborn — Chromatic Warding / Blue Dragonborn — Displaced Voice |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Chromatic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic.json) → [Green Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic/green.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "line", "lengthFeet": 30, "widthFeet": 5} → damageType = "poison" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Chromatic Dragonborn — Chromatic Warding / Green Dragonborn — Amphibious |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Chromatic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic.json) → [Red Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic/red.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "line", "lengthFeet": 30, "widthFeet": 5} → damageType = "fire", climbingSpeedFeet = 15 | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Chromatic Dragonborn — Chromatic Warding / Red Dragonborn — Natural Climber |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Chromatic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic.json) → [White Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/chromatic/white.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "line", "lengthFeet": 30, "widthFeet": 5} → damageType = "cold", swimmingSpeedFeet = 15 | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Chromatic Dragonborn — Chromatic Warding / White Dragonborn — Icewalker, Coldwater Swimmer |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Gem Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem.json) → [Amethyst Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem/amethyst.json) (deferred) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "force" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Gem Dragonborn — Psionic Mind, Gem Flight |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Gem Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem.json) → [Crystal Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem/crystal.json) (deferred) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "radiant" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Gem Dragonborn — Psionic Mind, Gem Flight |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Gem Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem.json) → [Emerald Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem/emerald.json) (deferred) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "psychic" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Gem Dragonborn — Psionic Mind, Gem Flight |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Gem Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem.json) → [Sapphire Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem/sapphire.json) (deferred) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "thunder" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Gem Dragonborn — Psionic Mind, Gem Flight |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Gem Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem.json) → [Topaz Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem/topaz.json) (deferred) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "necrotic" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Gem Dragonborn — Psionic Mind, Gem Flight |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Metallic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic.json) → [Brass Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic/brass.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "fire", burrowingSpeedFeet = 15 | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Metallic Dragonborn — Metallic Breath Weapon / Brass Dragonborn — Sand Burrower |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Metallic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic.json) → [Bronze Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic/bronze.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "lightning" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Metallic Dragonborn — Metallic Breath Weapon / Bronze Dragonborn — Amphibious |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Metallic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic.json) → [Copper Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic/copper.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "acid", climbingSpeedFeet = 15 | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Metallic Dragonborn — Metallic Breath Weapon / Copper Dragonborn — Natural Climber |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Metallic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic.json) → [Gold Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic/gold.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "fire" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Metallic Dragonborn — Metallic Breath Weapon / Gold Dragonborn — Amphibious |
| [Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dragonborn.json) → [Metallic Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic.json) → [Silver Dragonborn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dragonborn/metallic/silver.json) | creatureType = "dragon", size = "medium", walkingSpeedFeet = 30 → breathWeaponArea = {"shape": "cone", "lengthFeet": 15} → damageType = "cold" | Dragonborn — Low-Light Vision, Draconic Resistance, Breath Weapon / Metallic Dragonborn — Metallic Breath Weapon / Silver Dragonborn — Soft Landing |

### Dwarf

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Dwarf](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/dwarf.json) → [Hill Dwarf](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/dwarf/hill.json) | creatureType = "humanoid", size = "medium", walkingSpeedFeet = 25, darkvisionFeet = 60 | Dwarf — Dwarven Toughness, Dwarven Resilience, Stonebound Tremorsense / Hill Dwarf — Stability |

### Elf

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Elf](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/elf.json) → [Drow](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/elf/drow.json) | creatureType = "fey", size = "medium", walkingSpeedFeet = 30 → darkvisionFeet = 120, spellcastingAbilityChoice = ["intelligence", "wisdom", "charisma"] | Elf — Low-Light Vision, Keen Senses, Fey Ancestry, Trance, Magical Sleep / Drow — Dancing Lights, Faerie Fire, Darkness, Sunlight Sensitivity |
| [Elf](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/elf.json) → [High Elf](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/elf/high.json) | creatureType = "fey", size = "medium", walkingSpeedFeet = 30 → spellcastingAbilityChoice = ["intelligence", "wisdom", "charisma"] | Elf — Low-Light Vision, Keen Senses, Fey Ancestry, Trance, Magical Sleep / High Elf — Arcane Cantrip, Detect Magic, Misty Step |
| [Elf](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/elf.json) → [Wood Elf](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/elf/wood.json) | creatureType = "fey", size = "medium", walkingSpeedFeet = 30 → walkingSpeedFeet = 35, spellcastingAbilityChoice = ["intelligence", "wisdom", "charisma"] | Elf — Low-Light Vision, Keen Senses, Fey Ancestry, Trance, Magical Sleep / Wood Elf — Druidcraft, Longstrider, Pass Without Trace |

### Gnome

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Gnome](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/gnome.json) → [Forest Gnome](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/gnome/forest.json) | creatureType = "fey", size = "small", walkingSpeedFeet = 25 → spellcastingAbilityChoice = ["intelligence", "wisdom", "charisma"] | Gnome — Low-Light Vision, Gnomish Cunning / Forest Gnome — Forest Illusion, Animal Speech |
| [Gnome](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/gnome.json) → [Rock Gnome](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/gnome/rock.json) | creatureType = "fey", size = "small", walkingSpeedFeet = 25 | Gnome — Low-Light Vision, Gnomish Cunning / Rock Gnome — Earthbound Tremorsense |

### Goliath

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/goliath.json) → [Cloud Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/goliath/cloud.json) | creatureType = "giant", size = "medium", walkingSpeedFeet = 35 | Goliath — Powerful Build, Large Form, Mountain Leap, Altitude Acclimation, Giant Ancestry Aptitude / Cloud Goliath — Cloud's Jaunt, Cloud Strike, Cloudy Escape |
| [Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/goliath.json) → [Fire Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/goliath/fire.json) | creatureType = "giant", size = "medium", walkingSpeedFeet = 35 | Goliath — Powerful Build, Large Form, Mountain Leap, Altitude Acclimation, Giant Ancestry Aptitude / Fire Goliath — Fire's Burn, Born of Flame, Searing Ignition |
| [Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/goliath.json) → [Frost Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/goliath/frost.json) | creatureType = "giant", size = "medium", walkingSpeedFeet = 35 | Goliath — Powerful Build, Large Form, Mountain Leap, Altitude Acclimation, Giant Ancestry Aptitude / Frost Goliath — Frost's Chill, Born of Ice, Frigid Retaliation |
| [Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/goliath.json) → [Hill Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/goliath/hill.json) | creatureType = "giant", size = "medium", walkingSpeedFeet = 35 | Goliath — Powerful Build, Large Form, Mountain Leap, Altitude Acclimation, Giant Ancestry Aptitude / Hill Goliath — Hill's Tumble, Bulwark, Iron Stomach |
| [Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/goliath.json) → [Stone Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/goliath/stone.json) | creatureType = "giant", size = "medium", walkingSpeedFeet = 35 | Goliath — Powerful Build, Large Form, Mountain Leap, Altitude Acclimation, Giant Ancestry Aptitude / Stone Goliath — Stone's Endurance, Stone Strike, Cavernous Sight, Stone Throw |
| [Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/goliath.json) → [Storm Goliath](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/goliath/storm.json) | creatureType = "giant", size = "medium", walkingSpeedFeet = 35 | Goliath — Powerful Build, Large Form, Mountain Leap, Altitude Acclimation, Giant Ancestry Aptitude / Storm Goliath — Storm's Thunder, Storm Strike, Maelstrom Aura |

### Halfling

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Halfling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/halfling.json) → [Lightfoot Halfling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/halfling/lightfoot.json) | creatureType = "humanoid", size = "small", walkingSpeedFeet = 25 | Halfling — Halfling Luck, Brave, Halfling Nimbleness, Naturally Stealthy, Keen Hearing / Lightfoot Halfling — Natural Agility |

### Human

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Human](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/human.json) | creatureType = "humanoid", sizeChoice = {"from": ["small", "medium"], "when": "character-creation"}, walkingSpeedFeet = 30 | Human — Skillful, Versatile, Adaptable Learning |

### Orc

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Orc](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/orc.json) | creatureType = "humanoid", size = "medium", walkingSpeedFeet = 30 | Orc — Low-Light Vision, Adrenaline Rush, Relentless Endurance, Powerful Build, Savage Attacks, Menacing Presence |

### Tiefling

| Selected path | Intrinsics and choices | Effective trait occurrences by source |
|---|---|---|
| [Tiefling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/tiefling.json) → [Abyssal Tiefling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/tiefling/abyssal.json) | creatureType = "fiend", sizeChoice = {"from": ["small", "medium"], "when": "character-creation"}, walkingSpeedFeet = 30, needs = {"breathing": true, "food": true, "sleep": true}, ancestrySelection = {"required": true, "selectOneFrom": ["ancestry.tiefling.abyssal", "ancestry.tiefling.chthonic", "ancestry.tiefling.infernal"]} → ancestryTheme = "Demonic fiendish ancestry", spellcastingAbilityChoice = ["intelligence", "wisdom", "charisma"] | Tiefling — Low-Light Vision, Otherworldly Presence / Abyssal Tiefling — Abyssal Resistance, Poison Spray, Ray Of Sickness, Hold Person |
| [Tiefling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/tiefling.json) → [Chthonic Tiefling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/tiefling/chthonic.json) | creatureType = "fiend", sizeChoice = {"from": ["small", "medium"], "when": "character-creation"}, walkingSpeedFeet = 30, needs = {"breathing": true, "food": true, "sleep": true}, ancestrySelection = {"required": true, "selectOneFrom": ["ancestry.tiefling.abyssal", "ancestry.tiefling.chthonic", "ancestry.tiefling.infernal"]} → ancestryTheme = "Chthonic fiendish ancestry", spellcastingAbilityChoice = ["intelligence", "wisdom", "charisma"] | Tiefling — Low-Light Vision, Otherworldly Presence / Chthonic Tiefling — Chthonic Resistance, Chill Touch, False Life, Ray Of Enfeeblement |
| [Tiefling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/base/tiefling.json) → [Infernal Tiefling](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/species/ancestry/tiefling/infernal.json) | creatureType = "fiend", sizeChoice = {"from": ["small", "medium"], "when": "character-creation"}, walkingSpeedFeet = 30, needs = {"breathing": true, "food": true, "sleep": true}, ancestrySelection = {"required": true, "selectOneFrom": ["ancestry.tiefling.abyssal", "ancestry.tiefling.chthonic", "ancestry.tiefling.infernal"]} → ancestryTheme = "Infernal fiendish ancestry", spellcastingAbilityChoice = ["intelligence", "wisdom", "charisma"] | Tiefling — Low-Light Vision, Otherworldly Presence / Infernal Tiefling — Infernal Resistance, Fire Bolt, Hellish Rebuke, Darkness |

## Appendix B · Canonical mechanics and operational qualifiers

Descriptions below are reproduced from the pinned project. The structured entries preserve additional restrictions, target and action semantics, choice bindings, progression records, and nested alternatives. Stable IDs distinguish otherwise identical names. The table above supplies actual source occurrences, including shared definitions and intrinsic values that do not live in a trait record.

### Dragonborn

**Displaced Voice** · [trait.dragonborn.blue.displaced-voice](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.blue.json)

As an Action, cause your own audible vocal sounds to seem to originate from a point of your choice within 30 feet. You can use this ability at will while speaking during that Action. It creates no image, damage, or other sound effects, does not automatically imitate another creature's voice, and does not guarantee successful deception; observers can use the ordinary rules for observation and attempts to identify an illusion.

Operational fields: `{"activation": {"action": "action"}, "atWill": true, "rangeFeet": 30, "soundSource": "own-vocal-sounds", "apparentOrigin": "chosen-point-within-range", "duration": "while-speaking-during-action", "visualEffect": false, "guaranteesDeception": false}`

**Sand Burrower** · [trait.dragonborn.brass.sand-burrower](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.brass.json)

You have a Burrow Speed of 15 feet through loose natural sand only. You can not burrow through stone, compact soil, built structures or water with this trait, and you leave no permanent traversable tunnel. The trait does not independently negate suffocation, visibility or other terrain restrictions.

Operational fields: `{"movementMode": "burrow", "speedFeet": 15, "allowedMaterials": ["loose-natural-sand"], "disallowedMaterials": ["stone", "compact-soil", "built-structures", "water"], "leavesPermanentTraversableTunnel": false, "negatesSuffocation": false, "negatesVisibilityRestrictions": false}`

**Breath Weapon** · [trait.dragonborn.breath-weapon](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/species.dragonborn.json)

On your turn when you take the Attack action, you can replace one of the attacks with your Breath Weapon. Targets in the area defined by your dragon ancestry make a Dexterity saving throw against DC 8 + your Proficiency Bonus + your Constitution modifier. A failed save deals damage of your dragon ancestry's type; a successful save takes half damage. Uses equal Proficiency Bonus and all expended uses return after a Long Rest.

Operational fields: `{"activation": {"action": "attack", "replaces": "one-attack", "timing": "on-your-turn"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}, "savingThrow": {"ability": "dexterity", "dc": "8 + proficiency-bonus + constitution-modifier", "onSuccess": "half-damage"}, "damage": {"typeFrom": "selected-dragonborn-leaf", "progression": [{"characterLevel": 1, "dice": "1d10"}, {"characterLevel": 5, "dice": "2d10"}, {"characterLevel": 11, "dice": "3d10"}, {"characterLevel": 17, "dice": "4d10"}]}, "areaFrom": "selected-dragonborn-family-or-leaf", "originBindings": {"damageType": {"lookup": "leaf", "sourceField": "damageType"}, "area": {"lookup": "nearest", "sourceField": "breathWeaponArea"}}}`

**Chromatic Warding** · [trait.dragonborn.chromatic.chromatic-warding](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.chromatic.json)

At character level 5, as an Action, become immune to the damage type of your selected Chromatic Dragonborn ancestry for 1 minute. You can do so once per Long Rest. While active, replace your existing ancestry resistance to that damage type with 100 percent resistance; do not add 100 percent to the existing 50 percent. This does not independently grant immunity to conditions associated with that damage type.

Operational fields: `{"requiredCharacterLevel": 5, "activation": {"action": "action"}, "duration": {"minutes": 1}, "uses": {"value": 1, "recharge": "long-rest"}, "damageResistanceOverride": {"percent": 100, "damageTypeFrom": "selected-dragonborn-leaf", "stacking": "replace-lower-same-type"}, "doesNotGrantConditionImmunity": true, "originBindings": {"damageType": {"lookup": "leaf", "sourceField": "damageType"}}}`

**Natural Climber** · [trait.dragonborn.copper.natural-climber](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.copper.json)

You have a Climb Speed of 15 feet. This does not grant Spider Climb, ceiling traversal, or automatic success on exceptionally difficult surfaces.

Operational fields: `{"movementMode": "climb", "speedFeet": 15, "doesNotGrant": ["spider-climb", "ceiling-traversal", "automatic-success-on-exceptionally-difficult-surfaces"]}`

**Draconic Resistance** · [trait.dragonborn.draconic-resistance](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/species.dragonborn.json)

You have 50 percent resistance to the damage type associated with your specific dragon ancestry.

Operational fields: `{"damageResistance": {"percent": 50, "damageTypeFrom": "selected-dragonborn-leaf"}, "originBindings": {"damageType": {"lookup": "leaf", "sourceField": "damageType"}}}`

**Gem Flight** · [trait.dragonborn.gem.gem-flight](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.gem.json)

Starting at character level 5, you can use a Bonus Action on your turn to manifest spectral wings for 1 minute. While they last, you gain a flying speed equal to your current walking speed and can hover. You can use this trait once per Long Rest; it is independent of your Breath Weapon.

Operational fields: `{"requiredCharacterLevel": 5, "activation": {"action": "bonus-action", "timing": "on-your-turn"}, "duration": {"minutes": 1}, "uses": {"value": 1, "recharge": "long-rest", "resource": "gem-flight", "independentOf": "breath-weapon"}, "manifestation": "spectral-wings", "flyingSpeed": {"equalTo": "current-walking-speed"}, "canHover": true, "grantsPermanentFlight": false}`

**Psionic Mind** · [trait.dragonborn.gem.psionic-mind](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.gem.json)

You can communicate a telepathic message to one creature you can see within 30 feet. It understands the message if it understands at least one language, whether or not you share a language. This sends a message only: it does not read thoughts or grant the recipient a telepathic reply.

Operational fields: `{"atWill": true, "communicationMode": "telepathic-message", "rangeFeet": 30, "targetCount": 1, "requiresSight": true, "requiresRecipientLanguage": true, "requiresSharedLanguage": false, "oneWay": true, "grantsThoughtReading": false}`

**Metallic Breath Weapon** · [trait.dragonborn.metallic.metallic-breath-weapon](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.metallic.json)

Starting at character level 5, when you take the Attack action on your turn, you can replace one attack with a 15-foot cone of non-damaging Metallic Breath. Choose Enervating Breath or Repulsion Breath when used. Each creature in the area, including allies, makes the saving throw for the chosen effect against DC 8 + your Proficiency Bonus + your Constitution modifier. You can use either option once per Long Rest in total; this is a separate resource from ordinary Breath Weapon uses.

Operational fields: `{"requiredCharacterLevel": 5, "activation": {"action": "attack", "replaces": "one-attack", "timing": "on-your-turn"}, "uses": {"value": 1, "recharge": "long-rest", "resource": "metallic-breath-weapon", "independentOf": "breath-weapon"}, "area": {"shape": "cone", "lengthFeet": 15, "targets": "each-creature-in-area", "affectsAllies": true}, "choiceTiming": "on-use", "options": [{"id": "enervating-breath", "name": "Enervating Breath", "savingThrow": {"ability": "constitution", "dc": "8 + proficiency-bonus + constitution-modifier", "onSuccess": "no-effect"}, "onFailure": {"applyCondition": "condition.incapacitated", "endsAt": "start-of-your-next-turn"}}, {"id": "repulsion-breath", "name": "Repulsion Breath", "savingThrow": {"ability": "strength", "dc": "8 + proficiency-bonus + constitution-modifier", "onSuccess": "no-effect"}, "onFailure": {"pushFeet": 20, "direction": "directly-away-from-you", "forcedMovement": "instantaneous", "applyCondition": "condition.prone"}}]}`

**Natural Climber** · [trait.dragonborn.red.natural-climber](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.red.json)

You have a Climb Speed of 15 feet. This does not grant Spider Climb, the ability to move on ceilings, or automatic success on exceptionally difficult climbing surfaces.

Operational fields: `{"movementMode": "climb", "speedFeet": 15, "doesNotGrant": ["spider-climb", "ceiling-traversal", "automatic-success-on-exceptionally-difficult-surfaces"]}`

**Soft Landing** · [trait.dragonborn.silver.soft-landing](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.silver.json)

When you fall, you can use your Reaction to slow your own descent to 60 feet per round for up to 1 minute. If you land during this effect, you take no damage from the fall and the effect ends. You can use this trait at will. It does not protect another creature or grant flight or hovering.

Operational fields: `{"activation": {"action": "reaction", "trigger": "when-you-fall"}, "atWill": true, "targets": "self", "fallingSpeedFeetPerRound": 60, "duration": {"minutes": 1}, "endsOnLanding": true, "preventsFallingDamageOnLanding": true, "protectsOtherCreatures": false, "grantsFlight": false, "grantsHover": false}`

**Coldwater Swimmer** · [trait.dragonborn.white.coldwater-swimmer](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.white.json)

You have a Swim Speed of 15 feet. This trait does not let you breathe underwater.

Operational fields: `{"movementMode": "swim", "speedFeet": 15, "grantsWaterBreathing": false}`

**Icewalker** · [trait.dragonborn.white.icewalker](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dragonborn/ancestry.dragonborn.white.json)

You can move across and climb ordinary icy surfaces without ability checks required solely because of their iciness, and ordinary ice or snow Difficult Terrain does not cost you extra movement. You do not need the Dexterity saving throw to avoid falling Prone when it is triggered solely by ordinary slippery ice. This trait does not negate unrelated hazards, other causes of falling Prone, or magical effects with additional restrictions.

Operational fields: `{"terrain": ["ordinary-ice", "ordinary-snow"], "ignoresDifficultTerrainFrom": ["ice", "snow"], "ignoreIcySurfaceAbilityChecks": true, "slipperyIceSaveExemption": {"trigger": "ordinary-slippery-ice-alone", "savingThrow": "dexterity", "effect": "prone"}, "doesNotNegateOtherHazards": true}`


### Dwarf

**Dwarven Resilience** · [trait.dwarf.dwarven-resilience](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dwarf/species.dwarf.json)

Advantage on saving throws against poison effects and 50 percent resistance to poison damage.

Operational fields: `{"savingThrows": {"advantageAgainst": ["poison-effects"]}, "damageResistance": {"poison": 50}}`

**Dwarven Toughness** · [trait.dwarf.dwarven-toughness](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dwarf/species.dwarf.json)

Increase maximum hit points by 1 for each character level.

Operational fields: `{"hitPointsPerCharacterLevel": 1}`

**Stability** · [trait.dwarf.hill.stability](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dwarf/ancestry.dwarf.hill.json)

While on stable ground, gain advantage on checks and saving throws resisting forced pushing or being knocked Prone; does not cover pulling or confer immunity.

**Stonebound Tremorsense** · [trait.dwarf.stonebound-tremorsense](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/dwarf/species.dwarf.json)

While in contact with stone, activate 60-foot Tremorsense for 10 minutes as a Bonus Action. Uses equal Proficiency Bonus per Long Rest.

Operational fields: `{"sense": "tremorsense", "rangeFeet": 60, "requiresContact": "stone", "activation": "bonus-action", "durationMinutes": 10, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`


### Elf

**Dancing Lights** · [trait.elf.drow.dancing-lights](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.drow.json)

Gain Dancing Lights at level 1.

Operational fields: `{"spell": "dancing-lights", "level": 1, "atWill": true}`

**Darkness** · [trait.elf.drow.darkness](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.drow.json)

At level 5, know Darkness; once per Long Rest free, and also with spell slots. Does not automatically grant vision through magical Darkness.

Operational fields: `{"spell": "darkness", "level": 5, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Faerie Fire** · [trait.elf.drow.faerie-fire](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.drow.json)

At level 3, know Faerie Fire; once per Long Rest free, and also with spell slots.

Operational fields: `{"spell": "faerie-fire", "level": 3, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Sunlight Sensitivity** · [trait.elf.drow.sunlight-sensitivity](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.drow.json)

Direct unprotected sunlight applies Dazzled. Shade, effective tinted eyewear or other effective protection can prevent or end the exposure. Ordinary magical light is not automatically sunlight.

Operational fields: `{"condition": "condition.dazzled", "trigger": "direct-unprotected-sunlight", "end": "remove-exposure-or-use-effective-protection"}`

**Fey Ancestry** · [trait.elf.fey-ancestry](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/species.elf.json)

Advantage on saving throws to resist or end Charmed.

Operational fields: `{"savingThrows": {"advantageAgainst": ["condition.charmed"]}}`

**Arcane Cantrip** · [trait.elf.high.arcane-cantrip](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.high.json)

Know one Wizard cantrip; replace it with another Wizard cantrip after each Long Rest.

Operational fields: `{"originBindings": {"spellcastingAbility": {"lookup": "manifested-intrinsic", "intrinsicSlot": "spellcastingAbility", "scope": "source"}}, "spellList": "wizard", "level": 1, "replacement": "after-long-rest"}`

**Detect Magic** · [trait.elf.high.detect-magic](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.high.json)

At level 3, know Detect Magic; once per Long Rest free, and also with spell slots.

Operational fields: `{"originBindings": {"spellcastingAbility": {"lookup": "manifested-intrinsic", "intrinsicSlot": "spellcastingAbility", "scope": "source"}}, "spell": "detect-magic", "level": 3, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Misty Step** · [trait.elf.high.misty-step](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.high.json)

At level 5, know Misty Step; once per Long Rest free, and also with spell slots.

Operational fields: `{"originBindings": {"spellcastingAbility": {"lookup": "manifested-intrinsic", "intrinsicSlot": "spellcastingAbility", "scope": "source"}}, "spell": "misty-step", "level": 5, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Keen Senses** · [trait.elf.keen-senses](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/species.elf.json)

Gain +2 Listen and +2 Spot. Existing Perception composite calculation uses these bonuses.

Operational fields: `{"skillBonuses": {"skill.listen": 2, "skill.spot": 2}}`

**Magical Sleep** · [trait.elf.magical-sleep](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/species.elf.json)

Immune to magical sleep that is not willingly accepted. Can consent to a specific magical sleep effect; this is not a general immunity toggle.

Operational fields: `{"magicalSleep": {"immune": true, "mayConsentToSpecificEffect": true}}`

**Trance** · [trait.elf.trance](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/species.elf.json)

Complete a Long Rest through four hours of trancelike meditation. Elves do not need to sleep.

Operational fields: `{"longRestHours": 4, "sleepRequired": false}`

**Druidcraft** · [trait.elf.wood.druidcraft](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.wood.json)

Gain Druidcraft at level 1.

Operational fields: `{"originBindings": {"spellcastingAbility": {"lookup": "manifested-intrinsic", "intrinsicSlot": "spellcastingAbility", "scope": "source"}}, "spell": "druidcraft", "level": 1, "atWill": true}`

**Longstrider** · [trait.elf.wood.longstrider](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.wood.json)

At level 3, know Longstrider; once per Long Rest free, and also with spell slots.

Operational fields: `{"originBindings": {"spellcastingAbility": {"lookup": "manifested-intrinsic", "intrinsicSlot": "spellcastingAbility", "scope": "source"}}, "spell": "longstrider", "level": 3, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Pass Without Trace** · [trait.elf.wood.pass-without-trace](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/elf/ancestry.elf.wood.json)

At level 5, know Pass without Trace; once per Long Rest free, and also with spell slots.

Operational fields: `{"originBindings": {"spellcastingAbility": {"lookup": "manifested-intrinsic", "intrinsicSlot": "spellcastingAbility", "scope": "source"}}, "spell": "pass-without-trace", "level": 5, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`


### Gnome

**Animal Speech** · [trait.gnome.forest.animal-speech](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/gnome/ancestry.gnome.forest.json)

Speak with Animals is always prepared; cast without a spell slot Proficiency Bonus times per Long Rest, or with an applicable spell slot.

Operational fields: `{"spell": "speak-with-animals", "alwaysPrepared": true, "freeUses": "proficiency-bonus", "recharge": "long-rest", "slotCasting": true}`

**Forest Illusion** · [trait.gnome.forest.forest-illusion](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/gnome/ancestry.gnome.forest.json)

Cast Minor Illusion at will.

Operational fields: `{"spell": "minor-illusion", "atWill": true}`

**Gnomish Cunning** · [trait.gnome.gnomish-cunning](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/gnome/species.gnome.json)

Advantage on Intelligence, Wisdom and Charisma saving throws against magic only.

Operational fields: `{"savingThrows": {"advantageAbilities": ["intelligence", "wisdom", "charisma"], "scope": "magic-only"}}`

**Earthbound Tremorsense** · [trait.gnome.rock.earthbound-tremorsense](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/gnome/ancestry.gnome.rock.json)

Continuously sense contacts up to 15 feet while both the Rock Gnome and target touch connected earth or stone. This sense does not confer sight.

Operational fields: `{"sense": "tremorsense", "rangeFeet": 15, "activation": "passive", "requiresSharedContact": ["earth", "stone"]}`


### Goliath

**Altitude Acclimation** · [trait.goliath.altitude-acclimation](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/species.goliath.json)

You are naturally acclimated to high altitudes, including elevations above 20,000 feet, even if you have never visited them. You do not incur high-altitude travel penalties that arise solely from a lack of acclimation, and spending time at lower elevations does not remove this adaptation. You must still breathe normally and are not protected from extreme cold, unrelated environmental hazards, or loss of air.

Operational fields: `{"naturallyAcclimated": true, "altitudeLimitFeet": null, "includesElevationAboveFeet": 20000, "maintainsAcclimationAtLowerElevations": true, "ignores": "penalties-from-unacclimated-high-altitude-travel", "doesNotGrant": ["cold-damage-resistance", "extreme-cold-immunity", "suffocation-immunity", "protection-from-other-hazards"]}`

**Cloud Strike** · [trait.goliath.cloud.cloud-strike](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.cloud.json)

Once per turn, when you hit with a melee weapon or thrown-weapon ranged attack, spend one use of Cloud's Jaunt to deal 1d4 additional Thunder damage. A creature struck makes a Wisdom saving throw; on failure, you become invisible only to that creature until the start of your next turn or until immediately after you make an attack roll or cast a spell. This does not conceal you from other creatures.

Operational fields: `{"requiredCharacterLevel": 1, "trigger": "melee-weapon-or-thrown-weapon-attack-hit", "oncePerTurn": true, "extraDamage": {"dice": "1d4", "damageType": "thunder", "target": "same-target"}, "savingThrow": {"ability": "wisdom", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-visibility-effect"}, "invisibility": {"onlyAgainst": "struck-creature", "onFailedSave": true, "endsAt": "start-of-your-next-turn", "alsoEndsImmediatelyAfter": ["you-make-an-attack-roll", "you-cast-a-spell"], "notGlobalCondition": true}, "sharedUsesFromTrait": "clouds-jaunt", "extraUsesGranted": false}`

**Cloud's Jaunt** · [trait.goliath.cloud.clouds-jaunt](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.cloud.json)

As a Bonus Action, you teleport up to 30 feet to an unoccupied space you can see. You can do so a number of times equal to your Proficiency Bonus per Long Rest.

Operational fields: `{"activation": {"action": "bonus-action"}, "teleport": {"maximumDistanceFeet": 30, "destination": "unoccupied-space-you-can-see"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`

**Cloudy Escape** · [trait.goliath.cloud.cloudy-escape](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.cloud.json)

When a creature you can see hits you with an attack, you can use your Reaction to resist the damage of that attack by 50 percent, then teleport up to 30 feet to an unoccupied space you can see. This defensive use has its own Proficiency Bonus uses per Long Rest.

Operational fields: `{"requiredCharacterLevel": 4, "activation": {"action": "reaction", "trigger": "visible-creature-hits-you-with-an-attack-roll"}, "damageResistance": {"percent": 50, "scope": "triggering-attack", "duration": "triggering-attack-only", "stacking": "existing-resistance-rules"}, "teleport": {"maximumDistanceFeet": 30, "destination": "unoccupied-space-you-can-see", "timing": "after-triggering-damage-is-resolved"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}, "independentUsesFrom": "clouds-jaunt"}`

**Born of Flame** · [trait.goliath.fire.born-of-flame](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.fire.json)

You have 50 percent resistance to Fire damage. This does not independently grant immunity to effects that deal Fire damage.

Operational fields: `{"requiredCharacterLevel": 4, "damageResistance": {"percent": 50, "damageType": "fire"}}`

**Fire's Burn** · [trait.goliath.fire.fires-burn](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.fire.json)

When you hit a target with an attack roll and deal damage to it, you can deal an additional 1d10 Fire damage to that target. You can do so a number of times equal to your Proficiency Bonus per Long Rest.

Operational fields: `{"trigger": "when-you-hit-with-an-attack-roll-and-deal-damage", "extraDamage": {"dice": "1d10", "damageType": "fire", "target": "same-target"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}, "coversStrikeOfTheGiantsMode": "fire-strike", "duplicateStrikeBenefitGranted": false}`

**Searing Ignition** · [trait.goliath.fire.searing-ignition](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.fire.json)

On your turn while taking the Attack action, you can replace one attack with a burst of Fire centered on you. Each creature you choose within 15 feet makes a Dexterity saving throw. On failure, it takes 1d8 plus your Proficiency Bonus Fire damage and is Blinded until the start of your next turn; on success, it takes half damage and is not Blinded. Proficiency Bonus uses per Long Rest, once per turn.

Operational fields: `{"requiredCharacterLevel": 4, "activation": {"action": "attack", "replaces": "one-attack", "timing": "on-your-turn"}, "oncePerTurn": true, "area": {"shape": "sphere", "radiusFeet": 15, "center": "self", "targets": "creatures-of-your-choice"}, "savingThrow": {"ability": "dexterity", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "half-damage-only", "onFailure": "full-damage-and-condition"}, "damage": {"dice": "1d8", "add": "proficiency-bonus", "damageType": "fire"}, "applyConditionOnFailedSave": {"condition": "condition.blinded", "endsAt": "start-of-your-next-turn"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`

**Born of Ice** · [trait.goliath.frost.born-of-ice](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.frost.json)

You have 50 percent resistance to Cold damage.

Operational fields: `{"requiredCharacterLevel": 4, "damageResistance": {"percent": 50, "damageType": "cold"}}`

**Frigid Retaliation** · [trait.goliath.frost.frigid-retaliation](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.frost.json)

Immediately after a creature you can see within 30 feet hits you with an attack roll and damages you, you can use your Reaction to require it to make a Constitution saving throw. On failure it takes 1d8 plus Proficiency Bonus Cold damage and has Speed 0 until the end of its next turn; on success it suffers neither effect. Proficiency Bonus uses per Long Rest.

Operational fields: `{"requiredCharacterLevel": 4, "activation": {"action": "reaction", "trigger": "visible-creature-within-30-feet-hits-you-with-attack-roll-and-deals-damage", "timing": "immediately-after-damage"}, "rangeFeet": 30, "savingThrow": {"ability": "constitution", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-effect"}, "onFailedSave": {"damage": {"dice": "1d8", "add": "proficiency-bonus", "damageType": "cold"}, "speedSetFeet": 0, "speedEndsAt": "end-of-targets-next-turn"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`

**Frost's Chill** · [trait.goliath.frost.frosts-chill](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.frost.json)

When you hit a target with an attack roll and deal damage to it, you can deal an additional 1d6 Cold damage to that target and reduce its Speed by 10 feet until the start of your next turn. You can do so a number of times equal to your Proficiency Bonus per Long Rest.

Operational fields: `{"trigger": "when-you-hit-with-an-attack-roll-and-deal-damage", "extraDamage": {"dice": "1d6", "damageType": "cold", "target": "same-target"}, "speedReduction": {"feet": 10, "target": "same-target", "endsAt": "start-of-your-next-turn"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}, "alternateStrikeMode": {"id": "frost-strike", "name": "Frost Strike", "description": "Instead of the usual unconditional 10-foot Speed reduction from Frost's Chill, on a qualifying melee or thrown-weapon hit that deals damage you can use the same single 1d6 Cold damage rider and require a Constitution saving throw. On failure the creature's Speed becomes 0 until the start of your next turn. On success no Speed reduction occurs. Spend one existing Frost's Chill use, not an additional use.", "trigger": "melee-weapon-or-thrown-weapon-attack-hit-and-deal-damage", "oncePerTurn": true, "replaces": "frosts-chill-speed-reduction", "extraDamageAlreadyProvidedByParent": true, "savingThrow": {"ability": "constitution", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-speed-reduction"}, "onFailedSave": {"speedSetFeet": 0, "endsAt": "start-of-your-next-turn"}, "sharedUsesFromTrait": "frosts-chill", "extraUsesGranted": false}}`

**Giant Ancestry Aptitude** · [trait.goliath.giant-ancestry-aptitude](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/species.goliath.json)

When you select a Goliath Ancestry, choose Strength or Constitution as the ability governing inherited giant-ancestry effects that require a saving throw or attack modifier. The chosen modifier is used for those abilities, including advanced features unlocked later. This choice does not grant any proficiency, ability-score increase or additional use of another feature.

Operational fields: `{"requiredCharacterLevel": 1, "abilityChoice": {"from": ["strength", "constitution"], "when": "goliath-ancestry-selection", "perCharacter": true}, "savingThrowDC": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "attackModifier": "proficiency-bonus + chosen-giant-ancestry-ability-modifier", "doesNotGrant": ["ability-score-increase", "feat-selection", "proficiency", "extra-use-pool"]}`

**Bulwark** · [trait.goliath.hill.bulwark](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.hill.json)

When an effect would move you at least 5 feet against your will or make you Prone, you can use your Reaction to prevent the movement and Prone from that triggering effect. This costs a Reaction but has no separate use limit.

Operational fields: `{"requiredCharacterLevel": 4, "activation": {"action": "reaction", "trigger": "effect-would-forcibly-move-you-at-least-5-feet-or-make-you-prone"}, "prevents": {"forcedMovementFromTrigger": true, "applyConditionFromTrigger": "condition.prone"}, "atWill": true, "doesNotGrant": ["immunity-to-all-forced-movement", "extra-reaction"]}`

**Hill's Tumble** · [trait.goliath.hill.hills-tumble](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.hill.json)

When you hit a Large or smaller creature with an attack roll and deal damage to it, you can knock that creature Prone. No additional saving throw is specified. You can do so a number of times equal to your Proficiency Bonus per Long Rest.

Operational fields: `{"trigger": "when-you-hit-with-an-attack-roll-and-deal-damage", "targetMaximumSize": "large", "applyCondition": "condition.prone", "additionalSavingThrow": false, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}, "alternateStrikeMode": {"id": "hill-strike", "name": "Hill Strike", "description": "Instead of Hill's Tumble's automatic Prone effect against a Large or smaller target, on a melee or thrown-weapon hit that deals damage you can deal another 1d6 damage of the weapon's type. If the target is a creature, it makes a Strength saving throw or becomes Prone. This mode does not have Hill's Tumble's Large-size cap, but Prone is save-dependent. Spend one existing Hill's Tumble use, not an additional use.", "trigger": "melee-weapon-or-thrown-weapon-attack-hit-and-deal-damage", "oncePerTurn": true, "replaces": "hills-tumble-automatic-prone", "extraDamage": {"dice": "1d6", "damageTypeFrom": "triggering-weapon"}, "savingThrow": {"ability": "strength", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-prone"}, "applyConditionOnFailedSave": "condition.prone", "targetMaximumSize": null, "sharedUsesFromTrait": "hills-tumble", "extraUsesGranted": false}}`

**Iron Stomach** · [trait.goliath.hill.iron-stomach](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.hill.json)

When you eat food during a Short Rest and spend at least one Hit Die to recover hit points, regain additional hit points equal to your Constitution modifier plus Proficiency Bonus. Apply this additional healing once per qualifying Short Rest, not once per Hit Die.

Operational fields: `{"requiredCharacterLevel": 4, "trigger": "during-short-rest-with-food-and-at-least-one-hit-die-spent", "extraHealing": "constitution-modifier + proficiency-bonus", "oncePerShortRest": true, "perHitDie": false}`

**Large Form** · [trait.goliath.large-form](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/species.goliath.json)

Starting at character level 5, you can become Large as a Bonus Action if there is enough space. The transformation lasts for 10 minutes or until you end it without taking an action. While transformed, you have Advantage on Strength checks and your walking Speed increases by 10 feet. You regain the use after a Long Rest. This is an actual size change, not a permanent enlargement or an automatic increase in reach.

Operational fields: `{"requiredCharacterLevel": 5, "activation": {"action": "bonus-action", "requiresSpaceFor": "large"}, "sizeChange": {"to": "large", "actualSize": true}, "duration": {"minutes": 10, "canEndEarly": true, "endEarlyAction": "none"}, "advantageOnChecks": {"ability": "strength", "checkType": "ability-check"}, "walkingSpeedBonusFeet": 10, "uses": {"value": 1, "recharge": "long-rest"}, "doesNotGrant": ["automatic-reach-increase", "permanent-large-size"]}`

**Mountain Leap** · [trait.goliath.mountain-leap](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/species.goliath.json)

You can make standing Long Jumps and standing High Jumps using the distances and heights that would apply if you had completed the normal running approach. Each jump still expends movement, and all other jump, landing, and obstacle rules continue to apply.

Operational fields: `{"standingJumpAsRunning": ["long-jump", "high-jump"], "ignoresRunningApproachRequirement": true, "movementCost": "normal-jump-movement-cost", "doesNotGrant": ["additional-movement", "flight", "automatic-landing-success", "immunity-to-falling-damage"]}`

**Powerful Build** · [trait.goliath.powerful-build](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/species.goliath.json)

You have Advantage on ability checks you make to end the Grappled condition, and you count as one size larger when determining your carrying capacity. This does not otherwise change your actual size, space, reach, weapon permissions, or grapple size modifiers.

Operational fields: `{"advantageOnAbilityChecksToEndCondition": "condition.grappled", "effectiveSizeCategoriesHigherForCarryingCapacity": 1, "doesNotChange": ["actual-size", "occupied-space", "attack-reach", "weapon-permissions", "grapple-size-modifiers"]}`

**Cavernous Sight** · [trait.goliath.stone.cavernous-sight](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.stone.json)

You gain Darkvision with a range of 60 feet. If you already have Darkvision, its range increases by 60 feet.

Operational fields: `{"requiredCharacterLevel": 4, "sense": "darkvision", "rangeFeet": 60, "addToExistingDarkvisionFeet": 60, "stacking": "once-per-feature"}`

**Stone Strike** · [trait.goliath.stone.stone-strike](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.stone.json)

Once per turn, when you hit with a melee or thrown-weapon attack, you can spend one use of Stone's Endurance to deal 1d6 additional Force damage. If the target is a creature, it makes a Strength saving throw or is pushed 10 feet directly away from you. This is a different activation of the existing ancestry use pool.

Operational fields: `{"requiredCharacterLevel": 1, "trigger": "melee-weapon-or-thrown-weapon-attack-hit", "oncePerTurn": true, "extraDamage": {"dice": "1d6", "damageType": "force", "target": "same-target"}, "savingThrow": {"ability": "strength", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-push"}, "onFailedSave": {"forcedMovementFeet": 10, "direction": "directly-away-from-you"}, "sharedUsesFromTrait": "stones-endurance", "extraUsesGranted": false}`

**Stone Throw** · [trait.goliath.stone.stone-throw](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.stone.json)

As a Bonus Action, you magically hurl a rock at a target within 60 feet using a ranged spell attack based on your chosen Goliath ancestry ability. On a hit, the rock deals 1d10 Force damage, and the target makes a Strength saving throw or becomes Prone. You can use this ability Proficiency Bonus times per Long Rest.

Operational fields: `{"requiredCharacterLevel": 4, "activation": {"action": "bonus-action"}, "requiresItem": "rock", "attack": {"type": "ranged-spell-attack", "rangeFeet": 60, "attackBonus": "proficiency-bonus + chosen-giant-ancestry-ability-modifier"}, "damage": {"dice": "1d10", "damageType": "force"}, "savingThrow": {"ability": "strength", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-prone"}, "applyConditionOnFailedSave": "condition.prone", "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`

**Stone's Endurance** · [trait.goliath.stone.stones-endurance](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.stone.json)

When you take damage, you can use your Reaction to reduce that damage by 1d12 plus your Constitution modifier. You can do so a number of times equal to your Proficiency Bonus per Long Rest.

Operational fields: `{"activation": {"action": "reaction", "trigger": "when-you-take-damage"}, "damageReduction": {"dice": "1d12", "addAbilityModifier": "constitution", "appliesTo": "triggering-damage"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`

**Maelstrom Aura** · [trait.goliath.storm.maelstrom-aura](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.storm.json)

As a Bonus Action, project a 10-foot aura of magical wind and lightning until the start of your next turn or until you become Incapacitated. It does not extend through total cover. While active, you have 50 percent resistance to Lightning and Thunder damage, and attacks against you have Disadvantage. When another creature begins its turn in the aura, you may require a Strength save; on failure its Speed is halved until the start of its next turn. Proficiency Bonus uses per Long Rest.

Operational fields: `{"requiredCharacterLevel": 4, "activation": {"action": "bonus-action"}, "area": {"shape": "aura", "radiusFeet": 10, "center": "self", "blockedBy": "total-cover"}, "duration": {"endsAt": "start-of-your-next-turn", "alsoEndsWhen": "condition.incapacitated"}, "whileActive": {"damageResistance": [{"damageType": "lightning", "percent": 50}, {"damageType": "thunder", "percent": 50}], "attackRollsTargetingYou": "disadvantage"}, "startsTurnInside": {"target": "other-creature", "savingThrow": {"ability": "strength", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-speed-change"}, "onFailedSave": {"speedMultiplier": 0.5, "endsAt": "start-of-targets-next-turn"}}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`

**Storm Strike** · [trait.goliath.storm.storm-strike](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.storm.json)

Once per turn, when you hit with a melee or thrown-weapon attack, you can spend one use of Storm's Thunder to deal 1d6 additional Lightning damage. If the target is a creature, it makes a Constitution saving throw or has Disadvantage on attack rolls until the start of your next turn.

Operational fields: `{"requiredCharacterLevel": 1, "trigger": "melee-weapon-or-thrown-weapon-attack-hit", "oncePerTurn": true, "extraDamage": {"dice": "1d6", "damageType": "lightning", "target": "same-target"}, "savingThrow": {"ability": "constitution", "dc": "8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier", "onSuccess": "no-attack-roll-penalty"}, "onFailedSave": {"attackRollMode": "disadvantage", "endsAt": "start-of-your-next-turn"}, "sharedUsesFromTrait": "storms-thunder", "extraUsesGranted": false}`

**Storm's Thunder** · [trait.goliath.storm.storms-thunder](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/goliath/ancestry.goliath.storm.json)

When a creature within 60 feet of you deals damage to you, you can use your Reaction to deal 1d8 Thunder damage to that creature. You can do so a number of times equal to your Proficiency Bonus per Long Rest.

Operational fields: `{"activation": {"action": "reaction", "trigger": "when-a-creature-within-range-deals-damage-to-you"}, "rangeFeet": 60, "retaliation": {"dice": "1d8", "damageType": "thunder", "target": "triggering-creature"}, "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}`


### Halfling

**Brave** · [trait.halfling.brave](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/halfling/species.halfling.json)

Advantage on saves to resist or end Shaken, Frightened, Panicked, and fear-induced Cowering; this does not create new saving throw opportunities.

**Halfling Luck** · [trait.halfling.halfling-luck](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/halfling/species.halfling.json)

On a natural 1 for a d20 test, reroll and use the new result.

**Halfling Nimbleness** · [trait.halfling.halfling-nimbleness](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/halfling/species.halfling.json)

Move through spaces occupied by creatures larger than you, but do not end movement there unless another ability permits it.

**Keen Hearing** · [trait.halfling.keen-hearing](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/halfling/species.halfling.json)

Gain +2 to Listen; the existing native composite-skill procedure incorporates this in Perception.

Operational fields: `{"skillBonuses": {"skill.listen": 2}}`

**Natural Agility** · [trait.halfling.lightfoot.natural-agility](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/halfling/ancestry.halfling.lightfoot.json)

Gain +2 Climb, +2 Jump and +2 Move Silently.

Operational fields: `{"skillBonuses": {"skill.climb": 2, "skill.jump": 2, "skill.move-silently": 2}}`

**Naturally Stealthy** · [trait.halfling.naturally-stealthy](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/halfling/species.halfling.json)

May Hide when obscured by a larger creature.


### Human

**Adaptable Learning** · [trait.human.adaptable-learning](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/human/species.human.json)

Gain four additional skill ranks to allocate at character level 1. At each subsequent character level, gain one additional skill rank to allocate. These ranks supplement normal advancement and are allocated by the player under the usual skill-rank limits.

Operational fields: `{"additionalRanksAtCharacterLevel1": 4, "additionalRanksPerSubsequentCharacterLevel": 1, "allocation": "player-choice", "obeysNormalSkillRankLimits": true}`

**Skillful** · [trait.human.skillful](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/human/species.human.json)

At character creation, choose one additional Skill in which you gain proficiency. This choice is separate from your additional skill ranks and from any Upbringing benefits.

Operational fields: `{"grantType": "skill-proficiency", "chooseFrom": "any-native-skill", "choiceCount": 1, "when": "character-creation"}`

**Versatile** · [trait.human.versatile](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/human/species.human.json)

At character creation, choose one additional Origin feat. This choice is in addition to any feat granted by your other character-creation selections.

Operational fields: `{"grantType": "feat-choice", "featCategory": "origin", "choiceCount": 1, "when": "character-creation"}`


### Orc

**Adrenaline Rush** · [trait.orc.adrenaline-rush](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/orc/species.orc.json)

As a Bonus Action, take the Dash action and gain Temporary Hit Points equal to your Proficiency Bonus. You can use this trait a number of times equal to your Proficiency Bonus, regaining all expended uses after a Short Rest or Long Rest.

Operational fields: `{"activation": {"action": "bonus-action", "grantsAction": "dash"}, "temporaryHitPoints": {"value": "proficiency-bonus"}, "uses": {"value": "proficiency-bonus", "recharge": "short-or-long-rest"}}`

**Menacing Presence** · [trait.orc.menacing-presence](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/orc/species.orc.json)

Gain Advantage on Intimidation checks when you use your physical presence, appearance, or stature to intimidate a creature that can perceive it. This does not grant Intimidation proficiency or apply to unrelated social intimidation.

Operational fields: `{"skillCheckAdvantage": {"skill": "skill.intimidation", "when": "physical-appearance-stature-or-presence-used-to-intimidate", "requiresTargetCanPerceivePresence": true}, "grantsSkillProficiency": false}`

**Powerful Build** · [trait.orc.powerful-build](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/orc/species.orc.json)

Count as one size larger when determining carrying capacity and how much weight you can push, drag, or lift. This does not change your actual size, occupied space, reach, weapon permissions, or grapple modifiers, and does not grant the Goliath's advantage on checks to end Grappled.

Operational fields: `{"effectiveSizeCategoriesHigherForCarryingCapacity": 1, "effectiveSizeCategoriesHigherForPushingDraggingLifting": 1, "doesNotChange": ["actual-size", "occupied-space", "attack-reach", "weapon-permissions", "grapple-size-modifiers"], "doesNotGrant": ["advantage-on-checks-to-end-grappled"]}`

**Relentless Endurance** · [trait.orc.relentless-endurance](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/orc/species.orc.json)

When you would be reduced to 0 Hit Points but are not killed outright, you can remain at 1 Hit Point instead. After using this trait, you must finish a Long Rest before using it again.

Operational fields: `{"trigger": "reduced-to-zero-hit-points-but-not-killed-outright", "hitPointsInstead": 1, "uses": {"value": 1, "recharge": "long-rest"}}`

**Savage Attacks** · [trait.orc.savage-attacks](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/orc/species.orc.json)

When you score a critical hit with a melee weapon attack, roll one additional die of the weapon's damage and add it to the critical hit's damage. The interaction with optional critical escalation and damage maximization is subject to the shared critical-rules review.

Operational fields: `{"trigger": "critical-hit-with-melee-weapon-attack", "bonusDamageDice": {"count": 1, "dieFrom": "weapon-damage-die"}, "requiresCriticalHit": true}`


### Shared

**Amphibious** · [trait.shared.amphibious](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/shared/amphibious.json)

You can breathe air and water indefinitely. This trait does not grant a swimming speed.

Operational fields: `{"canBreathe": ["air", "water"], "grantsSwimSpeed": false}`

**Low-Light Vision** · [trait.shared.low-light-vision](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/shared/low-light-vision.json)

You perceive Dim Light as Bright Light, without a separate range limit. This does not permit you to see through Darkness.

Operational fields: `{"rule": "rule.low-light-vision"}`


### Tiefling

**Abyssal Resistance** · [trait.tiefling.abyssal.abyssal-resistance](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.abyssal.json)

You have 50 percent resistance to poison damage.

Operational fields: `{"damageResistance": {"percent": 50, "damageType": "poison"}}`

**Hold Person** · [trait.tiefling.abyssal.hold-person](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.abyssal.json)

At character level 5, always have Hold Person available. Cast it once without using a spell slot per Long Rest, and also with appropriate spell slots.

Operational fields: `{"spell": "hold-person", "level": 5, "alwaysPrepared": true, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Poison Spray** · [trait.tiefling.abyssal.poison-spray](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.abyssal.json)

At character level 1, know Poison Spray and cast it at will.

Operational fields: `{"spell": "poison-spray", "level": 1, "atWill": true}`

**Ray Of Sickness** · [trait.tiefling.abyssal.ray-of-sickness](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.abyssal.json)

At character level 3, always have Ray Of Sickness available. Cast it once without using a spell slot per Long Rest, and also with appropriate spell slots.

Operational fields: `{"spell": "ray-of-sickness", "level": 3, "alwaysPrepared": true, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Chill Touch** · [trait.tiefling.chthonic.chill-touch](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.chthonic.json)

At character level 1, know Chill Touch and cast it at will.

Operational fields: `{"spell": "chill-touch", "level": 1, "atWill": true}`

**Chthonic Resistance** · [trait.tiefling.chthonic.chthonic-resistance](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.chthonic.json)

You have 50 percent resistance to necrotic damage.

Operational fields: `{"damageResistance": {"percent": 50, "damageType": "necrotic"}}`

**False Life** · [trait.tiefling.chthonic.false-life](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.chthonic.json)

At character level 3, always have False Life available. Cast it once without using a spell slot per Long Rest, and also with appropriate spell slots.

Operational fields: `{"spell": "false-life", "level": 3, "alwaysPrepared": true, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Ray Of Enfeeblement** · [trait.tiefling.chthonic.ray-of-enfeeblement](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.chthonic.json)

At character level 5, always have Ray Of Enfeeblement available. Cast it once without using a spell slot per Long Rest, and also with appropriate spell slots.

Operational fields: `{"spell": "ray-of-enfeeblement", "level": 5, "alwaysPrepared": true, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Darkness** · [trait.tiefling.infernal.darkness](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.infernal.json)

At character level 5, always have Darkness available. Cast it once without using a spell slot per Long Rest, and also with appropriate spell slots.

Operational fields: `{"spell": "darkness", "level": 5, "alwaysPrepared": true, "freeUses": 1, "recharge": "long-rest", "slotCasting": true}`

**Fire Bolt** · [trait.tiefling.infernal.fire-bolt](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.infernal.json)

At character level 1, know Fire Bolt and cast it at will.

Operational fields: `{"spell": "fire-bolt", "level": 1, "atWill": true}`

**Hellish Rebuke** · [trait.tiefling.infernal.hellish-rebuke](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.infernal.json)

At character level 3, always have Hellish Rebuke available. Cast it once without using a spell slot per Long Rest, and also with appropriate spell slots. The free casting is at spell level 2, retaining the 2014 Infernal Legacy benefit.

Operational fields: `{"spell": "hellish-rebuke", "level": 3, "alwaysPrepared": true, "freeUses": 1, "recharge": "long-rest", "slotCasting": true, "freeCastSpellLevel": 2}`

**Infernal Resistance** · [trait.tiefling.infernal.infernal-resistance](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/ancestry.tiefling.infernal.json)

You have 50 percent resistance to fire damage.

Operational fields: `{"damageResistance": {"percent": 50, "damageType": "fire"}}`

**Low-Light Vision** · [trait.tiefling.low-light-vision](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/species.tiefling.json)

You perceive Dim Light as Bright Light, without a separate range limit. This does not permit vision through Darkness.

Operational fields: `{"rule": "rule.low-light-vision"}`

**Otherworldly Presence** · [trait.tiefling.otherworldly-presence](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/e87fd21bc5f25e79fd4068b9ac71dc26144468dd/data/rulesets/first-party-001/native/traits/tiefling/species.tiefling.json)

Know Thaumaturgy at character level 1 and cast it at will. Use the spellcasting ability chosen for your Fiendish Ancestry.

Operational fields: `{"spell": "thaumaturgy", "level": 1, "atWill": true, "spellcastingAbilityFrom": "selected-tiefling-ancestry"}`

