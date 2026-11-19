# Pillar 2 — Ruby Dragonborn: source corpus and edition comparison

**Date:** 2026-11-11
**Status:** **Source research only, not a Ruby feature approval or native implementation.** This audit precedes the independent Ruby mechanical design. The existing approved singular exceptional ancestry, rebirth acquisition and complete Species/Ancestry replacement boundaries are unchanged. The underlying first-party sources are **research references**; their distinctive character/lore expression is not first-party Dorks & Dice content.

## Source order and verification

The objective is to use the **actual published Gem sovereign and champion material**, not invent an official 5e Aspect of Sardior or quietly collapse three separate historical iterations into one mechanical package.

| Priority | Publication and provenance | Relevant material | Access and evidentiary limits |
| --- | --- | --- | --- |
| **A — first-edition origin** | Arthur W. Collins, **"That's Not in the Monster Manual!"**, *The Dragon* **#37** (May 1980), printed pp. **6–7, 35–36** (continuation); TSR | Introduces **Sardior as a unique Ruby Dragon**, not a sixth ordinary Gem breed. His **two distinct breaths**, Gem Dragon council, and seven named psionic disciplines establish sovereign identity. | Scanned primary issue: https://pied.nu/dragon/files/Drmg037.pdf . Text was checked against the article; host PDF screenshots were temporarily unavailable at review. |
| **A — first-party expanded sovereign** | Mark A. Jindra and Scott Brocius, **"The Legend of Sardior"**, *The Mind's Eye*, Wizards of the Coast (**January 24, 2003**; D&D 3e-era web article) | Expanded sovereign's **three breaths** (Force, Light, Fire), psionic mastery, teleportation, divination, and special creation/transformation of gem thanes. | Historical WotC URL: `http://www.wizards.com/default.asp?x=dnd/psm/20030124a`. Accessible preserved article: https://www.realmshelps.net/faerun/organizations/sardior/index.shtml . The WotC archive is no longer reliably served. |
| **A — direct 5e Dragonborn precedent** | **Fizban's Treasury of Dragons** (Wizards of the Coast, **2021**), Chapter 6, **"Dragonborn of Sardior," p. 185**, in **Dragonborn Champions** (pp. 184–185) | A **champion NPC**, not the sovereign himself. Heat Breath, metal heating, Mental Defense, Mind Blade, component-free Psionic Spellcasting, psychic resistance, hovering flight, Frightened immunity, limited Legendary Resistance. | Official source index and book: https://www.dndbeyond.com/sources/dnd/ftod ; publishing page: https://marketplace.dndbeyond.com/all-rulebooks/fizbans-treasury-of-dragons . Some official book contents require purchase/sign-in; mechanically faithful pointer/cross-check: https://dnd-wiki.org/wiki/Dragonborn_of_Sardior_%285e%29 and https://runerollers.com/apps/platform/library/bestiary/dragonborn-of-sardior-ftd . |
| **A — ordinary family baseline** | *Fizban's Treasury of Dragons* (2021), Chapter 1 **Gem Dragonborn**; Chapter 3 dragon identities and Chapter 6 Gem creatures | Five ordinary Gem types; **Psionic Mind**, 15-foot cone, resistance, temporary Gem Flight/hover; comparison to Ruby's exceptional inheritance and defenses. | Official edition/index above; Dorks & Dice already reconciles its own ordinary Gem rules in `data/rulesets/first-party-001/native/species/ancestry/dragonborn/gem.json`. |
| **B — official prototype, not final rules** | Wizards of the Coast, **Unearthed Arcana: Draconic Options** (2021), pp. **3–4**, Gem Dragonborn | Published development-stage testimony on Gem Dragonborn, Sardior and the five affinities. **Earlier Gem Flight at level 3** and **2d8 initial breath** were changed in the finished book; do not override the published 2021 rules. | https://media.wizards.com/2021/dnd/downloads/UA2021-DraconicOptions.pdf |
| **B — WotC associated psionic material** | *The Mind's Eye* web series: **"The Ruby Disciple"** (Jan. 24, 2003); **"Chosen of Sardior"** and **"Sardior's Knowledge"** (Sept. 26, 2003); later **Church of Sardior** material (2004) | Explores crystal weapons, psychic defenses, psionic disciplines, knowledge and devoted followers. **Classes, powers and church affiliations** are distinct from an individual's inherent Ruby ancestry and should be evaluated only if a particular feature merits transfer. | WotC historical source index: https://orbitalflower.github.io/rpg/wizards-3e-archive.html ; preserved Mind's Eye text collection indexed at https://www.scribd.com/document/840480240/Mind-s-Eye-Psionics-0304 . Independent verification is needed for any exact feature imported from the older articles. |
| **B — published family context** | *Monster Manual II* (D&D 3e, **2002**) and *Draconomicon* (D&D 3.5, **2003**), as identified by D&D Beyond's historical review | General Gem Dragon psionics and historical creature mechanics, not a new official singular Ruby ancestry grant. | Official lore overview: https://www.dndbeyond.com/posts/1039-what-we-know-about-gem-dragons-before-fizbans . Consult source books for any specific mechanic not separately confirmed here. |

