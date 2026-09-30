# Rules Layer

The Rules Layer records Dorks & Dice adjudication separately from immutable source material. It never edits a Source Layer revision in place.

## Stable concepts

`rule_concept` provides the source-independent identity for a rule concept. A concept has a stable normalized key, an entity type, a display name, and creation audit information. The concept entity type is stable anchor metadata for authoring and direct source binding; it is not required to equal the category of every historical variation that can later become effective through authoritative canonical history.

Direct source-specific implementations are attached through `rule_concept_source_binding`. Those direct bindings remain mechanically type-coherent: a source whose normalized entity type differs from the concept can not be directly bound merely because its name matches.

A directly bound canonical entity also establishes the concept's authoritative evolving history. Canonical entities connected through transitive `revision` or `rename` relationships participate in that same history, including a related variation whose mechanical category changed across editions. `variant` and `reprint` relationships do not by themselves make another canonical entity interchangeable for Rules Layer resolution.

This allows multiple editions or providers to implement the same evolving logical concept while preserving provenance and mechanical category distinctions. For example, a concept anchored by a 3.5e `prestigeClass` source may legitimately resolve to a 5e `subclass` revision when canonical revision/rename evidence establishes that they are the same evolving concept. The direct binding remains type-coherent, while the selected source determines the effective category.

## Global decisions

A `global_rule_decision` is an append-only adjudication for one concept. Current global decision kinds are:

- `select-source` - publish one exact source revision as the rule implementation;
- `json-merge-patch` - use one exact source revision as the immutable base and apply a Rules Layer-authored JSON Merge Patch;
- `json-rule-patch` - use one exact source revision as the immutable base and apply an optional object merge plus ordered, item-aware array operations.

Every global decision records a monotonically increasing decision number, the exact base source revision, optional adjudication note, stable Dorks & Dice user ID, and timestamp. Patch decisions additionally store the canonical patch document and its SHA-256 fingerprint.

A selected source revision must belong to the concept's authoritative canonical `revision`/`rename` history. A same-name source with no such identity evidence is rejected. A `variant` or `reprint` relationship alone is also insufficient. This preserves strict identity semantics while allowing legitimate cross-category edition evolution.

Selecting an exact source revision is intentional. If the same source entity is imported again and receives revision 2, a decision based on revision 1 does not silently move. A Rules Lawyer must explicitly create a new decision using revision 2 before a later published ruleset uses it.

Equivalent patch objects are canonicalized by recursively sorting object property names before fingerprinting. Reordering object properties therefore does not create a new decision when the ordered operation list, base source revision, and adjudication note are otherwise unchanged. Array operation order is preserved because operation order is semantically significant.

## JSON Merge Patch semantics

Rules Core implements object-root JSON Merge Patch semantics for authored rule changes. A patch is stored in the Rules Layer, never written back into the Source Layer.

Within a merge patch:

- a property containing a scalar replaces that property;
- an object recursively merges into the corresponding object;
- a property containing `null` removes that property;
- arrays replace the complete target array rather than merging item-by-item;
- a new property is added when it does not already exist.

The patch root must be a JSON object. Whole-document scalar/array replacement is intentionally rejected because a normalized rule entity must remain an object.

`json-merge-patch` remains supported as a simple and backward-compatible decision kind. It is appropriate when arrays do not need item-aware editing.

## Structured rule patch semantics

`json-rule-patch` adds deterministic array-aware operations without changing the preserved source document. A structured patch may contain an ordinary object merge patch plus one or more array operations. The object merge executes first, followed by array operations in authored order.

Array targets use RFC 6901 JSON Pointer paths. Rules Core supports the standard `~0` escape for `~` and `~1` for `/`, so unusual source property names remain addressable.

Current array operations are:

- `append` - add a supplied value to the end of the target array;
- `remove` - remove exactly one selected array item;
- `replace-by-key` - replace exactly one object selected by a property key/value pair;
- `insert-before` - insert a supplied value immediately before exactly one selected anchor;
- `insert-after` - insert a supplied value immediately after exactly one selected anchor.

