# Goliath — Strike of the Giants overlap and early progression review

**Status: implementation recorded.** The owner directed consolidation of duplicate abilities and continuation of the first-pass playtest rules. The native six Goliath Subspecies retain the SRD 5.2.1 abilities, add merged Strike-derived attack modes at level 1 (none separately for Fire), and unlock their associated non-ASI advanced abilities at total character level 4. Shared Large Form remains at level 5. Original Bigby's feat entitlements have **not** been granted; the source content was adapted as inherited mechanics. Unsettled cross-system concerns remain open for later playtests.

## Selected native implementation (first-pass, revisable)

**Level 1:** Cloud Strike, Stone Strike and Storm Strike are added offensive alternatives, each consuming one use of their original ancestry's PB/LR pool. Frost Strike and Hill Strike are alternate modes within the existing Frost's Chill/Hill's Tumble trait, each spending one of that trait's PB/LR uses; the original baseline mode stays usable as written. Fire Strike is a duplicate of Fire's Burn and adds neither damage nor charges. The new modes retain the melee/thrown-weapon trigger, once-per-turn limit and source-related saves. The original SRD 5.2.1 abilities keep their broader activation triggers.

**Governing modifier:** choosing a Goliath Subspecies also chooses Strength or Constitution once for saving throw DCs and any magical attacks made by its inherited giant-ancestry powers: **8 + PB + chosen ability modifier** for saves, **PB + chosen ability modifier** for attack modifiers. This replaces the absent generic +1 ASI from the source's advanced feats. It is a first-pass native reconciliation decision, not a claim that the 2023 feats had the same ability choices.

**Level 4:** Cloud gains Cloudy Escape; Fire gains 50% Fire resistance and Searing Ignition; Frost gains 50% Cold resistance and Frigid Retaliation; Hill gains Bulwark and Iron Stomach; Stone gains Cavernous Sight and Stone Throw; Storm gains Maelstrom Aura. The independently activated level-4 abilities retain their own source-based PB/LR charges where applicable. Resistances are scope-limited: only Fire/Frost are permanent; Cloud applies 50% only to the triggering attack and Storm's 50% Lightning/Thunder resistance lasts only for its aura.

**Level 5:** unchanged shared Large Form. No original Strike/advanced feat was assigned, no +1 ASI and no upbringing selection was inferred. The early extra powers, fourth-level features and six-way relative balance remain explicitly subject to playtest-driven changes; they are not finalized.

**Implementation caveat:** Native JSON provides referenceable rule definitions but does not guarantee the C# Character projector or action engine already supports dynamic mode selection, Giant Ancestry Aptitude, saving throws, area powers, resources or level-gated inheritance. These are separate consumer-mechanics dependencies.

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

## Consolidation gap analysis — what the first-pass implementation would actually add

**Question:** if the SRD 5.2.1 base and the Bigby's two-stage Strike/advanced-feat chain are merged rather than duplicated, what new mechanics remain to implement? This is a *design inventory*, not authorization to edit the six native Subspecies.

### Level 1 or other early level — meaningful additions to existing SRD boons

