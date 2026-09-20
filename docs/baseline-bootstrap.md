# Built-in source and rules baseline

Rules Core initializes a deterministic public baseline after the database schema is ready. Source registration, acquisition metadata, immutable source representations, canonical recognition, and Rules Layer adjudication remain separate so provenance is not collapsed into house rules.

## Startup behavior

`RulesCore:BootstrapBaseline` defaults to `true`.

On startup, the baseline bootstrapper:

1. ensures the built-in `SourcePackage` access/distribution boundaries exist;
2. records corpus-membership authority references for the 3e, 3.5e, 5.1, and 5.2.1 SRDs;
3. hydrates reviewed bundled snapshots of all four SRDs into the public immutable Source Layer when the expected representation is not already present;
4. registers four persistent hosted-source definitions as Advanced maintenance metadata for deliberate upstream comparison/rebuild work;
5. imports the local immutable Dorks & Dice baseline-rule source document through the normalized source pipeline;
6. synchronizes normalized skill/tool competencies from the exact reviewed bundled SRD representations into the Global Rules Layer;
7. on a fresh Rules Layer, publishes the six settled house rules and reviewed competency decisions together in the first global ruleset; on an existing installation, publishes a new immutable revision only when the reviewed competency synchronization adds missing baseline decisions.

The authority-reference records are stored in `source_package_authority_reference`. They identify the external authority used to determine corpus membership; they are not Source Layer parent entities and they do not contain the imported source body.

Startup does **not** fetch a remote SRD corpus. The reviewed 3e, 3.5e, 5.1, and 5.2.1 snapshots are embedded in the Rules Core build and hydrated locally. Runtime source/rule reads therefore do not depend on Dragon.ee, GitHub, an authority archive, or another remote host being online.

A conflicting built-in package identity stops bootstrap instead of silently changing package provenance. Existing hosted-source definition revisions are not overwritten, so later deliberate Rules Lawyer maintenance remains authoritative.

## Source-model note

The current persisted Source Layer is `SourcePackage`, `SourceRepresentation`, `SourceEntity`, and `SourceEntityRevision`. There is no persisted `SourceWork` or `SourceEdition` parent.

Some bootstrap catalog and hosted-source compatibility request types still use `WorkKey`, `WorkDisplayName`, `EditionKey`, and `EditionDisplayName`. In the current implementation these values organize acquisition metadata and help form stable representation origin identities; they are not database hierarchy nodes. Canonical publication identity is resolved separately from adapter evidence.

## SRD authority and parseable representations

Rules Core separates **corpus-membership authority** from the **representation parsed for import**.

### 3e

- Membership authority: archived SRD 3.0 distribution index at `https://web.archive.org/web/20080209011829/http://www.opengamingfoundation.org/srd.html`.
- Parseable maintenance representation: `https://www.dragon.ee/30srd/`.
- Hosted maintenance format: `legacy-srd-text` with `html-index` expansion.
- Normalized source label: `SRD3`.

The Dragon.ee mirror is a surviving parseable representation, not the authority defining SRD membership. Because it is mutable rather than Git-backed, the built-in acquisition path uses a manually reviewed 139-document manifest. Missing or unexpected HTML documents fail that maintenance acquisition rather than silently changing corpus membership.

### 3.5e

- Membership authority: archived official Wizards Revised 3.5 SRD ZIP at `https://web.archive.org/web/20160328013113/http://www.wizards.com/d20/files/v35/SRD.zip`.
- Parseable maintenance representation: `olimot/srd-v3.5-md` at reviewed commit `c7f30a0ce11a579f75456746f278a4c75f67b4c1`.
- Hosted maintenance format: `legacy-srd-text` using explicit `github-tree` roots for `basic-rules-and-legal`, `divine`, `epic`, `magic-items`, `monsters`, `psionics`, and `spells`.
- Normalized source label: `SRD35`.

`SRD3` and `SRD35` are Rules Core normalization labels. They are not asserted to be historical Wizards source codes.

The legacy maintenance adapter preserves document URI, heading, body, and deterministic normalized source identity. Classification is evidence-based rather than path-only. Recognized families include classes, prestige classes, NPC classes, races, skills, feats, spells, psionic powers, divine domains, salient divine abilities, monsters, and magic items; remaining material is retained as generic rule evidence rather than discarded.

### 5e and 5.5e

For 5e and 5.5e, the official SRD PDF is the corpus-membership authority. The reviewed structured JSON mirror is only the maintenance representation used to construct/verify the bundled source snapshot. It is pinned to `CoolFireGiant/hewnhero-srd` commit `d06d1dadee357857767b1e4da985df6609509bcf`.