**No confirmed official 5e Aspect of Sardior:** The *Fizban's* bestiary lists **Aspect of Bahamut** and **Aspect of Tiamat**, plus **Dragonborn Champions**, but not an **Aspect of Sardior** stat block. Web pages labeled “Aspect of Sardior,” including GM Binder documents and D&D Beyond's community-homebrew monsters, are **fan-created**, even where their titles say "Official". Treat them as nonauthoritative third-party design examples, **not** WotC source mechanics. See https://www.dndbeyond.com/sources/dnd/ftod and an illustrative explicitly homebrew page: https://www.gmbinder.com/share/-MvLgP5VqPbOuP30LcVG .

## First edition, 1980: singular Ruby and diverse psychic expressions

*The Dragon* #37 is the **first Ruby Dragon source**. Sardior is expressly **one unique individual**, while the five ordinary Gem Dragon types are recurring breeds. This historically supports our current **singular exceptional Ruby versus five ordinary Gem ancestries** distinction. It does **not** imply every Ruby Dragonborn must be a composite of the five.

The original Sardior has **two separate breath effects**, each with its own limited daily use: an **Amethyst-style supernatural shriek** and a **Crystal-style dazzling cloud**. This differs sharply from the 2021 champion's Fire-damage Heat Breath. His listed **psionic disciplines** include **Domination, ESP, Invisibility, Levitation, Molecular Rearrangement, Energy Control and Dimension Walk**. The article also distinguishes broad magical capabilities from ordinary Gem Dragons.

**Design-relevant evidence:** Telepathic perception, psychic control, concealment, flight/levitation, dimensional mobility and sensory disruption have much older roots in Ruby than the modern Heat Breath. The **specific shriek and dazzling cloud** should be considered when comparing breath identity, but converting original save/instakill and blindness values directly into ancestry effects is not automatically justified.

Primary text: https://pied.nu/dragon/files/Drmg037.pdf (especially printed pp. 7, 35–36).

## Third edition, 2003: independent Force, Light and Fire breaths

The first-party WotC **Legend of Sardior** develops the sovereign with **three** breath options, separated by use:

1. **Force:** A concussive **line** with force damage.
2. **Light:** A brilliant **cone**, causing damage and possible temporary **blindness**. The article does not cleanly specify a modern 5e damage-type taxonomy; do **not** automatically claim this is 5e **Radiant** merely because it is light.
3. **Fire:** A **cone** dealing fire damage.

It also gives the sovereign extremely broad **psionics**, magical protections, teleportation, and a **Create Thane** transformation power. That transformation includes replacement of an individual's **memories and personality** in its published monster/deity version. This is **not** the Dorks & Dice Rite of Rebirth contract, which explicitly retains historical identity and independently earned characteristics. The lore/deity's transformation ability may inform the origin of such a rite without importing those destructive memory semantics.

**Design-relevant evidence:** A Ruby identity can plausibly be **selectively psychic, radiant/light-associated, force-associated and heat-associated**, but these are **separate published manifestations** across versions. This does **not** approve three additional Breath Weapons, one free copy of each published attack, or an all-five-Gem composite. Sovereign abilities scale at deity/monster power and must be rewritten for a character-level ancestry if adopted.

Preserved source: https://www.realmshelps.net/faerun/organizations/sardior/index.shtml .

## Fifth edition, 2021: Dragonborn of Sardior — champion, not Aspect

The **Dragonborn of Sardior** (p. 185) is the closest published **Dragonborn** mechanical template. Unlike the 2003 divine sovereign, it is a **CR 6 Humanoid NPC** with a different limited ability set. Its primary traits are:

