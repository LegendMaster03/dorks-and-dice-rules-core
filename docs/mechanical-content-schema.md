# Rules Core mechanical content schema

Rules Core deliberately separates **source/provenance representation** from **mechanical game-data representation**.

The Source Layer is format-neutral. It preserves physical bytes, source-native identity, source-native revisions, access grants, publication evidence, canonical occurrences, and reconciliation metadata.

The mechanical content contract is not format-neutral. It uses the actual 5e.tools entity model as the reference/default schema and adds explicit Rules Core extensions only when another edition or source contains mechanics that can not be represented faithfully by the applicable 5e.tools family.

The intended flow is:

`source bytes -> source-native record -> translation -> 5e.tools-derived Rules Core content -> canonical/rules consumption`

`SourceEntityRevision.RawJson` remains the immutable source-native record. `SourceEntityRevision.ContentJson` is the translated mechanical body. Native revision fingerprints are calculated from `RawJson`, never from translation output.

`ContentJson` is explicitly versioned derived state. Translator improvements can be
replayed over existing revisions without changing `RawJson`, the native fingerprint,
or the native revision number. `normalization_version` records the last successfully
applied interpretation version; failed attempts are retained separately so background
maintenance does not loop indefinitely. Increment `SourceNormalizationVersion.Current`
whenever a translation/normalization change must be applied retroactively.

## Upstream 5e.tools review

The reference review was performed against the upstream 5e.tools corpus in `5etools-mirror-3/5etools-src` and the schema-template/schema tooling in `TheGiddyLimit/5etools-utils`. Representative structures were checked for:

- creatures/bestiary;
- spells;
- items/equipment;
- feats;
- classes and subclasses;
- races/species and subraces;
- backgrounds;
- conditions, statuses, and diseases;
- actions;
- optional features;
- books and adventures;
- source/publication metadata and `_meta`;
- fluff/content companions;
- `_copy` and copy-modification semantics;
- `_versions`/version and reprint conventions;
- tagged renderer text and references;
- polymorphic fields and family-specific alternatives.

The upstream model is a collection of rich, family-specific schemas. It is not one universal entity envelope. Common conventions exist, but a field present in one family must not be assumed to exist in every family.

Important upstream conventions include:

- `name` and `source` on ordinary mechanical entities;
- `page` where the source supplies a page locator;
- nested family-specific mechanical fields rather than flattened key/value projections;
- renderer-tagged text such as `{@spell ...}`, `{@item ...}`, `{@condition ...}`, dice/damage tags, and other reference syntax;
- `_copy` as a first-class inheritance/copy mechanism with structured modification operations;
- `_versions` and reprint/reference fields for versioned or reissued content where the family permits them;
- corpus `id` and `parentSource` on publication/corpus structures where applicable;
- `edition` only in schema families that define it;
- `_meta.sources` and related metadata for homebrew/source declarations;
- `uniqueId` as a source/editor identity convention where used, especially editable homebrew workflows, rather than as a universal entity identifier;
- fluff as companion content structures instead of requiring every mechanical entity to inline all descriptive material.

Rules Core preserves unknown 5e.tools fields rather than projecting native entities through a narrow DTO. This is required both for current rich structures and for forward compatibility with upstream fields that Rules Core does not yet understand.

## Native 5e.tools content

For an ordinary native 5e.tools mechanical entity:

- `RawJson` contains the complete native entity object accepted by the adapter;
- `ContentJson` is effectively the same complete entity object;
- unknown fields survive unchanged;
- `source`, `page`, `_copy`, nested mechanics, tagged text, family-specific fields, and any applicable identity/version fields remain intact;
- Rules Core does not flatten the entity into a generic `{name, entityType, segments}` structure.

Translation may add a Rules Core extension only when Rules Core itself needs additional information that is not expressible in the native family. It must not overwrite or reinterpret an upstream field with a different meaning.

## PCGen and 3e/3.5e translation

PCGen remains source-native evidence. PCC/LST parsing preserves campaign metadata, publication context, source names, publisher/date evidence, game modes, file references, native keys, operations, raw tag/value segments, and unsupported fragments.

Recognized PCGen mechanical records are translated into the applicable 5e.tools-derived family shape. The translator is based on actual PCGen 3e/3.5e conventions rather than synthetic 5e-style tags.

Current classification rules include:

- a PCGen `ABILITY` record whose `CATEGORY` is `FEAT` translates as a feat while retaining its `pcgen|ability|...` native key;
- a PCGen `RACE` record with `MONSTERCLASS`, or a race record in a strongly identified monster source path, translates as a monster while retaining its `pcgen|race|...` native key;
- ordinary playable race records remain races.