| Ancestry | Existing boon / redundant Strike portion | Remaining benefit after deduplication | Proposed consolidation |
| --- | --- | --- | --- |
| Cloud | Existing Bonus Action 30-ft teleport; Strike itself is **not** a teleport | Strike's **1d4 Thunder** and conditional visibility concealment *against the struck creature only* (Wisdom save) | Add an offensive mode to the Cloud ancestry. Both the offensive mode and Jaunt consume **one** charge from a shared PB/LR ancestry pool; melee/thrown weapon qualification and once-per-turn limit apply to Strike mode |
| Fire | Existing Fire's Burn already provides **1d10 Fire**, and qualifies on a broader range of attack rolls | **No additional Strike effect.** The source Fire Strike does not have another rider | Keep Fire's Burn unchanged, with existing broad attack eligibility and PB/LR pool. Do not grant a second identical damage pool or invent a new feature solely to even the number of modes |
| Frost | Existing Frost's Chill covers **1d6 Cold** plus unconditional **−10 ft** Speed | On an eligible melee/thrown weapon hit, the original Strike's Constitution-save option can lower Speed to **0** | Give the existing Chill **one alternative control mode**, no additional 1d6 damage, no additional resource pool. If using the stronger effect, failure means Speed 0; preserving the existing −10-ft penalty on a successful save is an optional first-pass synthesis **not present in the source Strike** and requires an explicit ruling |
| Hill | Existing Tumble automatically makes a qualifying **Large-or-smaller** target Prone, no additional save | Strike grants **1d6 weapon-type damage** and permits a **save-gated Prone** against bigger targets | Consolidate into one attack rider with choice between SRD auto-Prone within size limit and Strike damage-plus-save for broader targets, or grant just the weapon damage on eligible attacks. Do not apply two Prone conditions, and avoid automatic no-save Prone for Huge+ targets without a separate decision |
| Stone | Stone's Endurance reduces incoming damage as a Reaction; no offense | Strike adds **1d6 Force** on melee/thrown weapon hit and save-gated **10-ft push** | Add an offensive mode sharing the existing Stone PB/LR ancestry charges; keep reaction damage reduction distinct in timing |
| Storm | Storm's Thunder is a reactive Thunder damage response | Strike adds **1d6 Lightning** on melee/thrown weapon hit and **Constitution save** against Disadvantage on attacks | Add an offensive mode sharing Storm PB/LR ancestry charges, with attack eligibility and once-per-turn limit |

**Early-tier result:** **one exact duplicate (Fire), two partial overlaps needing mode selection (Frost and Hill), three substantially new modes (Cloud, Stone and Storm).** In terms of new deliverables, **five** Subspecies need an added or modified early mode, **one** does not. This is not necessarily a power gap: Fire's existing 1d10 Fire is stronger than several individual riders, and its level-4 package is unusually rich. Distinctness, resource economy and impact matter more than equal numbers of fields.

**Single pool caveat:** Granting new modes within each Subspecies's existing PB/LR ancestry pool keeps the number of activated uses equal, but allows some ancestries more choices. Giving each copied feat its own PB/LR pool would change the intended balance and undermine the reason to merge. Any future independently obtained Strike feat must have a separate, explicit duplicate-benefit policy.

### Level 4 — advanced feat residue once overlapping base/Strike functions are removed

| Subspecies | Advanced feat features still distinct from SRD boon and merged Strike | Resistance profile |
| --- | --- | --- |
| Cloud | **Cloudy Escape:** Reaction to resist damage from one attack **and** teleport 30 ft; can be a new defensive activation **mode of Cloud's Jaunt** rather than another independent teleport ability | **Temporary, attack-scoped** resistance to triggering attack only; not permanent Thunder resistance |
| Fire | **Born of Flame:** ongoing Fire resistance; **Searing Ignition:** selective 15-ft-radius Fire burst replacing one attack, with Dexterity save and possible Blinded | **Permanent 50% Fire damage resistance** under current native resistance scale |
| Frost | **Born of Ice:** ongoing Cold resistance; **Frigid Retaliation:** Reaction to a visible qualifying damaging hit, Constitution save vs Cold damage + Speed 0 | **Permanent 50% Cold damage resistance**; Frigid Retaliation reuses a control concept, but **not the same trigger** as attack-based Frost Strike |
| Hill | **Bulwark:** Reaction prevent qualifying forced movement or Prone; **Iron Stomach:** extra healing with food and Hit Dice on Short Rest | **No published damage resistance** from this feat; Bulwark prevents displacement/condition, not damage |
| Stone | **Cavernous Sight:** Darkvision 60 ft or extend an existing range; **Stone Throw:** Bonus Action magical Force rock attack with Str save or Prone | **No published damage resistance** from this feat |
| Storm | **Maelstrom Aura:** short-duration Bonus Action defensive/control aura; attacks against you have Disadvantage and creatures starting nearby can have Speed halved on a failed Str save | **Temporary 50% Lightning and Thunder resistance while the aura is active**, not permanent and not an independent passive trait |

