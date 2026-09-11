# Built-in source and rules baseline

Rules Core initializes a small deterministic baseline after the database schema is ready. The baseline is deliberately split into **source registration** and **rules adjudication** so installing Rules Core does not collapse source provenance into the Dorks & Dice house rules.

## Startup behavior

`RulesCore:BootstrapBaseline` defaults to `true`.

On startup, the baseline bootstrapper:

1. ensures the built-in source package/work/release identities exist;
2. registers missing built-in hosted-source definitions without fetching them;
3. imports the local immutable Dorks & Dice baseline-rule source document;
4. on a fresh Rules Layer only, creates the settled baseline concepts/decisions and publishes the first global ruleset.

The bootstrapper does **not** make network requests. Hosted SRD content is fetched only when a Rules Lawyer explicitly previews or refreshes a hosted source.

Existing immutable source identities must match the built-in metadata. A conflicting package/work/release identity stops startup rather than silently changing provenance.

Existing hosted-source definitions are never rewritten by bootstrap. If a Rules Lawyer edits or disables a built-in definition, later starts preserve that revision.

The rules baseline is also conservative. If the database already contains a published ruleset, a non-baseline concept, or a non-bootstrap global decision, startup imports the baseline source document but does not create or publish decisions over the existing Rules Layer.

## Built-in source families

### D&D 3e and 3.5e SRDs

Rules Core registers the public Wizards of the Coast SRD identities under package `wotc-srd-ogl` with `OGL-1.0a` provenance:

- `srd-3e` / 3e;
- `srd-3-5e` / 3.5e.

No live source URL is guessed. A structured acquisition representation will be attached when a source/adapter has been deliberately selected and validated.

### D&D 5e SRD 5.1 and 5.5e SRD 5.2.1

Rules Core registers the Wizards SRDs under package `wotc-srd-cc` with `CC-BY-4.0` provenance:

- `srd-5-1` / release `5.1` / game edition `5e`;
- `srd-5-2-1` / release `5.2.1` / game edition `5.5e`.

The built-in hosted acquisition definitions use the public `CoolFireGiant/hewnhero-srd` SRD-only 5e.tools-shaped representation. That repository identifies its entries as `SRD51` and `SRD52` and attributes the game text to Wizards of the Coast under CC BY 4.0. The representation is acquisition provenance; it does not replace Wizards as the canonical source provider.

The hosted definitions cover actions, backgrounds, bestiary, classes, conditions/diseases, deities, equipment/items, languages, magic variants, objects, optional features, races/species, senses, skills, spells, tables, traps/hazards, variant rules, and vehicles.

SRD 5.1 also includes `feats.json`. SRD 5.2.1 currently **does not**. The current upstream SRD-only representation contains two `Magic Initiate|SRD52` records with the same durable natural identity. One is internally contradictory/self-referential. Rules Core therefore quarantines that source family rather than adding page number to identity, dropping one record, or guessing which record should win.

The full 5e.tools data repository is not used as the public baseline corpus because its current entity records commonly retain original book source codes such as `PHB`, `DMG`, `XPHB`, or `XDMG` and separately mark SRD membership with `srd` / `srd52` flags. A source-code-only filter can not prove that an arbitrary full-data record belongs to the Creative Commons SRD corpus.

### Loot Tavern

The source registry separates Loot Tavern into two package families:

- `loot-tavern-free` — public/free releases;
- `loot-tavern-licensed` — restricted user-owned/Patreon/paid releases.

No specific Loot Tavern work is fabricated during bootstrap. Releases are attached to the appropriate package only when the actual release and its distribution/access terms are known. This prevents free distributable material from granting access to paid material and prevents a direct-link-only free release from being treated as though Rules Core may redistribute its original file.

## Dorks & Dice adjudicated baseline

The deterministic local source package `dorks-and-dice-baseline` contains the settled house rules that can be represented without resolving unfinished progression design:

- **Healing Potion Use** — bonus action uses rolled healing; using the action grants maximum possible healing.
- **Spell Preparation** — the normal prepared-spell restriction is removed.
- **Spellcasting Resource Choice** — casters may choose spell slots or spell points.
- **Controlled Creature Initiative** — a controlled creature acts on its controller's initiative.
- **Free Flavor Feats** — purely flavor feats do not consume a mechanical feat choice.
- **Cross-Edition Additive Compatibility** — 3e, 3.5e, 5e, and 5.5e are merged additively; omission from a newer edition does not remove a compatible older option; explicit conflicts require adjudication; source provenance is preserved.

On a fresh Rules Layer these six source entities are each bound to a stable `house.*` concept, selected as global decisions, and published as the first global ruleset revision.

The bootstrap intentionally does not encode unresolved class/prestige progression, feat cadence, BAB/skill-rank/save prerequisite conversion, prestige spellcasting progression, or other open design questions. Detailed runtime initiative-block execution also remains the responsibility of the Initiative Tracker rather than being invented by this bootstrap.

## Validation policy

Normal integration tests run with `RulesCore__BootstrapBaseline=false` so their historical isolated assumptions remain valid. `BaselineBootstrapIntegrationTests` invokes the bootstrapper directly and verifies:

- built-in source packages and edition families;
- hosted SRD 5.1/5.2.1 registrations without network hydration;
- SRD 5.2.1 feat quarantine;
- the six deterministic house-rule source entities;
- initial global baseline publication;
- repeat-bootstrap idempotence;
- preservation of a Rules Lawyer's hosted-definition edit;
- preservation of a later user-authored global decision.
