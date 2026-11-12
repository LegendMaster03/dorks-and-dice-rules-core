# Pillar 4 — Lycanthropy reconciliation and first progression experiment

**Date:** 2026-11-12
**Ruleset:** `first-party-001`
**Status:** Source comparison, owner-selected architectural boundaries, and **unapproved** werewolf progression candidates. This is not a playable character option, final numerical specification, native JSON, or an executable Rules Core feature.
**Companion evidence fixture:** `pillar4-lycanthropy-progression-experiment-20261112.json`

## 1. Approved boundaries

1. **Persistent supernatural nature is additive.** Acquiring lycanthropy does not permanently replace the subject's underlying Species/Ancestry. Its origin history, unrelated Background, Upbringing, Class, and independently sourced grants remain attributable to their respective sources.
2. **A change of form is a temporary Species/Ancestry replacement.** While the lycanthrope assumes a different form, that form supplies a **temporarily effective Species/Ancestry configuration**, not merely cosmetic appearance or an additional simultaneous complete Species package. When the form ends, the subject returns to the currently authoritative baseline configuration. The original baseline is not deleted or rewritten by the form change.
3. **Loss of control and transformation triggers are setting-dependent.** No compulsory full moon, control check, loss of player control, alignment change, or forced transformation rule is approved for all lycanthropes. Specific settings, curses or variants can author these independently; some transformations may be voluntary and controlled.
4. **Individual progression rules are required.** The general framework supplies current-state and transition operations, but werewolf, werebear, wererat and other supernatural natures may use distinct progression schedules and mechanisms. No uniform three-level progression or universal class is approved.
5. **Only reconstructive supernatural outcomes qualify for Pillar 2's `supernatural-reconstruction` origination reference.** Ordinary additive lycanthropy and reversible form changes do not create a new individual or invoke a new origination. Their temporary active configuration replacement must use common Character Origins change/reconstruction operations.
6. **DM overrides remain universal** across acquisition, forms, triggers, eligibility, retained grants, creature classification, control, progression, and restoration.

### Important correction to the earlier additive/reconstructive description

An additive supernatural **condition** may itself create a temporarily effective replacement of Species/Ancestry **while a form is active**. Thus, *persistent outcome* and *temporary form operation* are different axes. Additive does not mean all original Species traits remain mechanically active in every form; those interactions depend on the specific form rules. Conversely, changing forms does not make the supernatural condition permanently reconstructive.

## 2. Historical comparison and evidence

| Source | What is actually present | Reconciliation significance |
| --- | --- | --- |
| 3.5e SRD lycanthrope template | Natural versus afflicted lycanthropes; base and animal/hybrid forms; additive template, animal Hit Dice, silver damage reduction and Control Shape; its setting assumes full-moon/injury triggers and possible involuntary loss of control | Useful model for distinct form and acquired/natural sources, but its specific control, alignment, level-adjustment and triggers must not become Dorks & Dice universal rules |
| 3.5e *Savage Progressions*: Werewolf Template Class | Optional three template-class levels: wolf alternate form, then hybrid and additional benefits, then further natural-lycanthrope features; optional animal-class progression separated from the supernatural template | Concrete historical precedent for staggered form/feature access; **not** a requirement to take class levels or receive animal HD |
| SRD 5.1 werewolf | Humanoid (human, shapechanger), base/hybrid/wolf forms; transformed stat block retains almost all statistics aside from form-dependent items; immunities and bite curse | Source comparison for what to preserve or alter, not a playable template with identical PC grants |
| SRD 5.2.1 werewolf | Medium or Small Monstrosity (Lycanthrope), human/hybrid/wolf forms; form-specific size, movement, bite, language/gear, and a curse with an explicit DM-control consequence at a triggering HP event | Reconciles the existing native **monster** definition, **not** a universal rule that an afflicted player character must leave player control |