Current field mappings intentionally cover only defensible equivalences:

- spell level is derived from actual `CLASSES`/`DOMAINS` assignments only when those assignments agree on one level; class/domain access itself remains preserved as PCGen-specific mechanics;
- recognized spell school, simple verbal/somatic components, instantaneous duration, and descriptions map to spell-family fields;
- feat description and `MULT` repeatability map to feat-family fields; the reviewed `PRESTAT`/`PRESKILL`/`PRECLASS` subset and simple `PREMULT` groups are additionally normalized under `_rulesCore.character.prerequisites`, while unsupported prerequisite expressions remain source-specific;
- equipment weight maps directly, and PCGen `COST` is interpreted according to PCGen semantics: a bare numeric cost is gold pieces and is converted to the 5e.tools copper-piece `value` representation;
- ordinary race size, walk speed, and racial `BONUS:STAT` modifiers can map to race-family equivalents when faithful;
- monster size, recognized creature type, supported movement modes, CR, and descriptions can map directly;
- Size normalization recognizes the universal Fine/Diminutive/Tiny/Small/Medium/Large/Huge/Gargantuan/Colossal catalog; unknown source-defined Size values remain preserved as unmapped/source-specific evidence rather than being discarded.

### Normalized PCGen competency metadata

When a PCGen skill record contains competency mechanics Rules Core understands, translation records those semantics under `_rulesCore.competency` before canonical reconciliation. The normalized profile can carry:

- competency kind (`skill`, `specialized-skill`, or `tool`);
- competency family/category and specialty for specialized families such as `Craft (...)`, `Perform (...)`, and `Profession (...)`;
- facet type, shared learned-competency identity, and shared training-state key when an established cross-edition skill/tool identity exists;
- governing ability derived from `KEYSTAT`;
- trained-only behavior derived from `USEUNTRAINED`;
- Armor Check Penalty applicability derived from `ACHECK`;
- rank, class-skill-state, and training-state support;
- game edition/profile identity and capability qualification.

`Craft`, `Perform`, and `Profession` are competency families. A family is taxonomy/organization; a specialized child remains independently addressable and retains its own ranks, class-skill state, governing Ability, training state, source identity, and provenance. Nesting never causes numeric mechanics to be inherited or merged.

`Knowledge` is the intentional exception. A reviewed `Knowledge (X)` record normalizes directly to competency identity `X`. The Character-facing contract therefore exposes `Arcana`, `History`, `Psionics`, `The planes`, and similar specialties directly rather than exposing a visible `Knowledge -> X` hierarchy. The older source name and provenance remain preserved.

This is mechanical normalization, not source destruction. Original PCGen tags remain in `RawJson` and continue to be retained under `_rulesCore.pcgen.unmappedSegments` where applicable. Character-oriented consumers read the normalized competency contract rather than interpreting PCGen tags or parsing display names.

### Direct normalized competency identities

Reviewed direct identity translations change the normalized source name/type before canonical reconciliation. They are appropriate only when the older and later records are the same competency rather than related competencies or distinct facets.

The reviewed skill identities are:

- `Bluff` -> `Deception`;
- `Diplomacy` -> `Persuasion`;
- `Handle Animal` -> `Animal Handling`;
- `Heal` -> `Medicine`;
- `Intimidate` -> `Intimidation`;
- every 3.x `Knowledge (X)` specialty -> `X`;
- `Sense Motive` -> `Insight`;
- `Sleight of Hand` -> `Sleight of Hand`;
- `Survival` -> `Survival`.

The following apply only to 3e/3.0 sources:

- `Pick Pocket` -> `Sleight of Hand`;
- `Wilderness Lore` -> `Survival`.

Only these exact identity translations use `NormalizedSourceRecord.CanonicalIdentityKey` to converge source representations before Rules Layer normalization. A shared skill/tool learned competency does **not** use canonical source identity, because a skill Rule Concept and a tool Rule Concept retain different source types and mechanical contracts.

### Specialized competency facets

Some historical skills and later tool proficiencies represent different mechanical facets of one learned discipline. Rules Core records a separate learned-competency identity while preserving each facet as its own canonical source entity and Rule Concept.

The reviewed shared identities are:

- 3e `Alchemy`, 3.5e `Craft (alchemy)`, and later `Alchemist's Supplies` -> shared learned competency `Alchemy`;
- `Forgery` and later `Forgery Kit` -> shared learned competency `Forgery`.

