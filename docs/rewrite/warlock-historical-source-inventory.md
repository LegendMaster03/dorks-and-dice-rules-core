# Warlock historical source research — inventory before design

**Research status:** Expanded cross-edition research completed for five *identified* D&D 3.5e books and the staged SRD 5.1/5.2.1 Warlock data. **No native Warlock mechanics have been selected, and no class feature is activated by this report.** The source index is `data/reference/3x/warlock-invocations-research-index.json` (names, grade, family and provenance only).

## Source coverage

| 3.5e book | Warlock invocation entries indexed |
| --- | ---: |
| Complete Arcane (2004) | 50 |
| Complete Mage (2006) | 22 |
| Dragon Magic (2006) | 9 |
| Cityscape (2006) | 3 |
| Drow of the Underdark (2007) | 2 |
| **Total indexed** | **86** |

The grades contain 26 least, 29 lesser, 20 greater, and 11 dark invocations. These 86 entries are an inventory of the specified published sources, **not** 86 independently new native capabilities. Publication-specific prerequisites, spell-like-ability mechanics, repeated effects, and licensing remain review obligations. Additional sources (including periodicals and class variants) have not been comprehensively audited.

**Publication provenance:** Complete Arcane is a non-core-SRD copyrighted source. A book being indexed by a third-party site whose URL includes `srd` does not make its original rules open content. The inventory contains the invocation names and independently stated factual metadata only. It does not reproduce full descriptions, class prose, spell rules, or other copyrighted source text. Check licensed copies for source-accurate final adjudication.