**Sources:** 3.5e SRD https://www.d20srd.org/srd/monsters/lycanthrope.htm ; WotC historical *Savage Progressions* reference indexed at https://new.dndtools.org/templates/werewolf ; SRD 5.1 werewolf https://www.dndbeyond.com/sources/dnd/basic-rules-2014/monster-stat-blocks-w ; SRD 5.2.1 werewolf https://www.dndbeyond.com/monsters/5195267-werewolf ; license/baseline https://www.dndbeyond.com/srd .

**Repository evidence:** Existing selected-native monster definitions for `monster.werewolf`, `monster.werebear`, `monster.wereboar`, `monster.wererat`, and `monster.weretiger` use SRD 5.2.1 data and remain unchanged. They contain no character-specific lycanthropy acquisition, staged progression or form-reconstruction authority. Do not award a transformed PC the full monster's CR, Hit Dice, stat block, action economy, resistances, or allegiance just because its form resembles this monster.

## 3. First *candidate* individual werewolf progression

This is an intentionally minimal **wolf-specific experiment**, not a generic mandatory lycanthropy track. It tests whether the system supports unlocks over time while source and active form are orthogonal. Labels and unlock ordering are provisional and need owner review.

| Candidate expression state | Sample capability or change | What is *not* implied |
| --- | --- | --- |
| Acquired or inherited potential | Verify supernatural source and any dormant/unmanifested condition; original Species/Ancestry stays effective | Automatic curse transmission, moon trigger, wolf form, loss of control, or class levels |
| Wolf expression | A permitted transition temporarily installs a **wolf-form Species/Ancestry configuration** and restores the baseline when it ends | Replacement of historical origin, the normal wolf monster's entire stat block, or an involuntary shift |
| Hybrid expression | Another eligible form supplies a distinct temporary **hybrid Species/Ancestry configuration** and permits the corresponding return operation | Universal hybrid progression at a specified total character level or sharing all features with the wolf form |
| Further expression (optional) | Individual optional features may advance through separately authored prerequisites, events, time, supernatural practices, or character level | Mandatory transmission, immunity, damage reduction, control progression, extra animal HD, or 3.5e level-adjustment |

**Do not encode the rows as fixed numbered levels.** Source *Savage Progressions* contains staged unlocks, but an individual setting may choose different starting access, sequencing or no gradual awakening. The prototype fixture uses illustrative state labels only. Acquisition source (`inherited`, `afflicted`, `bestowed`, etc.) may influence options where specifically authored; neither it nor form access alone determines agency.

### Independent state dimensions

- **Underlying individual/origination:** unchanged historical person, current baseline Species/Ancestry and their verified grants.
- **Supernatural condition:** whether this werewolf influence is present, latent/expressed, suppressed, or removed; its authentic source event.
- **Expression/development:** eligible individual supernatural features or forms and the prerequisites that unlocked them.
- **Currently active form:** base, wolf, hybrid, or other expressly authored form. A non-base form temporarily replaces the effective Species/Ancestry; it does **not** rewrite the stored baseline.
- **Trigger/agency policy:** authored by the selected campaign, curse or supernatural source; can specify voluntary or compelled events, and separately whether player, DM or another authority controls actions. **No defaults are imposed here.**
- **Other earned grants:** independently sourced Background/Upbringing/Class grants are not deleted by the form swap; usability and compatibility in the current form are separately validated.

### Form transition transaction (proposed shared operation)

1. Resolve the current authoritative baseline configuration and the earned supernatural condition. Validate the specific form transition against *effective* campaign and feature rules, including authorized overrides.
2. Verify the requested form's eligibility, known active trigger/agency policy (if any), and source. A form request is not an automatic permission to shift.
3. Construct **one temporary effective Species/Ancestry configuration** for the target form, replacing the baseline configuration *for its duration*. Retain source-linked baseline history and provenance, and record the active form grant/transition.
4. Validate creature-type targeting, anatomy, senses, needs, equipment, spell components and dependent grants against the entire effective configuration. Do not silently retain baseline Species traits the form does not allow, or discard independently granted class/background benefits because they resemble Species traits.
5. On an authorized reversion, remove the temporary configuration and **re-resolve the current baseline from authoritative sources**, not a stale captured statistic snapshot. Apply any legitimate changes made while transformed (level-up, equipment, damage, newly earned grants or a later permanent reconstruction) according to their source rules.
6. If the baseline was permanently replaced while the temporary form was active, the temporary form **must not resurrect the superseded Species/Ancestry** on exit; unresolved interaction is reported for adjudication instead of silently restoring obsolete state.

