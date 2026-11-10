# Native equipment qualification contract

Status: executable shared proficiency and Weapon Mastery entitlement evaluation; category-specific gameplay effects deferred.
Ruleset: first-party-001.
Source decision: docs/rewrite/native-rules-design-specification.md, November 9, 2026.

## Architectural decision

Equipment proficiency is a binary qualification, not a rank or additional Skill/Competency proficiency. Weapon Mastery is an additional, separately sourced entitlement with a proficiency prerequisite. A single two-bit training mask represents the effective combination without conflating the grants. The common evaluator answers whether a qualification covers a native equipment type, without awarding an item, permitting an action, or calculating a check. Its results retain all contributing source/occurrence/grant identities without stacking duplicate benefits.

Alternatives:
- Reusing legacy Character projection unchanged would preserve legacy source-shaped tool-to-Competency translations, including retired identities. Rejected as a native data model, but its provenance, category, and eligibility concepts were retained.
- Parsing class weaponProficiencies/armorProficiencies/toolProficiencies display strings directly would conflate source tokens and stable identities. Rejected for executable eligibility; original fields remain as migration evidence.
- Separate ranked training systems for weapons, armor, shields, and tools would contradict the approved binary model. Rejected.
- Chosen: canonical equipment-type and equipment-group entities in NativeCatalogSnapshot, one authored equipmentProficiencyGrants contract, one resolver, category-specific downstream effects.

Native equipment types and groups live in data/rulesets/first-party-001/native/equipment-qualifications.json. This is an intentionally representative **proficiency taxonomy**, NOT a complete item inventory, item catalog, or collection of purchasable objects. NativeCatalogLoader checks referenced groups, category consistency, and cycles. No display-name inference is permitted.

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

Here, `equipment.weapon.mace` denotes the **base Mace weapon type**, not an individual weapon owned by a character. `equipment.group.weapon.family.mace` grants proficiency across the Mace family. This distinction applies throughout the weapon catalog. Mace-family proficiency covers the base Mace and Morningstar; proficiency with only the base Mace does not. Simple Weapon proficiency covers the Mace but not the Martial Morningstar. All Weapons covers both.

Valid categories: weapon, armor, shield, tool. An equipment type can belong to multiple authored groups. Parent groups confer broader coverage; sibling groups do not. A native item can point to a type with equipmentTypeId, but item possession does not infer qualification. Unknown items and missing links return unresolved rather than not proficient.

## Base weapon types, magic variants, item definitions, and inventory instances

The weapon hierarchy stops at the **base weapon type**, not at an owned or named item. The next levels belong to the item system, not to separate proficiency tracks:

1. **Category:** Weapon — `equipmentCategory: "weapon"`.
2. **Family:** Swords — `equipment.group.weapon.family.sword`.
3. **Base type:** Longsword — `equipment.weapon.longsword`.
4. **Item definition:** an ordinary Longsword, a +1 Longsword, or a Flame Tongue Longsword. These are distinct definitions with their own item/magic properties; each can reference the same canonical `equipmentTypeId`.
5. **Owned instance:** the particular item in a character's inventory, with its own ownership, enchantments, charges, durability, modifications, or other state. Instances are not native proficiency targets.

For **ordinary and magic variants retaining the base weapon's proficiency rules**, native item definitions explicitly link to that base type. Example *illustrative resolved native item definitions* (not currently promoted source content):

    { "id": "item.example.longsword", "name": "Longsword",
      "kind": "item", "equipmentTypeId": "equipment.weapon.longsword" }

    { "id": "item.example.plus-one-longsword", "name": "+1 Longsword",
      "kind": "item", "equipmentTypeId": "equipment.weapon.longsword" }

    { "id": "item.example.flame-tongue-longsword", "name": "Flame Tongue Longsword",
      "kind": "item", "equipmentTypeId": "equipment.weapon.longsword" }

    { "id": "item.example.flame-tongue-rapier", "name": "Flame Tongue Rapier",
      "kind": "item", "equipmentTypeId": "equipment.weapon.rapier" }

A Longsword proficiency resolves identically for the first three item definitions; it does **not** grant proficiency with the Rapier, even though both magic items use the Flame Tongue variant. Sword-family proficiency can cover both. The `item.example.*` records are examples only; no magic effects or real item identities are created by them.