Selectors may compare the entire array value, which supports scalar arrays such as proficiency or spell-name lists, or compare one property of an object. `replace-by-key` requires the keyed form. Removal, replacement, and ordered insertion deliberately require exactly one match. Zero matches or ambiguous multiple matches fail resolution rather than silently editing an unintended item.

Arrays are therefore no longer limited to wholesale replacement. A rule can preserve inherited list material while adding, removing, replacing, or positioning specific items. This is intended for edition/source combinations such as spell lists, proficiencies, traits, actions, choices, and similar ordered collections.

The structured patch and its fingerprint are returned as provenance with resolved rules. The immutable Source Layer revision remains unchanged.

## Published global rulesets

`ruleset_revision` is an immutable published snapshot of the latest global decision for every concept that currently has a decision. `ruleset_revision_entry` records the concept, decision, and exact source revision included in that publication.

Publication computes a SHA-256 fingerprint over the ordered effective decision set, including patch provenance. Publishing again without changing the effective set returns the existing latest revision instead of creating a duplicate. A changed decision set creates the next ruleset revision.

This establishes a stable point-in-time global ruleset that campaigns deliberately select. Publishing a newer global ruleset does not migrate any campaign automatically.

## Global mutation authority

Global Rules Layer mutation is accepted only when the request arrived through the authenticated Dorks & Dice Tool gateway and the redeemed context satisfies both conditions:

1. active site mode is `dorks-and-dice`;
2. effective roles scoped to that site mode include `Rules Lawyer`.

Direct/standalone mutation requests return unauthorized. An authenticated user without Rules Lawyer authority, or a Rules Lawyer operating in another site mode, receives forbidden.

The stable site user ID from the redeemed host context is stored on concept creation, bindings, decisions, and publication for auditability.

Source-content access remains independent. Global mutation responses use source/revision identifiers and decision metadata; binding responses do not disclose restricted source names or text.

## Global resolved rules

`GET /api/rules/{conceptKey}` resolves against the latest published global ruleset. The response includes concept identity, global ruleset revision/fingerprint, global decision provenance, exact source revision provenance, optional patch provenance, package/work/edition provenance, and the final resolved document.

The resolved `EntityType` describes the selected effective source variation, normalized at the rules boundary. It therefore changes when a legitimate cross-category history selects a different mechanical category. Selecting a 3.5e Prestige Class variation reports `prestigeClass`; selecting its authoritative 5e Subclass revision reports `subclass`. The RuleConcept's immutable entity type remains separate anchor metadata and is not used to mislabel the effective variation.

Terminology aliases normalize to canonical combined-system categories: `race` and `species` resolve as `species`; `subrace` and `subspecies` resolve as `subspecies`.

Before returning the resolved document, Rules Core applies Source Layer access control to the pinned source revision. Public source packages are readable anonymously. Restricted packages require a `user_source_grant` for the stable hosted user ID. A Rules Lawyer without that grant receives the same not-found result as a user querying a missing rule. Conversely, a user with a source grant may read the resolved rule without gaining Rules Lawyer mutation authority.

The resolved catalog `GET /api/rules` follows the same effective-category rule for item `EntityType`, category filtering, and category facets. Its route and response shape remain the consumer-facing contract; Phase 2.5 does not turn it into the complete Wiki history API.

## Campaign baseline selection

A campaign does not implicitly follow the latest global ruleset. `campaign_ruleset_selection` is an append-only record of the global `ruleset_revision` deliberately selected by that campaign's DM.

Selecting a newer global revision changes only the campaign's pending baseline. Existing published campaign rules continue resolving from their previously published snapshot until the DM explicitly publishes the campaign again. This separates two deliberate actions:

1. choose the global revision the campaign should build on;
2. publish the resulting campaign ruleset.

If the latest selected global revision is selected again, the operation is idempotent and does not create another selection record.

## Campaign overrides

`campaign_rule_decision` records append-only campaign-specific decisions for concepts that exist in the selected global baseline. Current campaign decision kinds are:

