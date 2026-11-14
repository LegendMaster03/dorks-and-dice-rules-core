# Character Origins — Pillar 2 characteristic identity and compatibility evidence

**Date:** 2026-11-11  
**Status:** Read-only evidence and review questions. No characteristic equivalence, inheritance policy, stacking, override, or balance decision is approved by this report.

**Tool:** `tools/character_origins_identity_compatibility_audit.py`  
**Tests:** `tests/test_character_origins_pillar2_identity_compatibility.py`

## Problem

The same Origin characteristic can appear in several source definitions, and separate Origin traits can partially overlap. Those facts must be recognized without either (a) granting the same benefit twice, or (b) accidentally collapsing distinct grants that happen to use the same name, reference, spell, or mechanical field.

Keep the following identities distinct:

| Identity | Example | Meaning |
| --- | --- | --- |
| Source-qualified **occurrence** | `species.orc#trait:powerful-build` | One author's complete documented feature, not a universal effect identifier |
| Shared **mechanic** (when explicitly defined) | `ability.breath-weapon` | Reusable ability definition independent of contributor |
| Mechanical **component** | `effectiveSizeCategoriesHigherForCarryingCapacity` | One part of a trait's behavior; not the whole trait |
| Parameter **configuration** | Breath Weapon Fire + Cone | Values chosen for a particular shared definition |
| **Contributor occurrence** | Two distinct Human contributors | Two sources of eligibility, not two copies of the earned effect |
| Manifested **Origin characteristic** | One chosen Breath Weapon | The character's selected trait instance; its source evidence must be retained |

The current native corpus does not yet provide an approved universal trait registry for every source occurrence. Consequently, this audit **reports evidence** instead of defining automatic mappings or mutating Origin profiles.

## Evidence relationships

The tool reads the same candidate pool as the multi-characteristic manifestation prototype. It reports:

- **Repeated source trait offers** from multiple contributors, separately indicating identical or different parameter configurations; this is never a command to grant multiple instances.
- **Equal referenced rules** across independently sourced traits, e.g., Elf and Orc `rule.low-light-vision`. A common `rule` reference suggests a shared underlying behavior, but does not by itself establish that whole traits or stacking policies are equivalent.
- **Equal local trait IDs** used for materially different complete definitions; they remain separate source-qualified occurrences.
- **Equal spell references** in different feature grants. Drow Darkness and Infernal Tiefling Darkness both reference the spell `darkness`, but the Tiefling's authored always-prepared clause is an additional grant condition.
- **Shared substantive mechanical components** (deliberately limited to meaningful fields, not generic `level`/`uses` metadata). Orc and Goliath Powerful Build share carrying-size equivalence but have different additional benefits. They must not be automatically merged or added together.
- **Identical intrinsic values** offered by multiple contributors. A 30-foot movement speed from Human and Orc does not mean 60 feet.
- **Competing intrinsic values** from multiple source records or contributors. The report does not choose the higher value, most recent source, or additive outcome.
- **Author-written alternatives** from one `sizeChoice` record. Small/Medium offered by Human is a genuine choice, not itself a contradiction.
- **Movement mirroring** between a record-level movement speed and a feature specifying a movement mode, e.g., Red Dragonborn's 15-foot Climb Speed and Natural Climber. Matching values can be one capability expressed in two places; differing values must be investigated, never silently corrected.
- **Low-Light Vision plus Darkvision**. The report preserves both as different source facts and flags coexistence/replacement questions for separate ruling.

The report carries through all deferred/unmapped source evidence from the existing candidate inventory. It does not assume that a currently prose-only trait has no effects.

## Reference examples

### Orc and Goliath Powerful Build

Both features use local trait ID `powerful-build` and provide a carrying-capacity size-equivalence component. Orc also explicitly covers push, drag and lift; Goliath offers advantage on checks to end Grappled. The **whole authored traits are different**.

A future semantic trait registry could reference one shared carrying-capacity submechanic from both definitions while preserving the remaining components and constraints. It should not blindly deduplicate full traits by ID, nor independently grant their shared component twice. **Exactly how partial overlaps combine is still undecided.**

### Elf, Orc and Drow senses

Elf and Orc Low-Light Vision traits both name `rule.low-light-vision`. This is strong evidence for one shared rule reference, but does not authorize acquisition or stacking.

Drow adds a 120-foot Darkvision intrinsic while inheriting Elf Low-Light Vision. The audit records both and asks whether they coexist, replace, or interact. This question concerns **Origin characteristic composition**, not implementing any vision or visibility gameplay mechanics.

### Wood Elf walking speed

