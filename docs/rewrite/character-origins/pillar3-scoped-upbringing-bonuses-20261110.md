# Pillar 3 — Scoped Upbringing check benefits

**Date:** 2026-11-10  
**Status:** **Approved native JSON semantics for Origin check bonuses**, not yet implemented in the common check evaluator. Competing non-Origin multipliers remain for general Skill/check reconciliation.

## Authored data and historical evidence

| Effective Upbringing | Existing benefit | Canonical Skill | Authored modifier | Eligible activity |
| --- | --- | --- | --- | --- |
| General Dwarf | `stonecunning` | `skill.history`, Intelligence | `proficiencyBonusMultiplier: 2` | History of stonework and its origins |
| Rock Gnome | `artificers-lore` | `skill.history`, Intelligence | `proficiencyBonusMultiplier: 2` | History of magical, alchemical, or technological objects |
| Drow Elf | `subterranean-survival` | `skill.survival` | Flat `bonus: 2`, `stacksWithTraining: true` | Underground navigation, tracking, foraging, subterranean hazards |
| Wood Elf | `woodland-fieldcraft` | `skill.survival` | Flat `bonus: 2`, `stacksWithTraining: true` | Forest navigation, tracking, foraging, natural woodland hazards |

The preserved **2014 SRD source** in `data/reference/srd/structured/races.json` supplies useful distinction, not independent authority to alter native rules:

- Historical Stonecunning explicitly treats the character as proficient on a qualifying History check and applies **twice the ordinary Proficiency Bonus instead of** the standard one.
- Historical Artificer's Lore permits **twice the Proficiency Bonus instead of any proficiency bonus normally applied**, without wording that awards general History proficiency.
- Native Drow/Wood Elf effects are already specifically authored flat `+2` effects. Their task scopes, rather than merely being in a forest or underground, determine applicability.

## Approved clarification: self-contained scoped History bonus

**Approved native interpretation for the current two scoped History benefits:** When its task qualifies, the feature supplies a **2 × normal character Proficiency Bonus check contribution** even if the character is not generally proficient in History. This contribution **replaces**, rather than adds to, an ordinary History proficiency or Expertise contribution for that check.

The selected benefit thus does not require the character to acquire an otherwise unrelated History proficiency merely to function. Both features retain their own narrow scopes; neither automatically grants a rank, Skill training across all History checks, or a new ranked Competency.

**Stonecunning-specific nuance:** The historical source additionally considers the user proficient **for that qualifying History check**. It does not make `skill.history` generally proficient for unrelated checks, character creation options, or broad prerequisites. **Artificer's Lore** is a check contribution on its scope and has no separately established general History proficiency.

**Illustrative outcomes, with PB = +3 (not a fixed character-level assumption):**

| Relevant check | Without scoped benefit | With approved scoped benefit |
| --- | ---: | ---: |
| Untrained History on qualifying stonework | +0 PB contribution | +6 PB contribution from Stonecunning |
| History proficient on qualifying stonework | +3 | +6 **total** PB contribution, not +9 |
| History Expertise-like ×2 on qualifying stonework | +6 | +6 **total** PB contribution, not +12 |
| Untrained History on qualifying magical object | +0 | +6 PB contribution from Artificer's Lore |
| Ordinary History unrelated to approved scope | Character's normal contribution | Unchanged |

Only the Proficiency Bonus part is illustrated. The governing Intelligence modifier, independently authored bonuses, and check-local conditions remain additional under their own rules.

**Still needs a general Skill/check ruling:** If a different feature also substitutes or multiplies the Proficiency Bonus on the same check, should the effective result be the **maximum applicable replacement**, an explicit priority, or another reviewed composition rule? The two original `×2` features are not normally inherited together because a character selects one effective Upbringing. **Do not add two doubled Proficiency Bonuses** merely because two contextual conditions are true.

## Existing fixed Survival bonuses

Drow `subterranean-survival` and Wood Elf `woodland-fieldcraft` explicitly provide **flat +2** on their qualified activities. Their authored `stacksWithTraining: true` means the fixed scoped modifier is **in addition to** whatever ordinary Skill training is valid for the check; it is not a substitute proficiency calculation. The corresponding Elven General Upbringing may choose Survival as its separate learned Skill, and the selected child still applies its own scoped +2 only to matching activities.

The same separation applies if the ordinary Skill uses a context-selected Ability. The modifier does not force Wisdom or automatically train distinct child Skills. Drow and Wood Elf Upbringings are alternative effective packages, not two child packages a character gains at once.

**Examples:** A Drow Elf character with Survival training can apply +2 to an eligible underground tracking check; the +2 does not apply to every check simply because the check occurs underground. A Wood Elf character can apply +2 to eligible forest foraging, not to every Survival check anywhere in a forest. If the character lacks Survival training, the scoped +2 still applies to an eligible task.

## Character Origins implications

1. **Keep these four benefits separate from Competency grant allocation**; they add no `Ranks` or `Proficient` to a native Competency.
2. **Do not convert the two History multipliers to fixed rank grants or a broad History proficiency.** A contextual PB replacement is materially different from persistent training.
3. **Do not double-count normal Skill proficiency and scoped double PB.** Resolve the actual check contribution before Character Sheet display, and show applicable provenance.
4. **Do not treat one Upbringing as two:** General Elf plus one selected Drow/Wood child is valid inheritance; Drow and Wood child benefits are not cumulative in ordinary character creation.
5. **Revisit balance by full packages:** Rock Gnome has multiple other features (including Tinkering, Prestidigitation, Clockwork Tinker and Magical Tinkering), so its contextual History benefit must not be judged alone. General Dwarf pairs Stonecunning with chosen crafting tradition and language.

## Pending decisions

- **Resolved and authored:** both features replace normal History proficiency contributions with 2 × PB even if the character is untrained. Stonecunning considers the character proficient only for its qualifying check; Artificer's Lore does not award Skill proficiency.
- Global resolution if two independent Expertise/PB multiplier replacements both apply to one check. The source wording rules out automatic addition; exact general composition remains for the Skill/check subsystem.
- **Still pending in code:** the common check evaluator must honor `proficiencyBonusApplication: replace-normal-proficiency-contribution` and `appliesWithoutSkillProficiency: true` from these Origin benefits, without granting general History training or adding normal proficiency twice. No C# implementation is part of this content-only pass.
