# Recurring 5.x baseline reconciliation questions

**Status: five general policies approved by the Owner (2026-10-18); family-specific unresolved decisions remain open.**

The seed coverage manifest identifies **1,422** entries with multiple candidates after the 19 identical Item Type representations were collapsed. The candidates are not automatically distinct native rules or independently approved gameplay mechanics. Most conflicts recur across large rule families.

## Highest-leverage families

| Family | Multiple-candidate native IDs | Repeated question |
| --- | ---: | --- |
| Magic and other items | 396 | When a revision changes an item's activation cost, charges, permitted effects, or attached spell, which parts form the single native item? |
| Spells | 317 | When effects, numerical strength, targets, casting procedures, or duration change, which become the single native spell? |
| Monsters | 285 | Which numerical stat block and which non-duplicated actions, traits and spells define a single native creature? |
| Class features | 121 | How are changed feature text, effects, resource costs and level acquisition reconciled without duplicate grants? |
| Mundane equipment | 88 | How do baseline equipment statistics, property references, proficiency rules, and optional mastery coexist? |
| Other categories | 215 | Includes subclass features, classes, conditions, language classifications, senses and actions. |
| **Total** | **1,422** | |

First three families: **998 / 1,422 (~70%)**. First five: **1,207 / 1,422 (~85%)**. These are *candidate counts*, not confirmed mechanical conflicts.

## General handling rules already supported by the design documents

**D1 — Native authority over imported defaults.** Existing Dorks & Dice decisions take precedence. Examples: SRD 5.1-style tiered Exhaustion; approved Grapple procedure and scoped Grappled effects; rolled Bonus Action / maximum Action potion drinking; eliminated prepared-spells requirement; independent movement; ordinary, Touch and Flat-Footed AC; critical escalation and individual-complexity design.

**D2 — One mechanic, not two edition modes.** Existing decisions govern their actual stated scope only. A rule whose core effects have not been reconciled remains unresolved; using one candidate as an implementation shortcut is prohibited.

**D3 — Share consequences, preserve distinct grants and procedures.** Trip, Topple, and Shove can all impose Prone without becoming one action. Weapon Mastery can extend eligible ordinary attacks without replacing basic attacks. Class features, feats and normal actions can share effect-resolution vocabulary without being duplicate entitlements.

**D4 — Prefer simplicity only where it does not erase approved mechanics.** Reformatting, terminology, reference syntax and identical numerical effects may be normalized. Real changes to odds, action cost, prerequisite, targeting, resource use or consequences can not be dismissed as cosmetic. Do not double-count the same defender impairment as both an AC penalty and a semantically equivalent attack Advantage grant.

## Five approved general reconciliation rules

**Approved by the Owner on 2026-10-18** and incorporated in `docs/rewrite/native-rules-design-specification.md`:

1. **Merge equivalent mechanics into a single native definition.** Ignore superficial differences only after confirming unchanged mechanical consequences.
2. **Retain unique compatible content.** An option appearing only in one imported baseline is eligible for the unified catalog when it does not conflict with Dorks & Dice design; retain prerequisites, grants and restrictions.
3. **Combine genuinely compatible additions.** Keep distinct additional capabilities without duplicating consequences or lifting eligibility, actions, charges and resource costs. Apply consistently to monsters, spells, items, classes and other categories.
4. **Resolve conflicting numerical values by category-specific policies.** Do not average, add or automatically prefer newer/older values. Establish the applicable field- and family-level rule with the Owner where the existing design is silent.
5. **Escalate fundamentally different procedures.** A mechanical redesign is not a text merge. Apply already-approved design rulings first; ask the Owner only about remaining genuine conflicts, grouped by reusable rule family.

These rules **authorize compatible reconciliation**, not arbitrary best-of-both combinations. They also do not declare every imported candidate automatically compatible. For mechanical equivalence or additive compatibility, confirm the complete mechanic including restrictions and interactions. Differences that remain unclear stay unresolved.

## Additional implementation guardrail: shared mechanical ownership

Centralize common effects (attack, damage, conditions, resource accounting, equipment properties, and progression) instead of copying their entire implementation into every spell, item, monster, and class. Preserve separately earned features and their acquisition levels. This is existing architectural direction, not a sixth newly approved gameplay ruling.

## Concrete recurring cases observed

- **Spells — magnitude:** Cure Wounds' healing dice differ. A consistent spell-strength policy might apply to many revisions, but no general preference is yet approved.
- **Spells — redesigned behavior:** Conjure Animals changes from summoning creatures to a spectral pack effect. A universal numeric tie-breaker can not reconcile this; the native procedure must be chosen.
- **Spells — resolution method:** Counterspell changes how interruption is determined. This is a shared spell-interaction decision, not a wording issue.
- **Monsters — quantitative and capability changes:** Aboleth HP and actions differ. Adult Black Dragon has changes to ability scores, defenses, spellcasting and actions. Adding all abilities from both versions without review might double-grant or distort the creature.
- **Magic items — activations:** Wand of Magic Missiles has different expenditure restrictions; Potion of Healing has an action-cost difference already constrained by the native potion rule.
- **Equipment — optional features:** Longsword's newer property includes Weapon Mastery; existing design treats mastery as an additional eligibility-gated interaction, not a replacement for ordinary attacks.
- **Class structure — acquisition:** Fighter and Wizard progression representations differ; the native character progression and spell preparation decisions constrain what can be adopted.
- **Languages — classification:** 'Exotic' versus 'Rare' is a taxonomy decision; it should be decided once for the language catalog, rather than per language.

## Efficient review order

1. Finish safe semantic formatting/identity normalization and reuse D1–D4. Do not introduce new mechanics through this step.
2. Apply approved Rules 2 and 3 across all eligible categories with automated tests checking scope, prerequisites and no duplicate effects.
3. Decide one **monster baseline/capability reconciliation policy**, then flag numerical and redesigned-action exceptions.
4. Decide **spell categories** separately for quantitative revisions, altered resolution procedures and fundamental redesigns. Do not force every spell into one indiscriminate merge rule.
5. Reconcile **item activation, charges and effect grants** under shared action/resource rules; reconcile **equipment/mastery** as optional eligible properties.
6. Reconcile **class progression and feature entitlements** as a dependency-aware group rather than independently from each class feature's text.
7. Present remaining genuinely distinct decisions to the Owner as one mechanical question per recurring family wherever possible.

**Approved:** compatible source-unique entries and genuinely compatible additions are retained. **Not approved:** silently merging incompatible mechanics, choosing a numerical winner without a category-specific policy, choosing one whole source edition by default, or granting optional features to ineligible characters. The Owner makes missing gameplay decisions; the design specification resolves previously decided questions. The validation audit remains advisory until normalization is complete.