A reusable magic-variant template (for example, `magicvariant.1-weapon` or `magicvariant.flame-tongue`) is **not** automatically a proficiency target, an owned inventory instance, or a new base type. Composition of base items with variant templates must yield a resolved item definition with an explicit `equipmentTypeId`. The current working corpus contains base items, named magic items, and reusable magic variants, many still awaiting native reconciliation. The equipment resolver does not execute that composition, does not infer type from a source `baseItem` token or a display name, and does not promote working definitions by itself.

**Exceptional magic items require explicit reconciliation.** The retained source for Sun Blade, for example, allows proficiency through either Longswords or Shortswords. A single `equipmentTypeId` cannot represent that alternative. The existing evaluator does not yet have a verified item-specific alternate-proficiency rule, so do not treat a basic Longsword link as a complete Sun Blade implementation. Multi-form weapons or items that change their underlying weapon type require similarly explicit handling. These exceptions should be authored as approved mechanics, not silently inferred from magical names or properties.

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

Choice grants declare count and fromCategory. By default `selectionKind` is `equipment-type`: optional `fromGroupIds` limits candidates to individual types covered by the listed groups, and optional `fromTypeIds` limits them to an explicit set of individual types. Both restrictions apply together if present. Unknown, duplicate, or cross-category type IDs fail closed.

For a choice of **weapon families** or other equipment groups, set `selectionKind` to `equipment-group` and supply a **required** `fromGroupIds` list. In this mode, the list is an exact whitelist of selectable equipment groups, *not* a set of parent scopes to expand. This prevents a choice of one family (Maces or Swords) from inadvertently offering All Weapons or Martial Weapons. `fromTypeIds` is not valid in group-selection mode. Only offer broad categories when the authored source expressly authorizes them.

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

