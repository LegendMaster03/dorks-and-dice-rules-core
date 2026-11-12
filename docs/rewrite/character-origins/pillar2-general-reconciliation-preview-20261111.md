# Pillar 2 — generalized characteristic reconciliation preview

**Date:** 2026-11-11  
**Status:** Read-only, provisional design evidence. This is **not** the default inheritance resolution system, an authoritative grant/stacking policy, a balancing mechanism, or a finished character profile.

**Tool:** `tools/character_origins_reconciliation_review.py`  
**Tests:** `tests/test_character_origins_pillar2_reconciliation_review.py`

## Purpose: from identity evidence to proposed-characteristic interactions

The [universal identity layer](pillar2-characteristic-identity-boundaries-20261111.md) preserves the mechanical definition, source occurrence, contributor offer, provenance, and the distinction between **offered** versus **actually granted** characteristics. The [curated native review](pillar2-curated-native-identity-review-20261111.md) identifies a few literal definition matches with drift validation. The new preview evaluates **which relationships arise when a user proposes specific offers for one character**.

A proposed choice **does not** become an authorized Origin grant just because the tool can look up its evidence. This proposal evaluator accepts only ephemeral candidate IDs from an existing read-only identity projection. It does not create an Origin, make default choices, assign grant IDs, spend an allocation budget, or decide compatibility.

## Source-neutral relationship classifications

| Proposed characteristics | Review outcome | What it deliberately does not do |
| --- | --- | --- |
| Two intrinsic values for the same slot, same value | `same-intrinsic-slot-and-value` | Add values or create duplicate effects |
| Different values for one intrinsic slot | `competing-intrinsic-slot-values` | Choose a winning Species or automatically replace a value |
| Different options within a single native authored choice occurrence | `alternatives-in-one-authored-intrinsic-choice` | Misdiagnose a standard Human size choice as a cross-Species conflict |
| Same source-qualified trait with identical parameters offered by multiple contributors | `repeated-source-trait-same-configuration` | Multiply a trait just because it was offered twice |
| Same source-qualified trait with different parameters | `repeated-source-trait-different-configurations` | Link or arbitrarily choose parameters, including Breath Weapon type/area |
| Different source traits with an explicit *provisional* same mechanic-definition ID | `explicit-identity-mapping-same-configuration` or `...different-configurations` | Treat the mapping as automatic stacking, suppression, or trusted equivalence |
| Different native occurrences whose complete authored content has been validated as identical | `identical-authored-content-same-configuration` or `...different-configurations` | Promote literal similarity to automatic semantic equality or a new grant |
| Different definitions in one explicitly mapped trait concept | `related-trait-concept-distinct-definitions` | Assume they are upgrades or interchangeable |
| Traits sharing one recognized mechanical component | `shared-component-possible-overlap` | Merge whole traits based on a shared field |
| Matching local IDs without a trusted broader identity | `same-local-trait-id-unknown-equivalence` | Conflate names and complete mechanics |
| A trait and intrinsic representing the same kind of movement speed | `trait-and-intrinsic-movement-same-value` or `...different-values` | Double a speed or silently override disagreement |

**Unrelated pairs** yield no pair relationship. That does **not** certify full compatibility: other unmodeled effects, prerequisites, global allocation limits and missing authoring policies still require review.

## Examples from the currently authored corpus

