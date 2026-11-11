# Pillars 1 and 3 — trusted Origin grant-projection boundary

**Status:** Engineering contract **proposal**, prepared against the selected native JSON. **Not a new rule, approved runtime API, authorization implementation, or stored character schema.** The approved benefit amounts and selections remain unchanged.

**Related:** [Origin choice authorization plan](pillar3-origin-choice-authorization-plan-20261110.md), [mixed-option conformance cases](pillar3-authorized-option-cases-20261110.json), and [Pillars 1 and 3 readiness](pillars-1-and-3-native-readiness-20261110.md).

## Problem and invariant

The existing `NativeEquipmentQualifications` evaluator handles authoritatively authored **root or `definition`** equipment grants, but accepts source occurrences and selected targets as caller assertions. Neither a source ID nor a selected benefit option supplied by a client proves that the character earned that source. In particular, the evaluator **does not** traverse a Background's nested `benefit-choice` branches as authorized grants.

**Invariant:** Only a trusted Character/Origin selection authority may turn a validated character's selected Origin option into an earned Skill, Competency, equipment qualification, language, inventory, or domain-specific benefit. The evaluator can validate ruleset content and eligible equipment types; it cannot substitute for ownership and selection authorization.

The new [mixed-option conformance matrix](pillar3-authorized-option-cases-20261110.json) pins the **five existing** source decisions and their **sixteen** alternative outcomes against native JSON. The matrix is reference/test evidence, **not a sixth source of rules** or a user-editable grant manifest.

## Two distinct stages, not one public request

### Stage A — client selection proposal

The client can suggest a Species, applicable Ancestry, one Background, one effective selectable Upbringing, and option selections. These are **untrusted intentions**. The proposal may identify existing source/benefit/option IDs, specific proposed Skill/Competency/equipment/language choices, and a proposed physical starting item. The client may **not** claim verified source occurrences, grant amounts, resulting entitlements, a ready inventory mutation, or a previously authorized native-catalog snapshot.

For a nested benefit, the client should identify its path by structured fields:

```json
{
  "sourceId": "background.spy",
  "benefitId": "spy-tradecraft-competency",
  "optionId": "thieves-tools"
}
```

That input **requests** the Thieves' Tools branch. It does not grant `equipment.tool.thieves-tools`. For the Entertainer instrument branch, an additional proposed `equipmentTypeId` is required for the `entertainer-musical-instrument` sub-choice; it must be validated against the catalog. An unselected child branch is never evaluated as an earned source.

### Stage B — trusted authorization and internal projection

A server-owned authority checks the character's actual selected/earned sources and the active, immutable Rules Core ruleset snapshot. It resolves all required choices against the relevant **authoritative native** source entities. Only after **all** checks succeed may it emit a trusted projection for the separate domain resolvers.

The following is an **internal projection fragment** showing how one authorized branch retains provenance, **not** a complete resolved character. Spy also has additional required selections (including Skills), omitted here deliberately. A full result may be marked `resolved` only after *all* of them have been validated. This is neither an accepted public API nor an existing C# record:

```json
{
  "contractVersion": 1,
  "rulesetId": "first-party-001",
  "fingerprint": "<active-native-fingerprint>",
  "exampleOnly": true,
  "appliedSources": [
    {
      "sourceId": "background.spy",
      "sourceOccurrenceId": "<server-assigned-origin-occurrence>",
      "acquisition": "selected-background"
    }
  ],
  "selectedPaths": [
    {
      "sourceOccurrenceId": "<server-assigned-origin-occurrence>",
      "benefitId": "spy-tradecraft-competency",
      "optionId": "thieves-tools"
    }
  ],
  "projectedGrants": [
    {
      "domain": "equipment-proficiency",
      "targetId": "equipment.tool.thieves-tools",
      "sourceId": "background.spy",
      "sourceOccurrenceId": "<server-assigned-origin-occurrence>",
      "benefitId": "spy-tradecraft-competency",
      "optionId": "thieves-tools",
      "grantId": "spy-tradecraft-thieves-tools"
    }
  ]
}
```