A family-only selection can use an explicit whitelist:

    {
      "equipmentProficiencyGrants": {
        "choices": [
          {
            "id": "weapon-family",
            "count": 1,
            "fromCategory": "weapon",
            "selectionKind": "equipment-group",
            "fromGroupIds": [
              "equipment.group.weapon.family.mace",
              "equipment.group.weapon.family.sword"
            ]
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

NativeEquipmentQualifications.Covers(projection, equipmentId) checks an equipment-type ID or a **resolved native item definition** explicitly linked by equipmentTypeId. Multiple mundane or magical item definitions can link to one canonical type without creating duplicate training. It does not accept an inventory-instance ID or unresolved magic-variant template. Results:
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

## Combined proficiency and Weapon Mastery state

`NativeEquipmentQualifications.ResolveTraining` builds a single snapshot-pinned projection from independent **`equipmentProficiencyGrants`** and **`weaponMasteryGrants`**. `CoversTraining` checks an equipment base type or reconciled native item definition. The result exposes a **two-bit integer mask**, not two authoritative booleans:

| Mask | Binary | Meaning |
| --- | --- | --- |
| 0 | 00 | Neither proficiency nor mastery |
| 1 | 01 | Proficiency only |
| 2 | 10 | A mastery entitlement without proficiency; **not usable mastery** |
| 3 | 11 | Proficiency and mastery; mastery is usable |

Bits: `NativeEquipmentTrainingFlags.Proficiency = 1`, `NativeEquipmentTrainingFlags.Mastery = 2`. `HasUsableMastery(flags)` is true only for **exactly** `flags == 3`; out-of-contract values are not accepted as additional training states. A fixed mastery group can create state 2 for a weapon the character has not qualified to wield. That bit is retained for provenance but **never authorizes mastery combat effects**; selecting a mastery weapon through a counted choice is instead restricted to currently proficient types. Neither proficiency nor mastery is stored independently on individual magic variants, and no equipment mastery bit indicates the weapon's innate mastery property or executes its effect.

Unlike a ranked Competency, this mask is a combination of **two independent entitlements**, not four skill ranks. No change to existing proficiency-only requests or API is required. The original `Covers` result remains binary for compatibility.

### Authoring and progression

The existing native feature definitions now declare mastery selections without cloning equipment taxonomy:

- `class-feature.barbarian.weapon-mastery-level-1`: count of 2 at Barbarian level 1, 3 at level 4, and 4 at level 10. Eligible candidates must belong to `equipment.group.weapon.melee` and be proficient.
- `class-feature.rogue.weapon-mastery-level-1`: two eligible proficient base weapon types. Its existing source restriction against treating mastery as Sneak Attack eligibility remains separate.
- `equipment.group.weapon.melee` and `equipment.group.weapon.ranged` classify the native weapon catalog. These are **orthogonal group memberships** alongside family and Simple/Martial scopes. A Javelin can be a melee weapon with a thrown use; the grouping describes base weapon classification, not the chosen attack action.

Example Barbarian feature grant:

```json
{
  "weaponMasteryGrants": {
    "choices": [{
      "id": "weapons",
      "countByClassLevel": [[1, 2], [4, 3], [10, 4]],
      "fromGroupIds": ["equipment.group.weapon.melee"],
      "requireProficiency": true
    }]
  }
}
```

The common resolver supports separate `weaponMasteryGrants.fixed` targets (native weapon base type or group) and `weaponMasteryGrants.choices` (distinct native base weapon types), with optional explicit `fromTypeIds` and/or `fromGroupIds` restrictions. Each counted choice **must** declare `requireProficiency: true`; omission or `false` is invalid rather than an implicit default. Only an authored `classFeature` may use `countByClassLevel`; other sources use a fixed `count`. It checks all authored references and rejects invalid, unexpected, stale, duplicated, and incomplete selections without publishing partial mastery grants. The source's `ClassLevel` is an **assertion**, not proof of advancement.

### API

Authenticated endpoints under `/api/mechanics/v1/` (or `/api/private/mechanics/v1/` on private ingress):

- `POST characters/equipment-training`: resolve source-authored proficiency and mastery entitlements.
- `POST characters/equipment-training/coverage`: recompute entitlements and the mask against a canonical base equipment type or reconciled item definition.

Example training request:

```json
{
  "proficiencies": {
    "rulesetId": "first-party-001",
    "fingerprint": "<active-native-fingerprint>",
    "sources": [{"sourceId":"class.barbarian","occurrenceId":"barbarian-entry"}]
  },
  "masterySources": [{
    "sourceId": "class-feature.barbarian.weapon-mastery-level-1",
    "occurrenceId": "mastery-entry",
    "classLevel": 1
  }],
  "selectedMasteries": {
    "mastery-entry/weapons": [
      "equipment.weapon.longsword",
      "equipment.weapon.mace"
    ]
  }
}
```

The coverage request wraps the above object in `training` and includes `equipmentId`. The response has a nullable integer `flags` (0–3 on resolved coverage; null when unresolved/unavailable) and independent `proficiencyProvenance` and `masteryProvenance`. It **does not accept** arbitrary caller-supplied flag values as authoritative state. The Character backend must verify source ownership, parent-Class level, prerequisites, and whether long-rest exchanges are permitted before submitting a request. The evaluator verifies source data and selection eligibility, not client identity or rest history.

### Boundaries

Weapon Mastery effects such as Sap, Nick, Push, and Topple remain source content awaiting combat-mechanics reconciliation; mastery bit 3 only establishes that the character can use the weapon's separately defined effect when its combat trigger and rules are satisfied. The resolver does not implement combat maneuvers or execute those effects. Magic variants continue to inherit their base type's training mask when linked explicitly, subject to separately authored exceptions such as Sun Blade's alternate proficiency rule.

## Outstanding policy

Weapon hit calculations, armor penalties, shield defense/usage, tool and Skill check interactions, untrained use, Advantage and bonus stacking are NOT selected by this evaluator. No universal benefit formula or automatic compensation for duplicate grants is approved. Approved domain-specific evaluators must supply these later.

Conditional grants need trusted, server-validated form/feature state. A client-submitted flag is not sufficient proof of a conditional entitlement. Such authoring and checking are deferred until that source context is approved.

Land and Water Vehicles appear in Origin source grants. Vehicle operation is not necessarily tool handling, and may need distinct driver/operator categories. Keep these grants unresolved for Character Origins until the vehicle qualification taxonomy and effects are approved. Do not remap them to tool or create a fifth general category silently.

For a narrow Gladiator arena-weapon choice, use fromTypeIds with the reviewed allowed native weapon IDs instead of an unrestricted fromCategory weapon choice. The catalog remains partial: expanding native item/type definitions and their authored memberships is separate from the common evaluator. An absent type is unresolved, not automatically untrained. Reconciliation must not invent category membership from historical names.

Validation: NativeEquipmentQualificationsTests exercises coverage, overlap, provenance, actual item links, tool/Skill separation, crafting associations, counted choices, narrow instruments/games, multiclass distinctions, and fail-closed malformed grants and groups. Tests must not assert an exact catalog count or fingerprint.