- **Red + Red Dragonborn:** Both contributors can offer the same source-qualified Breath Weapon with the same configured values. The tool identifies a repeated offer, **not two actual character grants**.
- **Red + Bronze Dragonborn:** Both can offer the ordinary Breath Weapon with differing configurations. The existing approved *ability-level* mixed-Heritage rule may independently combine eligible damage type and **complete** area; this generic pair review does not change or select that ability parameter policy.
- **Human:** One source offers the Small or Medium intrinsic size alternatives. These are detected as an authored choice, not an impossible mixed Species.
- **Human + High Elf:** Creature type is an intrinsic slot with differing offered values. Whether the final character may select either, or requires a stronger rule, is still a separate decision.
- **Orc + Cloud Goliath:** Both have Powerful Build under a shared provisional conceptual family but with different complete mechanics. The preview highlights their relationship **without declaring an upgrade**.
- **Orc + High Elf:** The provisional identity mapping can express matching Low-Light Vision definitions with separate contributor offers. No double grant follows from the match.
- **Black + Bronze Dragonborn:** The curated literal Amphibious group demonstrates identical authored content from different ancestry occurrences with independent provenance.
- **Red Dragonborn:** Climb speed is both a native intrinsic property and a complete trait. The preview highlights a same-value mirror, **not two additive speed grants**.
- **Verified nested contributor:** The previously manifested trait is offered with its immediate profile and ancestor provenance, not the full package of historical contributor Species.
- **Custom proposed characteristic:** The identity layer can carry explicit custom-policy provenance; the general relationship engine does not authorize it or require an invented parent.

## General mechanism before a default mixing policy

The **three-stage Origin workflow** remains:

1. **Deconstruct and assemble:** Gather source-qualified offers with all authored behavior, characteristic identity, eligible parameter configurations, and contributor evidence.
2. **Reconcile and manifest:** Review **explicitly proposed** selections and relationships; loop between selections and general constraints until choices and dependencies are resolved or marked for DM adjudication.
3. **Reconstruct:** Produce one current inherent-characteristic profile, separate from historical contributors or independently chosen Background/Upbringing, only when an authorized general rules/selection policy is defined.

The new preview specifically covers an **early part of Stage 2**. It shows what questions need decisions, not how the final rules should always answer them.

## Policy questions that require separate owner review

1. **Equivalent grant** — when should two equivalent offers yield one effective ability, two independent grants, or a replacement?
2. **Partially overlapping abilities** — can one constituent effect coexist with another? Is one an upgrade, substitution, or mechanically separate effect?
3. **Same intrinsic slot** — should standard child-source values replace their Species parent values, while mixed-contributor values require explicit player choice? The Wood Elf 30/35 walking-speed case illustrates why these may require different policies.
4. **Dependent choices** — when a trait appears multiple times, do embedded choice slots remain one shared choice or distinct choices?
5. **Allocation and balance** — what restrictions apply to inherited, partially custom, and fully custom selections, and which remain configurable for playtesting?
6. **Selection authority** — how is an explicit DM exception validated without losing its provenance or granting unrelated effects?

The **identity registry** must not silently implement answers to these questions. A complete future reconciliation result needs selected characteristic instances, policy identifiers, resolved/remaining dependencies, source-qualified grant provenance, and explicit unresolved status.

## Acceptance examples

The included tests cover same and different intrinsic values, within-source authored choice alternatives, repeated and differently parameterized Breath Weapons, literal or mapped same-definition evidence, concept-level Powerful Build differences, partial component overlaps, mirrored movement, unrelated traits, nested contributors, malformed proposals and complete provenance conservation.

All outputs are `draft-reconciliation-evidence-only`, `not-authorized`, `traitsGranted: false`, and `balance: not-evaluated`. The candidate identifiers are **ephemeral preview indices** and must not be persisted as approved grant identifiers.

### CLI

```bash
python3 tools/character_origins_reconciliation_review.py \
  --contributors ancestry.dragonborn.red ancestry.dragonborn.bronze \
  --select evidence-3 evidence-14

python3 -m unittest discover -s tests \
  -p 'test_character_origins_pillar2_reconciliation_review.py' -v
```

The illustrative `evidence-3`/`evidence-14` choices above are **preview positional IDs**, not fixed public contract IDs. They may refer to different candidates as native content evolves; users should inspect a current identity projection first.

**Boundary:** No native gameplay trait changes, Rules Core runtime modification, special-case mixed-Ancestry resolver, general grant authorization, or main-branch merge is required or implied.
