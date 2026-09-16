# Rules Layer relationships

Rules Core keeps relationships between rule concepts separate from source provenance and canonical identity.

Canonical identity answers whether two source representations are the same rule concept. Relationship metadata answers how distinct concepts are structurally or mechanically connected. A relationship must therefore never create a canonical alias or erase a distinct source entity.

## Subclass parent Class

A Subclass is a distinct first-class `RuleConcept` with entity type `subclass`. Its owning Class remains a separate first-class `RuleConcept` with entity type `class`.

Rules Core persists the structural relationship:

```text
Subclass --parent-class--> Class
```

For 5e.tools-shaped source material, `className` and `classSource` from the source-native Subclass identity provide the evidence used to establish this relationship after both concepts are normalized. The normalized relationship is persisted between stable Rules Core concept identities; consumers do not need to reparse the source document or nested Class content.

The resolved catalog exposes each relationship as `kind`, related Rule Concept ID/key, related entity type, and related display name. `parent-class` does not imply a Character level, acquisition level, prerequisite, or feature application rule. Those mechanics remain defined by source material and later progression work.

Subclass concept keys include the parent Class name segment when that source-native relationship is available, for example:

```text
class.wizard
subclass.wizard.school-of-evocation
```

This prevents same-named Subclasses belonging to different Classes from collapsing merely because their display names match.

Prestige Classes are not Subclasses. They remain independent concepts with the canonical entity type `prestigeClass`; this relationship contract does not attach them to a parent Class or define their advancement semantics.

## Composite skills

The supported mechanical relationship kind is `composite-skill` with:

- `composition: arithmetic-mean`;
- `direction: components-to-parent`.

The reviewed relationships are:

- `Hide` + `Move Silently` -> `Stealth`;
- `Listen` + `Spot` -> `Perception`;
- `Climb` + `Jump` + `Swim` -> `Athletics`;
- `Balance` + `Tumble` -> `Acrobatics`.

The granular competencies remain independent Rule Concepts and independent canonical source entities. They are not alternate names for the umbrella competency.

### Value composition

For a composite relationship, Rules Core evaluates each component using modifiers explicitly targeted to that component, then computes:

```text
derived parent value
    = arithmetic mean of effective component values, truncated toward zero
    + modifiers explicitly targeting the parent
```

The direction is intentionally one-way. A parent modifier never modifies component values.

For example, `Hide +9` and `Move Silently +3` derive `Stealth +6`. A `+2 Hide` modifier produces effective Hide `+11` and derived Stealth `+7`. A `+2 Stealth` modifier produces Stealth `+8` while Hide remains `+9` and Move Silently remains `+3`.

The evaluator consumes structured competency targets. It does not infer modifier intent from natural-language descriptions.

## Reconciliation

When all concepts in a known composite exist, the relationship is structurally recognized without requiring a Rules Lawyer to rediscover the consolidation.

The default recommendation is `derive-parent`:

```text
Preserve: granular competencies
Derive:   umbrella competency from the components
```

The granular competencies are the recommended rule-bearing foundation because they retain more information. Source-backed parent and component rules remain separately visible so conflicting mechanics can still be adjudicated.

A Rules Lawyer may record an explicit `independent-parent` override. Relationship rulings are numbered and persisted as structured data; they are not inferred from notes. Recording `derive-parent` again restores the default structural resolution while retaining ruling history.

## Deferred relationships

The current composite catalog intentionally does not attach `Search`, `Escape Artist`, `Spellcraft`, `Gather Information`, `Ride`, `Use Rope`, `Concentration`, `Use Magic Device`, remaining Knowledge specialties, or remaining Craft/Profession relationships to later competencies.

The relationship representation is intentionally generic enough to add partial contributions, one-to-many splits, skill-to-tool relationships, skill-to-saving-throw relationships, and other cross-type competency relationships later without placing those semantics in canonical identity or source adapters.
