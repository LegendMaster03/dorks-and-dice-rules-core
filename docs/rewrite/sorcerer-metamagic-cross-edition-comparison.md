# Metamagic across D&D 3.0, 3.5e, SRD 5.1 (2014), and SRD 5.2.1 (2024)

**Status (2026-10-23): Comparison and owner-approved Metamagic design.** The unified Sorcerer-specific Metamagic option system, distinct legacy additions, exact point costs and eligibility, twin modes, combinations, known-options progression, and Arcane Apotheosis discount are now approved and recorded below. **The Sorcerer native JSON remains pending**, and the rest of the class reconciliation is incomplete. Sorcery Points remain independent of spell slots and optional spell-point casting.

## Different resource and acquisition models

| Question | D&D 3.0 / 3.5e | D&D 5e (2014) | D&D 5.5e (2024) |
| --- | --- | --- | --- |
| Entitlement | Metamagic **feats**, not a Sorcerer-only grant; require ordinary feat acquisition | Sorcerer class Metamagic **options**, usually restricted to class feature | Sorcerer class Metamagic **options** |
| Cost | Higher-level casting slot; spell's own level normally unchanged except Heighten | Spends distinct **Sorcery Points** | Spends distinct **Sorcery Points** |
| Sorcerer access | Feat prerequisites/advancement, not an intrinsic fixed class level | Level **3** | Level **2** |
| Sorcerer options available at levels 3 / 10 / 17 | Depends on acquired feats | **2 / 3 / 4** | **2 / 4 / 6** |
| Combining different modifications | **Allowed**; adjusted required spell-slot levels add together, and the same feat cannot be repeated on one spell | Normally **one** option/spell; Empowered is an exception | Normally **one**; Empowered and Seeking are exceptions. At Sorcerer 7, active **Innate Sorcery** permits up to **two** Metamagic options on a spell through Sorcery Incarnate |
| Casting-time consequence | Sorcerer/Bard spontaneous Metamagic normally turns a standard-action spell into **full-round action**, or adds a full-round action for long-casting spells; historical Quicken generally excludes spontaneous casting | No general Metamagic casting-time increase | No general Metamagic casting-time increase |
| Option replacement | Gain other feats through character advancement | No general replace-one-on-Sorcerer-level rule | Replace one known option when gaining a Sorcerer level |

**3.0 vs 3.5e:** The examined 3.0 SRD Metamagic index lists **8 core feats** (Empower, Enlarge, Extend, Heighten, Maximize, Quicken, Silent, Still). The 3.5e SRD additionally lists **Widen Spell**, making **9 core metamagic feat categories** in this source comparison. These are *source-index counts*, not a cap on feats available from noncore supplements.

## Distinct historical feats versus modern options

**The 3.x costs below are additional required spell-slot levels, not Sorcery Points.**

