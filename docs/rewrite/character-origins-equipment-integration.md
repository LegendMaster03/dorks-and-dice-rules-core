# Character Origins: equipment qualification integration handoff

Target working branch (not edited by this work): feature/character-origins-competency-audit-20261109.
Audit: docs/rewrite/character-origins/pillar3-competency-contract-audit.md.

The shared native qualification infrastructure now defines equipment-type and equipment-group identities, source grant occurrences, fixed scopes, counted selectable scopes, category coverage, and duplicate provenance. Implementation files:
- src/RulesCore.Mechanics/NativeEquipmentQualifications.cs
- data/rulesets/first-party-001/native/equipment-qualifications.json
- docs/rewrite/equipment-qualification-contract.md
- tests/RulesCore.Mechanics.Tests/NativeEquipmentQualificationsTests.cs

This is a data-contract handoff, not permission to modify, merge, or deploy the Origins branch. Coordinate integration separately.

## Simple fixed qualifications

Criminal's obsolete competency.thieves-tools becomes one fixed equipment proficiency in its source entity, independent of Skill training:

    "equipmentProficiencyGrants": {
      "fixed": [
        {"id": "criminal-thieves-tools", "targetId": "equipment.tool.thieves-tools"}
      ]
    }

If the source is shaped with a definition object, place equipmentProficiencyGrants under definition. Otherwise place it at the root. Do not remove unrelated Skill, Competency, trait, or item grants.

High Elf and Wood Elf specific weapon proficiencies become one fixed record per actual weapon type, using explicit equipment.weapon.* native IDs. If a historical weapon type is missing from the catalog, reconcile the type before converting the grant. A character also proficient with equipment.group.weapon.martial receives one effective proficiency, with both source occurrences preserved.

## Selectable equipment type

A single specific Gaming Set:

    "equipmentProficiencyGrants": {
      "choices": [
        {
          "id": "gaming-set",
          "count": 1,
          "fromCategory": "tool",
          "fromGroupIds": ["equipment.group.tool.gaming-set"]
        }
      ]
    }

A Musical Instrument choice uses equipment.group.tool.musical-instrument instead. For any narrower, source-specific selection use fromTypeIds rather than broadening the category. This chooses one TYPE (for example equipment.tool.lute), not every instrument. Fixed class/category qualifications can overlap the selected qualification without stacking.

Gladiator's one specific arena-appropriate weapon should choose from the actual reviewed native weapon types, not an entire martial or simple category. The equipment choice contract supports fromTypeIds for this purpose. For example: {"id":"arena-weapon","count":1,"fromCategory":"weapon","fromTypeIds":["equipment.weapon.longsword","equipment.weapon.rapier"]}. Replace the example type list with the actually approved arena-eligible types; it is not an official Gladiator entitlement list. An unrestricted fromCategory weapon choice would be too broad.

For an Origin that explicitly grants a full weapon **family**, use the corresponding `equipment.group.weapon.family.*` target (for example, `equipment.group.weapon.family.mace`), or an intentional `selectionKind: "equipment-group"` choice with `fromGroupIds` explicitly listing **only** its allowed families. A group-choice without this whitelist is invalid. Use `equipment.group.weapon.all` only for proficiency with every weapon. Simple and Martial are independent classification scopes, not weapon families. A specific `equipment.weapon.*` target is a single canonical weapon type, not its family. Do NOT convert a one-weapon choice into an entire family or category.

## Mixed Skill/Competency/tool choices: Spy and Street-Raised

The equipment resolver deliberately does not allocate Competency ranks, proficiency or Skill training and does not decide one-of choices involving those other subsystems. The Spy and Street-Raised choice is a SINGLE entitlement with heterogeneous outcomes:
- selected Competency option: only the approved Competency rank/proficiency grant;
- selected Thieves' Tools option: only equipment.tool.thieves-tools proficiency;
- never both from a single selection.

The Origin-choice authority must validate the mutually exclusive choice and expose the selected grant as a distinct native source occurrence before calling NativeEquipmentQualifications.Resolve. A source/option definition with equipmentProficiencyGrants.fixed is viable when it is submitted ONLY after that option is selected and authorized. Do not place an unconditional fixed Thieves' Tools grant on Spy or Street-Raised. Do not convert Thieves' Tools to a Competency. A generic cross-domain choice resolver remains a separate Origin integration responsibility.

Entertainer's Disguise-versus-Instrument and Knight's Gaming-Set-versus-Instrument choices likewise retain their existing one-of cardinality. They need the same externally verified exclusivity if alternatives fall into different mechanics categories.

## Equipment and training are distinct

- Criminal, Spy, Street-Raised: eliminate all three obsolete competency.thieves-tools references, once the entitlement path has been integrated.
- Rogue and Criminal fixed duplicates: retain both provenance entries; do not invent extra rank, replacement tool, Expertise or compensation.
- Smithing, Cooking, Forgery, Navigation and associated tools: remain Competency training or equipment associations as approved; an Origin grant of the Competency does not imply a separate equipment proficiency.
- Open Lock Skill does not grant Thieves' Tools; a Thieves' Tools proficiency does not grant Open Lock Skill.
- Physical starting items are inventory grants; the equipment resolver does not supply them.
- Land Vehicles and Water Vehicles are deferred pending operator/vehicle category review. Do not map to tool by string similarity.

## Character request and evaluation

The Character builder must prove that a selected Origin/Class/feature was earned, including conditional grant and option choice. It then supplies native SourceId and a stable Source occurrence ID. For example, an authorized fixed Criminal grant:

    {
      "rulesetId": "first-party-001",
      "fingerprint": "<active fingerprint>",
      "sources": [
        {"sourceId": "background.criminal", "occurrenceId": "background-1"}
      ]
    }

The equipment resolver looks up the actual native authored grant; it does not accept an arbitrary target ID invented by the consumer. For counted equipment choices, supply SelectedChoices with key occurrenceId/choiceId and the selected canonical qualification IDs. First call without selection returns needs-choices and the active catalog candidate list. Call again with the validated selection.

Coverage calls may target a canonical equipment type or a native item explicitly bearing equipmentTypeId. Unknown types remain unresolved. Repeated source grants are Boolean; provenance is not discarded.

## Remaining integration tests

1. Criminal + Rogue both grant Thieves' Tools: two provenance occurrences, one qualification.
2. Spy chooses Thieves' Tools, or chooses one of the three Competencies: exactly one selected benefit.
3. Street-Raised does the same without a retired Competency identity.
4. Entertainer, Knight, Noble and Soldier choices preserve correct counts and specific type scopes.
5. Gladiator's selected weapon is specific; broad Barbarian proficiency may cover it, but not grant an additional reward.
6. High Elf/Wood Elf weapon grants coexist with broad martial proficiency without duplication.
7. Smithing and Navigation associations create no additional equipment-training gate.
8. Water/Land Vehicle grants remain visibly unresolved until approved.

Do not assume the representative equipment type catalog already includes every eligible type, and do not declare all Origins integration complete until mixed-domain choices and vehicle qualifications receive their separate decisions.