A future implementation may use equivalent typed records rather than this fragment. It must preserve the **content IDs, occurrence provenance, all-or-nothing validation, and trust boundary**. It must never treat the example fragment as a usable completed projection. The actual stable occurrence identifier comes from the Character/backend's verified acquisition records, never from the client or the example placeholder. Reconstructing this projection from user-supplied fields without validation would defeat the contract.

## Source selection and inheritance

- Select **one** Background and **one** effective Upbringing. Species and applicable Ancestry are separately selected per their native selection rule. A Species or Ancestry `defaultUpbringing` is only a *suggestion*; it is not another source.
- An inherited Upbringing's parent chain contributes each applicable source's benefits **once**. The selectable child, such as `upbringing.elf.high`, remains the effective selected Upbringing; its inheritance-only `upbringing.elf.general` parent is an applied source, **not** a second selectable Origin. The same rule applies to Gnome.
- The authority must reject an unknown, unselectable direct selection, missing or cyclic parent, duplicated source application, incompatible ancestry relationship, or unearned Background. A source's `selectable` state and parent links are ruleset content, not a UI-only filter.
- Distinguish the verified source occurrence, benefit ID, nested option ID, and authored equipment grant ID. These form a **structured identity** for attribution. Avoid ambiguous concatenation or treating a grant ID from one branch as usable under a different branch.
- Two independently earned sources can grant the same underlying proficiency or Competency, but neither source occurrence can be applied twice by repeating an input row.

## Choice authorization and domain projection

For each selected and inherited source:

1. Resolve its native benefits from the **same ruleset fingerprint**. Do not read inactive `working/` material as executable rules.
2. Fixed, unconditional benefits contribute their actual approved effects. Choices require the declared exact cardinality; `distinctChoices` must be respected. `chooseOne` and `chooseFrom` are source-local lists, not generic access to a domain.
3. When `grantType: "benefit-choice"`, select the exact authored parent `benefitId` and one approved `optionId` **first**. Only that option's nested `benefits` and optional `equipmentProficiencyGrants` may contribute grants. Other options have zero entitlement. The five existing mixed choices are independently pinned in the conformance matrix.
4. A nested equipment `fixed` grant is not an additional choice: Spy/Street-Raised Thieves' Tools immediately selects the canonical **one tool type** if and only if that outer branch was authorized. A nested equipment `choices` grant needs its own count and valid individual equipment-type selection (Entertainer Musical Instrument, Aristocratic Gaming Set). Selecting the outer option is necessary but **not sufficient** when an inner type choice exists.
5. Preserve each source's authored `competencyTraining` object and distinct grant occurrence. Do not infer proficiency from a rank, invent extra tools from `associatedTools`, or grant every Skill in a family from one choice. **After** source authorization, the separate approved experimental duplicate-Competency rule combines independent proficiency grants as either a rank in the same Competency (`add-rank`, provisional default) or an authorized new Competency proficiency (`alternate-proficiency`). This option is not an Origin-specific grant conversion and cannot be silently applied at the source-projection stage.
6. `choiceCategoryId` may resolve to a shared canonical Competency category (Guild Artisan); source-specific guild trade and eligibility constraints must still be checked. A shared category is not automatic permission to select every member.
7. Track source-restricted learned languages independently. Guild Merchant's second trade language is mutually exclusive with its Navigation rank choice and is still subject to the existing commercial-language constraint.
8. Distinguish ordinary equipment ownership, equipment qualification and Weapon Mastery. Origin proficiency grants can contribute the **proficiency bit (1)** of the common two-bit equipment-training result. No Origin in this contract implies the **Mastery bit (2)**; usable Weapon Mastery requires separately earned and authorized source content.

The Origin authority must not submit an arbitrary synthetic source ID or caller-authored `targetId` to `NativeEquipmentQualifications` as if it were catalog-backed. **Required future adapter:** either add an internal-only, typed and source-verified authorized-grant entry point to the shared resolver, or derive a trusted native option reference whose membership and targets the resolver itself can verify. Such an adapter is **not implemented here** and needs independent C# tests at implementation time.

## Atomic item and training relationships

These two source-level linkages are already authored:

