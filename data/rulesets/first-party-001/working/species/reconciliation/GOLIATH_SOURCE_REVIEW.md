# Goliath — first-pass source reconciliation research

**Status:** Goliath Species and six giant-ancestry Subspecies have selected first-pass native mechanics, including merged Strike modes and level-4 advanced features. One shared General Goliath Upbringing and one separate Giant Foundling Upbringing are native. Six prior invented Giant Society Upbringings have been withdrawn into research; the original Giant Foundling automatic Strike feat grant was deliberately omitted from its converted Upbringing; Rune Carver remains separate.

**Goal:** document the selected first-pass Goliath Species from SRD 5.2.1 and the outstanding review of historical material and *Bigby Presents: Glory of the Giants* (2023). Inclusion in native means playable for testing, not final or balance-complete.

## Required source set

| Source | Scope to research | Disposition |
| --- | --- | --- |
| 3.5e *Races of Stone* (and relevant 3.x giant rules) | Original Goliath physical and environmental traits, Powerful Build, mountain movement and adaptation, size interactions | Historical Species and mechanical reference; do not copy edition-specific implementation uncritically |
| 5e Goliath versions, including *Elemental Evil Player's Companion* / *Volo's Guide to Monsters* and *Mordenkainen Presents: Monsters of the Multiverse* | Earlier Goliath traits and revisions; Stone's Endurance, Powerful Build, Athletics/environmental adaptation and resources | Edition comparison, verify individual books before selecting rules |
| **5.5e SRD 5.2.1** | 35-foot movement, Medium size, Powerful Build, level-5 Large Form and six Giant Ancestry choices | Selected base in `native/species/base/goliath.json` and six leaves under `native/species/subspecies/goliath/`; pinned SRD source remains in `data/reference/srd/` |
| **Bigby Presents: Glory of the Giants** (2023) | Giantkind lore, six major giant types, the Ordning, giant societies, rune magic, Giant Foundling and Rune Carver backgrounds, giant-themed feats, Path of the Giant and relevant giant monsters | Additional mandatory comparison and design inspiration; **not** an automatic grant of book feats, background abilities or class features to Goliaths |

**Bigby's six Subspecies expansion proposals:** see [`GOLIATH_BIGBYS_SUBSPECIES_EXPANSION.md`](GOLIATH_BIGBYS_SUBSPECIES_EXPANSION.md) for source-specific comparisons and six candidate secondary innate adaptations. All remain in research until explicitly selected.

## Bigby's research coverage

1. **Six giant traditions:** Cloud, Fire, Frost, Hill, Stone and Storm. Compare their distinct supernatural, elemental, physiological and environmental signatures with the six SRD 5.2.1 Goliath Giant Ancestry choices. Verify whether their distinctions are best modeled as selectable Subspecies (potentially with shared nonselectable ancestry intermediates) or as a Species choice feature. Do **not** presume the Dragonborn hierarchy must be copied.
2. **Species versus culture:** Examine the Ordning, social structures, languages, religion, arts and regional environments for learned Upbringing candidates. Giant ancestry must not impose a caste, faith, personality, profession or cultural allegiance on the player.
3. **Rune traditions:** Compare rune lore and crafting with the existing Skills/Competencies and future Upbringing work. Consider runecraft training independently of biological giant descent.
4. **Giant Foundling and Rune Carver:** Giant Foundling is a native cultural Upbringing (languages and Skills) and deliberately does not grant the source Strike of the Giants feat; this is an intentional adaptation, not an unresolved grant. Rune Carver remains a separate Background/Feat research lead. Being raised among giants and being biologically descended from giants are independent concepts.
5. **Giant-themed feats:** Examine *Strike of the Giants*, the six ancestry-associated follow-up feats and *Rune Shaper* as optional or future feat research. Do not fold their progression or prerequisites into the base Species without a separate decision.
6. **Class and monster material:** Path of the Giant Barbarian, giant stat blocks and other advanced options can inform balance and design vocabulary. They are not automatically level-1 Species features.
7. **Physical mechanics:** Reconcile Powerful Build, size and carry/lift rules, movement, mountainous adaptation, damage prevention, Large Form, activation economy, resource uses and conditions against the native mechanics rather than importing older-edition wording verbatim.

