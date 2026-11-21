# Pillar 2 — Soul-powered Construct core

**Status:** Owner-approved source design (November 21, 2026); candidate implementation
awaits tests and integrated acceptance. Only this source is in scope.

**Source identity:** `construct-power-source.soul-gem` remains the existing
native ID for compatibility. Its physical form does **not** have to be a gem.
The first-party display name is **Soul-powered core**. Its design draws on an
external campaign setting for inspiration, but this document is original
Dorks & Dice rules text, not a reproduction of that source's lore.

## Operating characteristics

| Property | Candidate game rule |
| --- | --- |
| Normal maximum output | **8 simultaneous power units** |
| Overdrive maximum output | **12 simultaneous power units** |
| Shared Overdrive increase | **+50%** |
| Capture radius | **30 feet**, current provisional playtest value |
| Capture | **Automatic** upon eligible death in range |
| Soul storage capacity | **No fixed upper limit** |
| Normal soul-energy consumption rate | **Intentionally undefined** |
| Overdrive energy use | Accelerated soul consumption, without a fixed ratio |
| Energy availability | DM adjudication, rather than automatic fuel math |
| Ordinary on/off switch | None; the core remains inherently active |

Only its current generation output enters the normal Construct power
network. The number of souls imprisoned does not increase maximum
simultaneous output. This source does not borrow the mechanical engine's
daily ration or the solar generator's time-based battery ledger.

### Automatic capture

When an eligible creature dies within the core's capture radius, its soul
is drawn into the core without the Construct's consent or intervention.
The mechanism does not distinguish friends from enemies. It does not
require the Construct to be conscious, powered, or actively drawing
electricity, and ordinary disconnection from the Construct's network does
not prevent capture.

The DM determines soul eligibility, protective or competing magic,
and the outcome when the same death is within several eligible cores'
ranges. A capture event must be recorded by the authoritative game host
to affect soul history; a provisional preview is not a persistent
capture or resurrection restriction.

**There is no soul-storage limit, overflow rule, separate core-size tier,
or forced loss of a previously captured prisoner.** Soul identity should
be preserved in game history when available, so the DM can bring back
previously defeated enemies, companions, or other inhabitants during a
rescue scenario. That is a narrative possibility, not a mandatory
population list.

### Energy and Overdrive

The core draws power from captured souls. It may still imprison souls
when it is not connected to an active Construct network. Because the
amount and process of spiritual consumption remain undefined, **the
game system must not invent charge quantities, soul classes, automatic
decay, consumption order, or time-to-exhaustion**.

The DM determines whether the core can currently supply usable energy.
If it can, the generator has 8 power units of normal output. While
Overdrive is selected and the DM confirms sufficient usable energy,
the maximum output increases to 12. Overdrive uses soul energy faster,
but its exact cost and duration are deliberately not quantified.
If the DM determines the energy is insufficient, the core contributes
zero usable power, and downstream components follow the existing
network and continuity rules.

Neither ordinary source disconnection nor turning off another
Construct component is an approved way to prevent soul collection or
to preserve an imprisoned soul. Exceptional suppression, physical
destruction, and their effects on captives are DM adjudications;
this design does not mandate that destroying the core frees or destroys
the souls.

## Soul Imprisonment and Extraction

A soul imprisoned by the core cannot answer ordinary magic that would
return it to life. The core contains an extradimensional interior where
those it has captured may be found.

**Entering.** A rescuer can enter through extraordinary planar
transportation, such as the *gate* spell, directed at the core's interior.

**Inside.** Rescuers must locate the soul they are seeking. They cannot
cast spells while inside the core. The interior may contain other
imprisoned souls, including defeated enemies or anyone else the DM
determines remains there. No mandatory geography, chronology,
government, or explanation of the core's origin is provided.

**Returning.** Because the rescuers cannot perform spellcasting in that
interior, someone outside must establish their route back. The rescuers
must reach and use it to depart with the soul.

**After recovery.** A soul successfully extracted from imprisonment can
again be subject to applicable resurrection magic. Extracting it does
not automatically restore the body or waive that magic's usual
requirements.

**Irreversible loss.** If the DM determines that a soul's essence has
been completely consumed, ordinary resurrection and reincarnation
cannot restore it. A DM may choose what traces, memories, remnants,
or former captives remain within the core. The rules do not impose a
universal countdown before that outcome.

The DM may alter the rescue procedure for an individual campaign.
The concrete default of entering the interior, operating without
spellcasting, locating a prisoner, and relying on external aid for
escape communicates the intended risk.

## Implementation boundary

- First-party native content declares the normal and Overdrive ratings,
  current capture radius, involuntary capture policy, and deliberately
  nonnumeric fuel model.
- The Construct operating evaluator requires a source-specific context
  before considering soul power usable. A generic operating check
  without DM-adjudicated fuel state fails closed.
- `PreviewSoulCoreOperation` treats availability as externally adjudicated.
  It calculates **only** normal, Overdrive, or unavailable rated output,
  and delegates actual component availability to the existing network
  evaluator. It does not consume, create, or preserve souls.
- `PreviewAutomaticSoulCapture` identifies installed candidate cores
  within the provided distances, without consulting their active
  power-allocation flags. It does not create or delete a soul record.
- The internal `NativeConstructSoulCustodyLedger` records individually
  identified, host-verified captures, extraordinary extractions, and
  DM-declared final consumption without a numerical capacity or timer.
  Revision checks prevent stale replay. Its records retain previous capture
  episodes when a rescued, resurrected soul is captured again later.
- The authoritative gameplay host must persist these transitions and
  enforce imprisonment-related resurrection restrictions. A preview or
  in-process custody record alone does not alter persisted character state.
  The core's power-sufficiency determination remains independent from
  the list of souls: capturing one does not automatically grant a
  quantifiable energy reserve or imply a specific consumption schedule.
- If several generators share a power circuit, dispatch priority is not
  established by this source; do not invent a fuel-use allocation policy.
- The preexisting manufacturing recipe is unchanged. Its candidate
  ingredient and competency requirements were not rebalanced by
  approval of the core's gameplay behavior.

This preserves the DM's freedom to reveal a vast interior populated by
the consequences of a party's previous adventures without mandating a
fixed fuel schedule, chronology, storage limit, or setting mythology.

No changes to the solar, mechanical, infernal or arcane sources are
authorized by this source approval.
