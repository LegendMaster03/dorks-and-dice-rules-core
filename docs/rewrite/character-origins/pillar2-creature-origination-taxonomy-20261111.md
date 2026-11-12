# Pillar 2 — creature types and origination methods

Status: Selected native data-only scope for creature-type consolidation and initial
origination-method defaults. This does not implement character generation or resolve
Species-specific reproduction.

## Creature-type taxonomy

The Monstrous Humanoid and True Elemental categories are retired. The former
was a Monstrosity descendant not needed for the current native bestiary:
harpies, medusas, and minotaurs are already assigned Monstrosity in their
individual definitions. The historical Monstrous Humanoid defaults (d8 Hit Die,
Darkvision 60 feet, simple weapon competency, strong Reflex and Will saves)
must be reviewed when converting any additional source creatures. They are not
automatically awarded to every former member of that historical category.

True Elemental formerly added no need to breathe under Elemental. Removing the
intermediate category does NOT confer this property on all Elementals. A
creature with an elemental body must have any relevant physical characteristics
authored at the creature, Species, or later more specialized level.

Genies remain Elementals. The presence of elemental magic, a particular
elemental body, or a particular manifestation process does not require its own
effective creature type. The native Djinni and Efreeti stat blocks retain
their existing effective classifications and statistics.

Existing creature type targeting remains authoritative and is not inferred
from prior inheritance ancestry. Monster/NPC type defaults do not become
automatic playable Species grants.

## Type-linked origination defaults

The selected native contract is in
data/rulesets/first-party-001/native/rules/creature-origination.json.
Creature type definitions have an optional originationMethods list. Absent
means inherit the nearest defined list; explicit lists replace the inherited
list, including a multi-method list. Individual Species/Ancestries can make
express modifications. The generic Creature default is empty; this is a lack
of an established method, not an absolute prohibition.

Examples:
- Fiend: biological reproduction OR spawning.
- Plant: plant reproduction, separately from generic animal-like reproduction.
- Construct: assembly, with exceptions possible.
- Elemental: elemental manifestation; a Genie could have a specific biological
  reproduction permission without granting it to every Elemental.
- Ooze: division.
- Undead: reanimation or transformation.
- Humanoid, Beast, Dragon and relevant inherited branches: biological.

The method is chosen for the character, not deduced from its creature type
after the fact. Methods can require arbitrarily many contributor roles.
A creator, material component, ancestor, blueprint and animating force
are different kinds of inputs. None grants its own Species traits merely
by being involved in creation.

## Rite of Rebirth as a post-origination method

Rite of Rebirth is included in the same method catalog, but is a specialization
of Transformation, **not** a new individual being created and not a default
method inherited by all creature types. A qualifying specific Rite authorizes
the operation regardless of the creature's type-default method list. Its
presence in the catalog is not a universal right to perform a Rite.

The Rite replaces the entire current Species/Ancestry configuration, its
associated mechanical grants, and its Species-derived effective creature type.
It does not merge new Ancestry candidates with old ones. Independent grants,
Background, Upbringing, and historical contributors remain. Subsequent Rites
replace the then-current configuration instead of stacking previous grants.

A native Ancestry may explicitly declare `acquisitionMethods` as
[`"rite-of-rebirth"`]. That makes the Rite its normal acquisition gate,
excluding ordinary initial character creation and mixed-Heritage candidate
selection. An explicit DM/table override can still authorize other access.
Unannotated existing Ancestries are not newly restricted by this policy.

The exceptional Platinum, Fivefold Chromatic, and Ruby Dragonborn concepts
have such a **design-intended** Rite gate, with the specific category leader's
direct participation under the approved design. They have not yet become
native Ancestry definitions, so this change does not fabricate their records,
assign their mechanics, or make the Rite executable.

See [the Rite specification](pillar2-rites-of-rebirth-20261111.md) for the
approved complete-replacement semantics and exceptional acquisition decisions.

## Human-readable compatibility procedure

1. Choose a type-supported method, a specific authored ritual/transformation, or an explicitly DM-authorized exception.
2. Identify the inputs and contribution roles required by that method.
3. Check applicable process restrictions. Different creature types alone do
   not make biological contributors incompatible.
4. If the method is unsupported or explicitly restricted, the DM may
   authorize an exception with a recorded table decision.
5. Reconcile and manifest the resulting character's characteristics using
   the general Pillar 2 engine. Approval of origination is not approval
   of mechanical grants, effective creature type, or an entire Species package.

Unknown compatibility is not treated as a prohibition. The method list does
not simulate genetics, nor does it create a per-pair compatibility table.

## Follow-up work

- Audit creature assignments that historically depended on the retired types
  before changing any additional native bestiary records.
- Author process-specific contributor/input conditions for actual species.
- Make the C# Rules Core origin engine interpret these defaults; the current
  content-only rule intentionally does not claim execution support.
- Exercise biological, plant-reproduction, construct-assembly and Fiend
  spawning paths in regression tests, including DM-authorized exceptions.
- Implement Rite-specific acquisition checks and full Species/Ancestry grant
  replacement once the general Origin engine is executable.

The working source copy preserves the selected type hierarchy for the
existing mirror comparison. No main branch or existing PR branch is modified.
