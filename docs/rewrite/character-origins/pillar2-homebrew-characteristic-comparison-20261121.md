# Homebrew Characteristic Comparison and Valuation Reference

**Status:** Authoring and qualitative comparison rubric. **No approved numerical prices or creation allowance.**  
**Inspected baseline:** `9f50ab70a37be69a6748603d210fa9406ba7ea59`  
**Companion:** [Homebrew Species Conversion Guide](pillar2-homebrew-species-conversion-guide-20261121.md)

## Purpose and comparison rule

Compare the **entire configured mechanical behavior**, not the Species name, trait label, or isolated bonus. Kovac's passive, active, armor/defense, and spellcasting groups are useful browsing categories; **they are not mandatory gene types** and cannot determine inheritance, cost, or execution by themselves. A single characteristic can cross several groups.

Three separate decisions are required:

1. **Mechanical identity:** Is this the same canonical characteristic, including effect, constraints, and lifecycle? If so, reuse the definition.
2. **Configured value:** Does the selected value materially change the strength or functionality of that characteristic? If so, document a separate valuation case even if the canonical definition remains shared.
3. **Whole-profile interaction:** Does its contribution depend on other effective characteristics, linked source choices, or shared pools? Evaluate the combination, not a sum of isolated labels.

At the original `9f50ab70` documentation baseline, the ledger primarily keyed grants by `intrinsic:<slot>` or `trait:<canonical-origin-trait-id>`. The integrated pricing implementation now permits authored `NativeOriginAllocationPrice.Configurations` to select one replacement load from the effective value, bound parameters, choices, canonical definition, or resource-owner presence. Missing or ambiguous required configurations fail closed, and unrecognized mechanics remain unresolved. **This is a configurable mechanism, not an approved configured-value price taxonomy or numerical schedule.**

## Comparison dimensions

| Category | Mechanical dimensions to capture | What would make two configurations materially different? |
| --- | --- | --- |
| Passive features | Always-on versus conditional; trigger; target; scope; duration; stacking; suppression; prerequisite | A universal bonus versus one that works only while submerged; advantage on one check versus all checks |
| Active features | Action, Bonus Action, Reaction, or other timing; trigger; target; range/area; saving throw; scaling; duration | At-will versus limited, an action versus a reaction, damaging versus utility, no save versus save |
| Armor and defense | AC formula or fixed floor, armor/shield stacking, applicable attacks, resistances/immunities, bypasses and vulnerabilities | AC 13 + Dexterity versus AC 13 fixed; armor-compatible versus armor-disabled; permanent versus triggered |
| Spellcasting | Exact spell/list, spell level, known versus temporary access, at-will/free uses, casting ability, slot-casting permissions, components, progression and replacement | A cantrip versus leveled spell, once per Long Rest versus at will, slot casting allowed versus disallowed |
| Movement | Mode (walk, climb, swim, burrow, fly), numeric speed, hover, altitude, terrain, carried load, armor dependency, duration, action to activate | Permanent flight versus a one-minute hover; climb speed versus Spider Climb; swim speed versus water breathing |
| Senses | Type, range, light dependence, obstruction, precision, detection versus sight, countermeasures | Low-Light Vision versus Darkvision; detecting a location versus seeing a target; 10 feet versus 60 feet |
| Innate physiology | Creature type, size, respiration, rest, nutrition, environmental tolerance, anatomy, carrying interaction | Air-and-water breathing versus underwater-only; different size category; environment-specific survival |
| Resources | Pool owner, maximum, recharge event, short/long-rest behavior, independently tracked versus shared, expenditure and capacity growth | One use shared between abilities versus one independent use each; Proficiency Bonus uses versus one fixed use |
| Restrictions | Mutually exclusive states, armor or equipment requirements, limitations, drawbacks, cost-bearing prerequisites, anti-synergy | A real operational restriction versus a flavorful inconvenience; an inseparable limitation versus a freely removed penalty |
| Progression | Unlock level, scaling at future total character levels, choices on advancement, future pool growth | Immediate availability versus level-five unlock; flat effect versus new abilities at levels 3 and 5 |
| Dependencies and choices | Selected source, stable identity, owner, selection timing, prerequisite chain, linked alternatives, replaceable selections | Fixed damage type versus a one-time choice; inherited ability tied to one source versus freely re-selected |
| External mechanics | Requires spell, condition, combat action, spatial, item, skill, class, resource, or actor-state interpretation | A complete documented spell effect versus prose that relies on an unimplemented status or targeting rule |

