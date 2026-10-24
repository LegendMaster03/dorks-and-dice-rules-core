# Warlock Band 1 — base-class-only reconciliation crosswalk

**Status:** Completed source comparison; decisions pending. **Warlock remains pending-native-reconciliation.** This pass deliberately excludes individual invocations, expanded option catalogs, patron subclasses and prestige classes. Its purpose is to identify actual **class** design choices while reusing established shared rules.

The original 3.5e Warlock class begins in *Complete Arcane* (2004), with the March 16, 2005 errata controlling Eldritch Blast classification. Modern sources are the repo's pinned SRD 5.1 and SRD 5.2.1 structured class candidates. This is a factual progression comparison, **not** an approval of any particular reconciliation policy. Do not treat a source-specific class power as already granted by the native class.

## 20-level source progression

*The class-feature columns omit patron/subclass feature details. Invocations known are listed as **3.5 / 2014 / 2024**; counts are alternatives, not additive. “ASI” refers to ordinary advancement. The level-19 modern Epic Boon is a source feature, not automatically native eligibility.*

| Level | 3.5e Complete Arcane | 2014 base | 2024 base | Invocations known |
|---:|---|---|---|---:|
| 1 | Eldritch Blast 1d6; Least invocations | Pact Magic | Invocations; Pact Magic; Eldritch Invocation Options | 1 / 0 / 1 |
| 2 | Detect Magic | Invocations | Magical Cunning | 2 / 2 / 3 |
| 3 | Damage Reduction 1/cold iron; Eldritch Blast 2d6 | Pact Boon | — | 2 / 2 / 3 |
| 4 | Deceive Item | ASI | ASI | 3 / 2 / 3 |
| 5 | Eldritch Blast 3d6 | — | — | 3 / 3 / 5 |
| 6 | Lesser invocation grade unlock | — | — | 4 / 3 / 5 |
| 7 | Damage Reduction 2/cold iron; Eldritch Blast 4d6 | — | — | 4 / 4 / 6 |
| 8 | Fiendish Resilience 1 | ASI | ASI | 5 / 4 / 6 |
| 9 | Eldritch Blast 5d6 | — | Contact Patron | 5 / 5 / 7 |
| 10 | Energy Resistance 5 | — | — | 6 / 5 / 7 |
| 11 | Damage Reduction 3/cold iron; Eldritch Blast 6d6; Greater invocation grade unlock | Arcanum (6th level) | Arcanum | 7 / 5 / 7 |
| 12 | Imbue Item | ASI | ASI | 7 / 6 / 8 |
| 13 | Fiendish Resilience 2 | Arcanum (7th level) | Arcanum | 8 / 6 / 8 |
| 14 | Eldritch Blast 7d6 | — | — | 8 / 6 / 8 |
| 15 | Damage Reduction 4/cold iron | Arcanum (8th level) | Arcanum | 9 / 7 / 9 |
| 16 | Dark invocation grade unlock | ASI | ASI | 10 / 7 / 9 |
| 17 | Eldritch Blast 8d6 | Arcanum (9th level) | Arcanum | 10 / 7 / 9 |
| 18 | Fiendish Resilience 5 | — | — | 11 / 8 / 10 |
| 19 | Damage Reduction 5/cold iron | ASI | Epic Boon | 11 / 8 / 10 |
| 20 | Eldritch Blast 9d6; Energy Resistance 10 | Eldritch Master | Eldritch Master | 12 / 8 / 10 |

### Established matters requiring no new Wizard-style ruling

- Modern Warlock is d8, Charisma-based, proficient in Wisdom/Charisma saves, light armor and simple weapons. Historical 3.5e uses d6, three-quarter BAB, a good Will projection, and poor Fortitude/Reflex. Retaining the historical combat/saving projection follows existing class policy without changing modern Hit Die.
- Native Warlock keeps **Pact Magic** distinct from ordinary full-caster spell slots and uses published Short Rest recovery. Modern versions have equal finite 1st–5th-level spell choice counts in the imported data; native daily preparation is already removed. Open Spell Access is an independent optional campaign setting. Pact-specific spell-point conversion must not be invented.
- Mystic Arcanum at class levels **11, 13, 15, 17** provides limited higher spell access separate from Pact slots.
- The level-19 Epic Boon cannot override the shared rule that Epic Boons require **total character level 21+**. Preserve an otherwise eligible ASI/general-feat choice.
- Treat patron subclass levels **3, 6, 10, 14** as milestones, not as active subclass power definitions. The 2014 level-1 Patron feature is a historical source difference, not a second automatic 1st-level subclass grant.

