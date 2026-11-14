# Character Origins — mixed contributors and manifested characteristic provenance

**Date:** 2026-11-11  
**Scope:** Pillar 2 only. A read-only, proposed Origin representation for nested Heritage and the distinction between eligibility and manifestation. Not an approved full inheritance policy, trait budget, runtime character record, or gameplay evaluator.

**Prototype:** `tools/character_origins_nested_heritage.py`  
**Tests:** `tests/test_character_origins_pillar2_nested_heritage.py`

## Requirement

Pillar 2 must not assume every Heritage contributor has an unmodified first-party Species/Ancestry. A parent may itself have mixed Heritage, a contributor need not be a biological parent, and the number or relationship of contributors must not be hard-coded into Origin mechanics.

The previously established distinction remains fundamental:

- **Origination** explains how an individual came into existence.
- **Heritage** lists potential sources of inherent characteristics.
- **Manifestation** records the characteristics the individual actually expresses.
- A contributor relationship is **not** a grant of that contributor's complete original Species package.

Backgrounds and Upbringings are independently chosen cultural/life-experience content and **do not** form inherited biological traits.

## Provisional nested-contributor boundary

For the experiment, a contributor can refer to either:

1. A **directly selected native Species/Ancestry definition**, whose authored characteristic candidates may be inspected through its source chain; or
2. A **draft Origin profile**, whose only transferable characteristic candidates come from its **explicitly recorded manifested traits and parameter values**.

The second path is the important new behavior. Once a mixed contributor has manifested a Breath Weapon as Fire + Cone, its children receive *that available configuration* from it, **not** the full union of its Red and Bronze contributors' original Fire, Lightning, Line, and Cone values.

This is a **provisional manifested-only inheritance model** for design/testing. It is not yet an owner-approved universal genetics rule. Whether the system should support explicit **latent transmissible** characteristics that skip manifestation remains a separate design question; the prototype does not infer such latent inheritance by tracing grandparents.

The model therefore avoids automatically reinterpreting an unmanifested grandparent trait as an earned or eligible child trait. It also avoids implementing dominant/recessive genetic outcomes or forcing a biological family tree.

## First- and second-generation example

**First generation:** Red and Bronze Dragonborn contributors.

| Inheritable parameter | Grandparent values | First-generation choice |
| --- | --- | --- |
| Damage type | Fire from Red; Lightning from Bronze | **Fire** |
| Complete area | 30 × 5-foot line from Red; 15-foot cone from Bronze | **15-foot cone** |

The first generation's **draft manifested profile** contains one ordinary Breath Weapon configured as Fire + Cone, with both source occurrences recorded separately.

**Second generation:** that mixed character plus a Human contributor.

The mixed character contributes only its manifested **Fire + Cone** Breath Weapon; the Human contributes no candidate for that trait. The second generation can propose Fire + Cone, but **not Lightning + Cone or Fire + Line** through its mixed parent alone. A fresh direct contributor supplying Lightning or Line could make those values eligible again.

The result remains an *eligible candidate* until explicitly included in the new character's own manifestation. No automatic acquisition occurs.

**Third generation:** A second-generation profile explicitly manifesting Fire + Cone can be used as a contributor again. The source trace retains each generation's immediate contributor occurrence and the earlier provenance, while presenting the parent as **one** candidate source.

## Draft profile structure

This study uses a small, explicitly provisional record:

```json
{
  "profileId": "mixed-parent",
  "origination": {
    "description": "Freeform narrative; independent of the trait sources"
  },
  "contributors": [
    {
      "contributorId": "red-grandparent",
      "selectedSourceId": "ancestry.dragonborn.red"
    },
    {
      "contributorId": "bronze-grandparent",
      "selectedSourceId": "ancestry.dragonborn.bronze"
    }
  ],
  "manifestedTraits": [
    {
      "mechanicId": "ability.breath-weapon",
      "definitionOccurrenceId": "species.dragonborn#trait:breath-weapon",
      "parameters": {
        "damageType": "fire",
        "area": {
          "shape": "cone",
          "lengthFeet": 15
        }
      },
      "parameterEvidence": {
        "damageType": [
          {
            "contributorId": "red-grandparent",
            "kind": "direct-source",
            "selectedSourceId": "ancestry.dragonborn.red",
            "resolvedFromSourceId": "ancestry.dragonborn.red"
          }
        ],
        "area": [
          {
            "contributorId": "bronze-grandparent",
            "kind": "direct-source",
            "selectedSourceId": "ancestry.dragonborn.bronze",
            "resolvedFromSourceId": "ancestry.dragonborn.metallic"
          }
        ]
      }
    }
  ],
  "status": "draft-origin-profile",
  "created": true,
  "authority": "not-authorized"
}
```

This schema is intentionally narrow. It proves one trait, not the full Species or Character Origin package. The `origination` field is independent; the `contributors` entries represent sources, not biological identities or a fixed parent count. A zero-contributor origin is expressible with an empty manifestation, though Origin-established traits without a contributor require a separately approved source/authority mechanism.

A contributor can reference a prior profile through a `profileId`, rather than a native `selectedSourceId`. The profile ID and contributor occurrence ID have distinct purposes: the first identifies a source profile; the second identifies how *this* Origin uses that source.

## Eligibility vs manifestation

`heritage_candidate_pool(...)` produces the possible trait and parameter values, with source trails. **Nothing in that pool manifests automatically.**

`draft_origin_profile(..., proposed_parameters=None)` records **no manifestation** even if contributors could supply many traits.

Providing explicit proposed parameters validates them against the pool and records exactly one hypothetical manifested trait in this focused prototype. Unoffered or incomplete combinations do not create a partial Origin record.

When a parent profile is referenced, the prototype **revalidates its recorded manifestation against its recorded contributors** and compares source evidence. It rejects invented or changed parameter values, stale/forged provenance, cyclic profile references, nonexistent profiles, and duplicate contributor occurrence IDs. This is **internal study consistency**, not trusted authorization, because the test caller can still submit a synthetic catalog, profile registry, and policy definition.

Where two contributors supply the same selected value, the profile retains **all eligible contributing sources**. It does not falsely claim that one unobservable parent uniquely determined the trait.

## Current known limitations

- The proof currently recognizes **one configurable, explicitly selected compound trait** (ordinary Breath Weapon) per test blueprint. It does not yet inventory every Species trait as a potential manifested characteristic or model multiple simultaneous traits.
- Source-local trait ID equality is not universal trait equivalence. Different Species' independently defined traits need reviewed semantic identities before becoming one candidate pool.
- No rules yet decide **how many** traits may manifest, which ones conflict, when duplicated traits upgrade, whether parameters must remain linked for a given ability, or whether Origin-specific entitlements can be established without a Heritage contributor.
- One provisional linked/independent policy applies to the example trait's declared parameters. Compound parameter groups, cross-trait dependencies, and trait substitution remain to be designed.
- The prototype represents direct native contributor sources as eligible candidate definitions, not automatically as audited profiles of actual individuals. A future authoritative model should normalize **both direct standard and mixed contributors into one manifest profile representation**.
- No optional balance system has been approved. Source fidelity and playtestable characteristics should precede numerical balance assumptions.
- No combat, saving throw, resource, condition, spell or other mechanics are executed.

## Next Pillar 2 milestone

**Generalize the manifestation inventory from one experimental compound ability to multiple characteristics, including intrinsic attributes** such as creature type, physical size, movement, and senses. Then establish explicit dependency/override semantics. Prove that a standard Human, High Elf, and Dragonborn can be expressed as complete Origin profiles without any Species-specific creation logic.

Do not write gameplay evaluators, Character Sheet changes, or combat support as part of this milestone. The aim is **what the character possesses and why**, not how those features resolve during play.