- `select-source` - select an exact source revision instead of the baseline's resolved implementation;
- `inherit-global` - explicitly return the concept to the selected baseline implementation;
- `json-merge-patch` - apply a campaign-authored merge patch on top of the resolved global baseline document;
- `json-rule-patch` - apply a campaign-authored structured patch, including item-aware array operations, on top of the resolved global baseline document.

A `select-source` campaign decision may choose any accessible source revision in the concept's authoritative canonical `revision`/`rename` history. The selected source does not need a separate mismatched direct binding when canonical history already proves concept identity. An unrelated same-name source and a `variant`/`reprint`-only source remain invalid. `select-source` bypasses global patching because the campaign has deliberately selected a different source implementation.

Patch decisions do not choose a separate source revision. They compose on top of the selected global baseline. The deterministic resolution order is:

1. pinned global source revision;
2. optional global `json-merge-patch` or `json-rule-patch`;
3. optional campaign `json-merge-patch` or `json-rule-patch`.

Within each `json-rule-patch`, its optional merge object runs before its ordered array operations.

Campaign decisions remain present when the campaign selects a newer global baseline. On the next campaign publication, a current `select-source` override remains pinned to its selected source; patch decisions are reapplied to the newly selected global baseline; `inherit-global` follows that baseline without a campaign patch. This lets a campaign migrate globally while preserving intentional exceptions.

## Published campaign rulesets

`campaign_ruleset_revision` is an immutable published snapshot for one campaign. It records the exact `campaign_ruleset_selection` used as its baseline. Each `campaign_ruleset_revision_entry` records:

- the rule concept;
- the exact global baseline entry;
- the campaign decision that affected the entry, when one exists;
- the exact effective source revision.

Publication computes a SHA-256 fingerprint over the baseline selection and effective campaign decision set, including patch fingerprints. Repeating publication without any effective change returns the current campaign revision instead of creating a duplicate.

A later global publication, later source import, later campaign decision, or later campaign baseline selection can not change an already-published campaign revision.

## Campaign authority and visibility

Campaign authority comes only from the redeemed Tool Host context. Rules Core does not create its own campaign-membership store.

Campaign reads require:

1. authenticated Tool Host identity;
2. active `dorks-and-dice` mode;
3. explicit membership in the requested enabled campaign.

An authenticated nonmember receives not-found behavior. Campaign mutations additionally require the campaign-scoped `DM` role; a campaign Player receives forbidden.

Source-content access remains a separate axis. A DM may have authority to select a baseline, add a campaign patch, or publish campaign metadata without having a grant to read every restricted source represented in that ruleset. When a resolved campaign rule would expose restricted source content, the caller must independently possess the corresponding Rules Core source grant. Campaign membership and DM authority never grant that source access.

## Campaign resolved rules

`GET /api/campaigns/{campaignId}/rules/{conceptKey}` resolves against the latest published campaign ruleset, not the latest global ruleset and not merely the campaign's latest pending baseline selection.

The response includes campaign publication provenance, the pinned global baseline revision, global decision/patch provenance, optional campaign-decision/patch provenance, exact effective source revision provenance, package/work/edition provenance, and the final composed document.

Its resolved `EntityType` follows the campaign's exact effective source variation using the same canonical terminology normalization as global resolution. A campaign `select-source` override can therefore move the effective category from `subclass` back to `prestigeClass`, or forward again, when the selected revision belongs to the concept's authoritative revision/rename history. An inherited campaign remains on its pinned global baseline until the campaign deliberately changes and republishes its baseline.

## Preview and diff

Rules Core can evaluate a candidate global or campaign decision before that decision is persisted. Preview uses the same pinned source revisions, merge semantics, structured array operations, source-binding/history rules, and campaign composition order as publication.

A preview response contains:

- the concept and scope being previewed;
- the candidate decision kind and canonical patch fingerprint;
- the source revision used by the current/base rule and the source revision that would be effective after the candidate;
- the normalized merge or structured patch, when present;
- the complete base document;
- the complete candidate document;
- a deterministic structural change list using RFC 6901 paths.

