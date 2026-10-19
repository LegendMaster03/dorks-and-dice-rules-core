# Native monster families and tactical variants

**Owner design direction (2026-10-18):** Follow the more varied **5.5e/SRD 5.2.1 monster design** as the default direction. Keep distinct roles and tactics as distinct *native monster variants*, rather than combining every inherited attack, trait, action and numerical revision into one bloated creature. This is a cleaner, more labor-intensive content-reconciliation approach. It does **not** create parallel edition-dependent gameplay engines.

## Entity model

- **Creature family:** a grouping of related species, creatures or archetypes for browsing, encounter building, relationships and common traits. It does not imply an automatic feat or ability grant to every family member.
- **Monster variant:** an independent stat block with a meaningful, playable role (minion, warrior, boss, captain, stalker, caster, etc.). Each variant has its *own* coherent native statistics, actions, traits, encounter role and eligibility. Different variants are not the same entity's 2014/2024 switch.
- **Shared mechanics:** actions, conditions, attack resolution, damage, skills, spellcasting, roles and resource rules use the single Dorks & Dice native ruleset. Share implementations; do not duplicate rule physics per variant.
- **Legacy identity map:** old names are resolved against equivalent native variant identities before marking them as independent content. A difference in edition label or naming alone is not enough to manufacture a distinct creature.
- **Additional variants:** keep a genuinely unique older creature or distinct tactical configuration where it adds actual variety, after ensuring its costs, challenge and actions make sense. Use a descriptive role identity rather than an edition-prefixed clone. A specialized variant may have unique capabilities without granting those capabilities to every member of its family.

## Reconciliation workflow

1. Group SRD 5.1 and 5.2.1 monsters by **semantic family and combat role**, not only exact monster name.
2. Use SRD 5.2.1's **role-specific creature roster and corresponding stat block** as the primary default when a suitable variant exists.
3. Identify unchanged, renamed, moved or recast older mechanics and map them to the appropriate variant and shared native resolution. Do not double-grant an effect because it moved from a Trait to a Bonus Action or from one named attack to another.
4. Preserve compatible, nonredundant older mechanics according to the five approved reconciliation rules, while considering the intended *variant*, not assuming all added abilities belong on all members of the family.
5. When mechanics conflict, use the chosen coherent variant behavior. A materially distinct old option might justify a different **tactical variant** rather than a second procedure on the baseline variant. Do not invent an unnecessary variant for a purely numerical/stat-block edition revision.
6. Validate encounter roles, attacks, resources, action economy, CR coherence and compatibility with the native Grapple, status and AC rules. Content with genuine mechanical incompatibilities remains under review.

## Example: goblinoid roles in the imported SRDs

| Family | SRD 5.1 | SRD 5.2.1 variants | Reconciliation direction |
| --- | --- | --- | --- |
| Goblin | Goblin (CR 1/4) | Goblin Minion (CR 1/8), Goblin Warrior (CR 1/4), Goblin Boss (CR 1) | The old Goblin closely corresponds to Goblin Warrior: scimitar, shortbow and Nimble Escape. Review as a **semantic same-role predecessor** before deciding whether any truly independent old mechanics warrant a new role. Preserve distinct Minion and Boss identities. |
| Hobgoblin | Hobgoblin (CR 1/2) | Hobgoblin Warrior (CR 1/2), Hobgoblin Captain (CR 3) | Review old Hobgoblin against Warrior; Martial Advantage versus Pack Tactics/changed weapon damage is a real tactical distinction, not just formatting. Captain remains its own role. |
| Bugbear | Bugbear (CR 1) | Bugbear Warrior (CR 1), Bugbear Stalker (CR 3) | Review the older ambush/brute identity against the newer grapple-oriented Warrior and Stalker. A truly distinct ambush specialist could warrant a variant, but should not automatically stack Brute, Surprise Attack, Abduct and the new attacks onto every Bugbear. |

### Immediate data-quality consequence

The first automated cross-category pass retained source-unique catalog entries when they had no *exact-name* counterpart. It therefore retained `monster.goblin` alongside `monster.goblin-warrior`, `monster.hobgoblin` alongside `monster.hobgoblin-warrior`, and `monster.bugbear` alongside `monster.bugbear-warrior`. This is **source retention**, not final approval that all six are separate gameplay variants.

Before sign-off, all source-unique monsters must pass semantic role/alias reconciliation. Until that check, the earlier **467 source-only retained content** and **531 automatically admitted entries** cannot be interpreted as fully reconciled distinct native entities. Do not delete source data or invent legacy-named gameplay variants to make coverage statistics look better.

## Not decided by this document

- A universal rule that all original 5.1 numerical values are discarded regardless of monster role.
- A numeric challenge-adjustment algorithm for new combinations of older and newer abilities.
- Automatically converting source-edition differences into new variants.
- Assigning old abilities to arbitrary existing 5.2.1 variants without checking tactical identity, action budget and effect duplication.

**Status:** The Owner's preference for 5.5e-style tactical variety is recorded. The semantic family mapping and coherent variant definitions still require implementation and validation before any related entry can pass the final native baseline gate.