## Completed source comparison (findings, not native selections)

| Feature | *Races of Stone* (3.5e) | 5e *EEPC*/*Volo's* | 5e *Monsters of the Multiverse* | 5.5e SRD 5.2.1 |
| --- | --- | --- | --- | --- |
| Creature type | Monstrous Humanoid (with associated type implications) | Humanoid | Humanoid | Humanoid |
| Base size and walking speed | Medium, 30 ft | Medium, 30 ft | Medium, 30 ft | Medium, 35 ft |
| Powerful Build | Broader favorable size treatment for opposed checks, certain special attacks and oversized weapons; space and reach remain Medium | One size larger for carry/push/drag/lift | One size larger for carry/push/drag/lift | One size larger for carrying capacity and Advantage on ability checks to end Grappled |
| Movement/environment | Mountain Movement (standing jumps and accelerated climbing), permanent high-altitude acclimation | Mountain Born: cold resistance and altitude adaptation | Mountain Born retained with cold resistance | Mountain Born omitted |
| General athletic training | 3.5e culture/skills references rather than a baseline 5e Athletics proficiency | Natural Athlete: Athletics proficiency | Little Giant: Athletics proficiency plus carry benefit | No automatic Athletics proficiency |
| Damage reduction | None as a shared Goliath trait | Stone's Endurance once per Short/Long Rest | Stone's Endurance PB per Long Rest | Stone's Endurance only as one of six ancestry choices, PB per Long Rest |
| Giant ancestry choice | No six-way Giant Ancestry selection | No six-way Giant Ancestry selection | No six-way Giant Ancestry selection | Six choices: Cloud, Fire, Frost, Hill, Stone and Storm |
| Transformation | None | None | None | Large Form at level 5, once per Long Rest, 10 minutes, +10 ft speed and Advantage on Strength checks |

The older 3.5e +4 Strength, −2 Dexterity, +2 Constitution, +1 level adjustment, favored class, and Monstrous Humanoid-based implications are **edition-specific comparisons**, not automatically retained Species traits. In particular, do not automatically add 3.5e Darkvision or armor/weapon proficiency by changing a 5.5e Humanoid into a 3.5e Monstrous Humanoid.

Sources: https://dnd.arkalseif.info/races/races-of-stone--82/goliath--13/ ; https://dnd5e.wikidot.com/lineage:goliath-eepc ; https://dnd5e.wikidot.com/lineage:goliath-mpmm ; https://www.dndbeyond.com/species/1751439-goliath ; pinned SRD source in `working/species/base/goliath.json`.

## Six 5.5e Giant Ancestry abilities (selected first-pass Subspecies distinctions)

The staged SRD 5.2.1 Goliath candidate has **six mutually exclusive ancestry boons**, each with **Proficiency Bonus uses per Long Rest**. Each line below describes the published input selected for the corresponding native Subspecies (still open to playtest-driven revision):

| Ancestry | 5.5e source ability | Key mechanics / implications in Dorks & Dice |
| --- | --- | --- |
| Cloud | Cloud's Jaunt: Bonus Action teleport up to 30 ft to visible unoccupied space | Reliable position, escape and infiltration; requires the existing teleport targeting and occupancy rules |
| Fire | Fire's Burn: additional 1d10 Fire damage when an attack roll hits and deals damage | Straightforward burst damage; check future interplay with similar Feats rather than withholding the Species boon |
| Frost | Frost's Chill: 1d6 Cold damage on attack hit plus 10-ft speed penalty until start of user's next turn | Damage + control; avoid assuming the speed penalty is a condition, paralysis, or reduction below zero |
| Hill | Hill's Tumble: on damaging attack hit, make Large-or-smaller target Prone, no saving throw specified | **Major balance watch:** native Prone applies −4 melee AC, +4 ranged AC, own attack Disadvantage and a movement cost to stand, rather than standard 5e attacker Advantage/Disadvantage |
| Stone | Stone's Endurance: Reaction to reduce incoming damage by 1d12 + Constitution modifier | Defensive option previously shared by all Goliaths in 5e; in 5.5e only Stone inherits it, and it costs a Reaction |
| Storm | Storm's Thunder: Reaction after taking damage from creature within 60 ft, dealing 1d8 Thunder damage to source | Ranged retaliation, but trigger is damage received, not a freely chosen attack; do not invent a saving throw or visibility requirement |

All six inherit the shared Goliath Species traits, but do not automatically receive General Goliath Upbringing benefits if a different Upbringing was selected. The same option grants no more than PB activations per Long Rest; the six options are **alternatives**, not a pool of six powers owned by every Goliath. Damage types, damage rolls and condition interactions must be reconciled with native rules rather than edition-specific text.

## Bigby's specific contribution

*Bigby Presents: Glory of the Giants* (2023) predates the 2024 Goliath redesign. It is a **supplement to the ancestry research**, not a replacement Goliath Species definition.

- Giant society and the Ordning: six major giant kinds and differing social ideals. The ranking **Storm > Cloud > Fire > Frost > Stone > Hill** is a giant social hierarchy, **not** a mechanical power ranking or a basis for unequal Goliath Subspecies quality. The book explicitly discusses giants who reject it.
- Giant Foundling background: training in Intimidation and Survival and access to *Strike of the Giants*. This represents formative experience among giants and should be considered for Background/Upbringing, accessible independently of Goliath biological Species.
- Rune Carver background: History, Perception, Giant language and rune-oriented study; *Rune Shaper* is associated with the background. Rune crafting and teaching belong primarily to the learned/feat systems, not automatically to an anatomical trait.
- *Strike of the Giants* offers six giant-themed attack riders; additional giant feats expand these ideas. **Overlapping effects matter** when a Goliath later selects both their ancestry boon and a giant feat (e.g. additional Fire damage or repeated Hill Prone application). Do not grant these feats for free and do not presume all damage riders may or may not stack until the applicable native stacking rules and individual feat pass.
- *Path of the Giant* Barbarian and giant monster entries inform later classes, monsters and size interactions. They do not automatically imply all Goliaths share Barbarian progression, a fixed giant alignment, an Ordning rank or inherited rune spellcasting.

Publisher references: https://www.dndbeyond.com/sources/dnd/gotg ; https://www.dndbeyond.com/posts/1534-giant-personalities-an-infographic-on-the ; https://www.dndbeyond.com/posts/1531-first-look-giant-foundling-background-and-strike ; https://www.dndbeyond.com/posts/1551-channel-ancient-power-with-the-rune-carver .

## First-pass Species model — selected native baseline

**Selected shape: a nonselectable Goliath Species with six selectable giant-ancestry Subspecies.** A separate nonselectable family tier, as used for Dragonborn's Chromatic/Metallic/Gem, is not necessary at present because all six options share one giant-ancestry level. The hierarchy could grow later without requiring its initial complexity:

```text
species.goliath (nonselectable)
├─ subspecies.goliath.cloud   (selectable)
├─ subspecies.goliath.fire    (selectable)
├─ subspecies.goliath.frost   (selectable)
├─ subspecies.goliath.hill    (selectable)
├─ subspecies.goliath.stone   (selectable)
└─ subspecies.goliath.storm   (selectable)
```

Selected **common** traits: Humanoid; Medium; 35-ft walking speed; 5.5e-style Powerful Build; level-5 Large Form (Bonus Action, 10 minutes, once per Long Rest, +10 ft speed, Advantage on Strength checks and must have physical room to become Large). Selected **leaf** traits: the one relevant 5.5e ancestry boon described above (PB uses per Long Rest). No common Stone's Endurance, no unconditional universal cold resistance, no automatic 3.5e ability-score changes, no level adjustment, no assumed giant language or cultural values.

**Powerful Build is the largest native-mechanics integration question.** The native `rule.grapple-procedure` defines opposed grappling checks and distinct Escape Artist attempts, while 5.5e Powerful Build grants Advantage on ability checks to end Grappled. The first-pass description must explicitly state which native escape attempts receive the bonus. Do not silently transplant 3.5e's entire size modifier, oversized-weapon permissions, and automatic stacking on top of 5.5e Powerful Build.

**Large Form is real size change, not simply effective size.** The native `rule.spatial-foundation` has a nine-category size model with Large default 10×10-ft space but no universal added reach from size. Its historical grapple/AC size modifiers are not automatically active. Clarify that Powerful Build's carrying allowance may scale from the character's *current* actual size during Large Form, while **grappling size, weapon handling, occupied space and reach** must follow explicitly selected native mechanics, not a blanket additional category. In cramped spaces, transformation may be unavailable as the source requires room.

**Mountain legacy traits:** 3.5e Mountain Movement and altitude acclimation, and 5e Mountain Born, should be retained as research. Automatic universal cold resistance poorly fits six elemental giant ancestries (especially Fire) and stacks an additional elemental perk on top of the modern six boons. Alpine training is better considered for independently selectable Upbringings; physiological altitude adaptation may require a distinct environmental mechanics decision. A Goliath need not be raised in mountains.

**Selected Upbringings:** One traditional General Goliath Upbringing (Gol-Kaa) and one independently selectable Giant Foundling Upbringing (Giant, Intimidation and Survival training) are native. The six source giant ancestries are inherited traits, not six established Goliath clan cultures. Six invented Giant Society Upbringings were withdrawn from native; see [`GOLIATH_UPBRINGINGS_RECONCILIATION.md`](GOLIATH_UPBRINGINGS_RECONCILIATION.md) for discarded ideas and the conversion rationale. Giant Foundling intentionally grants no source bonus feat; any independently chosen giant-themed feats and Rune Carver are separate.

## 5e and 3.5e recovery assessment (selected and unselected findings)

**Status:** comparative source research only. Do not promote any item below without review and selection. The SRD 5.2.1 Goliath Species and six selectable Subspecies already in `native/` remain the coherent first-pass reference; all mechanics can be revisited after playtesting.

### Historical edition distinctions

| Trait | 3.5e *Races of Stone* (2004) | Early 5e *Elemental Evil* / *Volo's* (2015–16) | 2022 *Monsters of the Multiverse* | Selected SRD 5.2.1 |
| --- | --- | --- | --- | --- |
| Powerful Build | Treat size as one category larger when advantageous for opposed size modifiers and size-based special attacks; wield larger weapons without penalty; no extra space/reach | Count larger for carrying, pushing, dragging, lifting | Equivalent encumbrance effects under Little Giant | Larger carrying allowance plus Advantage on ability checks ending Grappled |
| Mountain Movement | Standing long/high jumps count as running; accelerated Climb at half Speed without −5 Climb check penalty | Not present | Not present | Not present |
| Altitude | Always acclimated; does not lose acclimation at lower elevations | Mountain Born grants high-altitude acclimation | Mountain Born grants high-altitude acclimation | Not present |
| Cold | No universal Cold damage resistance in original Goliath traits | Mountain Born originally cold-*climate adaptation* in *Volo's*; later official errata grants Cold resistance | Cold damage resistance | Not present |
| Athletics | No separate automatic 5e-style skill proficiency (has mountain mobility abilities) | Natural Athlete: Athletics proficiency | Little Giant: Athletics proficiency | Not present |
| Damage mitigation | No shared Stone's Endurance | Universal Stone's Endurance, 1/Short or Long Rest, Reaction, 1d12 + CON | Universal Stone's Endurance, PB/Long Rest | Stone ancestry's boon, PB/Long Rest |
| Body-language reading | +2 Sense Motive (3.5e skill) | Not present | Not present | Not present |
| Category and character-system baggage | Monstrous Humanoid, racial ability modifiers, +1 Level Adjustment, Barbarian favored class | Humanoid, fixed ability scores in original | Humanoid, flexible ability scores | Humanoid; ability scores deferred to wider system |

**Important source nuance:** *Volo's* original text and later errata differ in the protection granted by Mountain Born. Distinguish survival in extremely cold weather from Cold *damage resistance*. Earlier 5e versions should not be collapsed into one rule.

Source references:
- *Races of Stone* Goliath traits: https://dnd.arkalseif.info/races/races-of-stone--82/goliath--13/
- Early 5e Goliath: https://www.dndbeyond.com/species/22-goliath
- Official *Volo's Guide to Monsters* errata, Mountain Born: https://www.dndbeyond.com/sources/dnd/sae/volos-guide-to-monsters
- 2022 *Monsters of the Multiverse* Goliath summary: https://dnd5e.wikidot.com/lineage:goliath
- 2024 Goliath: https://www.dndbeyond.com/species/1751439-goliath
- 2024 Long Jump and High Jump rules: https://www.dndbeyond.com/sources/dnd/br-2024/rules-glossary
- 3.5e Climb accelerated penalty: https://stg.dndtools.org/rules/move-actions-17
- 5e climb movement cost: https://dnd-srd-sphinx.readthedocs.io/en/latest/adventuring.html

### Candidate dispositions, subject to approval

**A. Recover a limited standing-jump adaptation — recommended.** The clear 3.5e contribution is jumping from a standstill as though a running approach had been made, for both Long and High Jumps. That remains a meaningful distinction under the 2024 jumping procedure, which normally halves distance without a 10-foot approach. Jump distance still consumes movement; this does **not** grant free movement, ignore landing hazards, or bypass insufficient remaining Speed. A simple description referencing the native Jump procedure is more faithful than inventing a flat +2 to Athletics or a new Climb Speed.

**Accelerated-climb caveat:** 3.5e Goliaths avoided a −5 Climb check penalty to climb at half their speed; normal 3.5e climbing was quarter-speed. In modern 5.x, *ordinary* climbing already generally costs twice the movement (effectively half walking speed), and the current native spatial foundation has not specified a 3.5e accelerated-climb penalty. Therefore restoring this exact 3.5e clause **would largely duplicate baseline behavior**. Do not silently turn it into a 15-foot Climb Speed, automatic climb success or Spider Climb. If an improved climbing benefit is desired, design it separately as a deliberate new Species improvement.

**B. Recover innate high-altitude acclimation — reasonable second candidate.** Multiple historical versions grant Goliaths physiological acclimation independently of recent travel. The staged `working/hazards/high-altitude.json` treats 10,000-foot travel as more taxing and restricts acclimation above 20,000 feet without suitable native origin. Goliaths could receive a targeted exception to high-altitude travel penalties, including very high altitude, rather than generic immunity to suffocation, vacuums, environmental damage or arbitrary oxygen deprivation. This is biological adaptability and does not oblige a character to have been raised in the mountains. That hazard is still in `working/`; the mechanics would need integration when its environmental procedure becomes native.

**C. Expand Powerful Build for size-limited interactions — worth a separate ruling.** *Races of Stone* treats a Goliath as effectively one size larger for favorable opposed size modifiers, protection from certain size-restricted special attacks, and ability to wield oversized weapons; normal occupied space and reach remain unchanged. The native `rule.grapple-procedure` uses opposed grapple checks and an Escape Artist path rather than replacing both with Athletics/Acrobatics. The current native Goliath's Advantage on ability checks ending Grappled is **not automatically equivalent to Advantage on every opposed grapple roll**. Clarify exactly which escape rolls it covers when the native procedure is finalized. An optional additional effective-size benefit for qualifying grapple/trip/shove checks or defensive size thresholds is plausible, but do not unintentionally apply both size modifiers and escape Advantage to the same roll without a stacking decision. Oversized weapons require their own equipment resolution; 3.5e also allowed Powerful Build to stack with actual size-changing effects, which could make Large Form consequential outside carrying capacity. These are balance and rules-integration choices, not automatic consequences of the 2024 baseline.

**D. Natural Athlete / Little Giant — consider the Skill system before granting.** Early and 2022 5e grant Athletics proficiency. The current native/working skills model distinguishes `skill.athletics` as a composite parent of independently ranked `skill.climb`, `skill.jump` and `skill.swim`. A broad inherited Athletics proficiency is not equivalent to a bonus or training for all three child skills. If physical exceptionalism needs another expression, explicitly decide between a mechanical jump/climb benefit and a separate learned Athletics training entitlement. Do not assign both without reviewing duplication. The former is more defensibly an innate Species feature; training can belong to independently selected Upbringings.

**E. Cold damage resistance — do not grant universally without a separate comparison.** Original *Volo's* distinguished survival in a cold climate from Cold damage resistance; official errata and 2022 granted the latter. The staged `working/hazards/extreme-cold.json` already lets Cold-resistant creatures automatically succeed at environmental extreme-cold saving throws, but cold-weather endurance need not imply resistance to magical or attack-based Cold damage. Universal resistance would be an additional common defensive trait for six already strong ancestry options and could obscure Frost Goliath distinction. Alternatives: environmental-only acclimation, or Frost-specific Cold resistance considered alongside comparable individual Subspecies enhancements. Neither is selected.

**F. Sense Motive (+2), Giant language, favored class and markings — separate appropriately.** The old body-language reading bonus reflects social interpretation and is a candidate for an Upbringing/Skill feature (native `skill.insight` exists); it is not an innate personality requirement. Historical default Giant language, Barbarian favored class and Goliath markings or related feats do not automatically apply across independent Upbringings or the modern class/feat system. The 3.5e Monstrous Humanoid creature type and +1 Level Adjustment should not displace the selected Humanoid classification or reintroduce edition-specific character taxes.

### Decision tracking (Mountain Leap and Altitude Acclimation selected)

1. **Selected:** Shared **Mountain Leap** enables standing Long/High Jumps as running jumps, still subject to movement and landing rules.
2. **Selected:** Shared **Altitude Acclimation** prevents penalties for high-altitude travel arising solely from lack of acclimation, including above 20,000 feet; it does not grant Cold resistance or immunity to lack of air.
3. Review whether **Powerful Build** should explicitly grant additional size-related offensive/defensive benefits and how that interacts with the native opposed-grapple system and Large Form; keep oversized weapons a distinct question.
4. Leave Cold damage resistance, extra Athletics proficiency, original social reading bonus and older class/feat baggage for the comparative Species, Upbringing or dedicated mechanics passes unless new rationale emerges.

**Approval boundary:** only Mountain Leap and Altitude Acclimation are additionally selected in native in this pass. Powerful Build expansion, Cold resistance, other older traits, and all Bigby's-derived Subspecies proposals remain unselected.

## Open design questions (not proposals approved for implementation)

- The six giant ancestries have been selected as six playable Goliath Subspecies; review that structure only if later ruleset work supplies contrary evidence.
- Which abilities are genuinely innate and which should remain independently selectable learned traditions or later feats?
- Define native Powerful Build's exact interaction with opposed Grapple/Escape Artist resolution, size-dependent effects and carry capacity. Do not enable oversized weapon use or 3.5e special-attack size interactions automatically.
- Does inherited Large Form interact cleanly with native size, reach, occupancy and carrying capacity? Do not grant permanent Large size, extra reach or historical 3.5e AC modifiers merely from giant descent.
- What can be retained from original Goliath mountain adaptation without making all Goliaths share one regional upbringing?
- How do six giant options compare with the approved Chromatic/Metallic Dragonborn Subspecies in versatility, encounter power and exploration utility? In particular, native Prone makes Hill more potent than in standard 5.5e; Stone's reactive mitigation and Cloud's teleport have different encounter values. The objective is a first playable baseline, not final balancing.

## Verified references / further reading

- Publisher source table of contents: https://www.dndbeyond.com/sources/dnd/gotg
- Publisher's guide to Bigby's themes and the Ordning: https://www.dndbeyond.com/posts/1506-what-is-bigby-presents-glory-of-the-giants-a-huge
- Publisher's explanation of giant societies and six major kinds: https://www.dndbeyond.com/posts/1534-giant-personalities-an-infographic-on-the
- Publisher's Rune Carver / Rune Shaper overview: https://www.dndbeyond.com/posts/1551-channel-ancient-power-with-the-rune-carver
- Publisher's Giant Foundling / Strike of the Giants overview: https://www.dndbeyond.com/posts/1531-first-look-giant-foundling-background-and-strike
- Publisher's 2024 Goliath ancestry overview: https://www.dndbeyond.com/posts/1783-the-10-species-in-the-2024-players-handbook
- Selected native: `native/species/base/goliath.json` and `native/species/subspecies/goliath/`; original pinned SRD source evidence under `data/reference/srd/` and in repository history.

This file is a research map, not an exhaustive extraction of Bigby's or an approval to promote any rule. Maintain further source-specific proposals in `working/` until reviewed; the coherent SRD 5.2.1 baseline already lives in `native/`.
