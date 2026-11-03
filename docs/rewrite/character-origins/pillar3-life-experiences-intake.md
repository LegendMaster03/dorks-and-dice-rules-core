# Character Origins — Pillar 3 Life Experiences: Initial Intake

**Status:** Initial scoping and source inventory only. **No new Pillar 3 mechanics are approved.**  
**Current branch:** `feature/character-origins-pillar2-heritage`  
**Implementation:** C# and consumer implementation remain deferred until after Pillar 3 or 4, subject to specification progress.

## Existing decisions and separation

Pillar 1 established **Upbringing** as independent of Species and Ancestry. A Species/Ancestry may suggest `defaultUpbringing`, but players may choose another. Cultural languages, social conventions, and learned training do not automatically follow biological identity.

The proposed Pillar 3 scope keeps **Upbringing and Background distinct but treats them as two related parts of Life Experiences**, rather than forcing them into separate, incompatible creation systems.

- **Upbringing** concerns formative environment, socialization, family/community influences, languages, and early training.
- **Background** concerns meaningful personal history, activities, vocation, experiences, and connections leading into adventuring.
- **Life Experiences** is the shared design responsibility for how those sources interact and how acquired benefits are chosen, without assuming each must always be one mandatory dropdown or an independent bonus pool.

These descriptions are boundaries for reconciliation, **not yet an approved mechanical allocation of every possible grant**. An individual experience may cross both categories.

## Verified current content (2026-11-03, merged PR #130 baseline)

- The authoritative native catalog contains **ten selected Upbringing files** under `data/rulesets/first-party-001/native/upbringings/`. They include General Elf, High Elf, Drow, Wood Elf, General Dwarf, General Gnome, Forest Gnome, Rock Gnome, General Goliath, and Giant Foundling.
- Their independent `parent` links (where present) are **Upbringing inheritance of defined benefits**, not genealogical or Species inheritance.
- `upbringing.giant-foundling` explicitly works for any Species, grants learned Giant language and training, and deliberately does not grant Goliath ancestry powers, size changes, or the source feat.
- The non-authoritative `working/backgrounds/` contains **Acolyte, Criminal, Sage and Soldier**. The first three inspected records preserve source-shaped SRD 5.1/5.2.1 material; their `working/` location does not make them playable native Backgrounds.
- The `working/rules/character-origins.json` document records 5.2.1 Background candidate structure: selected ability scores, Origin feat, skill and tool proficiency, and starting equipment. This is **reference evidence**, not a decision that all benefits will belong exclusively to Background under Dorks & Dice.
- Pillar 1 already established selected Species and Ancestry traits. Pillar 3 should not silently move them into learned backgrounds or provide duplicates without review.

## First concrete comparisons to reconcile

1. **Orc raised in an Elven environment, then trained as a Soldier.** Biological species and type remain Orc, potential inherited abilities stay in Pillar 2, language/tradition may come from Elf Upbringing, and martial employment belongs to Background. Resolve any overlapping skill training only when rules are agreed.
2. **Elf raised outside Elven culture, then became an Acolyte.** Fey creature type and Trance come from Elf Species; no automatic Elvish language from genetics; learned religious and social features are Life Experiences.
3. **Goliath with Giant Foundling Upbringing and a separate occupational Background.** Avoid granting the Goliath's native recommended Upbringing automatically as a second full benefit package. Avoid counting the same training twice without a deliberate rule.
4. **Two characters with identical Upbringing but different Backgrounds.** Test that formative culture does not determine vocation or background benefits.
5. **Construct or spontaneously manifested person with no childhood in the conventional sense.** Test whether Upbringing can be absent or replaced by another formative-experience profile; do not invent compulsory childhoods to satisfy a schema.

## Questions for the Pillar 3 reconciliation

- Which parts of a 5.1/5.2.1 Background become Upbringing, Background, other advancement, or merely narrative prompts?
- Is there one total Life Experiences benefit allowance, separate allowances, or predefined complete configurations? Do not decide budgets without comparative examples.
- How are repeated skill/tool/language grants, overlapping feats, and multiple periods of formative experience handled?
- Where should ability-score development reside when source editions assign it differently, given that Species ability-score adjustments were intentionally deferred?
- Are Background choices a fixed starting package, a customizable biography, or both?
- How do nonstandard childhoods, rapid maturation, constructed beings, and memory alteration fit without forcing biological assumptions?
- How do we keep starting equipment in the ordinary inventory rather than a permanently separate subsystem?

## Proposed next action

First reconcile one **conventional** Life Experiences pair, such as General Elf Upbringing plus Sage Background, then one **cross-cultural** pair, such as Orc with General Elf Upbringing plus Soldier Background. Use the already-approved native Upbringings as the baseline, and source-shaped working Backgrounds only as historical evidence. Record resulting decisions before promoting any Background to `native/`.

Work on this pillar is **conceptually parallel** to Pillar 2, provided it does not settle inherent trait inheritance, creature-type targeting, or Species balancing by implication. Dependencies should be noted explicitly rather than silently filled in.