A direct Wood Elf source chain contains the base Elf's 30-foot walking speed and Wood Elf's 35-foot walking speed. It is tempting to assume a child record always overrides the ancestor; that would be a new **general** composition rule, which is not established merely by the existing JSON shape.

The audit flags competing intrinsic values without adding them or silently discarding the base value. A future standard preset needs an explicit **effective** speed and an auditable reason for selecting it.

### Dragonborn shared ability with distinct configurations

Red and Bronze offer the **same base** `species.dragonborn#trait:breath-weapon` with different bound parameters. This is one shared compound mechanic with two independently eligible damage-type and area sets. Per the established user decision, Fire/Lightning may combine independently with Line/Cone; provenance is retained.

The audit records repeated source-trait offers as **different parameter configurations**, not as extra Breath Weapon instances. Two identical Red contributors instead produce **same-parameter** offers.

The report does not calculate damage, action costs or use pools; those are fixed authored behavior of the trait, not a Pillar 2 combat task.

## Distinct trait variants versus hypothetical ancestral relationships

The [Rock Gnome reconstruction example](pillar2-gnome-dwarf-rock-gnome-reconstruction-20261111.md) deliberately distinguishes Stonebound Tremorsense (Dwarf) from Earthbound Tremorsense (Rock Gnome), even though both are members of the same sensory family. A possible historical origin in a Gnome–Dwarf population may explain why such a variant exists **without** implying that two present-day contributor records automatically generate it.

For future rule design, **mechanical-family membership**, **variant identity**, **historical/population relationship**, and **direct Heritage eligibility** must be independent. In particular:

- Same `sense: tremorsense` can justify a shared mechanical vocabulary, not automatic feature equivalence.
- A narrative hypothesis about population ancestry is not a contributor occurrence, an inheritable parameter, or proof of a particular individual's ancestry.
- A distinct established trait variant can be offered by its actual source without requiring a complete ancestral recipe.
- Any rule authorizing derived traits from specific contributors would be an **explicit additional rule**, not an implication of the existence of shared components.

A read-only illustrative [hypothesis fixture](pillar2-illustrative-ancestry-relationships-20261111.json) and tests keep that distinction visible. It is **not** Rock Gnome canon or an approved inheritance policy.

## Suggested future identity contract (not approved)

A future typed definition may need separate, explicit concepts of:

- `mechanicId`: authoritative shared mechanic used by a complete trait.
- `sourceOccurrenceId`: which source supplied the specific authored feature.
- `componentRefs`: shared submechanics within the feature.
- `parameterDefinitions`: configurable versus fixed fields; linked/independent eligibility rules.
- `compositionRelation`: explicit independent, replace, coexist, overlap, prerequisite, upgrade, or unresolved relation. These words are review categories, **not** automatic policies.
- `originEvidence`: contributor occurrence IDs and the exact source references, including nested manifested profiles.

A global rule should not automatically use the maximum numeric intrinsic value, pick a more specific ancestry, merge identical local IDs, or add bonuses. Such policies need approval, preferably after reviewing a representative set of standard and mixed configurations.

## Next Pillar 2 decisions and acceptance criteria

1. **Source-to-mechanic identity mapping:** Which known traits genuinely share complete mechanics, which share only components, and which merely look similar? Review repeated rule references and partial-overlap examples first.
2. **Default intrinsic composition:** How should physical size, creature type, walking and other movement modes, and vision resolve when several values are eligible? Separate **the standard direct-selection preset** from what must be permitted for mixed Heritage.
3. **Trait dependencies and modification:** A feature may require another trait, override its value, or share its resource pool. The Origin model needs to preserve and validate those relationships at selection time, without executing effects.
4. **Complete direct standard Origin presets:** Build a profile including all appropriate traits, attributes, parameter selections, and choice entitlements, then compare at the Origin-record level with a same-configuration mixed source.
5. **Multi-generation transmission and balance:** Keep the already discussed latent/manifested transmission hypothesis and optional point/duplicate policies separate until playtest evidence or a clear owner design ruling is available.

No runtime API, gameplay data, Character Sheet UI, or combat evaluator changes are part of this document.

## Inspection commands

```bash
python3 tools/character_origins_identity_compatibility_audit.py \
  --selected species.orc ancestry.goliath.cloud

python3 tools/character_origins_identity_compatibility_audit.py \
  --selected ancestry.elf.wood ancestry.elf.drow

python3 tools/character_origins_identity_compatibility_audit.py \
  --selected ancestry.dragonborn.red ancestry.dragonborn.bronze

python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_*.py' -v
```