The shared state is conceptual training/proficiency identity. Facet-specific mechanics do not transfer. A 3.x Alchemy/Craft profile can carry ranks, class-skill state, governing Ability, trained-only behavior, and Armor Check Penalty semantics. The later tool facet uses its own proficiency mechanics. Ranks never become a tool bonus, proficiency bonus never becomes ranks, and class-skill state is never discarded.

The source conversion relationship is `shared-competency-facet`. The normalized competency profile carries `identityKey`, `identityName`, `sharedTrainingKey`, and `facetType`. Later reviewed tool facets receive the same learned-competency identity in the Character mechanics consumer without rewriting their source document.

The Character mechanics contract promotes this identity into the first-class `competencies` catalog. The source-shaped skill/tool Rule Concepts remain implementation mechanics, while one universal semantic entry owns the Character display identity and training-state key. Reviewed direct historical aliases are handled by the same semantic policy. Family membership comes from the reviewed Rules-layer catalog rather than being reconstructed by Character consumers from source names.

### Scoped and related competency relationships

Cross-type similarity is not sufficient for shared identity.

- `Open Lock` -> `Thieves' Tools` is scoped to `open-lock`.
- `Disable Device` -> `Thieves' Tools` is scoped to `disable-device`.
- `Disguise` -> `Disguise Kit` is a `related-competency` relationship, not shared training identity. The historical skill covers broader disguise use than possession/proficiency with the later kit.

These relationships preserve the historical ranked skill and do not grant unrestricted tool proficiency.

The clean many-to-one consolidations Hide/Move Silently -> Stealth, Listen/Spot -> Perception, Balance/Tumble -> Acrobatics, and Climb/Jump/Swim -> Athletics remain distinct source-native competencies. Rules Layer represents those consolidations separately as directional `composite-skill` relationships; they are not competency families or shared facets.

`Perform` specialties remain independent specialized competencies and are not collapsed into modern `Performance`. Instrument-specific tool facets may be established only when source evidence identifies a defensible instrument correspondence. `Profession (...)` specialties likewise remain independent. Non-alchemy Craft specialties are not mapped to modern tools from name similarity alone.

The complete reviewed classification is maintained in `docs/competency-reconciliation-audit.md`.

PCGen monster race records normally encode **racial modifiers and racial hit-die declarations**, not a final 5e-style stat block. For example, `BONUS:STAT|STR|16`, `BONUS:COMBAT|AC|7|TYPE=NaturalArmor`, and `MONSTERCLASS:Aberration:8` do not by themselves establish the final Strength score, total AC, or HP formula. Rules Core therefore does not fabricate `str`, `ac`, or `hp` from those values. They remain preserved under the Rules Core extension until a translator has enough surrounding 3.x rules context to derive a faithful result.

## Rules Core extension namespace

A 3.x rule is not converted into a false 5e mechanic merely to fill a familiar field. Source-specific context and unmapped mechanics are separated inside `_rulesCore`:

```json
{
  "_rulesCore": {
    "context": {
      "edition": "3.5e",
      "sourceFormat": "pcgen-data",
      "nativeEntityType": "skill",
      "nativeName": "Bluff"
    },
    "competencyConversion": {
      "relationship": "direct-equivalence",
      "sourceType": "skill",
      "sourceName": "Bluff",
      "targetType": "skill",
      "targetName": "Deception"
    },
    "pcgen": {
      "unmappedSegments": [
        { "tag": "KEYSTAT", "value": "CHA" }
      ]
    }
  }
}
```

`_rulesCore.context` is translation/provenance context. It is persisted in `ContentJson` for inspection but is removed by `SourceEntityRevision.GetMechanicalContentJson()` before ordinary semantic comparison, additive resolution, patching, or resolved-rule output.

`_rulesCore.competencyConversion` records how the source terminology translated. For an unscoped reviewed mapping it documents the exact translation; for a scoped mapping such as Open Lock it remains the rule-bearing scope relationship.

`NormalizedSourceRecord.CanonicalIdentityKey` carries the reviewed exact competency identity separately from mechanical content. The normalized importer uses it to derive canonical identity during reconciliation, but it is neither source evidence nor part of `ContentJson`.

Historical persisted content may still contain `_rulesCore.exactCompetencyIdentity` from the earlier implementation. Canonical identity readers continue to recognize that marker for backward compatibility, but current imports do not write it.

### Character-support extension

Normalized rule content can publish Character-oriented support mechanics under `_rulesCore.characterSupport`. This extension is rule-bearing and therefore survives `RulesMechanicalContent.ForRules()`; only `_rulesCore.context` is removed from the rule-bearing view.

