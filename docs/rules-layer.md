# Rules Layer

The Rules Layer records Dorks & Dice adjudication separately from immutable source material. It never edits a Source Layer revision in place.

## Stable concepts

`rule_concept` provides the source-independent identity for a rule concept. A concept has a stable normalized key, an entity type, a display name, and creation audit information. Source-specific implementations are attached through `rule_concept_source_binding` rather than making a source record itself the canonical rule identity.

This allows multiple editions or providers to implement the same concept while preserving their individual provenance.

## Global decisions

The first global decision type is `select-source`. A `global_rule_decision` selects one exact `source_entity_revision` for a concept and records:

- a monotonically increasing decision number within the concept;
- the exact selected source revision;
- an optional adjudication note;
- the stable Dorks & Dice user ID that made the decision;
- the decision timestamp.

Decisions are append-only. Repeating the current decision with the same selected revision and note is idempotent; changing either creates the next decision.

Selecting an exact source revision is intentional. If the same source entity is imported again and receives revision 2, a decision selecting revision 1 does not silently move. A Rules Lawyer must explicitly adopt revision 2 before a later published ruleset uses it.

## Published global rulesets

`ruleset_revision` is an immutable published snapshot of the latest global decision for every concept that currently has a decision. `ruleset_revision_entry` records the concept, decision, and exact source revision included in that publication.

Publication computes a SHA-256 fingerprint over the ordered effective decision set. Publishing again without changing the effective set returns the existing latest revision instead of creating a duplicate. A changed decision set creates the next ruleset revision.

This establishes a stable point-in-time global ruleset that campaigns deliberately select. Publishing a newer global ruleset does not migrate any campaign automatically.

## Global mutation authority

Global Rules Layer mutation is accepted only when the request arrived through the authenticated Dorks & Dice Tool gateway and the redeemed context satisfies both conditions:

1. active site mode is `dorks-and-dice`;
2. effective global roles include `Rules Lawyer`.

Direct/standalone mutation requests return unauthorized. An authenticated user without Rules Lawyer authority, or a Rules Lawyer operating in another site mode, receives forbidden.

The stable site user ID from the redeemed host context is stored on concept creation, bindings, decisions, and publication for auditability.

Source-content access remains independent. Global mutation responses use source/revision identifiers and decision metadata; binding responses do not disclose restricted source names or text.

## Global resolved rules

`GET /api/rules/{conceptKey}` resolves against the latest published global ruleset. The response includes concept identity, global ruleset revision/fingerprint, global decision provenance, exact source revision provenance, package/work/edition provenance, and the preserved source document.

Before returning the source document, Rules Core applies Source Layer access control. Public source packages are readable anonymously. Restricted packages require a `user_source_grant` for the stable hosted user ID. A Rules Lawyer without that grant receives the same not-found result as a user querying a missing rule. Conversely, a user with a source grant may read the resolved rule without gaining Rules Lawyer mutation authority.

## Campaign baseline selection

A campaign does not implicitly follow the latest global ruleset. `campaign_ruleset_selection` is an append-only record of the global `ruleset_revision` deliberately selected by that campaign's DM.

Selecting a newer global revision changes only the campaign's pending baseline. Existing published campaign rules continue resolving from their previously published snapshot until the DM explicitly publishes the campaign again. This separates two deliberate actions:

1. choose the global revision the campaign should build on;
2. publish the resulting campaign ruleset.

If the latest selected global revision is selected again, the operation is idempotent and does not create another selection record.

## Campaign overrides

`campaign_rule_decision` records append-only campaign-specific decisions for concepts that exist in the selected global baseline. The first two decision kinds are:

- `select-source` - select an exact source revision instead of the baseline's source revision;
- `inherit-global` - explicitly return the concept to the selected baseline implementation.

A `select-source` decision may only choose a source entity already bound to the global concept. Like global decisions, the campaign decision pins an immutable source revision rather than following later imports automatically.

Campaign decisions remain present when the campaign selects a newer global baseline. On the next campaign publication, a current `select-source` override remains effective; `inherit-global` follows the newly selected baseline. This lets a campaign migrate globally while preserving intentional exceptions.

## Published campaign rulesets

`campaign_ruleset_revision` is an immutable published snapshot for one campaign. It records the exact `campaign_ruleset_selection` used as its baseline. Each `campaign_ruleset_revision_entry` records:

- the rule concept;
- the exact global baseline entry;
- the campaign decision that affected the entry, when one exists;
- the exact effective source revision.

Publication computes a SHA-256 fingerprint over the baseline selection and effective campaign decision set. Repeating publication without any effective change returns the current campaign revision instead of creating a duplicate.

A later global publication, later source import, or later campaign baseline selection can not change an already-published campaign revision.

## Campaign authority and visibility

Campaign authority comes only from the redeemed Tool Host context. Rules Core does not create its own campaign-membership store.

Campaign reads require:

1. authenticated Tool Host identity;
2. active `dorks-and-dice` mode;
3. explicit membership in the requested enabled campaign.

An authenticated nonmember receives not-found behavior. Campaign mutations additionally require the campaign-scoped `DM` role; a campaign Player receives forbidden.

Source-content access remains a separate axis. A DM may have authority to select a baseline or publish campaign metadata without having a grant to read every restricted source represented in that ruleset. When a resolved campaign rule would expose restricted source content, the caller must independently possess the corresponding Rules Core source grant. Campaign membership and DM authority never grant that source access.

## Campaign resolved rules

`GET /api/campaigns/{campaignId}/rules/{conceptKey}` resolves against the latest published campaign ruleset, not the latest global ruleset and not merely the campaign's latest pending baseline selection.

The response includes campaign publication provenance, the pinned global baseline revision, global decision provenance, optional campaign-decision provenance, exact effective source revision provenance, package/work/edition provenance, and the preserved source document.

## Current API

Global mutation endpoints:

- `POST /api/global/rules/concepts`
- `POST /api/global/rules/concepts/{conceptId}/bindings`
- `PUT /api/global/rules/concepts/{conceptId}/decision`
- `POST /api/global/rules/publish`

Global resolved read endpoint:

- `GET /api/rules/{conceptKey}`

Campaign endpoints:

- `PUT /api/campaigns/{campaignId}/rules/baseline`
- `PUT /api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision`
- `POST /api/campaigns/{campaignId}/rules/publish`
- `GET /api/campaigns/{campaignId}/rules/{conceptKey}`

The current implementation establishes exact source selection, global publication, deliberate campaign migration, and campaign source overrides. Rich merge/patch decisions, arbitrary campaign-only concepts, temporary/session overrides, diff/rollback UI, and broader authoring workflows remain later layers built on the immutable publication model.
