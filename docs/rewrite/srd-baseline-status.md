# Complete structured SRD baseline — import and reconciliation status

**Imported:** all 90 JSON files from the pinned, unofficial SRD-only 5etools-format
data pack `CoolFireGiant/hewnhero-srd@d06d1dadee357857767b1e4da985df6609509bcf`.

**Location:** `data/reference/srd/structured/`. Exact file names and SHA-256
digests are recorded in `data/reference/srd/structured-manifest.json`.
The original JSON includes SRD 5.1 and SRD 5.2.1 rulebook sections,
creatures, classes, species, feats, spells, items, equipment, actions,
conditions and other structured material.

**Important limitations:**
- This is a faithful full import of the **pinned data pack**, not a verified
  full-text match to both official SRD PDFs. The pack is not an official release.
- Raw source records are preserved separately from the Dorks & Dice native JSON
  authority. This is **not** a source-first runtime architecture and introduces
  no permanent SRD importer.
- `data/reference/srd/reconciliation-inventory.json` is an automated **diff
  inventory**, not a set of approved native rules. It matches source records
  tentatively by entity category, name and parent, and ignores source/page
  metadata. False-positive matches remain possible; identical-looking data
  alone is not a universal equivalence proof.
- The inventory does **not** adjudicate differences by itself. Differing versions
  remain accessible as evidence. Specific decisions are recorded separately
  in `data/reference/srd/reconciliation-decisions.json` when approved.

## First comparisons

| Category | Changed matches | Only 5.1 | Only 5.2.1 |
| --- | ---: | ---: | ---: |
| Skills | 18 | 0 | 0 |
| Core actions | 13 | 7 | 4 |
| Conditions | 15 | 0 | 0 |
| Base classes | 12 | 0 | 0 |
| Subclasses | 8 | 4 | 4 |
| Spells | 317 | 2 | 22 |
| Monsters | 285 | 38 | 49 |
| Items | 396 | 97 | 68 |

These are structural/content differences, not a determination that each record
has distinct gameplay mechanics. Only 20 item-type records were identical
after limited cosmetic normalization; other records commonly vary in text,
references or mechanics.

Both SRD rulebook document trees have been preserved in full. They have
different top-level chapter organizations (45 sections in the 5.1 pack book
versus 16 in 5.2.1), so chapter-count comparisons are not proof of missing
text or gameplay equivalence.

## Native reconciliation rules

1. The native JSON is the Dorks & Dice rules authority. Do not add
   edition/source markers or legacy import names as required native runtime
   state. Source comparisons belong in `data/reference/` and future import
   mappings in `data/conversions/`.
2. Begin with rules identical in meaning, then rules whose differences are
   explicitly resolved in existing Dorks & Dice decisions. Check before
   applying a 5.1 or 5.2.1 behavior as a default when the decision is absent.
3. Do not merge entities just because names match: preserve genuine subtypes,
   procedures, and distinct mechanical effects. Apply the agreed subtype
   folders (base/sub/prestige, species/subspecies).
4. Attribute SRD 5.1 and SRD 5.2.1 correctly under CC BY 4.0. Keep the
   third-party pack provenance separate from claims of official fidelity.
5. No engine, DB, deployment, source parser, or public/tester exposure is
   authorized by this source-copy step.

## Outstanding milestone

The complete imported source pack is available for deterministic review.
**Native reconciliation of the entire pack is NOT complete** and must not
be described as completed merely because the files have been copied.

## Approved reconciliation: Exhaustion

The Owner explicitly selected **SRD 5.1 Exhaustion** as the single native
Dorks & Dice Exhaustion condition. This rule is in `data/native/conditions.json`:
six cumulative levels with tier effects, disadvantage on ability checks
(level 1), half Speed (level 2), disadvantage on attacks and saves (level 3),
half maximum HP (level 4), Speed 0 (level 5), and death (level 6).
It retains SRD 5.1's one-level recovery from a long rest **provided food
and drink were consumed**, and one level removed when raised from the dead.
Effects that remove exhaustion reduce levels by their stated amount.

The SRD 5.2.1 per-level numerical penalties are **not** native defaults.
The decision/provenance is stored under `data/reference/srd/`, separately
from the native condition; source-edition labels are not required at runtime.

This is an accepted **mechanical ruling**, with data-only validation. The new
runtime has not yet been implemented, so these tests establish the serialized
contract, not execution of effects.

## Reconciled shared conditions

The following conditions have matching operational consequences in both SRDs,
allowing one native definition without preferring an edition:

| Condition | Native effects |
| --- | --- |
| Deafened | Can not hear; automatically fails checks requiring hearing |
| Frightened | Disadvantage on attack rolls and ability checks while the fear source is in line of sight; can not willingly move closer to that source |
| Poisoned | Disadvantage on attack rolls and ability checks |
| Restrained | Speed 0; attacks against the creature have Advantage; its attacks and Dexterity saving throws have Disadvantage |

The definitive normalized records are in `data/native/conditions.json`. This
reconciles mechanical **consequences**, not spell durations, applications, or
related creature effects. Approval-by-equivalence is documented in
`data/reference/srd/reconciliation-decisions.json`. The condition records
have no source-edition dependency.

**Unresolved conditions from the imported pair:** Blinded (its native
attacker-side Disadvantage is previously decided, but the complete defensive
interaction still requires care), Charmed, Grappled, Incapacitated,
Invisible, Paralyzed, Petrified, Prone, Stunned and Unconscious. Some of
these contain identical core effects and additional differences, or inherit
the disputed rules of another condition; they remain staged, rather than
partially claiming a completed native condition.
