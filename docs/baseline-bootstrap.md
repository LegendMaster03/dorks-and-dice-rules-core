# Built-in source and rules baseline

Rules Core initializes a small deterministic baseline after the database schema is ready. The baseline is deliberately split into **source registration** and **rules adjudication** so installing Rules Core does not collapse source provenance into the Dorks & Dice house rules.

## Startup behavior

`RulesCore:BootstrapBaseline` defaults to `true`.

On startup, the baseline bootstrapper:

1. ensures the built-in source package/work/release identities exist;
2. records the official PDF authority for SRD 5.1 and SRD 5.2.1;
3. imports the local immutable Dorks & Dice baseline-rule source document;
4. on a fresh Rules Layer only, creates the settled baseline concepts/decisions and publishes the first global ruleset.

The bootstrapper makes **no network requests**. In particular, it does not hydrate an SRD corpus merely because a third-party JSON representation claims that an entry belongs to the SRD.

Existing immutable source identities must match the built-in metadata. A conflicting package/work/release identity stops startup rather than silently changing provenance.

The rules baseline is conservative. If the database already contains a published ruleset, a non-baseline concept, or a non-bootstrap global decision, startup imports the baseline source document but does not create or publish decisions over the existing Rules Layer.

## PDF authority and structured representations

For 5e and 5.5e, the official SRD PDF is the corpus-membership authority. A structured JSON representation may be used later for convenient field-level ingestion, but it does not decide what content belongs to the SRD.

Rules Core records these authority documents on fresh databases:

- SRD 5.1: `https://media.wizards.com/2023/downloads/dnd/SRD_CC_v5.1.pdf`
- SRD 5.2.1: `https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf`

The authority link is attached to the logical source release, not to an individual downloaded copy. The expected ingestion workflow is:

```text
official PDF
-> extract/review a versioned membership manifest
-> acquire a structured representation
-> admit only entities represented by the PDF manifest
-> validate duplicates/ambiguities
-> preview
-> import immutable source revisions
```

This specifically prevents a 5e.tools/repack-specific source code, `srd` flag, page number, or malformed `reprintedAs` field from silently expanding or shrinking the canonical SRD corpus.

The PDF also resolves the previously observed SRD 5.2.1 feat ambiguity at the conceptual level: `Magic Initiate` is explicitly present in the official feat section. A duplicate `Magic Initiate|SRD52` in a structured repack is therefore a representation defect to resolve, not a reason to omit the official feat from the SRD corpus.

## Built-in source families

### D&D 3e and 3.5e SRDs

Rules Core registers the public Wizards of the Coast SRD identities under package `wotc-srd-ogl` with `OGL-1.0a` provenance:

- `srd-3e` / 3e;
- `srd-3-5e` / 3.5e.

No live mirror is guessed. Their source documents/adapters will be attached only after a deliberate source-selection and validation pass.

### D&D 5e SRD 5.1 and 5.5e SRD 5.2.1

Rules Core registers the Wizards SRDs under package `wotc-srd-cc` with `CC-BY-4.0` provenance:

- `srd-5-1` / release `5.1` / game edition `5e`;
- `srd-5-2-1` / release `5.2.1` / game edition `5.5e`.

The official PDFs above are persisted as `membership-authority` references. Structured corpus hydration is intentionally not automatic until a PDF-derived manifest has been generated and reviewed.

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
- official SRD 5.1 and 5.2.1 PDF authority references;
- the six deterministic house-rule source entities;
- initial global baseline publication;
- repeat-bootstrap idempotence;
- preservation of a later user-authored global decision.
