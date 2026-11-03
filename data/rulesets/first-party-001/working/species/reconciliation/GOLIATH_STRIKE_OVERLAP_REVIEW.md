# Goliath — Strike of the Giants overlap and early progression review

**Status: research and recommendation only.** The existing six SRD 5.2.1 Goliath Subspecies remain native. No early Strike grants, extra charges, or later-level feats have been selected or promoted. This follows the owner's request to consider an early ancestral Strike (perhaps level 1) and grant corresponding advanced *Bigby's* features around **character level 4**, after investigating overlap. These are not Origin feats in this model.

## Source contracts to keep distinct

1. **SRD 5.2.1 Giant Ancestry:** one selected boon per Goliath Subspecies, with PB uses / Long Rest. The Fire, Frost and Hill boons trigger from qualifying damaging **attack rolls** (not solely melee or thrown weapons); the other three have their own Bonus Action / Reaction triggers.
2. **Bigby's *Strike of the Giants* (2023):** one chosen option, each usable on a **melee weapon attack hit** or **thrown-weapon ranged attack hit**, no more than **once per turn**; uses PB / Long Rest. Associated options have specific damage, condition, saving throw and duration rules. The source feat originally requires martial weapon proficiency or Giant Foundling background.
3. **Bigby's advanced giant feats:** each requires character level 4 and the corresponding matching Strike feat. They contain a +1 ASI and one or more named features. If we adapt them as granted Subspecies features, we must state whether the +1 ASI and feat prerequisites are removed, and replace save DCs that depend on that ASI.

Sources:
- Publisher Strike overview: https://www.dndbeyond.com/posts/1531-first-look-giant-foundling-background-and-strike
- Strike option details (secondary index): https://dnd5e.wikidot.com/feat:strike-of-the-giants
- Selected Goliath JSON: `native/species/subspecies/goliath/{cloud,fire,frost,hill,stone,storm}.json`
- SRD 5.2.1 source baseline: https://roll20.net/compendium/dnd5e/Goliath

## Exact overlap matrix (current 5.5e boon vs. early Bigby's Strike)

| Lineage | Current selected SRD boon | Bigby's Strike (level-1 candidate) | Distinctness | Implementation recommendation |
| --- | --- | --- | --- | --- |
| **Cloud** | *Cloud's Jaunt*: Bonus Action teleport 30 ft to visible free space, PB/LR | Cloud Strike: melee/thrown hit adds **1d4 Thunder**; **Wisdom save** or attacker becomes invisible **to that creature only**, until the next turn or own attack/spell; PB/LR | **Distinct.** Hit-dependent offense/invisibility vs independently triggered teleport | Could add a genuine second ancestral manifestation. Evaluate total uses and whether the Strike shares charges with Jaunt |
| **Fire** | *Fire's Burn*: damaging attack-roll hit adds **1d10 Fire**, PB/LR | Fire Strike: melee/thrown weapon hit adds **1d10 Fire**, PB/LR, once per turn | **Direct duplication.** The existing boon has broader qualifying attacks, including spell/bow hits; Strike is narrower | **Do not award as a second independent damage rider.** Treat existing Fire's Burn as the family's strike-equivalent for progression and eligibility. If a later early-level enhancement is desired, it requires a distinct new design, not the unchanged feat |
| **Frost** | *Frost's Chill*: damaging attack-roll hit adds **1d6 Cold** and unconditional **−10 ft Speed** until your next turn, PB/LR | Frost Strike: melee/thrown hit adds **1d6 Cold**; **Constitution save** or **Speed becomes 0** until your next turn, PB/LR | **High partial overlap.** Same damage, different degree and certainty of Speed control; existing boon broader attack types | Do **not** combine 2d6 Cold automatically. Potentially offer one of two control modes on a qualifying weapon hit, with one use and one damage rider; the stronger stop requires the source's save |
| **Hill** | *Hill's Tumble*: damaging attack-roll hit makes **Large-or-smaller creature Prone**, **no extra save**, PB/LR | Hill Strike: melee/thrown hit adds **1d6 damage of weapon type** and forces **Strength save** vs Prone; source's Strike text lacks the Goliath's Large-size cap | **High partial overlap.** Both impose Prone; Strike gains extra damage and may affect bigger creatures, but its Prone is save-dependent | Do **not** impose Prone twice. Potential alternative mode: automatic Prone within Large-size cap **or** +1d6 with Strength save for Prone, preserving broader target sizes; one use, not two |
| **Stone** | *Stone's Endurance*: Reaction, reduce incoming damage **1d12 + Constitution modifier**, PB/LR | Stone Strike: melee/thrown hit adds **1d6 Force**; Strength save or push target 10 ft directly away, PB/LR | **Distinct.** Reactive mitigation vs offensive push/damage | Can add as independent strike manifestation, subject to resource/attack eligibility decision |
| **Storm** | *Storm's Thunder*: Reaction when damaged by creature within 60 ft, deal **1d8 Thunder** to source, PB/LR | Storm Strike: melee/thrown hit adds **1d6 Lightning**; Constitution save or target has Disadvantage on attack rolls until your next turn, PB/LR | **Distinct.** Reactive Thunder vs proactive Lightning/debuff | Can add as independent strike manifestation, subject to resource/attack eligibility decision |