For every category, consider **how often it applies** and **what problem it solves**. An uncommon movement mode can bypass obstacles, scouting, grappling, melee threat, or terrain constraints even if it adds no damage. A low-damage cantrip may still be valuable when it supplies at-will range, damage type, or unusual targeting.

## Equivalence, variants, and independent identities

**Exact equivalent:** Same canonical mechanics and same effective configuration. Share the `origin-trait` definition and any approved valuation. Different Species sources remain separate provenance, not separate mechanics or automatic duplicate entitlements.

**Parameterized equivalent:** Same canonical function with an authored, valid choice (for example a spellcasting ability, damage type, or range). Reuse the canonical mechanic while recording the selected configuration and asking whether its power is materially different. Not every configuration is independently legal or currently selectable.

**Related but distinct:** Mechanical operation, timing, resource ownership, stacking, or progression differs beyond an approved parameter. Write or reference a different mechanical definition, or require an explicit schema extension if a generalized parameter can express the difference without changing the mechanic.

**Composite/linked:** Two effects function as a single inseparable package or share a dependent choice/resource. Value the package once with all members and restrictions; do not price each half and again charge for the entire package. Separately authored **interaction premiums** may apply to independent coexisting benefits when the combination itself adds power.

**Novel interaction:** An authored rule may produce a new, fixed characteristic from already effective inputs. The current universal synergy evaluator is restricted to fixed canonical outputs and supported selectors. The inherited-gene combination work is separate; neither system implies that two matching Natural Armor offers must stack or upgrade.

### Matching evidence

A proposed reuse record should include the exact old and new effects, differing parameters, scope, availability, and source provenance. "Both are natural armor," "both fly," or "both cast a spell" is insufficient. If a match is conditional or interpretation-dependent, mark it **candidate match**, not approved identity.

## How the existing allocation mechanism works

The public `NativeOriginAllocationPreview.Assess` is read-only and primarily evaluates mixed biological construction. The construction engine also has an **opt-in host-supplied initial creation gate** for **direct, mixed, and custom biological** Origins under the same biological method. These two entrypoints have different applicability and authority; do not describe the old preview's direct-method exemption as a rule for the enforced gate.

A `NativeOriginAllocationPolicy` has an explicit revision, rule ID, method ID, allowance, and positive `NativeOriginAllocationPrice` entries. It may add positive `NativeOriginAllocationInteractionPrice` entries for coexistence of explicitly specified effective mechanics.

- **One effective grant is priced once**, even if several sources offered it. Distinct effective grants that survive reconciliation are distinct charges unless an approved bundle prices them as one.
- A **multi-key bundle** means one complete inseparable package; the current ledger verifies that its effective members have shared contributor-lineage evidence. Bundles are not a shortcut to match unrelated donors.
- **Progression is committed at creation.** An ability that unlocks at level five is included in the starting configuration's valuation; it is not charged again solely because its unlock arrives.
- A **latent gene is unexpressed evidence**, not an effective grant, and has no creation-allocation charge or benefit. This is not a discretionary zero price for an unfamiliar active mechanic.
- **Interaction premiums are additive** to ordinary charges when all required effective keys are present. They must be explicitly authored, not inferred by a keyword or a class name.
- **Missing and unrecognized costs remain unresolved.** Pricing an unsupported effect does not turn its data into a valid executable grant.
- The current policy rejects nonpositive individual load entries, overlapping key coverage, and malformed interaction definitions. It does **not** implement arbitrary negative refunds for limitations.
- A scoped, authenticated DM creation-allocation waiver or allowance adjustment is distinct from changing the catalog's normative price schedule. No client claim can waive the gate itself.

