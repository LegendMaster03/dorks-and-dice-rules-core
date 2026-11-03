# Goliath — first-pass source reconciliation research

**Status:** source review and research plan only. Goliath remains an unresolved Species in `working/species/base/goliath.json`. No native Goliath Species, Subspecies, Upbringing, or feat has been approved by this document.

**Goal:** develop a coherent first-pass Goliath Species for playtesting by reconciling historical material, the 2024 SRD baseline and giant-focused material from *Bigby Presents: Glory of the Giants* (2023). Approval means playable for testing, not final or balance-complete.

## Required source set

| Source | Scope to research | Disposition |
| --- | --- | --- |
| 3.5e *Races of Stone* (and relevant 3.x giant rules) | Original Goliath physical and environmental traits, Powerful Build, mountain movement and adaptation, size interactions | Historical Species and mechanical reference; do not copy edition-specific implementation uncritically |
| 5e Goliath versions, including *Elemental Evil Player's Companion* / *Volo's Guide to Monsters* and *Mordenkainen Presents: Monsters of the Multiverse* | Earlier Goliath traits and revisions; Stone's Endurance, Powerful Build, Athletics/environmental adaptation and resources | Edition comparison, verify individual books before selecting rules |
| **5.5e SRD 5.2.1** | 35-foot movement, Medium size, Powerful Build, level-5 Large Form and six Giant Ancestry choices | Imported baseline: `working/species/base/goliath.json` (pending native reconciliation) |
| **Bigby Presents: Glory of the Giants** (2023) | Giantkind lore, six major giant types, the Ordning, giant societies, rune magic, Giant Foundling and Rune Carver backgrounds, giant-themed feats, Path of the Giant and relevant giant monsters | Additional mandatory comparison and design inspiration; **not** an automatic grant of book feats, background abilities or class features to Goliaths |

## Bigby's research coverage

1. **Six giant traditions:** Cloud, Fire, Frost, Hill, Stone and Storm. Compare their distinct supernatural, elemental, physiological and environmental signatures with the six SRD 5.2.1 Goliath Giant Ancestry choices. Verify whether their distinctions are best modeled as selectable Subspecies (potentially with shared nonselectable ancestry intermediates) or as a Species choice feature. Do **not** presume the Dragonborn hierarchy must be copied.
2. **Species versus culture:** Examine the Ordning, social structures, languages, religion, arts and regional environments for learned Upbringing candidates. Giant ancestry must not impose a caste, faith, personality, profession or cultural allegiance on the player.
3. **Rune traditions:** Compare rune lore and crafting with the existing Skills/Competencies and future Upbringing work. Consider runecraft training independently of biological giant descent.
4. **Giant Foundling and Rune Carver:** Compare their background material as possible Background/Upbringing research, not intrinsically inherited Species abilities. Preserve the distinction between a character raised among giants and one descended from giants.
5. **Giant-themed feats:** Examine *Strike of the Giants*, the six ancestry-associated follow-up feats and *Rune Shaper* as optional or future feat research. Do not fold their progression or prerequisites into the base Species without a separate decision.
6. **Class and monster material:** Path of the Giant Barbarian, giant stat blocks and other advanced options can inform balance and design vocabulary. They are not automatically level-1 Species features.
7. **Physical mechanics:** Reconcile Powerful Build, size and carry/lift rules, movement, mountainous adaptation, damage prevention, Large Form, activation economy, resource uses and conditions against the native mechanics rather than importing older-edition wording verbatim.

## Open design questions (not proposals approved for implementation)

- Should each of the six giant ancestries become a playable Goliath Subspecies, or remain one choice within the Goliath Species? Confirm selection and inheritance consequences before creating native children.
- Which abilities are genuinely innate and which should remain independently selectable learned traditions or later feats?
- Does inherited Large Form interact cleanly with 3.5e Powerful Build and the native spatial/size system? Do not grant permanent Large size merely from giant descent.
- What can be retained from original Goliath mountain adaptation without making all Goliaths share one regional upbringing?
- How do six giant options compare with the approved Chromatic/Metallic Dragonborn Subspecies in versatility, encounter power and exploration utility? The objective is a first playable baseline, not final balancing.

## Verified references / further reading

- Publisher source table of contents: https://www.dndbeyond.com/sources/dnd/gotg
- Publisher's guide to Bigby's themes and the Ordning: https://www.dndbeyond.com/posts/1506-what-is-bigby-presents-glory-of-the-giants-a-huge
- Publisher's explanation of giant societies and six major kinds: https://www.dndbeyond.com/posts/1534-giant-personalities-an-infographic-on-the
- Publisher's Rune Carver / Rune Shaper overview: https://www.dndbeyond.com/posts/1551-channel-ancient-power-with-the-rune-carver
- Publisher's Giant Foundling / Strike of the Giants overview: https://www.dndbeyond.com/posts/1531-first-look-giant-foundling-background-and-strike
- Publisher's 2024 Goliath ancestry overview: https://www.dndbeyond.com/posts/1783-the-10-species-in-the-2024-players-handbook
- Existing staged SRD baseline: `working/species/base/goliath.json`; original pinned text under `data/reference/srd/`.

This file is a research map, not an exhaustive extraction of Bigby's or an approval to promote any rule. Maintain source-specific findings in `working/` until reviewed.