Form duration and whether death, unconsciousness, dispelling, external polymorph, rest, or forced removal causes reversion are left to the particular rules or campaign. None is assigned a universal automatic result here.

## 4. Contrasting acceptance cases for the shared framework

1. Human acquires a dormant werewolf influence: baseline Human remains effective with no invented form features.
2. Human with authorized wolf expression changes form: effective Species/Ancestry becomes the authored wolf-form configuration; the underlying Human baseline remains available for restoration. Reversion restores that baseline.
3. Elf with Fey targeting eligibility transforms into a wolf: the form's explicit creature classification and retained trait rules govern targeting; neither Fey and Beast simultaneously nor all Elf traits are automatically granted.
4. A setting permits entirely voluntary transformations and retains player agency: no moon schedule, skill check, loss of control or change in alignment is inferred.
5. A different setting compels a shift under a specified event but retains player agency: **triggering a shift and controlling actions are separate fields**.
6. A third setting requires a control check or authorizes DM control: only that explicit rule changes agency. It must remain distinguishable from the 2024 **monster statblock** DM-control condition.
7. A baseline changes while a form is active: reversion reconstructs from the latest valid baseline and does not restore superseded Species grants.
8. A hybrid form becomes available later: this unlock does not alter the current base form or retroactively grant all hypothetical werewolf powers.
9. Lycanthropy ends: remove only the supernatural condition and its sourced grants; do not roll back unrelated Class or Background history. Reversion behavior if removal happens during an active form requires specific rule/adjudication.
10. A DM authorizes a new form or overrides its conditions: the result is scoped to the authorized character/campaign and does not amend published first-party definitions.

## 5. Consequences and owner decisions still open

- **Werewolf progression content:** Which abilities should appear at acquisition, wolf expression, hybrid expression and later development? Is there a useful standard path, or should source-specific grants determine all stage access? Do not finalize numbers or labels yet.
- **Source and transmission:** Whether natural-born and afflicted cases differ mechanically, whether an afflicted subject can transmit the condition, and by which method. Historical rules disagree across editions.
- **Effective creature classification:** The repository monster has Monstrosity type and Lycanthrope tag; the original species may have Fey, Dragon, Humanoid or another type. The PC's active form and targeting eligibility require an authored first-party decision.
- **Scope of form replacement:** Which baseline traits, physical intrinsic values, languages and ongoing effects can survive form replacement? Do not infer an answer solely from names like Wolf, Elf or Lycanthrope.
- **Agency and triggers:** Which setting modules or authored supernatural sources select these rules? No universal moon, alignment or DM-control policy.
- **Balancing:** How staged supernatural powers compare with normal characters, without mandatory template class levels, animal HD or level adjustment. No unrestricted free power track has been approved.
- **Cure/suppression/recovery:** Individual effects, eligible conditions, and reversibility are not standardized by this initial experiment.
- **Interaction with permanent reconstruction and other polymorphs:** Priority/reversion when the underlying baseline or temporary effect changes during a form transition must remain unresolved until modeled and tested against Pillar 2.

**Implementation boundary:** This experiment is read-only design. The shared C# Character Origins runtime is not yet capable of the above verified temporary-form replacement and reversion. Do **not** wire this prototype directly into Character Sheet, invent unverified grants, or alter the five werecreature bestiary records to simulate playable progression. The proposed operations belong in shared Rules Core architecture after a cross-check against reconstructive cases such as ceremorphosis.
