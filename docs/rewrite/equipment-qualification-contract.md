# Native equipment qualification contract

Status: executable common scope/grant evaluation; category-specific gameplay effects deferred.
Ruleset: first-party-001.
Source decision: docs/rewrite/native-rules-design-specification.md, November 9, 2026.

## Architectural decision

Equipment proficiency is a binary qualification, not a level or additional Skill/Competency proficiency. The common evaluator answers whether a qualification covers a native equipment type, without awarding an item, permitting an action, or calculating a check. Its results retain all contributing source/occurrence/grant identities but grant no duplicate benefit.

Alternatives:
- Reusing legacy Character projection unchanged would preserve legacy source-shaped tool-to-Competency translations, including retired identities. Rejected as a native data model, but its provenance, category, and eligibility concepts were retained.
- Parsing class weaponProficiencies/armorProficiencies/toolProficiencies display strings directly would conflate source tokens and stable identities. Rejected for executable eligibility; original fields remain as migration evidence.
- Separate ranked training systems for weapons, armor, shields, and tools would contradict the approved binary model. Rejected.
- Chosen: canonical equipment-type and equipment-group entities in NativeCatalogSnapshot, one authored equipmentProficiencyGrants contract, one resolver, category-specific downstream effects.

Native equipment types and groups live in data/rulesets/first-party-001/native/equipment-qualifications.json. This is an intentionally representative type catalog, NOT a complete item inventory or a set of purchasable objects. NativeCatalogLoader checks referenced groups, category consistency, and cycles. No display-name inference is permitted.

**Weapon hierarchy:** Weapon is the top-level equipment category (`equipmentCategory: "weapon"`, with the broad scope `equipment.group.weapon.all`). Weapon families such as **Maces**, **Swords**, and **Axes** are intermediate equipment groups under this category, identified by `equipment.group.weapon.family.*`. Individual canonical weapons remain `equipment-type` leaves. Simple and Martial are overlapping classifications of individual weapons, not replacements for the weapon-family hierarchy. Every currently reconciled native weapon type has an explicit authored family membership; additional source weapon definitions must be assigned a reviewed family when promoted to native content.

## Identity schema

A specific type:

    {
      "id": "equipment.weapon.longsword",
      "name": "Longsword",
      "kind": "equipment-type",
      "equipmentCategory": "weapon",
      "groupIds": ["equipment.group.weapon.martial"]
    }

A group:

    {
      "id": "equipment.group.weapon.martial",
      "name": "Martial Weapons",
      "kind": "equipment-group",
      "equipmentCategory": "weapon",
      "parentGroupIds": ["equipment.group.weapon.all"]
    }

A weapon family is an intermediate group beneath Weapon:

    {
      "id": "equipment.group.weapon.family.mace",
      "name": "Maces",
      "kind": "equipment-group",
      "equipmentCategory": "weapon",
      "parentGroupIds": ["equipment.group.weapon.all"]
    }

Its individual weapon types have both a family membership and a Simple/Martial classification:

    { "id": "equipment.weapon.mace",
      "kind": "equipment-type", "equipmentCategory": "weapon",
      "groupIds": ["equipment.group.weapon.simple", "equipment.group.weapon.family.mace"] }

    { "id": "equipment.weapon.morningstar",
      "kind": "equipment-type", "equipmentCategory": "weapon",
      "groupIds": ["equipment.group.weapon.martial", "equipment.group.weapon.family.mace"] }

Here, the specific `equipment.weapon.mace` identity represents an individual ordinary Mace weapon *type*, while `equipment.group.weapon.family.mace` denotes proficiency with the entire Mace family. The same distinction applies throughout the weapon catalog. Mace-family proficiency covers both native Mace and Morningstar; proficiency with only the individual Mace does not. Simple Weapon proficiency covers the Mace but not the Martial Morningstar. All Weapons covers both.

Valid categories: weapon, armor, shield, tool. An equipment type can belong to multiple authored groups. Parent groups confer broader coverage; sibling groups do not. A native item can point to a type with equipmentTypeId, but item possession does not infer qualification. Unknown items and missing links return unresolved rather than not proficient.

## Authored grants

Eligible native Classes, Origins, features, feats, and other source entities may contain equipmentProficiencyGrants either at their root or inside definition. Fixed grants name one canonical type or group. For a Rogue:

    {
      "equipmentProficiencyGrants": {
        "fixed": [
          {"id": "thieves-tools", "targetId": "equipment.tool.thieves-tools"}
        ]
      }
    }

For an individual weapon *type*, use `targetId: "equipment.weapon.longsword"`. For a weapon family, use an explicit group such as `equipment.group.weapon.family.sword`. For all weapons, use `equipment.group.weapon.all`. Simple and Martial remain separate classification scopes (`equipment.group.weapon.simple` and `equipment.group.weapon.martial`). Shields use `equipment.group.shield.all`. Selecting a weapon `equipment-type` never implicitly selects its whole family.

Choice grants declare count and fromCategory; optional fromGroupIds narrows candidates to types in the union of the specified groups, and optional fromTypeIds narrows to explicitly enumerated type IDs. When both are supplied, eligible types must satisfy both restrictions. Unknown, duplicate, or cross-category type IDs fail closed. The default selectionKind is equipment-type. fromTypeIds is only valid for equipment-type choices. The optional selectionKind equipment-group offers selectable entire group proficiency; use it only when the authored source grants such a broad option.

    {
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
    }

