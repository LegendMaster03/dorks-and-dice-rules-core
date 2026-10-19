# New 3.0/3.5 conditions — first standalone tranche

**Baseline:** stacked on `feature/3x-condition-defense-reconciliation` (PR #99). This work adds six native conditions; it does not merge or overwrite PR #99.

The native catalog contains **24** unique conditions, each with one authoritative, edition-independent definition. The additional six are Dazed, Dazzled, Entangled, Fascinated, Nauseated, and Sickened.

| Condition | Native rule and reason for distinct identity |
| --- | --- |
| **Dazed** | Prohibits Actions, Bonus Actions, and Reactions but does not intrinsically end Concentration, restrict movement, or suppress AC. Therefore it does **not** inherit Incapacitated. |
| **Dazzled** | −1 to attacks, Spot and Search. Broader 3.5 clarification adopted (3.0 affected attacks only), preserving existing native `skill.spot` and `skill.search` components. |
| **Entangled** | Half Speed or zero if anchored, cannot Run or Charge, −2 attack, effective Dexterity −4. 3.5 spell interruption DC 15 + spell level recorded, but its integration with native Concentration is unresolved. |
| **Fascinated** | Quiet stationary attention, limited actions, −4 reactive Listen/Spot, additional save when threatened, ends on obvious threats or an ally's Action. |
| **Nauseated** | Movement or a simple move-equivalent activity only; no attacks, spells or concentration. Legacy move-action equivalence to native resources remains unresolved. |
| **Sickened** | −2 to attacks, weapon damage, saves, ability and skill checks, rather than an unreviewed conversion to Disadvantage. Does not inherently imply Poisoned. |

**Status of the mechanics:** These definitions are suitable for native data and future conversion work. Entangled's spellcasting disruption and Nauseated's move-equivalent action grant have **explicit non-executable integration markers**; neither has been silently mapped into a different 5.x rule.

## Guardrails

- Keep Flat-Footed/Helpless and their Touch AC consequences as established in PR #99.
- Do not substitute Incapacitated for every historical action restriction: it would end Concentration in cases where the original effect did not.
- Do not double-penalize complex Perception by applying a historical Spot penalty both to the child skill and again to the derived parent.
- Preserve native numerical distinctions (−1/−2/−4) where the original intent is meaningfully different from Advantage or Disadvantage.
- Keep the 3.0/3.5 evidence record in `data/reference/3x/standalone-conditions.json`; leave the pinned CC BY SRD 5.1/5.2.1 data untouched.

## Historical difference review

The 3.0 Dazzled definition has a −1 attack penalty, and the 3.5 version additionally penalizes Spot/Search. Entangled's spell interruption check was usually DC 15 in 3.0; 3.5 added spell level to that DC. Fascinated and Sickened use the reviewed 3.5 definitions and are not claimed as verbatim 3.0 entries.

Sources: [3.0 condition summary transcription](https://opengamecontent.fandom.com/wiki/D20_System_v3.0_SRD/Condition_Summary) and [3.5 condition summary](https://www.d20srd.org/srd/conditionSummary.htm). These are historically licensed Open Game Content transcriptions, not part of the repository's CC BY 4.0 5.x pack.

## Deferred review

Confused, Cowering, Fatigued, Panicked, Pinned, Shaken, Staggered, Turned and Held require inheritance, combat movement or source-system decisions. Dying/Stable/Disabled and ability/energy damage remain separate states or progression mechanics rather than blindly importing old death thresholds.

All changes here are to data and tests. The full executable condition interpreter is not yet built.
