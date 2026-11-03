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

## First comparative cases — source-derived, not selected native Background rules

### Case A: High Elf + High Elf Upbringing + Sage Background

- **Pillar 1:** The conventional Elf base and High Elf Ancestry grant the already-selected Fey type, Low-Light Vision, Elf characteristics and High Elf innate spell advancement.
- **Native Upbringing:** `upbringing.elf.high` extends `upbringing.elf.general`. In their existing records, the pair offers learned Elvish, a cultural skill-training choice, traditional weapon proficiencies and another language.
- **Working Background source:** `background.sage` preserves a 5.2.1-shaped definition with Arcana, History, calligrapher's supplies, Magic Initiate (Wizard), ability-score choices, and starting-equipment choices. This definition is **not** authoritative `native/` Background content.
- **Design issue:** Learned magic or a feat from life experience can coexist with inherited High Elf spellcasting, but duplicate spell selection, eligibility, and grant timing should be addressed through the relevant general feat/spell rules. Do not strip an approved Ancestry spell because a source Background grants something similar.
- **Inventory issue:** The source equipment should become actual inventory grants if selected, not a permanently separate starting-equipment container.

### Case B: Orc + General Elf Upbringing + Soldier Background

- **Pillar 1:** Orc remains Humanoid with the approved Orc traits. A different cultural Upbringing must not turn Orc into Fey or automatically grant Elf Trance.
- **Native Upbringing:** `upbringing.elf.general` offers learned Elvish and a choice of Insight, Perception or Survival training, regardless of the character's inherited Species.
- **Working Background source:** `background.soldier` includes Athletics, Intimidation, a gaming-set tool, Savage Attacker as an Origin feat, ability-score choices and starting equipment.
- **Design issue:** Orc **Menacing Presence** grants contextual advantage on certain Intimidation checks, while Soldier's *source* training grants Intimidation proficiency. These are distinct mechanisms; their combined treatment must use the existing skill rules, without an assumed extra proficiency or replacement. Orc **Savage Attacks** and the *source* Savage Attacker feat have similar names but different definitions.
- **Eligibility issue:** Any mandatory social or biological link between Soldier and Orc would contradict the already-established separation of Species from Upbringing and Background.

### Case C: Goliath + Giant Foundling Upbringing + Soldier Background

- **Native Upbringing:** `upbringing.giant-foundling` already grants Intimidation and Survival training plus Giant and an additional language. It explicitly does not grant Goliath powers or the original source Background's feat.
- **Working Background source:** Soldier would separately offer Intimidation among its skill proficiencies.
- **Design issue:** This tests repeated acquired training from **two** Life Experiences sources. No duplication compensation or extra rank is yet approved. Do not automatically stack two full packages without reviewing the Life Experiences advancement model.
- **Historical distinction:** Giant Foundling was adapted into a native Upbringing, but its source Background identity does not imply it must remain a second independent native Background package.

### Case D: Construct without a conventional childhood

- **Pillar 2:** Its creators, design, materials and animating magic concern Origination/Heritage and its effective inherent profile.
- **Pillar 3:** Training after animation, implanted knowledge, occupational service or a compressed formative experience may establish acquired benefits. These possibilities should not be conflated with biological parentage or mandated as one universal childhood model.
- **Design issue:** Whether an Upbringing or Background can be omitted, replaced, or custom-defined is still unresolved; preserve room for alternatives.

## First Pillar 3 decision order (proposed, not approved)

1. Establish **what a Life Experiences selection represents** and how independently chosen Upbringing and Background interact. This is already conceptually two related parts, not two unrelated origins.
2. Compare the **grants and narrative roles** across the native Upbringings and four source Backgrounds before reallocating any bonus.
3. Address **overlap and duplicate learned grants**, using the existing competency/skill model where appropriate.
4. Determine the roles of ability-score allocation, Origin feats, and equipment **without** blindly reproducing the 5.2.1 Background bundle.
5. Stress-test unconventional or rapid formative experiences, then expand the native Background catalog only as approved.

The recommendation is to begin with Cases A and B, retaining Cases C and D as targeted stress tests. This work can proceed while Pillar 2 inheritance budgets and player feedback remain open. It must not change approved Pillar 1 mechanics as a side effect.
