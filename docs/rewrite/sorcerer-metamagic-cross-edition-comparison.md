# Metamagic across D&D 3.0, 3.5e, SRD 5.1 (2014), and SRD 5.2.1 (2024)

**Status (2026-10-23): Source comparison for owner review. No new rules approved, no native Metamagic implementation, and no changes to Sorcery Points or the established independent spell-slot/spell-point casting resources.**

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

## Consequences for native Dorks & Dice review — NOT owner approvals

1. **Preserve two entitlement types rather than overwrite one:** A character may qualify for a Metamagic **feat** through ordinary feat progression, and a Sorcerer may gain native Sorcerer **Metamagic options** through class advancement. Do not grant non-Sorcerers the class-only options automatically or require Sorcerer levels to earn a general feat.
2. **Use their actual cost currencies:** Sorcerer options spend **Sorcery Points**; historical feats could instead alter the **spellcasting expenditure required** (higher-level slot or corresponding increased spell-point casting cost), and should not also charge Sorcery Points unless a separately approved effect explicitly says so. This works within the already settled separation of the three resources.
3. **Retain non-equivalent effects:** Historical Empower (50% numerical effect) and modern Empowered (rerolled damage dice), and historical Heighten (raising spell level) and modern Heightened (save Disadvantage), are not duplicates. Historical Maximize and Widen have no straightforward core 5.x replacements.
4. **Treat Twinned as a deliberate edition conflict:** Restoring the 2014 second-target version while also retaining 2024's economical upcast-target version requires an explicit decision about whether to offer two named techniques, their eligibility, and their combination rules. Do not silently convert the 2014 feature into 2024's narrower replacement.
5. **Do not import the 3.x spontaneous casting-time penalty as a global native rule without approval:** It would interact with the modern Bonus Action and shared full-round commitment system. Historical Quicken has additional inherent incompatibility.
6. **Check 2024 advantages selectively:** Careful, Extended, Heightened, expanded option counts, Seeking and Transmuted are meaningful improvements/additions, while Twinned is fundamentally redesigned and some Quickened restrictions are edition-dependent.
7. **Keep stacking governed by the grants:** A 3.x feat's combinability is not automatic permission for a Sorcerer to ignore a modern class feature's two-option cap. The interplay of a feat and a class option on one spell must be defined by the shared spell-alteration process.

## Sources consulted

- D&D 3.0 SRD Metamagic Feats index and rules: https://www.dandwiki.com/wiki/3e_SRD:Metamagic_Feats
- D&D 3.5e SRD Metamagic feats: https://www.d20srd.org/srd/feats.htm
- D&D 3.5e optional spell-point Metamagic interaction: https://www.d20srd.org/srd/variant/magic/spellPoints.htm
- Staged SRD5.1 / SRD5.2.1 Sorcerer: `data/reference/srd/structured/class/class-sorcerer.json`
- Staged Sorcerer Metamagic features: `data/native/features/classes/sorcerer/`
- Staged SRD5.1 / SRD5.2.1 Metamagic option entries: `data/reference/srd/structured/optionalfeatures.json`
- Published 2024 PHB Sorcerer/Metamagic options: https://www.dndbeyond.com/classes/2190884-sorcerer
- 2024 versus 2014 official comparison: https://www.dndbeyond.com/posts/1769-2024-sorcerer-vs-2014-sorcerer-whats-new
