# Five-Pact Warlock reconciliation — expanded owner-approved scope

**Decision:** 2026-10-25. **Status:** all five Pact designs are now within the **Pact reconciliation workstream**; this is not approval of unreviewed native effects, an executable feature, or a non-SRD Patron implementation.

## Edition classification

- The original **D&D 3.5 Warlock** did **not** have 2014-style Pact Boons. Fiendish/fey/celestial origin is a **Patron/source** analogue in native Dorks & Dice.
- The named **D&D 4e Eldritch Pacts** (Fey, Infernal, Star, etc.) classify the **power source / Patron-like identity**, not the native weapon/familiar/book/talisman/Seeker-specific Pact Boon form.
- **D&D 5e 2014 / 2024** supplies Blade, Chain and Tome; **Tasha's Cauldron of Everything (2020)** supplies official 2014-era **Pact of the Talisman**; **Unearthed Arcana: The Faithful (2016)** supplies the **unpublished playtest Pact of the Star Chain**.
- Both added Pacts are **outside SRD 5.1 and SRD 5.2.1**, and their rules text is not licensed as CC SRD material. The SRD-only source inventory and its option counts (42 union names, 35 2014 selections, 28 2024 invocations) must remain **historically unchanged**. Independently written metadata/comparison is *not* an SRD import.

## Five candidates in the Pact pillar

| Pact | Provenance and source category | Historical baseline | Native disposition |
| --- | --- | --- | --- |
| **Blade** | SRD 5.1 Pact Boon, SRD 5.2.1 Invocation | Conjure/bond weapon; 2024 CHA attacks and damage choice | **Approved native 2024 base + mundane weapon binding + intrinsic 5th-level second weapon attack.** Remaining 2014 ritual/storage questions are open |
| **Chain** | SRD 5.1 Pact Boon, SRD 5.2.1 Invocation | Pact familiar | **Intrinsic 5th-level familiar extra attack approved**, but base familiar/command conversion unresolved |
| **Tome** | SRD 5.1 Pact Boon, SRD 5.2.1 Invocation | Book of Shadows; ritual/cantrip options | **Intrinsic 5th-level Animate Dead/Haste/Call Lightning** (each free 1/Long Rest) approved; base Tome conversion unresolved |
| **Talisman** | *Tasha's Cauldron of Everything* 2020, officially published 2014-era Pact Boon, non-SRD | Wearable/transferable talisman: on failed ability check, wearer adds **1d4**, proficiency-bonus uses/Long Rest; recreate via 1-hour ceremony, previous talisman ends | **New approved reconciliation candidate**, not yet an approved native definition. Evaluate protective/teleport-related Invocation family separately |
| **Star Chain** | WotC *Unearthed Arcana: The Faithful*, 2016; **playtest**, never published as an official Pact Boon; non-SRD | Seeker-linked chain of starlight with seven motes; carries **Augury as a ritual without a spell-known expenditure** and **advantage on an Intelligence check once per Short/Long Rest**; 1-hour replacement ceremony | **New approved reconciliation candidate**, **Seeker-Patron-restricted in original source**; no level-5 advancement approved or playable Fiend access |

**Source level vs native level:** Both Talisman and Star Chain were **level-3 Pact Boons in their original 2014-era class progression**. Native Dorks & Dice already grants a **dedicated first Pact at Warlock class level 1**, not from an Invocation selection. Evaluate all five under that approved **level-1 acquisition rule**, while retaining historical source levels.

## Talisman — additional official 2014 options to reconcile

*Tasha's* has three related **optional Eldritch Invocations**; they remain **Invocation-pillar choices**, not automatic Pact features:

1. **Rebuke of the Talisman** — Pact of the Talisman required; reaction to harm and push a qualifying attacker of the talisman wearer.
2. **Protection of the Talisman** — Warlock 7+, Pact of the Talisman; saving-throw protection for the wearer after a failure.
3. **Bond of the Talisman** — Warlock 12+, Pact of the Talisman; teleportation between wearer and Warlock within relevant planar and use restrictions.

These are non-SRD reference/reconciliation names only. Determine costs, timing, uses, and eventual intrinsic Pact advancement **without quietly auto-granting all three**.

## Star Chain — explicit Patron/Pact restriction fixture