| Published champion ability | Verified source mechanic | Dorks & Dice relevance without automatic approval |
| --- | --- | --- |
| **Heat Breath** | One **30-foot cone** dealing **Fire** damage on a Constitution save; **metal objects** in the area heat up briefly, potentially inflicting additional Fire damage and attack penalties on creatures contacting them. | Distinctive heat/metal control option, **not** the ordinary five Gem damage types and not automatically the Ruby ancestry's final affinity. Do not import monster Recharge 6 or full dice. |
| **Mental Defense** | **Unarmored AC** includes **Intelligence modifier** in addition to its normal considerations. | Possible psionic defense; preserve compatibility with separately authored AC calculations and armor rules. |
| **Mind Blade** | A melee/ranged attack adding **Psychic** damage to its weapon damage. | Possible psychic projection; **three-attack Multiattack** is a monster action and should not become an ancestry benefit. |
| **Spellcasting (Psionics)** | Three **component-free**, limited-use manifestations: **Bigby's Hand, Hypnotic Pattern and Telekinesis**, using Intelligence. | A separate psionic expression; no automatic spells, slots or unrestricted spell-list access. Distinguish inherited powers from class/Patron advancements. |
| **Flight** | **30-foot flying speed with hover**. | Strong precedent for Ruby hover; its progression still needs comparison against temporary ordinary Gem hover and already-approved Platinum/Fivefold continuous nonhover flight. |
| **Defenses** | **Psychic resistance**, **Frightened immunity** and **one failed-save-to-success use/day**. | Strong evidence for a focused psychic defense and limited champion resilience, but not five simultaneous Gem resistances or a universal mandatory legendary chassis. |

**Do not infer:** The NPC's character level, armor class, creature type, alignment, attack count, specific ability scores, spell save DC, class training, or listed equipment from its **CR**. The existing Dorks & Dice Dragon creature type, total-character-level scaling and complete Rite replacement semantics take precedence.

Official published book: https://www.dndbeyond.com/sources/dnd/ftod ; stat reference: https://runerollers.com/apps/platform/library/bestiary/dragonborn-of-sardior-ftd .

## Source conflicts and classification rules

1. **Ruby is singular, not a sixth ordinary Gem leaf.** Supported from the first 1980 portrayal through modern reference lore. The pre-existing exceptional Ruby design boundary remains approved.
2. **Breath has no one continuous invariant across editions.** 1980 **shriek/dazzle**, 2003 **Force/Light/Fire**, 2021 **Fire/heat-metal**. A future design must pick an intentional ancestry mechanic rather than claiming the latest champion represents the only Ruby precedent.
3. **Sovereign versus champion matters.** Do not mistake the 5e champion for Sardior's own Aspect, and do not equate deity-scale 2003 spell/psionic lists with inherent Dragonborn levels.
4. **Damage and defense types are distinct.** Psychic **resistance** in the 2021 champion does not require **Psychic breath**; a Fire breath does not necessarily justify Fire resistance if the approved Ruby defensive expression is independently authored. The 2003 "Light" breath does not itself establish modern **Radiant** typing.
5. **Psionic feats and prestige classes are additional sources, not automatically ancestry.** The Ruby Disciple and Chosen of Sardior do not automatically grant powers to every reborn Ruby character. Learned and patron powers remain separate.
6. **Ordinary Gem mechanics are an input, not guaranteed inheritance.** A Ruby transformation must explicitly state whether it **replaces or enhances** the ordinary Psionic Mind, Gem Flight, single-affinity resistance, and Breath Weapon. No default sixth leaf or five-ancestry aggregate.
7. **Source provenance is not copyright permission.** Mechanical systems can be investigated and adapted while preserving separately authored first-party Dorks & Dice expression and omitting unique protected character lore/visuals.

## Evidence still to inspect when choosing mechanics

- Verify exact **physical-book** language for *Fizban's* Chapter 6, p. 185, when the owner can access a legitimate copy. Public summaries and preserved text consistently support the mechanical overview, but the primary book content is gated online.
- If adapting a specific 2003 **psionic power**, inspect the original individual WotC article and its edition's power rules. Only the source group and core 2003 sovereign article have been checked here, not every psionic enhancement.
- Confirm the **2021 first-party finalized** Gem Dragonborn baseline over the **2021 UA** playtest whenever a disputed level threshold or number is involved.
- Compare distinct **Force/Light/Fire**, **Heat Breath**, **blindness**, **mind control**, **mental defense**, and **hover** design spaces against Platinum Heart/Mind/Wings and Fivefold's approved last-stand identity. This list names **research candidates**, not approved Ruby features.

## Approved scope and next artifact

Existing authoritative design: [Ruby Dragonborn concept](pillar2-ruby-dragonborn-concept-20261111.md), [Rite of Rebirth](pillar2-rites-of-rebirth-20261111.md), native Gem family JSON, and approved exceptional Platinum/Fivefold specifications.

**Output of this pass:** Source inventory and edition comparison only. **No Ruby mechanical selections are made.** The next Ruby design review should use this corpus as its evidence base, separating *historical sovereign powers*, *published 5e champion features*, *ordinary Gem Dragonborn traits* and *class/acquired psionics*.