| Capability | 3.x Metamagic feat | 2014 Sorcerer option | 2024 Sorcerer option | Meaningful difference |
| --- | --- | --- | --- | --- |
| Protect allies from an area/save spell | No direct equivalent among examined core 3.x Metamagic feats | **Careful, 1 SP:** selected targets automatically succeed on saves, but can take half damage normally | **Careful, 1 SP:** selected targets automatically succeed and avoid the half damage they would otherwise take on a successful save | 2024 improvement |
| Increase casting range | **Enlarge, +1:** doubles eligible range | **Distant, 1 SP:** double ranged reach or touch to 30 ft | Same 1 SP | Similar outcome, different cost/eligibility |
| Alter variable effects | **Empower, +2:** multiply variable numerical effects by 1.5 | **Empowered, 1 SP:** reroll up to Charisma-modifier damage dice, taking new rolls | Same 1 SP | **Not equivalent effects**; retain both for comparison |
| Extend duration | **Extend, +1:** double eligible duration | **Extended, 1 SP:** double eligible duration, max 24 hours | Same, **plus Advantage on saves to maintain Concentration** | 2024 additional benefit |
| Increase magical force/target saves | **Heighten, variable additional slot levels:** actually increases spell level and level-dependent properties, including save DC | **Heightened, 3 SP:** one target Disadvantage on its **first** save against the spell | **Heightened, 2 SP:** one target Disadvantage on **all** saves against that spell | **Not equivalent effects**; 2024 stronger than 2014 in cost and persistence |
| Maximize numerical effects | **Maximize, +3:** variable numerical effects maximize | No equivalent core Sorcerer option | No equivalent core Sorcerer option | Historical option removed from modern base system |
| Speed casting | **Quicken, +4:** historical extra free-action casting (typically unusable with spontaneous Sorcerer casting) | **Quickened, 2 SP:** one-Action spell becomes Bonus Action; 2014 bonus-action-spell restriction applies | **Quickened, 2 SP:** Bonus Action, with explicit level-1+ same-turn casting restriction | Similar intent, incompatible costs/action semantics |
| Remove verbal components | **Silent, +1** | **Subtle, 1 SP:** removes verbal and somatic components | **Subtle, 1 SP:** also removes nonconsumed, unpriced material components | Modern Subtle bundles historical capabilities |
| Remove somatic components | **Still, +1** | Included in Subtle | Included in Subtle | Historical standalone feat remains meaningful to a caster without Sorcerer Metamagic |
| Expand spell area | **Widen, +3 (3.5e SRD):** doubles eligible area dimensions | No equivalent option | No equivalent option | Additional historical tactical capability |
| Add a target | No direct counterpart among examined core 3.x Metamagic feats | **Twinned:** a second eligible target of the same spell; cost equals spell level in SP (1 for cantrips) | **Twinned, 1 SP:** effective spell level +1 **only for spells whose upcasting adds a target** | **Major redesign**; 2014 broader target eligibility, 2024 cheaper narrower specialization |
| Retry missed spell attack | No direct equivalent in examined core 3.x Metamagic feats | Not in original 2014 core Sorcerer options; added in *Tasha's Cauldron of Everything* | **Seeking, 1 SP:** reroll missed spell attack; allowed alongside another option | Expanded core option (2024 cost reduced from Tasha's 2 SP) |
| Change damage type | No direct equivalent in examined core 3.x Metamagic feats | Not in original 2014 core Sorcerer options; introduced in *Tasha's* | **Transmuted, 1 SP:** change between acid/cold/fire/lightning/poison/thunder | Expanded core option |

**Source nuance:** The project's staged `data/reference/srd/structured/optionalfeatures.json` currently includes 8 named Sorcerer options from SRD51 and 8 from SRD52, while 2024 source class text refers to Seeking and Transmuted. Their published 2024 rules were independently checked against the current official D&D Beyond Sorcerer class rules. Do not misreport absence from the staged optionalfeatures list as absence from the 2024 book.

## Approved native direction — unified Sorcerer Metamagic (2026-10-23)

**Owner decision:** Keep **modern Sorcerer class-specific Metamagic**, paid for with the separately tracked **Sorcery Points** class resource. Do **not** implement the 3.x Metamagic feats as a second, parallel Metamagic advancement/spending system *for the Sorcerer*. Instead, **adapt 3.x mechanical functions missing from the modern option repertoire into additional options of the same Sorcerer class feature**. The owner expressly named **Widen Spell** and **Maximize Spell**. This is an architectural ruling, **not** an authorization to convert +N historical spell-slot levels directly into N Sorcery Points or assign specific action costs.

**Source-capability audit for adaptation (specific native names, prices and restrictions pending):**

| 3.x function | Whether the modern class option already represents it | Native disposition |
| --- | --- | --- |
| **Widen Spell** | No comparable core 2014/2024 area-expansion option | **Approved for addition** as a Sorcery Point Metamagic option: broaden eligible area dimensions, with exact shape/geometry and cost to be defined |
| **Maximize Spell** | No core 2014/2024 maximization option; modern Empowered Spell only rerolls dice | **Approved for addition** as a Sorcery Point Metamagic option: maximize appropriate variable numerical effects, with target variable types, cost and limits to be defined |
| **Historical Empower Spell** | **Not equivalent** to modern Empowered Spell; historical version multiplies eligible variable numerical values by 1.5 rather than rerolling damage dice | **Needs a distinct native option** under the approved rule to recover nonrepresented historical functions; use an unambiguous new name after approval of its exact effect and cost |
| **Historical Heighten Spell** | **Not equivalent** to modern Heightened Spell, which imposes Disadvantage on saving throws | **Needs a distinct native option** under the approved rule for unrepresented historical functions; distinguish effective spell-level increase from a save-modifier option; exact interaction with upcasting and cost remain open |
| **Enlarge, Extend, Silent, Still, Quicken** | Corresponding modern functionality exists through Distant, Extended, Subtle, Quickened | Reconcile the edition-specific differences inside existing modern options where reasonable; **do not add duplicated copies solely because the feat names differ** |

