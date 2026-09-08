# Rules Layer

The Rules Layer records Dorks & Dice adjudication separately from immutable source material. It never edits a Source Layer revision in place.

## Stable concepts

`rule_concept` provides the source-independent identity for a rule concept. A concept has a stable normalized key, an entity type, a display name, and creation audit information. Source-specific implementations are attached through `rule_concept_source_binding` rather than making a source record itself the canonical rule identity.

This allows multiple editions or providers to implement the same concept while preserving their individual provenance.

## Global decisions

A `global_rule_decision` is an append-only adjudication for one concept. Current global decision kinds are:

- `select-source` - publish one exact source revision as the rule implementation;
- `json-merge-patch` - use one exact source revision as the immutable base and apply a Rules Layer-authored JSON Merge Patch to the preserved source document.

Every global decision records a monotonically increasing decision number, the exact base source revision, optional adjudication note, stable Dorks & Dice user ID, and timestamp. Merge-patch decisions additionally store the canonical patch document and its SHA-256 fingerprint.

Selecting an exact source revision is intentional. If the same source entity is imported again and receives revision 2, a decision based on revision 1 does not silently move. A Rules Lawyer must explicitly create a new decision using revision 2 before a later published ruleset uses it.

Equivalent merge patches are canonicalized by recursively sorting object property names before fingerprinting. Reordering object properties therefore does not create a new decision when the base source revision and adjudication note are otherwise unchanged.

## JSON Merge Patch semantics

Rules Core implements object-root JSON Merge Patch semantics for authored rule changes. A patch is stored in the Rules Layer, never written back into the Source Layer.

Within a patch:

- a property containing a scalar replaces that property;
- an object recursively merges into the corresponding object;
- a property containing `null` removes that property;
- arrays replace the complete target array rather than merging item-by-item;
- a new property is added when it does not already exist.

The patch root must be a JSON object. Whole-document scalar/array replacement is intentionally rejected because a normalized rule entity must remain an object.

A global merge patch is applied directly to its pinned source revision. The resolved response exposes both the original source provenance and the patch fingerprint/document so a caller can explain which fields came from immutable source material versus Dorks & Dice adjudication.

## Published global rulesets

`ruleset_revision` is an immutable published snapshot of the latest global decision for every concept that currently has a decision. `ruleset_revision_entry` records the concept, decision, and exact source revision included in that publication.

Publication computes a SHA-256 fingerprint over the ordered effective decision set, including merge-patch provenance. Publishing again without changing the effective set returns the existing latest revision instead of creating a duplicate. A changed decision set creates the next ruleset revision.

This establishes a stable point-in-time global ruleset that campaigns deliberately select. Publishing a newer global ruleset does not migrate any campaign automatically.

## Global mutation authority

Global Rules Layer mutation is accepted only when the request arrived through the authenticated Dorks & Dice Tool gateway and the redeemed context satisfies both conditions:

1. active site mode is `dorks-and-dice`;
2. effective global roles include `Rules Lawyer`.

Direct/standalone mutation requests return unauthorized. An authenticated user without Rules Lawyer authority, or a Rules Lawyer operating in another site mode, receives forbidden.

The stable site user ID from the redeemed host context is stored on concept creation, bindings, decisions, and publication for auditability.

Source-content access remains independent. Global mutation responses use source/revision identifiers and decision metadata; binding responses do not disclose restricted source names or text.

## Global resolved rules

`GET /api/rules/{conceptKey}` resolves against the latest published global ruleset. The response includes concept identity, global ruleset revision/fingerprint, global decision provenance, exact source revision provenance, optional merge-patch provenance, package/work/edition provenance, and the final resolved document.

Before returning the resolved document, Rules Core applies Source Layer access control to the pinned source revision. Public source packages are readable anonymously. Restricted packages require a `user_source_grant` for the stable hosted user ID. A Rules Lawyer without that grant receives the same not-found result as a user querying a missing rule. Conversely, a user with a source grant may read the resolved rule without gaining Rules Lawyer mutation authority.

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
- `json-merge-patch` - apply a campaign-authored merge patch on top of the resolved global baseline document.

A `select-source` decision may only choose a source entity already bound to the global concept. It bypasses the baseline global merge patch because the campaign has deliberately selected a different source implementation.

A campaign `json-merge-patch` does not choose a separate source revision. It composes on top of the selected global baseline after any global merge patch has been applied. This creates a deterministic order:

1. pinned global source revision;
2. optional global merge patch;
3. optional campaign merge patch.

Campaign decisions remain present when the campaign selects a newer global baseline. On the next campaign publication, a current `select-source` override remains pinned to its selected source; a `json-merge-patch` is reapplied to the newly selected global baseline; `inherit-global` follows that baseline without a campaign patch. This lets a campaign migrate globally while preserving intentional exceptions.

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

## Current API

Global mutation endpoints:

- `POST /api/global/rules/concepts`
- `POST /api/global/rules/concepts/{conceptId}/bindings`
- `PUT /api/global/rules/concepts/{conceptId}/decision`
- `POST /api/global/rules/publish`

`PUT /api/global/rules/concepts/{conceptId}/decision` remains backward compatible with source-only selection. Supplying a `mergePatch` object turns the decision into `json-merge-patch` while retaining the supplied source revision as the immutable base.

Global resolved read endpoint:

- `GET /api/rules/{conceptKey}`

Campaign endpoints:

- `PUT /api/campaigns/{campaignId}/rules/baseline`
- `PUT /api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision`
- `POST /api/campaigns/{campaignId}/rules/publish`
- `GET /api/campaigns/{campaignId}/rules/{conceptKey}`

Campaign decision requests use `decisionKind` to choose `select-source`, `inherit-global`, or `json-merge-patch`. A campaign merge patch uses `mergePatch` and leaves `sourceEntityRevisionId` null.

The current implementation now establishes exact source selection, authored object merge/replace/delete semantics, immutable global publication, deliberate campaign migration, and campaign-specific composition. Arbitrary campaign-only concepts, temporary/session overrides, richer array-aware semantic merge operations, diff/rollback UI, and broader authoring workflows remain later layers built on the immutable publication model.
