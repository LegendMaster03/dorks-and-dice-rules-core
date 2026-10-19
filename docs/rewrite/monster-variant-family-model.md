# Native monster families and DM-selected tactics

**Owner correction (2026-10-18): Tactics alone do not create different monsters.** A DM can use one creature's existing actions and movement in different ways. Preserve the role-specific variety of 5.5e where a creature really has a different stat block, capability set, or encounter role, not where it merely favors a different strategy.

## Design distinctions

- **Creature family:** A semantic grouping for browsing and encounter construction, such as Goblin, Hobgoblin and Bugbear.
- **Creature identity:** One coherent native monster stat block with all approved traits, actions, and constraints. A renamed older monster can be an alias of the current identity.
- **DM tactics:** Decisions about targets, positioning, attack selection, teamwork, retreat, surprise, timing, and other actions already available in that creature's stat block. These are not fixed in the creature identity and do not require separate records, switches or profiles.
- **Meaningful creature variant:** A mechanically distinct role, such as Goblin Minion (CR 1/8), Goblin Warrior (CR 1/4), or Goblin Boss (CR 1), with different statistics or actual capabilities. Such entries can be separate creatures.
- **Older mechanical abilities:** Reconcile compatible additions into the one appropriate creature definition, without duplicating effects. Older attack/action suites with different numerical or procedural outcomes require review, not indiscriminate stacking. Tactics alone never justify restoring an entire old stat block as a separate monster.

## SRD 5.1 to 5.2.1 mapping examples

| Older identity | Current native identity | Reason |
| --- | --- | --- |
| Goblin | Goblin Warrior | Same base combat role; newer family also includes Minion and Boss |
| Hobgoblin | Hobgoblin Warrior | Same core role; old Martial Advantage and new Pack Tactics are **different mechanical capabilities** to reconcile within a single coherent monster, not separate tactical identities |
| Bugbear | Bugbear Warrior | Same base role; old Brute/Surprise Attack and new grappling actions need integration review, not automatic creation of an 'ambush variant' |
| Goblin Boss | Goblin Boss | Distinct, stronger encounter role; remains its own stat block |
| Hobgoblin Captain | Hobgoblin Captain | Distinct leadership capabilities and CR; remains its own stat block |
| Bugbear Stalker | Bugbear Stalker | Distinct statistics, CR and capabilities; remains its own stat block |

**DM freedom and mechanical grants are separate.** A DM can choose any legal behavior at the table, but a monster does not automatically gain an older mechanical trait that has not been reconciled into its native stat block. Conversely, having several actions is not itself a reason to produce several monster variants.

## Applied repository changes

- **285** same-name 5.1/5.2.1 creature pairs use one selected 5.5e-style native stat-block default.
- **23** older monster names map to the corresponding new creature identity, including Goblin, Hobgoblin and Bugbear to their Warrior records.
- **84** independent older traits were previously carried into the newer definitions.
- **3** monster families are represented in `data/native/monster-families.json`; different roles remain separate members.
- **64** single-source monster records remain retained. The old Hobgoblin and Bugbear are *not* among them; they are aliases, so neither creates a duplicate combat stat block.
- **249** entries in `data/reference/srd/monster-legacy-variant-review.json` preserve older mechanical questions, including old Hobgoblin and Bugbear features. These entries are **not** additional operative monsters. The original pinned SRDs remain intact.

The candidate backlog remains **1,663**, because both newly mapped aliases were already counted as retained single-source records. This correction changes identity accounting and avoids tactical duplicates; it is not an additional reduction of the original unresolved-pair inventory.

## Reconciliation outcome — removed 5e abilities

The shared **285** monster stat blocks retain their selected 5.5e statistics and redesigned ordinary attack suites. The previous **84** independently compatible older traits remain included. Subsequent reconciliation restored **145 more older abilities** (traits, actions, and legendary choices), as well as **33 older spell grants** that had no 5.5e counterpart. The newer spellcasting ability and 5.5e action model remain authoritative.

The retained reference comparison file contains **249 historical feature-review entries**, but they are *not* 249 outstanding mechanical decisions. Every originally flagged feature now has a feature-level disposition: already present, moved/reworked, restored, replaced by the newer attack suite, or still unresolved. Exactly **2 unresolved monster-mechanics decisions across 2 creatures** remain. The three beast Pounces and Weretiger Pounce have been reconciled without altering the 5.5e attacks. The Assassin's first-turn Advantage was restored independently; its automatic critical against a surprised target remains undecided.

| Creature | Outstanding Owner decision | Reason |
| --- | --- | --- |
| Assassin | Assassinate automatic critical hit | The older feature makes hits against surprised creatures critical. The native surprise eligibility and interaction with Dorks & Dice critical escalation need a defined procedure; first-turn Advantage is already restored. |
| Priest | Divine Eminence resource and damage | The older feature spends a spell slot as a bonus action for extra Radiant melee damage. The current Priest has no spell slots, and its Mace already deals Radiant damage. Do not invent a resource or duplicate the base damage. |

In addition to those two questions, **64 source-unique monster entries** remain retained as independent catalog content. This is not an edition-conflict backlog. Newer-only monsters such as Pirate and Pirate Captain need no older comparison.

Remaining engineering and acceptance work is **native markup/schema conversion, integration against unified Dorks & Dice effects and action timing, monster-family consistency, and challenge/encounter balancing checks**. Additional older traits and legendary choices can affect difficulty even though the selected CR was not changed. None of these are justification for creating a second monster solely for different DM tactics.

Detailed per-feature dispositions: `data/reference/srd/monster-legacy-variant-review.json`. Cumulative statistics: `docs/rewrite/native-reconciliation-statistics.json`.
