# Source Library and monster presentation

Rules Core keeps source-native material separate from translated mechanical content and from adjudicated/published rules. The Source Library is the user-facing entry point for asking **what the actual sources contain**. It does not silently promote source material into the Rules Layer.

## Bundled SRDs

Baseline bootstrap hydrates the reviewed bundled SRD representations into the public immutable Source Layer:

- 3e SRD (`SRD3`)
- 3.5e SRD (`SRD35`)
- SRD 5.1 / 5e (`SRD51`)
- SRD 5.2.1 / 5.5e (`SRD52`)

Normal Library use is local. A user does not need an account, a manual import action, or access to an upstream host to browse these bundled sources. The Library reports local entity counts, monster counts, and the most recent visible import time. Counts shown with `+` reached the browser query cap and are intentionally lower bounds.

Hosted Source Definitions and manual source administration remain under **Advanced** for maintenance and non-bundled source workflows.

## Source representation contract

A `SourceEntityRevision` deliberately has two relevant representations:

- `RawJson` is the immutable source-native record and participates in native revision identity.
- `ContentJson`, exposed through `GetMechanicalContentJson()`, is the translated rule-bearing representation consumed by Rules Core presentation and the Rules Layer.

For native 5e.tools records these may be very similar. For other adapters they can differ substantially. The UI must not collapse the distinction or recreate translation rules in JavaScript.

The Source Layer API exposes both views under the same source-access rules:

1. Search accessible source entities:

   `GET /api/sources/entities?entityType=monster&q=<name-or-source>&limit=100`

2. Retrieve the latest accessible entity and its Rules Core mechanical document:

   `GET /api/sources/entities/{entityId}`

3. Retrieve the exact source-native document for that same accessible revision:

   `GET /api/sources/entities/{entityId}/native`

The native endpoint returns not-found when the source package is not accessible to the current request identity, matching the existing source-detail access boundary.

## Monster presentation

Source monsters and published monsters share the same structural presentation renderer. The renderer uses the compact 5.5e monster-stat-block information hierarchy without converting older-edition mechanics into 5.5e mechanics.

The primary presentation is organized as:

1. name, size, creature type, descriptive tags, and alignment;
2. Armor Class, Hit Points, Speed, and Initiative when present;
3. all six abilities with score, modifier, and saving throw;
4. optional details such as skills, senses, languages, Challenge Rating, XP, proficiency bonus, vulnerabilities, resistances, immunities, condition immunities, and gear;
5. traits and spellcasting;
6. actions;
7. bonus actions;
8. reactions, legendary actions, mythic actions, lair actions, regional effects, and other structural sections when present.

Unknown rule-bearing fields are not discarded merely because they do not fit the common 5.5e headings. Additional top-level mechanics are presented under **Additional Mechanics**. Rule-bearing `_rulesCore.pcgen` values, including unmapped legacy source segments such as 3e/3.5e mechanics, are surfaced under **Source-Specific Mechanics**. Other Dorks & Dice extension mechanics are presented separately.

The renderer performs display formatting only. It does not parse legacy stat-block prose, synthesize initiative from Dexterity, translate skills, or apply edition-specific semantic conversion tables. Those decisions belong in the upstream normalized mechanical model.

## Source detail hierarchy

Playable/readable rule content appears first. Source provenance and integration information are secondary disclosures. Source detail then makes both representations explicitly available:

- **Source-native document** — the immutable `RawJson` record;
- **Rules Core mechanical document** — the translated mechanical record that drives presentation.

This keeps provenance inspectable without forcing users through metadata before reaching the monster stat block.

## Rules Layer boundary

Importing or browsing an SRD never automatically makes those entities table rules. Source entities remain available for browsing, normalization, version review, concept binding, consolidation, and explicit Rules Lawyer publication.

Published Rules render the effective mechanical document selected or produced by the Rules Layer. Source Library renders the mechanical representation of the selected source entity while retaining direct access to its source-native record. Same-name material from different editions is therefore never made equivalent merely by presentation.