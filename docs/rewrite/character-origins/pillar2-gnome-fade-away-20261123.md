# Pillar 2 — Gnome Fade Away (2026-11-23)

**Status:** Owner-approved new biological trait, introduced on a draft branch
stacked on PR #208 (High Elf) and its existing preceding branch stack.
Not merged; no changes to `main`.

## Approved design

The existing Gnome Species foundation gains **`trait.fade-away`** as
one new canonical biological characteristic. Forest Gnome and Rock
Gnome inherit it from `species.gnome`; neither requires a second,
Ancestry-local copy.

Adapt *Fade Away*, the Gnome racial feat from *Xanathar's Guide to
Everything*, **without the feat's ability-score improvement**:

- **Trigger:** Immediately **after taking damage**, the Gnome may use
  its **Reaction** to magically become **Invisible**. The triggering
  damage is not retroactively prevented or reduced.
- **Duration:** Until the end of its **next turn**, ending sooner
  immediately after the character **attacks**, **deals damage**, or
  **forces another creature to make a saving throw**.
- **Resource:** **One use per Short or Long Rest**, not Proficiency
  Bonus uses. The resource and Reaction are both required.
- This is a real invisibility effect in the intended gameplay rules,
  not merely Advantage on Hide checks or camouflage.
- This new characteristic is separate from `trait.magical-cunning`,
  which remains a magic-only saving-throw benefit.

The native payload uses existing supported mechanics:
`activation.action = reaction`,
`activation.trigger = immediately-after-you-take-damage`,
`invisibility.endsAt = end-of-your-next-turn`,
`invisibility.alsoEndsImmediatelyAfter = [you-attack,
you-deal-damage, you-force-a-creature-to-make-a-saving-throw]`,
and `uses = {value: 1, recharge: short-or-long-rest}`.
No additional generic mechanic shape is introduced; the current
canonical trait recognizer already supports these structural fields.

## First-party biological price

The provisional price is **4 points** as previously proposed and
explicitly approved as the shared Gnome increment. No existing
biological gene is repriced.

| Selection | Previous | New | Target |
| --- | ---: | ---: | ---: |
| Shared Gnome | 5 | **9** | 17–18 (standalone diagnostic) |
| Forest Gnome | 14 | **18** | 17–18 |
| Rock Gnome | 8 | **12** | 17–18 |

Forest Gnome reaches target without changing its animal speech,
illusion, camouflage or tracking abilities. **Rock Gnome remains six
points below** the target and requires a separate, approved review
of its existing/historical biological characteristics.

First-party price policy changes
`biological-pricing-20261123-v11` → **v12**. Biological gene count:
**93 → 94**; pricing entries: **98 → 99**, including five intrinsic
slots. The first-party allowance remains **18**.

## Implementation and verification boundaries

- Canonical trait and shared Gnome Species reference are authored,
  with source inheritance through existing construction logic.
- Existing native shape grammar recognizes `activation`,
  `invisibility`, and `uses`; no new global invisibility/reaction
  executor was implemented.
- Focused xUnit tests cover direct Gnome/Forest/Rock allocation,
  one inherited `species.gnome` source grant, trigger and ending
  conditions, canonical uses and recharge, and the source-neutral
  `NativeResourceResolution` primitive behavior.
- **These tests do not prove actual gameplay Reaction consumption,
  timing, invisibility state transitions, interruption, or
  authenticated rest resource persistence.** Those are combat and
  character-state integration responsibilities and remain for the
  unified acceptance pass.
- Full .NET test suite and gameplay tests must run independently
  before any merge.

The Rock Gnome review is next; no additional Rock Gnome ability
or history-based feat was approved in this change.
