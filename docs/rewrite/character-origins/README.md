# Character Origins — Pillar 2 draft data structures

**Status:** Design fixtures, not approved native mechanics and not executable implementation.  
**Branch:** `feature/character-origins-pillar2-heritage`  
**Authoritative selected rules:** `data/rulesets/first-party-001/native/`

## Purpose

Develop a provisional character-origin representation from the *approved semantic distinction*:

> Origination establishes how an individual came into existence. Heritage identifies the sources from which inherent characteristics may be derived. A character's actual characteristics are determined by the traits that manifest or are explicitly established through its origin. Neither the existence of a contributor nor a relationship with that contributor automatically grants its complete mechanical characteristics.

The approved player-choice direction rejects mandatory Punnett squares, genetic dice rolls, and unlimited trait grants. **Neither a point-buy nor a substitution budget has been selected.**

`pillar2-origin-profile.schema.json` describes a **testing artifact**, not the final native JSON schema or character-save contract. Any field may change after reviewing trait composition, eligibility, balance, type targeting, and tester feedback.

## Shape of the draft representation

| Field | Role | Important non-assumption |
| --- | --- | --- |
| `origination.method` | How the individual was created | No limit to biological reproduction |
| `origination.contributors[]` | People, sources, or processes involved | Zero or more; each may have mixed heritage |
| `heritageSources[]` | Sources from which traits might qualify | Does not grant the complete source Species package |
| `conventionalSelection` | Optional preset route to existing Species/Ancestry and separate Upbringing | Not established as compulsory for all origins |
| `effectiveProfile` | Candidate or approved creature type, size, walking speed | No monster statblock defaults and no assumed cross-type eligibility |
| `traitCandidates[]` | Traceable existing trait or proposed concept | Candidate is not a grant |
| `openQuestions[]` | Required rulings before promotion | Preserves uncertainty explicitly |

**Important identity distinction:** `species.elf` is a globally indexed native entity ID; `low-light-vision` inside its `traits[]` array is a local trait ID. For now, `nativeTraitLocation: {entityId, localTraitId}` provides traceable reference without falsely claiming an independent global trait entity exists. Whether common traits become independent entities remains an unresolved design question.

## Representative fixtures

1. `examples/conventional-high-elf.json`: existing approved Elf and High Elf selections, with independent High Elf Upbringing, used as the no-extra-complexity control. The fixture enumerates approved traits, not new grants.
2. `examples/mixed-elf-orc-parents.json`: both biological parents have mixed Elf/Orc heritage. The child has several eligible **candidates**, not a playable approved set; no percentages, assumed dominance, or automatic full packages.
3. `examples/constructed-person.json`: creator, fabricator, and animating magical source; neither creator's personal Species nor Class features flow automatically to the construct. Proposed body concepts and creature type remain illustrative.

## Known design stress points

- **Shared vision:** Elf and Orc both possess native Low-Light Vision. A single resulting effect should not multiply because there are multiple ancestral sources.
- **Anatomy and needs:** Powerful Build, Trance, breathing, physical attacks, natural movement, and body shape have different potential dependencies; a generic parentage relationship is not sufficient to grant them.
- **Type classification:** The existing nine Species include Fey Elf, Humanoid Orc, Dragon Dragonborn, and other creature types. Cross-type targeting has not been resolved.
- **Progression:** Goliath Large Form, High Elf spells, Tiefling spells, and Dragonborn breath damage have character-level progression. A selected trait must not lose its progression or grant Class levels.
- **Independent upbringing:** The existing `defaultUpbringing` ID is a suggestion. Upbringings and Backgrounds are Life Experiences, not inherited sources.
- **Homebrew extensibility:** A DM should eventually be able to define a new Species's traits and have them considered by the same eligibility and combination rules, without hardcoding pairings.
- **Avoid over-specifying implementation:** This material supports rules reconciliation now. Executable C# and consumer integration remain deferred until after Pillar 3 or 4.

## Promotion gate

Before any draft mechanism is promoted to authoritative `native/`:

1. Review a conventional Elf against its approved configuration without altering existing effects.
2. Agree on an actual mixed Elf/Orc character's allowed traits and effective profile, without assuming a point cost.
3. Review prerequisites, redundant benefits, and conflicts against a physically unconventional creature.
4. Verify the approach can accept a homebrew Species and mixed-heritage parents.
5. Only then refine schema and separately record which authored mechanics have or lack executable support.

The tester-wide Elf and Orc feedback request can proceed during this work. The responses are evidence for rulings, not automatic rule changes.
