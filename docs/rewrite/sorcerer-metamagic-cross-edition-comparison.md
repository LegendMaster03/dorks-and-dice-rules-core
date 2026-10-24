# Metamagic across D&D 3.0, 3.5e, SRD 5.1 (2014), and SRD 5.2.1 (2024)

**Status (2026-10-23): Source comparison with an approved native Metamagic architecture.** The owner approved **Sorcerer class-specific Metamagic options using Sorcery Points**, supplemented with substantively distinct 3.x effects as options in that **same system**. Specific costs, option names, selection progression, balancing/eligibility and combinations remain to be reconciled; **no Sorcerer native JSON implementation has been made**. The established spell slots versus spell points choice and independent Sorcery Point resource are unchanged.

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

**Still requiring owner review:** Sorcery Point costs, the scope of maximizable and widenable effects, distinct names for the historical Empower/Heighten effects, modern option version differences (particularly 2014 versus 2024 Twinned Spell), how many options a Sorcerer learns and when, using more than one option on the same spell, and the handling of full-round/spontaneous historical casting time. Default spellcasting resources remain **either spell slots or spell points** while Sorcery Points remain independent.

## Further implementation considerations (not additional owner approvals)

1. **One Sorcerer Metamagic entitlement:** The class grants its options through Sorcerer levels. 3.x-exclusive functions are *adapted to that same list* rather than granting an automatic second feat path. Other classes do not gain the Sorcerer-specific options by default.
2. **One Metamagic spending currency:** Sorcerer Metamagic costs **Sorcery Points**, independent of the character's casting spell slots or casting spell points. Historical +slot-level costs are comparative evidence, **not native prices**.
3. **Non-equivalent historical effects remain meaningful:** Widen and Maximize are to be added; historical Empower and Heighten have distinct effects that require separate names and careful integration into the unified option list.
4. **Twinned Spell is still an edition conflict:** The broad 2014 second-target effect versus narrower 2024 upcast-target effect has not been reconciled. The architectural choice does not silently select the newer replacement.
5. **Spell-combination and casting-time limits are not settled:** Neither free universal stacking from 3.x nor the 3.x spontaneous casting-time penalty has been approved for the native class.
6. **Review modern improvements selectively:** Careful, Extended, Heightened, Seeking and Transmuted are material additions/changes, but their effects and names must be reconciled feature by feature.
7. **No automatic extension to feats or other classes:** This ruling concerns Sorcerer Metamagic. Any future metamagic-granting feat or different class's access requires its own design decision.

## Sources consulted

- D&D 3.0 SRD Metamagic Feats index and rules: https://www.dandwiki.com/wiki/3e_SRD:Metamagic_Feats
- D&D 3.5e SRD Metamagic feats: https://www.d20srd.org/srd/feats.htm
- D&D 3.5e optional spell-point Metamagic interaction: https://www.d20srd.org/srd/variant/magic/spellPoints.htm
- Staged SRD5.1 / SRD5.2.1 Sorcerer: `data/reference/srd/structured/class/class-sorcerer.json`
- Staged Sorcerer Metamagic features: `data/native/features/classes/sorcerer/`
- Staged SRD5.1 / SRD5.2.1 Metamagic option entries: `data/reference/srd/structured/optionalfeatures.json`
- Published 2024 PHB Sorcerer/Metamagic options: https://www.dndbeyond.com/classes/2190884-sorcerer
- 2024 versus 2014 official comparison: https://www.dndbeyond.com/posts/1769-2024-sorcerer-vs-2014-sorcerer-whats-new
