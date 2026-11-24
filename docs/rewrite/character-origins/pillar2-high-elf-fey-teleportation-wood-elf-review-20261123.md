# Pillar 2 — High Elf Fey Teleportation; Wood Elf Magic review (2026-11-23)

**Status:** Owner-approved High Elf enhancement implemented on a draft branch
stacked on Abyssal PR #207. The owner subsequently decided to **leave Wood
Elf and Drow unchanged at their existing 18/18 biological allocations**.
The earlier discussion of an additional Wood Elf Druid cantrip is superseded,
not pending implementation. No changes to `main`.

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

## Wood Elf and Drow — balance settled, unchanged

**Decision:** Both Wood Elf and Drow are already at the **18/18** trial
allowance. Leave their existing native biological characteristics,
spells and prices unchanged. Do not introduce additional spell grants,
replace existing spells or adjust their point costs merely to incorporate
historical XGE feats.

Wood Elf retains **Druidcraft, Longstrider, Pass Without Trace, Natural
Camouflage, Trackless Passage**, its 35-foot walking speed, and current
spellcasting choice. The previously discussed extension of *Wood Elf
Magic* (Druidcraft plus one extra selectable Druid cantrip) would have
cost at least one additional point and exceeded the allowance; that
proposal has been **set aside by the owner**, not deferred as a required
implementation task. Its existing spell package already captures the
feat's two leveled spells.

Drow retains its existing innate spell progression and linked
Darkvision/Sunlight Sensitivity. *Drow High Magic* is not added to this
full-budget Ancestry.

The separate **Wood Elf 30-/35-foot movement reconciliation** is a
pre-existing technical integration issue and is not a reason to reopen
biological balance or grant new characteristics.

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