Authority references are:

- SRD 5.1: `https://media.wizards.com/2023/downloads/dnd/SRD_CC_v5.1.pdf`
- SRD 5.2.1: `https://media.dndbeyond.com/compendium-images/srd/5.2/SRD_CC_v5.2.1.pdf`

The maintenance path retains an explicit reviewed membership catalog for aggregate structured files where the mirror can contain material from more than one release.

For SRD 5.1 the reviewed aggregate membership includes Acolyte, Grappler, and the SRD races Dragonborn, Dwarf, Elf, Gnome, Half-Elf, Half-Orc, Halfling, Human, and Tiefling.

For SRD 5.2.1 the reviewed aggregate membership includes Acolyte, Criminal, Sage, Soldier; Dragonborn, Dwarf, Elf, Gnome, Goliath, Halfling, Human, Orc, Tiefling; the reviewed Origin, General, Fighting Style, and Epic Boon feats represented in the official PDF. The structured mirror's duplicate `Magic Initiate` records are not allowed to redefine durable identity: the reviewed 2024/SRD52 representation is selected rather than adding page number to native identity as a workaround.

Edition-specific bestiary/spell files are used where available. Mixed aggregate files remain filtered by source code plus reviewed membership evidence where required.

## Persistent hosted SRD definitions

Fresh installations register these maintenance definitions:

- `builtin-wotc-srd-3e`
- `builtin-wotc-srd-3-5e`
- `builtin-wotc-srd-5-1`
- `builtin-wotc-srd-5-2-1`

The 3e/3.5e definitions target public package `wotc-srd-ogl` with OGL-1.0a provenance. The 5.1/5.2.1 definitions target public package `wotc-srd-cc` with CC-BY-4.0 provenance.

Git-backed representation URLs are pinned to reviewed commits. The mutable 3e mirror is constrained by the reviewed manifest. Bootstrap registers missing definitions but does not overwrite a later Rules Lawyer revision.

These definitions are Advanced maintenance/acquisition metadata. Ordinary Source Library availability comes from the embedded reviewed snapshots, not from a live preview or refresh of those hosts.

## Built-in source packages

### Wizards OGL SRDs

Package `wotc-srd-ogl` is the public access boundary for the bundled 3e and 3.5e SRD representations. Bootstrap catalog compatibility keys distinguish `srd-3e` and `srd-3-5e`, but the persisted imported entities are directly package-scoped and each immutable revision points to its supplying `SourceRepresentation`.

### Wizards Creative Commons SRDs

Package `wotc-srd-cc` is the public access boundary for bundled SRD 5.1 and SRD 5.2.1. Canonical publication evidence distinguishes the actual releases and game editions independently of the package.

### Loot Tavern

The baseline package registry reserves two access families:

- `loot-tavern-free` — public/free releases;
- `loot-tavern-licensed` — restricted user-owned/Patreon/paid releases.

Bootstrap does not fabricate publication identities or source bodies for Loot Tavern. Material is added only when its actual representation and access/redistribution conditions are known.

## Canonical recognition and cross-edition resolution

Importing several editions does not establish edition precedence. Source entities preserve their native identities and physical provenance. Canonical recognition may identify exact cross-representation identity or explicit source history, but a Dorks & Dice `RuleConcept` remains a separate Rules Layer object.

The Rules Layer may bind multiple implementations to one concept and then explicitly select, consolidate, or override them. It does not encode a general rule such as “newest edition wins.”

When published rules use source contributions, access is evaluated across the actual applied provenance set. A public base can not redistribute a restricted incorporated contribution. Campaign inheritance retains those access requirements unless the campaign decision replaces the global source/contribution path.

Runtime canonical substitution follows the same access rule: if a published snapshot points to an inaccessible source revision, Rules Core may use another revision associated with the same canonical entity when that alternative comes from a package accessible to the requesting user. Trusted canonical identity may intentionally connect different normalized presentations of the same underlying entity; the substitution reuses that identity but never exposes another user's inaccessible representation.

## Reviewed bundled SRD competency baseline

Source Layer import does not normally make source content an effective table rule. Arbitrary uploads, private packages, hosted-source refreshes, and other imported material still require the ordinary Rules Lawyer normalization, decision, and publication workflow.