The extension has three independent arrays:

```json
{
  "_rulesCore": {
    "characterSupport": {
      "recoveryProcedures": [
        {
          "key": "recovery.example",
          "displayName": "Example Recovery",
          "presentationRole": "short-rest",
          "available": true,
          "applicability": {
            "kind": "character-capability",
            "requiresCharacterState": true,
            "requiredCapabilityKeys": ["example.recovery"]
          },
          "inputs": [
            {
              "key": "pointsToSpend",
              "valueKind": "integer",
              "origin": "character-state",
              "required": true
            }
          ],
          "choices": [
            {
              "key": "resource",
              "prompt": "Choose a resource.",
              "required": true,
              "options": [
                { "key": "focus", "displayName": "Focus", "value": "focus-points" }
              ]
            }
          ],
          "rolls": [
            {
              "key": "recoveryRoll",
              "rollKind": "d20",
              "prompt": "Roll recovery.",
              "required": true,
              "mechanicKey": "check.example-recovery",
              "rollMode": "normal"
            }
          ],
          "effects": [
            {
              "key": "spend",
              "targetKind": "resource",
              "targetKey": "recovery-points",
              "operation": "expend",
              "amountInputKey": "pointsToSpend"
            },
            {
              "key": "restore",
              "targetKind": "resource",
              "targetChoiceKey": "resource",
              "operation": "adjust",
              "amountRollKey": "recoveryRoll",
              "referenceKey": "rule-defined-limit"
            }
          ]
        }
      ],
      "passiveValues": [
        {
          "key": "passive.example-awareness",
          "displayName": "Example Awareness",
          "evaluationKind": "sum",
          "constant": 7,
          "inputs": [
            {
              "key": "awarenessContribution",
              "valueKind": "integer",
              "origin": "derived",
              "required": true,
              "participatesInValue": true
            }
          ],
          "relatedConceptKey": "skill.example-awareness",
          "relatedAbilityKey": "wisdom"
        }
      ],
      "qualifications": [
        {
          "key": "qualification.example-training",
          "displayName": "Example Training",
          "category": "source-defined-training",
          "family": "example",
          "associatedConceptKey": "training.example",
          "stateInput": {
            "key": "state",
            "valueKind": "string",
            "origin": "character-state",
            "required": true
          }
        }
      ]
    }
  }
}
```

These arrays are normalized Rules Core mechanics, not a source-format compatibility layer. A translator or reviewed normalized source must establish them from defensible source evidence. Character consumers never infer them from source-native display text.

Recovery procedure keys and qualification categories are open identities. `short-rest` and `long-rest` are optional presentation roles only. They do not imply duration or consequences. Recovery effects use structured target/operation semantics and may obtain a target from a declared choice or an amount from a declared Character input or runtime roll. Optional effect conditions can predicate an effect on one declared input value or choice. Runtime requirements can explicitly state Character state, choices, rolls, resource expenditure, and other runtime facts; structural requirements are also derived from declared inputs/choices/rolls/effects.

Passive values use the existing scalar mechanic evaluator and the dedicated `passive-value` kind. The normalized definition must supply the formula inputs/constant; Rules Core does not add a universal `10 + modifier` rule.

Qualifications carry a typed `stateInput` rather than a fixed proficiency boolean. A source can therefore represent a string training state, boolean qualification, integer rank, or another currently supported typed Character fact without defining Armor/Weapons/Tools/Languages as universal categories.

`available: false` preserves a normalized entry while marking it unavailable under the effective rule. Campaign/global JSON patches can alter or remove Character-support metadata through the existing rule-decision machinery; no second override layer exists.

`_rulesCore.pcgen.unmappedSegments` is also rule-bearing: those values represent mechanics that have not been translated into a faithful 5e.tools field. They remain in the rule-bearing view for mechanical inspection even when the separate canonical competency identity causes edition-specific representations to reconcile to the same competency.

The extension is subordinate to the complete `RawJson`; it is not a replacement 3.x ontology. Unsupported source fragments and operations remain separate source evidence until a translator can construct a legitimate mechanical entity.

Recovery roll definitions may optionally set `rollMode` to `normal`, `advantage`, `disadvantage`, or `emphasis`. Consumers use that as the rule-derived default but must allow a user to override the mode at roll time. `rollKind: "d20"` indicates that the standard d20 selection semantics apply. A consumer may always accept a manually entered result from physical dice instead of generating dice automatically.

## Legacy SemanticJson field