**Advanced-tier result:** Ten named feature units across six feats: Cloud (1), Fire (2), Frost (2), Hill (2), Stone (2), Storm (1). **Two units are straightforward permanent damage resistance grants** (Fire, Frost). The other **eight** comprise reactions, attacks, senses, healing or aura control. Cloud and Storm have *conditional* resistance embedded within their other feature; this is not an additional permanent resistance.

The two passive resistance grants do not themselves fulfill the rest of their feats: the remaining **eight** advanced-tier benefit units still need representation to preserve Bigby's full non-ASI mechanical scope. If implementation instead aims only to restore missing passive defenses, then Fire and Frost are covered, but the book's distinctive attack, reaction and utility features are intentionally **not** covered.

### Proposed minimal coherent progression, not approved

- **Level 1:** retain all six current SRD ancestry boons without damage stacking or a duplicated Fire use pool.
- **Early level (1–3 to select):** either augment five ancestries with the five merged Strike modes above sharing existing ancestry charges, or keep Strike-derived modes until the advanced tier. The user has suggested perhaps level 1; do not assume a fixed unlock.
- **Level 4:** grant the corresponding advanced feat's **non-ASI** features as Subspecies features, not feats. Cloud's escape can extend Jaunt with a distinct Reaction trigger, while Stone's new throw remains a separate attack type and Storm's aura is distinct from Thunder retaliation. **Do not automatically make all resistances permanent:** Cloud is one attack, Storm only while an aura is active, Fire/Frost are permanent.
- **Level 5:** retain shared Large Form.
- **Do not grant** the original advanced feats' six +1 ability-score increases, background requirements, or Strike prerequisite as extra player choices unless explicitly reconciled elsewhere.

### Gaps and dependency decisions if this plan is selected

1. **Early merge treatment:** exact timing, whether the five added/modified modes use the existing PB/LR pool, and how a weapon-only Strike trigger integrates without narrowing the existing broader SRD ancestry attacks.
2. **Frost/Hill synthesis:** identify which save/no-save alternatives are allowed; do not double-count damage or Prone. *A new alternative-mode rule is a Dorks & Dice design decision, not a direct Bigby's transcription.*
3. **Fire balance:** zero *additional* early Strike effect is not missing source content; it is duplication avoided. Compare the Fire level-4 package before inventing compensation.
4. **DCs and attacks:** Bigby's Strike DC can use Strength or Constitution; several advanced feats reference an ability score increased by the feat. Removing the generic ASI requires choosing a native governing ability for Stone Throw, Storm's aura and other save effects. Unresolved until selected.
5. **Resistance/effect schemas:** use the native percentage-based resistance scale (ordinary source Resistance maps to 50%); distinguish permanent Fire/Cold from Cloud attack-scoped resistance and Storm one-round aura resistance. Resolve stacking, trigger timing, source-target selection, and whether resistance is applied before/after reactive damage reductions.
6. **Feat interoperability:** a Goliath may later select Strike/advanced feat independently or gain it via another source. Do not grant duplicate entitlements automatically or prohibit non-Goliaths from obtaining those feats.
7. **Balance:** power delivery at level 4 immediately precedes level-5 Large Form; effect complexity differs by ancestry. Assess Fire Blinded, Hill native Prone, Stone Prone rock, and Storm attack Disadvantage/aura against native conditions before accepting the final level-4 package.
8. **Environment and combat engine:** selected Species/Subspecies JSON does not itself prove execution support; once approved, test inheritance and level-gated feature serialization separately from native action-economy integration.

**Net conclusion:** After consolidation, the *Bigby's* Strike layer adds **3 distinct modes + 2 upgrades**, not 6 independent feat grants. The level-4 layer includes **2 simple permanent resistance benefits + 8 other distinct benefit units**, with additional situational resistance embedded in Cloud and Storm. No new arbitrary compensating Fire feature is required to get a coherent first-pass rule; deciding *how* Fire compares with the others is a playtest-balance question.

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

**Recorded disposition:** Merged early Strike effects with the original SRD ancestries without duplicate charge pools. Advanced non-ASI feat effects are now inherited at level 4; see selected implementation at the top of this document. Path alternatives are retained as earlier research, not concurrent native rules.
