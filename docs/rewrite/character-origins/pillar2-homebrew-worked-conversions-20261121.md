# Worked Homebrew Species Conversions

**Status:** Illustrative conversion exercises; **not** new selectable Species, approved traits, approved prices, or executable native JSON.  
**Evidence baseline:** `9f50ab70a37be69a6748603d210fa9406ba7ea59`  
**Instructions:** First read the [conversion procedure](pillar2-homebrew-species-conversion-guide-20261121.md) and [comparison reference](pillar2-homebrew-characteristic-comparison-20261121.md).

These examples show **what a DM must specify** and **which parts can map to real canonical mechanics**. They do not authorize custom creation, bypass acquisition restrictions, or claim to be ready-to-import schemas. All invented Species names, optional mechanics, level choices, and numeric *mechanical effects* below are **hypothetical teaching material**, not balance approval. No point costs are assigned.

## Example A — Kobold-like innate spellcasting

**Source request (Kovac's example):** A Species knows a cantrip similar to Fire Bolt and can cast Chromatic Orb once per Long Rest. The source statement alone leaves the casting ability, level of acquisition, and other resource rules unspecified.

### Resolve the missing decisions

For this **illustrative variant**, the table chooses:

| Property | Fire Bolt-like cantrip | Chromatic Orb access |
| --- | --- | --- |
| Underlying spell | Exactly the canonical `fire-bolt` spell **if** intended effect matches; otherwise new authored effect | `chromatic-orb`, subject to verifying the available spell definition and rules edition |
| Acquisition | Character level 1 | **Hypothetically** character level 3; choose level 1 instead only if source/table says so |
| Uses | At will | One free casting per Long Rest |
| Spellcasting ability | Charisma, selected/established from an authorized source-scoped casting-ability choice | Same source-scoped Charisma; **not** borrowed from an unrelated Species donor |
| Spellcasting ability scope | Spell attack and any applicable spell DC follow actual spell rules | Spell attack/DC and spell effects follow actual spell rules |
| Spell-slot casting | Cantrip at will; no spell slot needed | **Not granted** by this illustrative Species; slot use requires a separately specified permission |
| Resource ownership | No finite free-use pool | One independent pool tied to its effective spell grant |
| Recharge | Not applicable | Long Rest restores free use |
| Progression | Follow the underlying cantrip's applicable spell scaling; do not create multiple Species charges for damage scaling | Remains once per Long Rest after unlock unless explicitly modified |
| Dependencies | Verifiable spell identity and selected source-bound Charisma | Verifiable spell identity, source-bound Charisma, valid unlock level, functioning resource tracking |

**Canonical reuse:** The repository contains `trait.fire-bolt`: at character level 1 it grants Fire Bolt at will and binds `spellcastingAbility` from a manifested intrinsic scoped to the actual source. Reuse this **only** if "similar to Fire Bolt" truly means the same mechanical spell. An alternate range, effect, damage type, or scaling requires review.

**New or additional authoring:** The baseline biological gene catalog does **not** contain a canonical Chromatic Orb Species gene. The table needs a properly reviewed innate-spell grant or new canonical trait that references an actual valid spell, with exact `level` (character unlock), `freeUses`, `recharge`, `slotCasting`, and source-scoped ability binding. `trait.longstrider` and `trait.darkness` demonstrate existing **related field patterns** for one free use per Long Rest, but both also explicitly allow slot casting; copying those without changing that permission would incorrectly expand this example. The actual spell semantics, casting components, and slot execution are distinct spell-system responsibilities, not implicit benefits of a grant.

**Manifestation and valuation:** The final starting configuration contains both committed spell benefits (with Orb locked until the chosen level), plus all other Species foundation and features. It is not a complete playable Species merely because the spells were mapped. Price the full cantrip + later-unlocking Orb course as appropriate to the reviewed decomposition, avoiding double-charging a shared dependency. Until calibrated, both the unknown configured price and any missing semantic authoring remain visible.

**Software boundary:** The native innate-spell progression projector can read supported authored spells and locked future grants; it does not create new spell definitions, cast spells, or spend slots. The innate-spell ledger computes free-use state changes against authenticated profiles but the host must persist the ledger atomically. Do not treat a hypothetical JSON sketch or a DM's source text as already supported by the renderer or admission gate.

## Example B — Flight-capable Species

Three features may all be called "Flight" while having very different behavior.

| Variant | Starting and future behavior | Restrictions and resources | Authoring/valuation issue |
| --- | --- | --- | --- |
| **B1: Permanent winged flight** (hypothetical) | Fly Speed 30 feet at level 1 while wings function; no fixed duration | No use pool, no activation action; the source must define armor, equipment, and conditions | Author a verified perpetual flying mode; distinguish from hovering, flight spell effects, and nonfunctional wings |
| **B2: Armor-restricted permanent flight** (hypothetical) | Fly Speed 30 feet at level 1 | Cannot fly while wearing medium or heavy armor; always-on when restriction is satisfied | Same speed as B1 but a materially different operational condition; ensure it can actually be checked |
| **B3: Temporary spectral flight** (**existing native trait**, only from eligible acquisition) | Native `trait.spectral-flight`: at character level 5, Bonus Action grants flight speed equal to current walking speed and hover for 1 minute | One use per Long Rest; explicitly independent of Breath Weapon | Real authored definition, but its source eligibility, runtime application, and valuation must still be checked |

A fourth homebrew ability might grant flight for one minute **twice** per Long Rest or only while unarmored, or progress from temporary to permanent flight. That is a new configured or authored case until the exact semantics are supported. Do not substitute B3 because its name sounds appropriate.

### B1 minimum mechanical record

- **Inherent characteristic:** proposed permanent fly-speed grant, 30 feet, beginning at character level 1.
- **Physiology:** functional wings (narrative/anatomical evidence, not an additional bonus by itself).
- **Activation:** passive; **duration:** persistent while applicable; **recharge:** none.
- **Armor:** explicitly unrestricted for B1; for B2, the medium/heavy armor restriction belongs to the ability's conditions, not to an unrelated negative coupon.
- **Interaction:** no automatic hover, no ability to remain airborne when unable to move, and no advantage on attacks unless independently authored. Ordinary fall, terrain, visibility, and combat rules still apply.
- **Dependencies:** actual movement-mode interpretation, current-state applicability, and ability to adjudicate flight loss during play.
- **Price:** unresolved pending configured flight variants and complete-Species calibration.

### Why the flight price is not "all creation points"

An ability can be high-impact without being worth the **entire** creation allowance in every campaign. Flight's encounter impact changes with ranged attacks, environment, ceilings, enemy reach, action economy, armor choices, resource limits, and what the **rest of the Species** already offers. Valuing flight as the automatic full budget would preempt those comparisons and could make otherwise ordinary flight-capable Species impossible or force their other innate traits to be omitted. Instead, assess the permanent/temporary/restricted configuration **and the whole effective Species package**, flag uncertainties, and calibrate against standard and mixed cases later. This supplies **no numerical discount** or approved flight allowance.

## Example C — Natural Armor identity and an explicitly permitted improvement

The baseline 85-file canonical biological gene directory has **no** `trait.natural-armor` entry. The following hypothetical definitions must be authored/reviewed before they can be real canonical grants. Existing generic AC rules do not automatically create an inherited Natural Armor gene.

**Proposed candidate definition C1 (hypothetical):** "While not wearing armor, your base AC equals 13 + Dexterity modifier. You may use a shield under the ordinary applicable shield rules. Wearing armor uses the armor's normal AC calculation instead; do not add these base-AC formulas together."

| Incoming Species feature | Comparison | Conversion |
| --- | --- | --- |
| Species A: exactly C1 | Same formula, armor rule, shield rule, applicability | Reference the one new canonical C1 identity after it is authored |
| Species B: exactly C1 under a different feature name | Mechanically identical despite name | Reuse C1; keep B's distinct source occurrence/provenance |
| Species C: base AC 13 fixed, no Dexterity addition | Different formula | **Not** the same configuration; needs separate authoring/valuation |
| Species D: AC 13 + Dexterity but cannot use shields | Different restriction | Needs a distinct configured case or definition, not a false exact match |

### Two biological contributors and possible stronger expression

1. Two eligible donors might each offer an instance of the **same** C1 gene. Under the current biological model they remain distinct source offers and can be inherited separately within per-kind limits.
2. Selecting one effective Natural Armor expression ordinarily produces **one** C1 grant, not AC 26, two AC formulas, or a free bonus. Latent inherited instances do not protect the character or trigger synergies.
3. If the table **explicitly permits** a new combined result, it could author a hypothetical **C2**: "While unarmored, base AC equals 14 + Dexterity modifier, with the same shield and armor rules as C1." The exact input-gene eligibility, expression procedure, replacement of C1, source provenance, and balance must all be specified. C2 is **not** automatically derived from having two C1 copies.
4. Since the original documentation baseline, the integration branch has added an explicit inherited-gene input mode to the universal `origin-synergy` evaluator. A table could authorize C2 only after authoring C1 and C2 as supported canonical mechanics, supplying a verified rule with `inputEvidence: "inherited-gene"`, and selecting the exact two authenticated input occurrences. The resulting fixed C2 trait would replace consumed C1 expressions, not stack their armor formulas. C1/C2 remain hypothetical; a general AC upgrade formula, configurable combination output, and price schedule have not been approved.

**Important:** The flat AC 17 example in *An Elf and an Orc Had a Little Baby* is **not** an approved Dorks & Dice AC formula, cost, or stacking rule. No approved cost is established for C1 or C2. AC valuation must consider Dexterity interaction, armor/shield compatibility, creature size, other defensive traits, and higher-level equipment availability.

## Example D — A complete unfamiliar homebrew Species intake

**Glasstide Kin** is an **invented illustration** to show a full conversion. It is not a published or authorized selectable Species.

### Hypothetical source entry

> Glasstide Kin are Medium Humanoids with a 30-foot Walking Speed. They breathe air and water, swim at 15 feet, and perceive Dim Light as Bright Light but cannot see through Darkness. At character level 1 they gain *Pressure Echo*: while submerged in a continuous body of water, they can use an Action once per Long Rest to learn the current locations of creatures within 20 feet sharing that water, except creatures wholly behind a solid barrier. This is instantaneous, is not sight, does not change cover, does not grant advantage, and does not automatically cancel Hidden or other conditions. They gain no additional Species progression or innate spellcasting.

This specification intentionally states more than a typical flavor paragraph. The behavior of Pressure Echo against water barriers, stealth, and invisible creatures is **table-authored**: it detects the location at the moment of use even if the creature is invisible or hiding, provided the target shares connected water and is not blocked by a solid barrier; it does not reveal future movement, identity, appearance, or line of sight. A DM still adjudicates ordinary hidden/targeting rules after that instant.

### Deconstructed characteristic ledger

| Inherent characteristic | Exact proposed configuration | Existing reusable definition? | Disposition |
| --- | --- | --- | --- |
| Creature type | Humanoid | Native intrinsic slot `creatureType` | Reuse if source and selection are authorized |
| Size | Medium | Native intrinsic slot `size` | Reuse if source and selection are authorized |
| Walking Speed | 30 feet | Native intrinsic slot `walkingSpeedFeet` | Reuse if source and selection are authorized |
| Amphibious | Breathe air and water indefinitely; **no** swim speed implied | `trait.amphibious` | Exact reuse |
| Swim Speed | 15 feet; **no** water-breathing implied | `trait.coldwater-swimmer` | Exact reuse; separate from Amphibious |
| Low-Light Vision | Dim Light as Bright Light, no sight through Darkness | `trait.low-light-vision` | Exact reuse |
| Pressure Echo | Action; submerged in connected water; locations of nearby creatures within 20 feet; instant; once per Long Rest | **No matching canonical gene established** | New provisional trait; manual DM operation until validated/executable |

**Unfamiliar mechanic authoring note (not executable JSON):**

| Property | Hypothetical Pressure Echo requirement |
| --- | --- |
| Provisional identity | `trait.example.pressure-echo` (not a real catalog ID) |
| Trigger and action | User chooses to activate using an Action, only while submerged in connected water |
| Effect and area | Reports locations, not sight, of creatures in a 20-foot radius sharing the same continuous water; exclude targets fully blocked by solid barrier |
| Uses and resource | One use per Long Rest, **independent** of any spell or other innate trait; attached to the Pressure Echo effective grant |
| Required level | Character level 1; no further progression in this example |
| Limitations | Instantaneous detection; no damage, saves, concealment removal, targeting permission, or ongoing sensory mode |
| Open implementation question | Generalized water connectivity, obstacle, and hidden-target interpretation may not be executable by the current effect projectors |
| Price | **Unpriced and unresolved**, not zero |
| Authority | Author/editor defines the rule; trusted content publication or authenticated scoped DM ruling needed for software entitlement |

### Reconstruct and disposition

The **proposed level-one Species** comprises all seven ledger rows (three foundation intrinsics, three canonical traits, and one novel trait). It has no Background, Upbringing, starting equipment, bonus languages, class features, or later unlocks in its *Species* package. These are handled independently as appropriate.

At the tabletop a DM can choose to run the specified Pressure Echo effect manually. For a software-authenticated Origin, however, the new trait still needs a reviewed canonical definition, a trusted acquisition/selection path, an interpreter or explicit supported adapter, authorization, and a completed biological creation price schedule if the host requires it. The example is **mechanically specified for human adjudication but not proven mechanically executable or balanced**. Missing authoring means the engine should report unresolved rather than silently removing Pressure Echo and approving a weaker Species.

The three existing trait identities should not be redefined merely to rename them for Glasstide Kin. Their actual occurrence in a new source remains distinguishable from existing Species occurrence records. If the DM changes water breathing or Swim Speed materially, revisit identity and valuation.

### Whole-profile check

- The foundation has one creature type, one size, and one walking speed; Amphibious and Swim Speed are not conflated.
- No direct and mixed method is presumed; a custom Species source and its eligibility must be properly authorized.
- Pressure Echo's action, area, target filters, use limit, recharge, level, and limitations are specified, but automated operation is unresolved.
- No new spellcasting ability or fabricated spell slots are introduced.
- There is no automatic deduction or negative-point credit for the underwater requirement.
- At character creation all seven effective entries require valuation coverage or a documented unresolved/DM waiver outcome. A biological latent gene, if present in a different scenario, would be kept separate.
- The result is a **conversion proposal** pending source authoring, authorization, configured pricing, and later testing.

## A reusable final example checklist

For each new Species verify the same order: source inventory → inherent/cultural separation → canonical identity versus new mechanic → all configured choices and progression → resources and dependencies → acquisition permissions → complete effective Origin → explicit unresolved items → full-profile valuation → later testing. The accompanying [capability gap review](pillar2-homebrew-capabilities-and-review-20261121.md) identifies which steps currently require manual work.