The four checked-in reviewed SRD representations are a deliberately narrower bootstrap exception for competencies. Eligibility is based on the exact embedded representation identity: package, origin identity, adapter format, and SHA-256 content hash must match the reviewed bytes. Merely importing another skill into `wotc-srd-ogl` or `wotc-srd-cc` does not make it eligible for baseline publication.

For eligible `skill` and `tool` records, bootstrap uses the already-normalized Source Layer identity and the ordinary stable concept-key convention. It does not maintain a second conversion table. Reviewed direct convergence performed by `PcGenCompetencyConversions` and `ExactCompetencyTranslationPolicy` therefore remains authoritative. For example, source-native Bluff and canonical Deception converge before Rules Layer synchronization, so bootstrap creates/reuses `skill.deception` rather than publishing an additional `skill.bluff` concept solely because an older edition used that name.

Direct convergence is distinct from composite competency relationships. Hide and Move Silently remain independent concepts beside Stealth; Listen and Spot remain independent beside Perception; Climb, Jump, and Swim remain independent beside Athletics; Balance and Tumble remain independent beside Acrobatics. `KnownMechanicalRelationships` continues to supply the existing `derive-parent` behavior when all required concepts are present. Bootstrap does not encode a second hierarchy.

Older reviewed competencies that have no approved direct convergence remain independent effective concepts. The same rule applies to individual Knowledge, Craft, Perform, Profession, and psionic specialties represented by the reviewed corpus. Bootstrap never replaces those with wildcard family rows.

When more than one reviewed implementation maps to one competency concept, synchronization first applies the existing mechanical equivalence/additive-resolution policy inside the exact reviewed source set. A single reviewed implementation, or multiple mechanically identical reviewed implementations, can establish a deterministic baseline directly. When reviewed implementations genuinely differ, the later work in the checked-in reviewed baseline catalog supplies only the **default presentation** for that concept. The other reviewed implementations remain available as edition-specific competency profiles, and this fallback does not assert that their mechanics are equivalent or establish a general "newest edition wins" rule for imports. A later Rules Lawyer decision remains authoritative.

Existing Global Rule decisions are authoritative. Baseline synchronization does not replace or advance a Rules Lawyer-authored competency decision, and it does not mutate prior immutable ruleset revisions. On an installation that already has the historical six-rule baseline, synchronization adds only missing reviewed competency concepts, canonical bindings, and decisions, then publishes a new immutable global revision when those baseline decisions changed the effective ruleset. Re-running bootstrap is idempotent: stable concept keys, canonical binding uniqueness, existing-decision checks, and ruleset fingerprints prevent duplicate concepts, bindings, decisions, or no-op revisions.

## Dorks & Dice adjudicated baseline

The deterministic public package `dorks-and-dice-baseline` contains the settled house-rule source material currently seeded by bootstrap:

- **Healing Potion Use** — bonus action uses rolled healing; using the action grants maximum possible healing.
- **Spell Preparation** — the normal prepared-spell restriction is removed.
- **Spellcasting Resource Choice** — casters may choose spell slots or spell points.
- **Controlled Creature Initiative** — a controlled creature acts on its controller's initiative.
- **Free Flavor Feats** — purely flavor feats do not consume a mechanical feat choice.
- **Cross-Edition Additive Compatibility** — 3e, 3.5e, 5e, and 5.5e are merged additively; omission from a newer edition does not remove a compatible older option; explicit conflicts require adjudication; source provenance is preserved.

On a fresh Rules Layer these source entities are bound to stable `house.*` concepts and published in the first global ruleset revision together with the reviewed bundled SRD competency baseline.

Unresolved class/prestige progression, feat cadence, BAB/skill-rank/save prerequisite conversion, prestige spellcasting progression, and similar open design questions remain unseeded. Detailed initiative-block execution remains the Initiative Tracker's responsibility.

## Anonymous global access

Public packages and published global rules do not require authentication. Restricted package content still requires a matching source grant, and contribution-aware resolution checks every source revision actually used by a published decision.

Canonical recognition metadata does not grant source access. The private-source regression suite verifies that two users can share canonical identity while each resolves only through their own accessible package/representation.

## Validation policy

Most historical integration tests run with `RulesCore__BootstrapBaseline=false` so isolated test assumptions remain stable. Dedicated bootstrap/source tests cover authority references, all four bundled snapshots, hosted maintenance definitions, anonymous bundled-source visibility, repeat-bootstrap idempotence, reviewed legacy corpus constraints, 5e/5.5e membership constraints, cross-edition normalization/adjudication, contribution provenance and access, initial global publication, preservation of later Rules Lawyer edits, and anonymous public access.