**No independent feat entitlement is inferred from this ruling.** In this Sorcerer base-class reconciliation, the historical 3.x Metamagic features are **source mechanics to adapt into class options**, not an additional selection of generic feats automatically granted to other classes. This does not silently prohibit a future, separately designed feat or class feature in another subsystem.

**Subsequently approved:** Detailed costs, eligibility, names, Twinned Spell modes, option-learning levels, stacking and casting-time policy, and the level-20 Arcane Apotheosis adjustment have been settled as recorded in the next section. Default spellcasting resources remain **either spell slots or spell points** while Sorcery Points remain independent.

## Owner-approved Metamagic specifications (2026-10-23)

**Owner approval:** The Sorcerer uses one class-specific, Sorcery Point-funded Metamagic options pool. The previously recommended names, numeric prices, effect restrictions, option selection progression, two Twinned Spell modes, per-spell combination rules, and Arcane Apotheosis adjustment are **approved native design decisions**. These are declarations to be implemented after the remaining Sorcerer Band 1 reconciliation; they do **not** make the currently staged Sorcerer JSON executable.

### Adapted 3.x functions: approved names, costs and limits

| Option | Price | Approved effect and restrictions |
| --- | --- | --- |
| **Amplified Spell** (historical Empower effect) | **3 Sorcery Points** | Increase by **50%, rounding down**, **one eligible variable numerical result** from the initial resolution of a spell actually cast at **level 7 or lower**, using its **rolled result**, not its maximum. Distinct from modern **Empowered Spell**, which rerolls damage dice. |
| **Ascendant Spell** (historical Heighten effect) | **2 Sorcery Points per effective level added** | Increase a spell's **effective magical level by one or two**, up to **spell level 9** and no higher than the highest spell level the Sorcerer can cast. Gain **+1 to the spell's save DC per effective level**. Effective level affects spell-level-dependent interactions such as counterspelling and magical protections. It does **not** grant the damage, additional targets or other benefits of casting with a higher-level slot. Distinct from modern **Heightened Spell**, which causes saving throw Disadvantage. |
| **Widened Spell** | **4 Sorcery Points** | For a spell actually cast at **level 6 or lower**, double the eligible **dimensions of one continuous spell area** (such as sphere radius, line dimensions, cone, cylinder or cube dimensions), not just its computed surface area. Does **not** extend casting range, duration, damage, or the number of separately created areas. |
| **Maximized Spell** | **5 Sorcery Points** | For a spell actually cast at **level 6 or lower**, maximize **one eligible variable numerical roll** in its **initial resolution**, including eligible damage or healing. It does **not** maximize attack rolls, saving throws, opposed checks, or automatically maximize repeated later damage from ongoing effects. |

**Casting-level eligibility:** The level 6/7 thresholds check the **actual spell level used to cast**, including normal upcasting, rather than only the spell's printed level. The limits on eligible variables and initial resolution are important even where historical feats had broader consequences. The exact spell-area geometry and modifier precedence in unusual spell descriptions can be handled by shared spell-effect adjudication without replacing these approved core rules.

**Approved combined Amplified + Maximized interaction:** If two-option permission is available, their combined cost is **8 Sorcery Points**, and the affected initial result is **the normal maximum plus half of an independently rolled eligible result** (round the half down). Do not merely take 150% of the maximum or maximize the second roll. Both options still must be eligible for the spell under their own level and effect restrictions.