**Important differences:** Bigby's *Strike of the Giants* is **weapon-based**, whereas modern Goliath Fire/Frost/Hill options are attack-roll-based. Do not silently rewrite all six boons to the older weapon restriction. The native Hill's Tumble is especially consequential because native Prone uses different AC modifiers than stock 5e. The Bigby's Hill Strike has a saving throw; the SRD 5.2.1 Hill boon does not.

## Advancement around level 4 — overlap with existing ancestry boons

There is little **exact** duplication between the advanced six feat **feature packages** and the existing SRD boons, even though some reuse the same theme. Consequently **level-4 inherited features are much more straightforward than six additional level-1 Strike grants.**

| Giant | Advanced Bigby's feat | Distinct benefits (exclude generic +1 ASI pending a separate decision) | Overlap and watch points |
| --- | --- | --- | --- |
| Cloud | Guile of the Cloud Giant | *Cloudy Escape*: Reaction after visible attacker hits; resist damage from that attack then teleport 30 ft; PB/LR | Shares **teleportation** with Jaunt but uses a Reaction and mitigates an attack; two sources of repositioning, no same-trigger double-teleport |
| Fire | Ember of the Fire Giant | Permanent Fire resistance; *Searing Ignition*: replace one attack with selected-target, close-range Fire burst; on failed Dex save, Blinded until next turn; PB/LR | Fire already has single-target damage; AoE Blind and resistance are new, but add an outsized level-4 power spike; assign valid DC without ASI |
| Frost | Fury of the Frost Giant | Permanent Cold resistance; *Frigid Retaliation*: Reaction after qualifying visible damaging hit, Constitution save or Cold damage plus Speed 0; PB/LR | **The Speed-0 idea repeats Frost Strike**, but it occurs as a **Reaction** when damaged, not on your attack, and can remain a distinct ability. Multiple Cold attacks may consume independent PB/LR charges |
| Hill | Vigor of the Hill Giant | *Bulwark*: Reaction prevent 5-ft-or-more forced movement or Prone effect; *Iron Stomach*: extra healing during qualifying food + Hit-Dice Short Rest | Shares Prone condition **defensively** vs Tumble's offense; Bulwark has no PB recharge in the published feat, only Reaction cost. Confirm actual Short Rest/Hit Dice rules |
| Stone | Keenness of the Stone Giant | Darkvision 60 ft / extend existing by 60 ft; *Stone Throw*: Bonus Action 60-ft magical ranged spell attack, Force damage and Strength save vs Prone; PB/LR | Stone Strike (if granted early) provides **Force damage from a weapon hit** and a push; Stone Throw provides a **different bonus-action attack** and Prone. Attacking ability/DC must be selected without removed ASI |
| Storm | Soul of the Storm Giant | *Maelstrom Aura*: Bonus Action short-lived 10-ft aura, Lightning/Thunder resistance and defensive attack disadvantage, with possible speed-halving on Strength save; PB/LR | Thunder reaction remains separate; Lightning resistance is temporary and Storm Strike adds a distinct Lightning attack rider. Multiple overlapping tactical abilities plus shared Large Form at 5 |