### Actual class decisions to resolve

| Decision | Why required | Recommended direction, **not approved** |
|---|---|---|
| Invocation choices known | 3.5e ends at 12, 2014 at 8, 2024 at 10 | One nonadditive native progression, preserving underlying option prerequisites |
| Eldritch Blast | Automatically granted 3.5e spell-like invocation versus optional modern cantrip | One base entitlement and **one** attack/damage progression; do not stack 3.5e dice with modern beams |
| Pact Boon | Independent choice at 2014 level 3 versus part of 2024 invocation catalog | Choose one access path; do not grant double Boons |
| Original 3.5e class powers | Distinct built-in defense, healing, item-use and magic-detection features | Consider retaining them as class grants with shared native resolution, not as dozens of new subclasses |
| Invocation grades | 3.5e Least/Lesser/Greater/Dark at 1/6/11/16 versus modern per-option level prerequisites | Record unlock metadata now; detailed prerequisites are catalog work |
| Capstone and recovery | 2014 full Pact slot recovery after a 1-minute entreaty at 20; 2024 Magical Cunning at 2 upgraded by Eldritch Master at 20 | Use the 2024 upgrade path to avoid redundant full-charge grants |

### Historical features, class grants rather than subclass powers

3.5e Warlock has **Detect Magic at 2**; cold-iron-qualified **Damage Reduction at 3/7/11/15/19** (1 through 5); **Deceive Item at 4** (historically take 10 on Use Magic Device checks while distracted/threatened); **Fiendish Resilience at 8/13/18** (historical limited-use healing 1, 2, 5 per round for 2 minutes); **Energy Resistance at 10/20** (two fixed energy choices, resistance 5 then 10); and **Imbue Item at 12** (historical item-creation feat plus magic-item-use check substituting for missing prerequisite spells). These are actual original **base** features. Exact native conversion of damage reduction, skill checks and crafting is shared-system work; the decision to grant them remains open.

The original 3.5e Eldritch Blast is automatically granted and counts as an **invocation under published errata**, though it does not consume one of the 1–12 chosen invocation slots. Original damage reaches 9d6 at level 20, with one blast shape and one essence per blast. Modern 5.x Eldritch Blast is a selectable cantrip with beam scaling; imported 2024 does not automatically include it as a class feature. The base-class pass must decide baseline access and **one** scaling algorithm, while the individual shapes and essences can wait.

**Modern overlap warning:** Historical Detect Magic and modern Eldritch Sight overlap; Deceive Item and Imbue Item rely on shared magic-item competencies/crafting; Fiendish Resilience is not identical to the modern Fiendish Vigor invocation; Magical Cunning and Eldritch Master are connected. Preserve their distinction without double counting the same benefit.

### Inactive content to preserve

All Warlock imported optional-feature records, including Blade/Chain/Tome Boons and each Eldritch Invocation, remain candidates for **later** option reconciliation. Patron subclass features remain candidates for the later subclass pass. `data/reference/3x/warlock-invocations-research-index.json` is an archival research inventory and does not need to be resolved to finish Warlock Band 1.

No source candidates were deleted, no source coverage statuses were changed, and no Warlock native class feature was activated in this comparison.

**Next action:** Obtain the few class-level owner decisions above, then complete the native levels 1–20 definition, reconciled feature grants, source manifest accounting, and tests. A separate tester-facing PDF follows approval and early feedback rather than controlling this base-class scope.

**Sources:** [3.5e Warlock class](https://srd.dndtools.org/srd/classes/baseCarc/warlock.html), [Complete Arcane 2005 errata](https://www.scribd.com/document/511430654/Complete-Arcane-Errata-03-16-2005), `data/native/classes/base/warlock.json`, and `data/reference/srd/structured/class/class-warlock.json`.