`NormalizedSourceRecord.SemanticJson` is retained temporarily for compatibility with the earlier adapter contract, but it is not the mechanical content contract and no longer drives canonical semantic fingerprints, Rules Layer comparison, patching, resolved output, or trusted-lineage alias eligibility. New translation work must use `ContentJson`.

The old PCGen `{name, entityType, segments}` projection is therefore not a target representation. It may be removed once the compatibility surface no longer needs the legacy property.

## PDF boundary

A PDF representation is valid Source Layer material even when no mechanical entity can be extracted confidently. Page-level text fragments therefore normally have `ContentJson = null` and retain only native/source evidence.

A PDF fragment becomes a monster, spell, item, feat, or other normalized mechanical entity only when an extractor has enough information to construct a legitimate family-shaped object. Rules Core must not fabricate a 5e.tools entity simply because a page contains text that resembles one.

## Canonical identity is separate

Mechanical translation does not determine source access or erase source-native evidence. Different packages retain independent raw records, revisions, and grants even when exact translation deliberately maps them to one canonical competency.

For the reviewed exact competency translations, canonical identity is intentionally shared across edition-specific source representations. This is narrower than general rule reconciliation: it applies only to the explicit importer whitelist and does not make source bodies or package access global.

For other entities, a PCGen record and a 5e.tools record can have different canonical entities or explicit revision/reprint/rename/variant relationships when their evidence requires it.

Canonical metadata never grants access to a private source representation. Rules Layer bindings remain canonical-aware and access-aware.

## Mechanical versus native consumers

Code must choose the representation based on intent.

Mechanical consumers normally use `SourceEntityRevision.GetMechanicalContentJson()`. It starts from translated `ContentJson` when present, falls back to `RawJson` for legacy/native records with no translation, and removes non-mechanical Rules Core translation context. This includes:

- semantic comparison and compatibility;
- automatic cross-edition resolution;
- rule preview;
- rule patching and overrides;
- resolved rules;
- campaign baselines;
- source-revision mechanical review;
- source-browser mechanical entity views.

Native/provenance consumers continue to use `RawJson`. Examples include native revision fingerprints and source-version relationship evidence that depends on explicit source-native references.

## Revision semantics

`RawJson` defines the native revision. A change in translated output alone must not manufacture a new source-native revision.

When an unchanged native record is reprocessed by an improved translator, Rules Core may refresh `ContentJson` on that existing revision. If the native source body changes, a new native revision is created using the canonicalized raw-body SHA-256 fingerprint.

This distinction allows translation logic to improve without falsifying source history.

## Seeding boundary

There is no separate developer seeding path for normal 3e, 3.5e, 5e, or 5.5e source ingestion. The ordinary **Add Source** workflow is also the initial seeding workflow.

On the first successful import from a registered trusted source lineage, Rules Core persists reusable canonical recognition metadata. Later imports can reuse that identity knowledge without gaining access to the first user's source package or source bytes. Rules Core does not require or maintain a global mirror of the complete 5e.tools or PCGen corpus.

The reviewed bundled SRD snapshots remain a separate public baseline concern. CI uses deterministic local fixtures and never depends on live upstream repositories.
## Character mechanics consumer projection

Character-oriented tools must not parse source-native or source-format-specific JSON to reconstruct common checks, 3.x sheet mechanics, or publisher-specific procedures. The normalized consumer contract in `docs/character-mechanics-consumer.md` projects stable mechanic keys, typed inputs, evaluation semantics, applicability, relationships, competency profiles, and accessible provenance while retaining this document's source-native/translated-content boundary.

When multiple accessible source representations share a reviewed canonical competency identity, the consumer may project multiple normalized competency profiles. This preserves capability-qualified mechanics such as 3.x ranks/class-skill state even when the published effective rule selects a later-edition presentation.
### Character prerequisite normalization

Character-facing PCGen prerequisites are normalized only when Rules Core can preserve their source semantics exactly. The executable subset currently includes `PRESTAT`, `PRESKILL`, and `PRECLASS`, plus simple `PREMULT` groups whose children are single requirements from that subset. Normalized groups retain their `N of M` match count and each predicate retains its source target, operator, and threshold.

More complex PCGen prerequisite trees remain in `_rulesCore.pcgen.unmappedSegments`; they are not flattened or guessed. The original prerequisite tags also remain in the source-native `RawJson` even when an executable normalized predicate is available. Character projection evaluates normalized prerequisites after the dependent Character mechanics have resolved and reports `true`, `false`, or an explicit unresolved state. An unsatisfied prerequisite does not delete or reject a selected Character option; it is surfaced as an eligibility result and conflict so a caller or DM can decide how to proceed.