These are accounting properties, **not** proof that the schedule reflects actual gameplay power. At this baseline no first-party numerical characteristic prices or starting allowance are approved. A local experimental ledger cannot be presented as a balanced published budget. See `NativeOriginAllocationPreview.cs` and `pillar2-biological-combine-gene-offers-20261121.md`.

### Valuation disposition record

Use this table for each effective characteristic or coherent bundle:

| Disposition | Meaning | Next action |
| --- | --- | --- |
| **Reviewed equivalent** | Same complete supported mechanic and configured values as a genuinely reviewed existing case | Reuse approved price **when one exists** |
| **Variant needs calibration** | Existing canonical identity but materially different numeric/conditional/spell/resource configuration | Document configuration selector and exact mechanical distinction; await valuation review/calibration |
| **New mechanic unpriced** | No existing equivalent, or missing mechanical interpretation | Author complete semantic definition; obtain integration review and later valuation |
| **Interaction unpriced** | Effective coexistence/combination changes capability beyond isolated effects | Identify actual interacting grants, prerequisites, and candidate interaction policy |
| **Manual DM adjudication** | Table can play a precisely ruled effect but the engine cannot validate or execute it | Record the scoped outcome and software limitation; no fictitious engine success |
| **Mechanically unresolved** | Required behavior, dependency, or source permission is unknown | Resolve source/authoring before calling the Species complete |

Do not record a numerical `0` for any of the last five categories merely to let a ledger pass. Do not price a fictional drawback as a transferable credit. If a limitation is inextricable from a benefit, compare that **complete combined package** against other complete packages.

## Complete-profile assessment before balance testing

After mechanical reconciliation, inspect the entire **starting** character:

1. Creature type, size, basic locomotion, all inherent abilities and authored exceptions.
2. Effective—not merely offered, proposed, or latent—characteristics and provenance.
3. Total active and future spell repertoire; selection and replacement rights; independent/shared resources; free versus slot casting.
4. Permanent, limited-duration, and conditional movement/defense; armor and terrain interaction.
5. Mutually reinforcing combinations (e.g., flight plus ranged offense; defense plus strong resource recovery), not just isolated traits.
6. Limitations that actually constrain use, and whether the environment makes them relevant.
7. Future-level milestones and other advancement branches committed to the starting Origin.
8. Whether the direct preset and a mechanically identical mixed/custom outcome receive consistent evaluation.
9. Whether any resulting effect relies on an unimplemented runtime, DM ruling, unclear spell source, or unresolved default rule.

Record **two independent conclusions**: mechanical completeness (authorized versus manual/unresolved) and balance confidence (unreviewed, analytically reviewed, or playtested with stated conditions). A complete and executable configuration can still be unbalanced. Conversely, a familiar-looking point total cannot make an incomplete effect mechanically sound.

## Later testing questions (not a numerical analysis here)

When the pricing and inherited-gene-combinations tracks integrate, later review should compare standard and converted Species under identical assumptions for starting level, advancement, environment, encounter mix, rest frequency, and party composition. Include: sustained versus burst performance, action economy, exploration bypass, defense/avoidance, resource exhaustion, shared-pool tradeoffs, progression breakpoints, synergy combinations, and campaign sensitivity.

Use reproducible encounter/character studies for screening, and playtests to assess actual play experience and unforeseen interactions. Neither automatically establishes universal balance. Log uncertainty and adverse test cases instead of editing price numbers to force a predetermined verdict.

## Scope beyond biology

Construct Assembly has component/material installation costs, resource production, power allocation, and operational applicability; these are not equivalent to a biological gene price. Magical creation and transformations have their own eligibility, duration, acquisition and replacement behavior. Authors may reuse `trait.low-light-vision` or another true canonical characteristic in any method that expressly grants it, but **must not transfer biological inheritance permissions, per-kind capacities, or the biological initial allowance** to that method.

For worked examples, see [Spellcasting, flight, Natural Armor, and unfamiliar Species](pillar2-homebrew-worked-conversions-20261121.md).