The engine rejects a source that supplies both a root and nested equipmentProficiencyGrants block, an unsupported property at the grant-block or grant-entry level, or a block with no grants for the requested mode. Those situations return unavailable rather than silently dropping effects. Evaluation is bounded across the entire request: up to 1,000 fixed-grant occurrences and 200 choice occurrences, separate from per-source limits. These limits are work-safety bounds, not gameplay limits.

Class sources may also declare multiclassFixed and multiclassChoices. A Source with GrantMode = multiclass reads ONLY these entry-specific grants; it never defaults to the standard Class grants. Not all Classes have an approved multiclass grant structure yet. Existing Bard artistic training and multiclass skill choices remain separate from equipment qualification.

## Resolver contract

NativeEquipmentQualifications.Resolve accepts RulesetId, Fingerprint, Sources, and optional SelectedChoices. Each Source has SourceId, OccurrenceId, and GrantMode (standard by default; multiclass only for Class sources). The caller MUST verify that the character earned the source, is at the appropriate advancement event, and satisfied feature/choice prerequisites; a matching ID alone is never proof of entitlement.

Choice key: sourceOccurrenceId/choiceId. Both component IDs reserve the slash separator; using a slash within either ID is invalid so that two unrelated choices cannot share the same persisted key. The combined key may contain up to 321 characters. SelectedChoices maps that key to distinct eligible qualification IDs of the declared count. Missing selections return needs-choices and eligible candidates, without partial qualification grants. Malformed grants, unresolved references, unsupported choices, and duplicated source occurrence IDs return unavailable without partial grants. Stale fingerprints return invalid.

A resolved projection lists each unique qualification and all matching NativeEquipmentQualificationProvenance occurrences. Repeated qualifications are Boolean, with no stacked numerical modifiers.

NativeEquipmentQualifications.Covers(projection, equipmentId) checks an equipment-type ID or a native item explicitly linked by equipmentTypeId. Results:
- resolved: Proficient true or false, plus all matching grant provenance
- unresolved: Proficient null where the type/link/scope is unknown
- unavailable: Proficient null when the grant projection has not fully resolved

Authenticated endpoints under /api/mechanics/v1/ (or /api/private/mechanics/v1/ on the private ingress):
- POST characters/equipment-proficiencies: a NativeEquipmentQualificationRequest
- POST characters/equipment-proficiencies/coverage: an object containing qualifications (same request) and equipmentId (the target)

Example qualification request:

    {
      "rulesetId": "first-party-001",
      "fingerprint": "<active-native-fingerprint>",
      "sources": [
        {
          "sourceId": "class.barbarian",
          "occurrenceId": "class-entry-1",
          "grantMode": "standard"
        }
      ]
    }

## Source-specific boundaries

- Barbarian martial group covers the native Longsword and other explicitly classified martial weapon types; shield proficiency is independently represented.
- The Weapon category is above individual weapon-family subcategories. A Mace-family grant covers the explicitly grouped Mace (Simple) and Morningstar (Martial). An individual Mace grant does not confer Morningstar proficiency; neither individual grant implies proficiency with the whole family. Group and classification grants can overlap, retaining provenance without stacking.
- A specific weapon grant does not confer its sibling weapons. Broad and narrow overlapping grants preserve their independent provenance with one binary outcome.
- Rogue Thieves' Tools proficiency does not imply skill.open-lock. Open Lock Skill does not grant Thieves' Tools proficiency. Possessing the tool item does not confer proficiency.
- A Gaming Set qualification does not imply every Gaming Set; Musical Instrument qualification does not imply every instrument unless an authored group does so.
- Smithing, Cooking, Forgery and other native Competencies retain their own ranks/proficiency. Associated tools are equipment associations, not an independent required training track. The engine never derives equipment grants from associatedTools.
- The Rogue's existing martial-with-finesse-or-light-property scope is currently represented by an explicit native group with membership for the types reconciled so far. It is not a general 5.x item property filter and must grow alongside the equipment catalog.
- Druid natural attacks in eligible forms are form/combat entitlements, not presumed equipment proficiencies. Existing raw class content is retained for that separate reconciliation.

## Outstanding policy

Weapon hit calculations, armor penalties, shield defense/usage, tool and Skill check interactions, untrained use, Advantage and bonus stacking are NOT selected by this evaluator. No universal benefit formula or automatic compensation for duplicate grants is approved. Approved domain-specific evaluators must supply these later.

Conditional grants need trusted, server-validated form/feature state. A client-submitted flag is not sufficient proof of a conditional entitlement. Such authoring and checking are deferred until that source context is approved.

Land and Water Vehicles appear in Origin source grants. Vehicle operation is not necessarily tool handling, and may need distinct driver/operator categories. Keep these grants unresolved for Character Origins until the vehicle qualification taxonomy and effects are approved. Do not remap them to tool or create a fifth general category silently.

For a narrow Gladiator arena-weapon choice, use fromTypeIds with the reviewed allowed native weapon IDs instead of an unrestricted fromCategory weapon choice. The catalog remains partial: expanding native item/type definitions and their authored memberships is separate from the common evaluator. An absent type is unresolved, not automatically untrained. Reconciliation must not invent category membership from historical names.

Validation: NativeEquipmentQualificationsTests exercises coverage, overlap, provenance, actual item links, tool/Skill separation, crafting associations, counted choices, narrow instruments/games, multiclass distinctions, and fail-closed malformed grants and groups. Tests must not assert an exact catalog count or fingerprint.