- **Gladiator:** `gladiator-arena-weapon` and the first starting item share `equipmentTypeChoiceId: "arena-weapon"`. The item is an ordinary, inexpensive weapon of the **same canonical base type**. The DM determines whether it is appropriate to the arena discipline; no automatic arena-suitability predicate is required. The broader `equipment.group.weapon.all` is a candidate pool, not a family/category-wide grant. Net and Trident still lack canonical native types.
- **Entertainer:** the one Performance item has `sourceBenefitChoiceId: "entertainer-performance-method"`, and its `conditionalItemChoices` are keyed to `disguise-kit` and `musical-instrument`. The Disguise branch permits an ordinary Disguise Kit or mundane prop **without granting a separate Disguise Kit qualification**. The Instrument branch requires a mundane instrument with the **same canonical type** chosen by `entertainer-musical-instrument`.

For each, future authorization must preserve consistency between the selected **training** and matching ordinary **inventory** outcome before recording the package. An unknown item, missing catalog base type, incompatible type or unsupported mundane/item identity may prevent completed materialization. Arena appropriateness is adjudicated by the DM and is **not** a machine-enforced blocker. These relationships are not yet implemented by the equipment resolver and are not solved merely by JSON references.

An item in inventory, a cart from Guild Merchant, or an ordinary physical Disguise Kit must never imply proficiency. The item and qualification projections retain separate provenance even if their validation is coupled.

## Separate vehicle domain

Soldier Land Vehicles and Sailor Water Vehicles are already authored `qualificationCategory: "vehicle"` benefits. This contract **preserves** those entitlements and their source provenance but does not turn them into tool proficiencies, Vehicle Competencies, ordinary inventory, or Weapon Mastery. The common equipment resolver's four categories remain weapon, armor, shield and tool. A future vehicle/operator resolution contract still requires its own decision; until then it is not truthful to report a complete executable Origin projection for this domain.

## Failure semantics and required tests

A future authority needs distinguishable outcomes:

| State | Meaning | Effective earned projection |
| --- | --- | --- |
| `needs-choices` | Valid earned sources, but a required option or selected item remains unspecified | **None** |
| `unavailable` | A reference, eligibility, required domain authority, contextual item rule or source proof cannot be established | **None** |
| `invalid` | Malformed, extra, contradictory, stale-fingerprint, duplicated or unauthorized selection requests | **None** |
| `resolved` | All required source, option and applicable item validations have succeeded | Only independently validated grants |

The exact status names follow the established qualification resolver style and are **proposed**, not a committed API. A partial response may describe required or blocked choices for user guidance but must never publish that partial set as **usable earned grants**. A domain-specific preview may exist separately, clearly marked non-authoritative. Neither a successful earlier snapshot nor an older verified option persists across an incompatible ruleset fingerprint without review/revalidation.

When implementation starts, tests must cover: forged source/occurrence, duplicated inherited parents, unselectable parent chosen directly, invalid option and extra option, missing or duplicate choices, nested equipment grant leakage, two distinct Spy Skill choices, stale catalog/category membership, overlapping fixed qualifications with separate provenance, Guild Artisan eligibility, Gladiator type/item mismatch and mundane checks, Entertainer branch/item mismatch, and unsupported vehicle execution. The [static conformance tests](../../../tests/test_character_origins_authorized_option_contract.py) verify only that the **existing JSON supports these distinctions**. They do **not** test an implementation that does not exist.

## Scope decision pending implementation

The content contract for Pillars 1 and 3 can be reviewed now, but full Origin code integration is **deferred until Pillar 2** establishes mixed-Ancestry, contributed-heritage and manifested-trait semantics. No service boundary or character-creation runtime implementation should be finalized as a Pillar 1/3 prerequisite. Future implementation may decide whether Origin authority lives in Rules Core, the Character Sheet/backend, or a dedicated shared service. **Whichever service owns it** must authenticate character acquisition and bind source IDs to actual earned Origins, not merely echo client selections. Keep Rules Core as the sole authoritative source of rule semantics and approved grant rules; keep character-specific state authoritative in the Character service.

**This proposal approves no new Origin grant allocations, feats, Weapon Mastery effects, vehicle mechanics or runtime endpoints.** The independent experimental duplicate-Competency proficiency rule already supports a rank-conversion mode; this proposed source authorization contract must respect that existing approved policy without creating new source grants.
