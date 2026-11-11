# Pillar 2 — shared structured mechanics evaluation study

**Date:** 2026-11-11  
**Status:** Experimental, read-only evaluation. **Not** trusted character state, an approved native runtime API, an earned feature, a complete combat evaluator, or a mixed-Heritage balance rule.  
**Tool:** `tools/character_origins_mechanics_preview.py`  
**Tests:** `tests/test_character_origins_pillar2_mechanics_preview.py`

## Purpose

Prove that the **same evaluated mechanical definition** can receive its parameters from a directly selected standard Species/Ancestry, a mixed-contributor eligible selection, or an independently authored homebrew source. The evaluator operates on the **configured ability record**, not the Species name, Ancestry family, or source-local trait ID.

The pipeline currently consists of:

1. Source-preserving Species/Ancestry inventory.
2. One authored compound trait with explicit symbolic binding contracts.
3. Candidate parameter contributions with contributor provenance.
4. Per-trait linked or independent parameter selection.
5. A non-authoritative **configured trait instance** with the chosen parameter values.
6. **This slice:** a reusable, strict evaluator deriving numerical facts from structured ability fields.

The prototype is deliberately limited to structured area-effect damage abilities such as the ordinary Dragonborn Breath Weapon. Unknown fields and unsupported expressions are rejected rather than interpreted from narrative prose.

## Current evaluation coverage

For the standard `species.dragonborn#trait:breath-weapon`, the tool reads:

| Field | Current behavior |
| --- | --- |
| `activation` | Preserves the Attack action, one-attack replacement, and on-turn timing. **Does not** decide if the action is available. |
| `area` | Preserves a complete contributor-supplied line or cone, including dimensions. **Does not** enumerate targets or measure map intersection. |
| `savingThrow` | Resolves `8 + proficiency-bonus + constitution-modifier` to a numeric DC from an explicitly supplied example character context. Retains Dexterity and `half-damage` on success. **Does not** roll a save or apply the outcome. |
| `damage` | Chooses the latest eligible authored level-threshold dice expression (1d10 at 1, 2d10 at 5, 3d10 at 11, 4d10 at 17); reports minimum and maximum raw dice rolls. **Does not** roll or apply damage. |
| `uses` | Resolves an authored fixed integer or `proficiency-bonus` to the preview's maximum-use count, retaining its Long-Rest or other recharge text. **Does not** consume uses or enforce recovery. |

The example caller provides `characterLevel`, `proficiencyBonus`, and `abilityModifiers` independently. This pass **does not verify** that PB and ability modifiers correspond to an authorized character or to a particular class progression. Those values must eventually come from trusted, shared Rules Core character projection, not from an unverified client.

A constrained additive expression parser accepts numeric constants, `proficiency-bonus`, and named ability modifiers. It does not use arbitrary language evaluation and rejects unrecognized functions, unknown symbols, or arithmetic operations not represented in the current native formula.

The evaluator also rejects malformed die expressions, duplicate/unsorted level thresholds, unsupported area shapes, unrecognized top-level mechanic fields, absent expected fields, invalid input types, and invalid character context values.

## Standard versus mixed equivalence

The native Red Dragonborn preset supplies Fire and a 30 × 5-foot line. The owner-approved independent ordinary Breath Weapon parameter policy also permits a Red/Bronze pair to configure Fire and that same line, or Fire and a Bronze 15-foot cone, or Lightning with either area.

Under the **same character-level, PB, and Constitution modifier context**:

- Standard Red and mixed Red/Bronze configured as Fire + Line produce the **same structured mechanical preview**.
- All four allowed Red/Bronze parameter pairs use the same activation, saving throw formula, resource limit, and level-based damage dice; their damage type and area differ according to the selection.
- At levels 1, 5, 11, and 17 (and adjacent boundaries), the dice expression comes from the existing base Dragonborn progression, not from special code for each ancestry.
- Parameter provenance remains distinct even when the numerical mechanics are identical. Provenance never becomes a second copy of the ability or extra charges.

An independently authored synthetic homebrew source can supply another damage type, action cost, recharge and damage formula and still pass through the same structured evaluator. This demonstrates **shared code path**, not automatic semantic recognition for every homebrew effect.

## Current limitations and correctness boundary

This is a **numerical and structured-field evaluation**, not a complete character or combat engine. The following remain unexecuted: validating that the character may take the action, map targeting, resolving saving throws, rolling damage, applying resistances/vulnerabilities and other consequences, and consuming/recharging resources.

The original Dragonborn prose description still mentions dragon ancestry; it is retained for authoring provenance but not parsed as a source of executable effects or rewritten to reflect hypothetical mixed origins. Final characteristic definitions need a general description/template approach.

This proof also does not yet:

- establish authoritative `mechanicId` implementation contracts in Rules Core C#;
- grant the trait or determine which candidate traits actually manifest;
- address biological versus nonbiological contributors or recursively mixed contributors;
- decide stacking, replacement, cost, and balance rules;
- interpret other Species' prose-only or partially structured features;
- model conditions and target-specific outcomes;
- fully reconstruct all mechanics of any standard Species.

The next design/engineering milestone is to replace the experimental field-specific evaluator with **shared, typed Rules Core mechanical operations**, paired with trusted Origin manifestation and parameter authorization. A Standard Species must not receive a hidden implementation advantage over a custom Heritage or homebrew. Only after semantic completeness is demonstrated should tests claim fully evaluated standard-character reconstruction.

## Commands

```bash
python3 tools/character_origins_mechanics_preview.py \
  --contributors ancestry.dragonborn.red ancestry.dragonborn.bronze \
  --damage-type fire \
  --area '{"shape":"cone","lengthFeet":15}' \
  --level 5 --pb 3 --constitution-modifier 2

python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```
