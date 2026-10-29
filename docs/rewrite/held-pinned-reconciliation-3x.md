# D&D 3.0/3.5 Held and Pinned — native reconciliation

**Status:** selected native rules data, dependent on PRs #99, #100, #102 and #103; the final unified combat maneuver and casting-time interpreter are not implemented.

This tranche adds **Held** and **Pinned**, bringing the unified catalog from 28 to **30 native conditions**. These are deliberately distinct from Helpless, Paralyzed, Grappled and Incapacitated.

## Held: 3.0 magical immobilization

The 3.0 condition describes a magically held creature as helpless, physically immobilized and unable to undertake physical actions, but still breathing, aware and able to take purely mental actions. Native Held therefore **inherits Helpless** and explicitly bars physical activity, movement and speech. It **does not inherit Incapacitated**, because that would automatically end Concentration and remove the historical pure-mental action exception. Held supplies no second duplicate AC adjustment; Helpless supplies its normal −5 effective Dexterity and melee vulnerability.

The 3.5 Hold Person spell uses **Paralyzed** rather than adopting 3.0 Held as a new universal spell effect. Historical conversion should retain that distinction: an imported 3.0 Held effect maps to Held, whereas a 3.5 Hold Person effect maps to Paralyzed. Neither rule automatically overrides the aware-Petrified decision.

## Pinned: tighter grapple, not helplessness

In the 3.0 and 3.5 grapple procedures, a pinned creature is held immobile for one round **but is not Helpless**. It continues to be Grappled. The 3.0 representation gives outsiders +4 to attack; 3.5 instead subtracts 4 AC against attackers other than the pinner. Native implementation adopts the 3.5 **defender-side** −4 AC adjustment. The approved Grappled denial of applicable Dexterity/dodge defense against outsiders continues to apply; these are distinct defenses, not duplicate instances of a single penalty.

The pinning creature may elect to prevent speech. Without that choice, Pinned does not silence the target. No full Incapacitated inheritance is applied, so available escape options remain possible.

**Precondition:** A Pinned effect requires a separately active Grappled instance from the same pinner. This prevents applying Pinned alone from creating a temporary Grappled state that disappears when the pin is broken.\n\nBreaking a pin with an opposed grapple check or the separately retained Dexterity-based Escape Artist check **removes Pinned, not Grappled**. Voluntary release by the pinner ends that pinner's grapple; independent grapples by other combatants remain.

The historical 3.5 pinned casting restrictions and DC 20 + spell level Concentration skill check are preserved as data but marked **pending native spellcasting reconciliation**. Likewise, the 3.x initiation in place of an attack and the Standard Action escape cost are recorded, not silently converted into new Action or Reaction budgets. The source-defined pin duration is one round unless overridden.

**Important:** The 2014 Grappler feat applies Restrained when it pins a target. The source conversion of that feat is not automatically rewritten to apply this newly defined Pinned condition. Native condition and source-feature conversion are separate tasks.

## Testable examples

For a defender with Normal AC 19, Touch AC 13 and Dexterity +3, assuming no other AC bonuses:

- Pinned, attacked by its pinner: 19 Normal AC / 13 Touch AC.
- Pinned, attacked by an outsider: 12 Normal AC / 6 Touch AC (−3 lost Dexterity from Grappled and a further −4 from Pinned).
- Held, attacked in melee: 7 Normal AC / 1 Touch AC (Helpless effective Dexterity −5 plus −4 melee exposure).
- A successful escape from Pinned leaves Grappled in place until separately removed.

These examples reflect the proposed **native data contract**, not live execution of the new Rules Core combat runtime.

## References and remaining work

Source comparisons and licensing boundaries: `data/reference/3x/held-pinned-reconciliation.json`. Original paraphrased definitions are in `data/native/conditions.json` and relationships/procedures in `data/native/held-pinned.json`. Historical material is 3.x Open Game Content, separate from the stored 5.x CC BY reference corpus.

Further candidates: **Staggered**, **Confused**, **Turned**, along with broader death/dying and nonlethal damage status reconciliation. Pinned action economy, multiple grapplers, spellcasting, and Grappler feat conversion require later system integration.

Sources: [3.0 condition summary](https://opengamecontent.fandom.com/wiki/D20_System_v3.0_SRD/Condition_Summary), [3.0 Hold Person](https://www.dandwiki.com/wiki/3e_SRD%3AHold_Person), [3.5 condition summary](https://www.d20srd.org/srd/conditionSummary.htm), [3.5 grappling](https://www.d20srd.org/srd/combat/specialAttacks.htm).