Advanced-feature source details:
- https://dnd5e.wikidot.com/feat:guile-of-the-cloud-giant
- https://dnd5e.wikidot.com/feat:ember-of-the-fire-giant
- https://dnd5e.wikidot.com/feat:fury-of-the-frost-giant
- https://dnd5e.wikidot.com/feat:vigor-of-the-hill-giant
- https://dnd5e.wikidot.com/feat:keenness-of-the-stone-giant
- https://dnd5e.wikidot.com/feat:soul-of-the-storm-giant

## Proposed progression alternatives (none selected)

**Path A — recommended for the initial playtest:** **Level 1 existing ancestry boon; level 4 advanced giant feature; level 5 shared Large Form.** Treat the existing ancestry boon as satisfying the *conceptual* Strike prerequisite for that Subspecies's automatic level-4 feature, without literally granting `Strike of the Giants` feat or extra charges. At level 4 grant the native adapted follow-up feature(s), **excluding the +1 ASI** and using explicitly selected ability modifiers for DCs. The overlapping Strike options are retained as research or independent feat choices for later feat reconciliation. This path preserves all six equal acquisition levels and avoids writing arbitrary compensation powers for Fire.

**Path B — grant a distinct Strike option earlier (perhaps character level 2 or 3) and advanced feature at level 4:** Cloud/Stone/Storm gain a new attack rider, but **Fire gains nothing from a literal second feat** and Frost/Hill have overlapping conditions. To make B coherent, need an *express alternative/upgrade* for each:
- Fire: cannot reuse Fire Strike as an additional effect; choose a genuinely new fire-themed benefit or a clearly labeled additional charge pool if deliberately accepting more Fire's Burn uses. This is a new design decision, not published text.
- Frost: present stronger save-gated Speed-0 Strike effect as an optional rider **instead of** the SRD's unconditional −10 feet for that use; not extra 1d6.
- Hill: present save-gated, bonus-weapon-damage Strike as an alternative to no-save Prone; not two independent Prone checks on one hit.
- Cloud/Stone/Storm: grant new rider, but decide between **shared PB/LR use pool** with the primary ancestry boon or another independent PB/LR pool; this affects comparisons to Fire/Frost/Hill.
- Preserve once-per-turn melee/thrown restriction on the original Strike mode unless the rewrite explicitly changes it.
This path is possible, but the need to invent a Fire equivalent and resolve unequal charge economies makes it a materially larger reconciliation than Path A.

**Path C — level 1 full Bigby's Strike alongside level 1 SRD boon, level 4 full advanced features:** Not recommended without a deliberate power-budget decision. It gives some ancestries two genuinely different early abilities, but Fire duplicated ones. It also creates independent PB/LR charge pools for multiple nearly identical damage/control effects, and may disproportionately benefit martial weapon builds.

### Cross-cutting compatibility requirements

1. **Level is total character level** for all Species/Subspecies features, not class level. Shared Large Form remains at 5. The 4/5 adjacency creates a sizable Species progression in a short interval; intentionally playtest it.
2. **Use the native condition meanings.** Bigby's Hill Prone and Fire Blinded effects must reference native conditions; do not assume 2014/2024 stock combat consequences.
3. **ASI vs DC:** removing feat's +1 ASI leaves some original feat save DCs and Stone Throw's attack modifier undefined. One coherent new rule would use a chosen Strength or Constitution modifier consistently for ancestry powers, but that is **proposed**, not yet selected; the Bigby's Strike DC already allows Str or Con.
4. **Resource pooling:** define whether Strike and original boon have one PB/LR pool or independent pools. Advanced feat powers generally add distinct PB/LR pools; any exceptions must be explicit. Avoid reapplying the same named damage rider twice to one attack through separate sources accidentally.
5. **Feat independence:** a non-Goliath Giant Foundling and a character choosing Strike through future optional feats must remain possible. Goliath ancestry should not automatically consume general feat choices or duplicate feat grants on character sheets. Define what happens if a Goliath later independently receives the same Strike.
6. **Early level choice is not approved.** The owner has asked us to *investigate the overlap* before settling level-1 versus another early level and the ~level-4 advancement. Record any subsequent approval separately and then promote to native.

**Recommendation:** approve Path A as a lean baseline if the goal is first-pass playtest coverage. Use Path B only if the extra early Strike is an important intentional power increase; resolve Fire's duplication before implementation.
