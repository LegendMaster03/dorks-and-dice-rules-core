# Rules Layer mechanical relationships

Rules Core keeps mechanical competency relationships separate from source provenance and canonical identity.

Canonical identity answers whether two source representations are the same competency. Mechanical relationships answer how distinct competencies participate in a rule calculation. A mechanical relationship must therefore never create a canonical alias or erase a granular source entity.

## Composite skills

The first supported relationship kind is `composite-skill` with:

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