### Twinned Spell: one class option with two exclusive modes

| Mode | Price | Effect |
| --- | --- | --- |
| **Twin** (2014-derived) | **Sorcery Points equal to the spell's actual casting level**, minimum **1** (including cantrips) | Add a second creature in ordinary range to a spell that qualifies under the **2014 single-target restrictions**: the spell targets exactly one creature, is not Self-range, and cannot target multiple creatures at the spell's current level. |
| **Expanded Targeting** (2024-derived) | **1 Sorcery Point** | Treat an eligible spell as **one level higher solely for acquiring the extra target** that its normal higher-level casting text would grant. The spell must specifically add a target when upcast. Do not also award damage or other ordinary upcasting benefits. |

The player learns **one Twinned Spell option** and chooses **exactly one mode per casting**. Neither mode can be applied twice to one casting, and the two modes cannot be combined on one spell. This keeps the broader 2014 capability and the narrower, cheaper 2024 functionality.

### Access, combinations and Arcane Apotheosis

- **Metamagic options known:** **2 at Sorcerer level 2**, **4 at level 10**, **6 at level 17**. The 3.x-derived additions are **options in this same pool**, not automatic additional selections, feats or a separate resource system. On gaining a Sorcerer level, the Sorcerer may replace **one known option** with another eligible option.
- **Combination limit:** Normally **one different Metamagic option per spell**, with preserved specific exceptions allowing Empowered Spell or Seeking Spell alongside another option. While **Sorcery Incarnate** enables its benefit during **active Innate Sorcery**, the character may apply **up to two different options to one spell**, paying each price. No option can be applied to the same casting twice; even with a specific exception, **two is the maximum during Sorcery Incarnate**.
- **Action economy:** Do **not** import the generic 3.x spontaneous-casting full-round Metamagic penalty; modern casting time and explicit option-specific restrictions, including Quickened Spell's restrictions, control.
- **Arcane Apotheosis at Sorcerer level 20:** While Innate Sorcery is active, reduce the cost of **one Metamagic option per turn by up to 2 Sorcery Points** (minimum 0) rather than making an arbitrarily expensive option free. A 2-point option becomes free; Amplified costs 1, Widened costs 2, Maximized costs 3, and a one-level Ascendant costs 0 if it receives the benefit. The second applied option, if any, pays its ordinary cost. **This modifies only Arcane Apotheosis's Metamagic discount; it does not decide the separate compatibility of earlier Sorcerous Restoration features with the level-20 capstone.**

**Pending class-wide work:** Spell access/known-spell advancement, Flexible Casting conversions in spell-point casting mode, precise Sorcerous Restoration advancement, the historical Familiar, and remaining Sorcerer class grants. **No generic 3.x Metamagic feat entitlement is implied for other classes.**

## Source-comparison considerations (superseded where resolved above)

The earlier seven considerations led to the approved native specifications above. The only open cross-system questions are how those declarations are represented by the shared spell-effect, geometry and saving throw engines, and the other Sorcerer class features that have not yet been reconciled. Do not treat the historical alternative models in the source comparison as additional selectable editions at runtime.

## Sources consulted

- D&D 3.0 SRD Metamagic Feats index and rules: https://www.dandwiki.com/wiki/3e_SRD:Metamagic_Feats
- D&D 3.5e SRD Metamagic feats: https://www.d20srd.org/srd/feats.htm
- D&D 3.5e optional spell-point Metamagic interaction: https://www.d20srd.org/srd/variant/magic/spellPoints.htm
- Staged SRD5.1 / SRD5.2.1 Sorcerer: `data/reference/srd/structured/class/class-sorcerer.json`
- Staged Sorcerer Metamagic features: `data/native/features/classes/sorcerer/`
- Staged SRD5.1 / SRD5.2.1 Metamagic option entries: `data/reference/srd/structured/optionalfeatures.json`
- Published 2024 PHB Sorcerer/Metamagic options: https://www.dndbeyond.com/classes/2190884-sorcerer
- 2024 versus 2014 official comparison: https://www.dndbeyond.com/posts/1769-2024-sorcerer-vs-2014-sorcerer-whats-new
