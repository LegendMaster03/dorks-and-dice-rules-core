# Pillar 2 — High Elf Fey Teleportation; Wood Elf Magic review (2026-11-23)

**Status:** Owner-approved High Elf enhancement implemented on a draft branch
stacked on Abyssal PR #207. The owner also approved the **direction** of
retaining Druidcraft and adding one separately selectable Druid cantrip to
Wood Elf, but that Ancestry's already-full price requires a separate balance
decision before enabling the new choice. No changes to `main`.

## High Elf — approved and implemented

The native High Elf already has a single
`trait.misty-step` innate spell gene, unlocking at **character level 5**,
with one free casting and additional casting with an appropriate spell slot.

Adapt the Fey Teleportation effect from *Xanathar's Guide to Everything*:
- Change the free casting to **one per Short or Long Rest**.
- Preserve level-5 unlock, the source-selected spellcasting ability, and
  optional spell-slot casting.
- **Do not** grant an additional copy of Misty Step or another free-use pool.
- Do not grant Sylvan as an automatic biological language entitlement.
  Language acquisition belongs to a separate Upbringing review.
- Price the **complete enhanced** gene at **5 points**, previously 3.
  The spell base price remains 3 for a second-level spell, with a +2
  modifier for Short or Long Rest recovery.
- The High Elf allocation changes **15/18 → 17/18**. Shared Elf Species,
  Wood Elf and Drow are unchanged.

The innate-spell progression projector now recognizes both
`long-rest` and `short-or-long-rest`. The innate-spell resource
ledger now recognizes both in its pool validation and uses the
**individual source spell's recharge schedule** when recovering pools,
rather than hardcoding `long-rest` for every grant. A Short Rest
must therefore restore a permitted Short-or-Long-Rest pool without
refilling unrelated Long-Rest-only spells. The existing
`NativeResourceResolution` already implements this recovery contract.

### Existing integration boundary

**High Elf's changeable Wizard cantrip is an open `spellList` grant**.
The current `NativeOriginInnateSpellProgressionProjector` correctly
reports `spell-list-projection-deferred` for the full High Elf
spell course because no verified per-spell list choice / replacement
projector exists. That also prevents bootstrapping a complete High Elf
innate-spell ledger in the current implementation. The Misty Step
payload and generic projector/ledger recognition are updated; this
increment does **not** complete open Wizard cantrip projection,
live spell casting, or authenticated resource persistence.

Tests cover gene and Ancestry data, the full revised cost, unchanged
Detect Magic, and shared primitive rest behavior (Misty Step Short/Long,
Detect Magic Long only). They do not claim an executable High Elf
end-to-end spell ledger flow.

The first-party trial pricing revision is updated from
`biological-pricing-20261123-v10` to **v11**, retaining **93 genes,
98 entries**.

## Wood Elf — approved concept, balance still unresolved

The existing Wood Elf `ancestry.elf.wood` provides:
- **Druidcraft** cantrip at will (one point);
- **Longstrider**, free once per Long Rest starting at character level 3;
- **Pass Without Trace**, free once per Long Rest starting at level 5;
- **Natural Camouflage** and **Trackless Passage**, with a woodland
  biome binding;
- 35-foot walking speed, source-selected spellcasting ability, and
  the common Elf biological foundation.

The user-approved *Wood Elf Magic* direction is to **retain Druidcraft
and add one other Druid cantrip of choice**. The feat's other two spells
are already present and must not become duplicate spell grants.

The existing Wood Elf allocation is **18/18**. A second Druid cantrip
conservatively costs **at least one additional point** under the
first-party spell pricing rule, resulting in **19/18** before considering
any surcharge for choice flexibility or later replacement. Do not
silently declare the extra cantrip free, decrease the prices of
unrelated characteristics, or raise the universal allowance.
**No additional Wood Elf cantrip has been added in this branch.**
The follow-up review should select an authorized way to resolve this
budget conflict, then choose whether the added Druid cantrip is fixed
at creation or replaceable after a Long Rest. Open-list spell selection
needs corresponding mechanical projection and provenance support.

## Elven Accuracy

This unrelated attack reroll has no natural mechanical match to the
current Elf foundation or the High/Wood Elf innate spell progressions.
It remains a separate candidate, not an inherited bonus granted here.

## Testing and merge policy

Focused static validation and regression unit tests are authored.
The complete .NET test suite, CI, runtime spellcasting, Wood Elf movement
reconciliation and browser acceptance tests must be independently run
before merge. This PR stays draft, based on the preceding Abyssal work,
without touching `main`.