Sources:
- [Complete Arcane invocation index](https://srd.dndtools.org/srd/magic/invocations/invocations/invocationscarc.html); [Warlock historical class](https://dndtools.net/classes/warlock/)
- [Complete Mage invocation index](https://srd.dndtools.org/srd/magic/invocations/invocations/invocationscm.html)
- [Dragon Magic invocations](https://srd.dndtools.org/srd/magic/invocations/invocations/invocationsdragonmagic.html)
- [Cityscape invocations](https://srd.dndtools.org/srd/magic/invocations/invocations/invocationscity.html)
- [Drow of the Underdark invocations](https://srd.dndtools.org/srd/magic/invocations/invocations/invocationsdotu.html)
- [Cross-book Warlock-only invocation index](https://srd.dndtools.org/srd/magic/invocations/classInvocationLists/warlockInvocations.html)

## Original 3.5e Warlock mechanics: high-confidence source facts

**Chassis:** d6 Hit Die, three-quarter BAB, good Will, poor Fortitude and Reflex, simple weapons, light armor but no shields, two skill points plus Intelligence modifier per level. The original alignment eligibility was any chaotic or any evil; it is historical data and not an approved native requirement.

**Invocations:** At-will spell-like abilities rather than ordinary spells. Original grades unlock at Warlock class levels **1/6/11/16** (least/lesser/greater/dark), with **12 known by 20**. Save DC and rules interactions depend on an individual invocation's level equivalent, not on ordinary Warlock Pact Magic. Invocation use in 3.5e was affected by action economy, attacks of opportunity, concentration/interruption, spell resistance and armor failure; translation into native rules needs its own decisions.

**Eldritch Blast:** The original is a separate at-will spell-like ray (not itself a choice from the invocation-known pool) with 60-foot range, ranged-touch resolution and spell resistance. Damage increases from 1d6 at class level 1 to 9d6 at 20. Its source milestones are **1, 3, 5, 7, 9, 11, 14, 17, 20**. Historical shapes modify delivery and essences modify attack properties; an original blast admits at most one of each. Modern 5.x Eldritch Blast is a cantrip with its own targeting and scaling, not this automatically granted class feature. Do not stack the two numerical formulas or assume a blast shape is compatible with extra beams.

**Distinct historical class features:**
- Level 2: at-will magical detection
- Levels 3/7/11/15/19: cold-iron-qualified damage reduction 1–5
- Level 4: Deceive Item, affecting the old Use Magic Device check
- Levels 8/13/18: Fiendish Resilience, briefly granting historical fast healing 1/2/5
- Levels 10/20: resistance to two chosen energy types, values 5/10
- Level 12: Imbue Item, allowing crafting qualification through the historical magic-device skill and appropriate item-creation feat

These are factual historical feature landmarks; exact skill translation, resource changes, rest timings, healing and damage-reduction resolution are not approved.

## Invocation corpus by type

The historical catalog distinguishes **blast shapes**, **eldritch essences**, and **other invocations**. Examples of mechanically significant shapes: Eldritch Spear, Eldritch Chain, Eldritch Cone, Eldritch Doom, Hideous Blow (*Complete Arcane*), Eldritch Glaive and Eldritch Line (*Dragon Magic*). Examples of essences: Frightful Blast, Brimstone Blast, Vitriolic Blast, Utterdark Blast (*Complete Arcane*); Hammer Blast, Baneful Blast, Hindering Blast and Binding Blast (*Complete Mage*); Deteriorating Blast and Penetrating Blast (*Dragon Magic*).

The **other** category is not only combat-oriented: items cover scouting/perception, movement and flight, disguises, mind effects, environmental changes, counter-magic, recovery, summoning and transformation. Notable additional options include Crawling Eye, Disembodied Hand, Mask of Flesh, Witchwood Step, Nightmares Made Real (*Complete Mage*), Cocoon of Refuse and Devil's Whispers (*Cityscape*), and Spider-Shape and Sudden Swarm (*Drow of the Underdark*).

**Index correctness warning:** The aggregated cross-book index appears to mislabel Dragonward, Eldritch Line and Penetrating Blast by category; the dedicated *Dragon Magic* invocation index supports Dragonward as an ordinary invocation, Eldritch Line as a blast shape and Penetrating Blast as an essence. Hindering Blast is an eldritch essence in the *Complete Mage* entry, despite the aggregate listing calling it ordinary. The catalog uses the corrected categories. Also, *Dragon Magic* includes **Dragonfire Adept draconic invocations**, which must not be silently imported into the Warlock list.

## Comparisons supported by the staged 2014/2024 source pack

| Concern | 3.5e | 2014 SRD 5.1 | 2024 SRD 5.2.1 |
| --- | --- | --- | --- |
| Invocation count at level 20 | 12 | 8 | 10 |
| First invocation | Warlock 1 | Warlock 2 | Warlock 1 |
| Choices form | Four-grade at-will invocation catalog | Eldritch Invocations feature, prerequisites by class level | Eldritch Invocations from level 1, enhanced catalog and prerequisites |
| Eldritch Blast | Automatic class feature, original damage progression | Optional Warlock cantrip | Optional Warlock cantrip |
| 2014 Pact Boons | No direct separate Pact Boon feature | Separate choice at class level 3 | Blade, Chain and Tome are invocation options |
| Patron milestones | Not a corresponding 5.x patron subclass | Patron at class level 1 | Subclass at class level 3 |
| Pact Magic | Original at-will invocations, **no ordinary Pact slots** | Distinct Pact slots, Short Rest recovery, Mystic Arcanum | Distinct Pact slots, Short Rest recovery, Magical Cunning and Mystic Arcanum |

The SRD 5.1 invocation counts by level are 0/2/2/2/3/3/4/4/5/5/5/6/6/6/7/7/7/8/8/8. The SRD 5.2.1 sequence is 1/3/3/3/5/5/6/6/7/7/7/8/8/8/9/9/9/10/10/10. The current 2024 imported option block explicitly references **28 invocation entries**, including the three Pact choices. Distinguish these source counts from the new indexed 3.x historical entries; names and effects may overlap.

The original 3.x source's **at-will invocation model does not imply that ordinary 2014/2024 Pact spells are at will**. Different abilities may impose usage, cost, rest or prerequisite restrictions.

## Research conclusions — not native design approvals

1. A Warlock cannot be source-complete using only *Complete Arcane* and the two modern SRDs. The five identified 3.5e books alone index 86 invocation entries.
2. The most consequential conflicts are **blast attack delivery**, **invocation eligibility/progression**, **historic at-will spell-like functionality**, **Pact Boon packaging**, and **native conversion of passive/limited-use 3.5e class features**.
3. Distinguish factual name overlap from mechanical identity. For example, Devil's Sight, Beguiling Influence, Eldritch Spear and Repelling Blast appear across editions but can have different eligibility and effects. Historical Fiendish Resilience and modern Fiendish Vigor are **not** presumed equivalent.
4. Before any implementation, create a per-option crosswalk comparing name, source, grade, function, prerequisites, timing, cost, stacking and legal use status. This index is the **historical half** of that crosswalk and intentionally does not make those choices.
5. Detailed feat and prestige-class interactions remain later-pass concerns, but source evidence includes 3.5e spell-like-ability feats and Imbue Item's dependence on historical item-creation feats.

**No owner ruling is requested by this document.** The next deliverable is comparison and categorization of source options, then a focused decision package, then a Warlock class draft for the particularly experienced early tester. Avoid producing the final player-facing PDF before decisions and early feedback; a source appendix may accompany an internal draft.

**Scope/limits:** No official proof that the cross-book third-party index is exhaustive of Dragon Magazine or online archival material; no final direct licensed-book proofreading, no verification of individual 2014/2024 invocation effect mappings beyond source progressions and candidate examples, and no decision on how to combine mechanics.