The 2016 official Wizards of the Coast **playtest** source says a character **dedicated to the Seeker** could select Star Chain instead of the conventional Pact Boons. The source further states that the chain's exact form might differ based on the Patron. This is a positive example of **a specific Pact having a Patron eligibility requirement**.

**Source-faithful initial native eligibility rule for analysis:** `pact = star-chain` requires a **Seeker-linked Patron relationship**, subject to standard Patron acceptance and any future individually approved alternative. **Do not globally treat a Patron's willingness as a waiver of an explicitly recorded type/trait requirement.** Whether other lore-appropriate patrons may grant a variant Star Chain is a **separate future decision**, not granted by this scope expansion.

**Crucial vertical-slice interaction:** **Fiend remains the only playable Patron** during the current first Patron implementation. Therefore **Star Chain is in scope for data/reconciliation and non-playable synthetic eligibility tests, but is not a valid Fiend+Star Chain default build**. A functional Star Chain playtest needs an approved **Seeker Patron, other explicit eligibility extension, or separately authorized test fixture**. Merely adding a Seeker fixture must not create a playable new Patron or allow an unapproved second Patron/Pact.

**Tome overlap:** `Augury` and Intelligence knowledge overlap Tome's spell/ritual identity, but that does not justify automatically combining Star Chain with Tome or prohibiting both in the future. Compare the Seeker restriction, artifact identity, replacement ceremony and distinct upgrades before deciding whether it remains a separate native Pact. Its status as a **fifth reconciliation option** is approved; its final mechanically distinct implementation remains undecided.

## Intrinsic Pact progression after adding two options

**Only Blade, Chain and Tome have explicitly approved level-5 Deepened-Pact-like advancements.** **Do not copy those level-5 grants to Talisman or Star Chain**, invent a new 5th-level grant, or make Pact of the Talisman/Star Chain baseline weaker/stronger by fiat.

The architecture now requires a review of **each Pact's functional role and advancement through Warlock levels 1–20**, including what belongs to the Pact versus optional Patron-restricted Invocations.

Proposed, *unapproved* directions:
- Talisman: protective failure mitigation, scalable support, ally mobility, bearer-based resource allocation. Audit Rebuke/Protection/Bond so they remain optional unless owner directs otherwise.
- Star Chain: divination, intelligence/revelation, star/light guidance, forecasting; examine shared `Augury` / Tome ritual handling, and how Seeker-sourced powers coexist with Patron identity.

## Implementation and acceptance boundary

- Five separate Pact identities in the reconciliation catalog, **one dedicated first Pact selection at Warlock level 1**. Other-Pact acquisition gates remain deferred.
- Preserve all original SRD categories and counts; **do not insert non-SRD rules into the SRD inventory `options` array**.
- Retain **Fiend** as the only currently playable Patron. Talisman is not inherently Patron-restricted in its source; Star Chain explicitly is.
- Prohibit invalid Seeker-only Star Chain selection on Fiend builds until owner authorizes some Seeker-linked exception or Patron inclusion. Model source-eligibility and individual-Patron willingness independently.
- Preserve closed approved Fiend decisions and separate Patron/Pact/Invocation grants. Invocations are bound to one actual Pact and removed from use (not acquisition records) when it is broken.
- Do **not** automatically give Talisman or Star Chain the BG3 Deepened Pact level-5 benefits that were approved for Blade, Chain and Tome.
- Guided and custom builders use the same entitlements and compatibility checks.
- The ongoing ten-Patron × three-Pact research remains **historical**; a new five-Pact matrix is a **separate review extension**, not a validated 50-pair compatibility claim.

## Sources

- WotC official 2016 playtest PDF, *Unearthed Arcana: The Faithful*: https://media.wizards.com/2016/dnd/downloads/UA%20Non-Divine%20Faithful%20SFG.pdf
- *Tasha's Cauldron of Everything*, 2020: Pact of the Talisman and Rebuke/Protection/Bond Invocation descriptions (copyrighted, not included in SRD 5.1/5.2.1)
- WotC official 2024 Warlock revision overview: https://www.dndbeyond.com/posts/1756-2024-warlock-vs-2014-warlock-whats-new
- D&D 4e *Arcane Power* Warlock Pact source families — background only (not Pact Boon counterparts)
- Related native decisions: `docs/rewrite/warlock-pact-pillar-reconciliation.md`; `docs/rewrite/warlock-patron-pact-feature-allocation-study.md`.

**No new playable feature, Patron subclass or SRD text is imported by this study.**