Object changes are reported at the changed property path as `add`, `remove`, or `replace`. Arrays are reported as one `replace` at the array path when their ordered contents differ. The authored structured patch remains available alongside the diff, so a UI can explain that an array replacement in the result came from specific operations such as `append`, `remove`, or `insert-before` rather than from a wholesale authored replacement.

Preview is deliberately non-persisting: it does not create a global decision, campaign decision, or ruleset revision. Global preview requires the same Dorks-mode `Rules Lawyer` authority used for global mutation. Campaign preview requires campaign membership plus the campaign-scoped `DM` role.

Because preview returns source-backed documents, change authority is not enough to see restricted material. The caller must independently have a Rules Core source grant for every restricted source document returned by the preview. Inaccessible restricted material uses not-found behavior.

## Current API

Global mutation and authoring endpoints:

- `POST /api/global/rules/concepts`
- `POST /api/global/rules/concepts/{conceptId}/bindings`
- `POST /api/global/rules/concepts/{conceptId}/preview`
- `PUT /api/global/rules/concepts/{conceptId}/decision`
- `POST /api/global/rules/publish`

The global preview and decision endpoints accept the same decision request shape. Source-only requests preview/select the exact source revision. Supplying `mergePatch` previews/creates a `json-merge-patch` decision. Supplying `structuredPatch` previews/creates a `json-rule-patch` decision. A request can not supply both patch forms.

Global resolved read endpoints:

- `GET /api/rules`
- `GET /api/rules/{conceptKey}`

Campaign endpoints:

- `PUT /api/campaigns/{campaignId}/rules/baseline`
- `POST /api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview`
- `PUT /api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision`
- `POST /api/campaigns/{campaignId}/rules/publish`
- `GET /api/campaigns/{campaignId}/rules`
- `GET /api/campaigns/{campaignId}/rules/{conceptKey}`

Campaign preview and decision requests use `decisionKind` to choose `select-source`, `inherit-global`, `json-merge-patch`, or `json-rule-patch`. Patch decisions leave `sourceEntityRevisionId` null. `json-merge-patch` uses `mergePatch`; `json-rule-patch` uses `structuredPatch`.

The current implementation establishes exact source selection, authoritative revision/rename-history participation, authored object merge/replace/delete semantics, item-aware array composition, non-persisting preview/diff, immutable global publication, deliberate campaign migration, and campaign-specific composition. Arbitrary campaign-only concepts, temporary/session overrides, richer multi-field selectors/set-style array operations, rollback UI, and broader authoring workflows remain later layers built on the immutable publication model.

## Rules Wiki reference reads are not Rules Layer decisions

Rules Wiki also needs to browse accessible source history before every source-backed concept has a published Rules Layer decision. The first-party `/api/wiki/references` read model is deliberately separate from the effective consumer `/api/rules` contract described above.

A Wiki reference may be backed only by accessible canonical source history. Reading such a reference does not create a `rule_concept`, `global_rule_decision`, campaign decision, or publication. If no published global/campaign selection applies, the reference read model chooses a deterministic accessible fallback for presentation and labels it `unresolved-fallback`; that choice is not an adjudication and is never persisted by the read.

When a published Rules Layer selection applies, the Wiki reference uses that accessible selected source revision as its effective variation. Its effective category follows that selected variation. Campaign references preserve the existing inherited-versus-override publication semantics.

Ordinary users may read accessible reference history and request read-only semantic comparison without gaining Rules Lawyer or campaign-DM mutation authority. Source grants remain an independent hard boundary: inaccessible source variations are omitted from reference rows, facets, counts, detail, history, fallback selection, and comparison.

The resolved consumer API routes and response shapes remain compatible with other Tools. Phase 2.5 changes the meaning of resolved `EntityType` where required so it consistently reports the selected effective variation's category rather than the concept anchor category. Source-only Wiki references still do not appear in `/api/rules` merely because they can be browsed in Rules Wiki.
