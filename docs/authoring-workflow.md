# Rules authoring workflow

Rules Core exposes stateless authoring workflows for the Dorks & Dice Rules Lawyer UI and campaign DM tooling. The workflows deliberately separate browsing, previewing, saving, and publishing so that inspecting a candidate can never mutate the active ruleset.

## Global authoring sequence

A Rules Lawyer operating in the `dorks-and-dice` site mode uses the following sequence:

1. `GET /api/global/rules/authoring` to browse rule concepts and publication state.
2. `GET /api/global/rules/authoring/concepts/{conceptId}` to inspect one concept, its source bindings, exact accessible source revisions, latest saved decision, and whether that decision is still unpublished.
3. Construct a `SetGlobalRuleDecisionRequest` using one exact bound source revision and either no patch, `mergePatch`, or `structuredPatch`.
4. `POST /api/global/rules/concepts/{conceptId}/preview` to resolve the candidate and inspect its deterministic diff without saving it.
5. `PUT /api/global/rules/concepts/{conceptId}/decision` to append the approved decision.
6. `POST /api/global/rules/publish` to publish a new immutable global ruleset revision when the accumulated saved decisions are ready.

The authoring endpoints do not create draft database records. The client may freely abandon a candidate after preview. Saved decisions remain append-only and publication remains a separate deliberate operation.

## Global authoring overview

`GET /api/global/rules/authoring` returns:

- the latest published global ruleset revision, when one exists;
- the total number of concepts;
- how many concepts currently have at least one decision;
- how many latest decisions differ from the decision present in the latest publication;
- one summary per concept with binding count, latest decision identity/kind, published decision identity, and `hasUnpublishedChanges`.

This lets the UI distinguish concepts that have never been adjudicated, concepts whose latest decision is already published, and concepts with pending saved work.

## Global concept authoring state

`GET /api/global/rules/authoring/concepts/{conceptId}` returns the concept and all Rules Layer source-binding records. Binding IDs and source-entity IDs are Rules Layer metadata and remain visible to an authorized Rules Lawyer.

Source descriptive metadata is filtered separately through Rules Core source grants. `accessibleSources` includes names, source codes, package/work/edition provenance, and exact revision IDs only for bound source entities the current user may access. `restrictedBindingCount` reports how many bindings exist without exposing the restricted source names or content.

Each accessible source includes its immutable revisions ordered newest first. The UI can therefore deliberately choose the exact revision that will be pinned by a candidate decision.

The concept response also includes the latest saved global decision, the decision currently present in the latest published ruleset, the latest publication number, and whether the latest saved decision is unpublished.

## Campaign authoring sequence

A campaign DM uses a parallel workflow while remaining pinned to a deliberately selected global baseline:

1. `GET /api/campaigns/{campaignId}/rules/authoring` to inspect the selected global baseline, latest campaign publication, and pending state.
2. `GET /api/campaigns/{campaignId}/rules/baselines` to list published global ruleset revisions and identify which revision is currently selected and which revision backs the latest published campaign ruleset.
3. `GET /api/campaigns/{campaignId}/rules/baselines/{rulesetRevisionId}/preview` to compare a candidate global revision with the campaign's current selected baseline before changing the selection.
4. `PUT /api/campaigns/{campaignId}/rules/baseline` to append the deliberate baseline selection. Selection alone does not change the campaign rules active for players.
5. `GET /api/campaigns/{campaignId}/rules/authoring/concepts/{conceptId}` to inspect the baseline implementation, campaign decision state, bindings, and exact accessible source revisions.
6. Construct a `SetCampaignRuleDecisionRequest` using `inherit-global`, `select-source`, `json-merge-patch`, or `json-rule-patch`.
7. `POST /api/campaigns/{campaignId}/rules/concepts/{conceptId}/preview` to compare the selected global baseline with the proposed campaign result without saving it.
8. `PUT /api/campaigns/{campaignId}/rules/concepts/{conceptId}/decision` to append the approved campaign decision.
9. `POST /api/campaigns/{campaignId}/rules/publish` to publish a new immutable campaign ruleset revision and make the selected baseline plus current campaign decisions active.

Selecting a newer global baseline and saving a campaign decision are tracked separately. A DM can therefore see whether a campaign needs publication because its baseline changed, because one or more campaign decisions changed, or both.

## Campaign baseline discovery and migration preview

`GET /api/campaigns/{campaignId}/rules/baselines` returns every published global ruleset revision newest first. Each candidate includes its revision number, fingerprint, publication provenance, entry count, `isSelectedBaseline`, and `isPublishedCampaignBaseline`.

The selected baseline and published campaign baseline are deliberately different concepts. The selected baseline is the latest append-only `CampaignRulesetSelection` and drives current DM authoring. The published campaign baseline is the global revision pinned by the latest immutable campaign ruleset visible to players. A DM may therefore select and author against a migration without changing active play until publication.

`GET /api/campaigns/{campaignId}/rules/baselines/{rulesetRevisionId}/preview` is read-only. It compares the requested candidate with the current selected baseline and reports:

- added, removed, changed, and unchanged rule concepts;
- the current and candidate global decision number/kind for each concept;
- the latest historical campaign decision for each affected concept, when one exists;
- campaign decisions that remain active because the concept exists in both baselines;
- campaign decisions that become inactive because the concept disappears from the candidate baseline;
- historical campaign decisions that become active again because a concept absent from the current baseline reappears in the candidate.

When no baseline has been selected yet, the preview treats every candidate concept as added and provides an initial-baseline review instead of an error.

The migration preview intentionally returns Rules Layer identities and decision metadata rather than source-backed rule documents. A DM can therefore evaluate baseline structure without receiving restricted source content. Source grants remain required later when source-backed rule or decision previews are requested.

## Campaign authoring overview

`GET /api/campaigns/{campaignId}/rules/authoring` returns a DM-safe authoring view. Before a baseline is selected it returns an empty concept list and no publication requirement, allowing the UI to prompt the DM to choose a baseline rather than treating the campaign as an error.

After baseline selection the response includes:

- the current append-only baseline selection and exact global ruleset revision;
- the latest published campaign ruleset, when one exists;
- `hasUnpublishedBaselineChange`, which is true when the current baseline selection is not the selection used by the latest campaign publication;
- `needsPublication`, which is true when the baseline changed or a current-baseline concept has an unpublished campaign decision;
- counts for current-baseline concepts, concepts with explicit campaign decisions, and pending campaign decisions;
- one summary per concept in the current selected global baseline.

Concept summaries identify the baseline global decision, latest campaign decision, campaign decision included in the latest campaign publication, and `hasUnpublishedOverrideChange`.

Only concepts present in the current selected global baseline participate in the overview and pending counts. Historical campaign decisions for concepts no longer present in that baseline remain preserved as history but do not make the current campaign ruleset appear dirty. The baseline migration preview can surface those historical decisions when a candidate revision removes or restores their concepts.

## Campaign concept authoring state

`GET /api/campaigns/{campaignId}/rules/authoring/concepts/{conceptId}` returns not-found when the campaign has no selected baseline or when the concept is not part of the current selected baseline.

For a current-baseline concept it returns:

- the campaign and current baseline selection;
- stable concept identity;
- the exact global decision pinned by the selected baseline;
- all Rules Layer source-binding records;
- exact source revisions the current DM may independently access;
- a count of restricted bindings whose source metadata is not disclosed;
- the latest saved campaign decision, when one exists;
- the campaign decision included in the latest campaign publication, when one exists;
- separate unpublished-baseline and unpublished-campaign-decision flags.

The baseline global decision exposes Rules Layer provenance and authored patch metadata, not unrestricted source text. Source-backed candidate documents continue to flow through the decision preview endpoint, which performs the independent source-access check before returning content.

## Authorization boundaries

Global authoring endpoints require the same global change authority as global mutation:

- authenticated Tool Host context;
- active site mode `dorks-and-dice`;
- effective global role `Rules Lawyer`.

Anonymous global requests return unauthorized. Authenticated users without Rules Lawyer authority, including a Rules Lawyer operating in another site mode, return forbidden.

Campaign authoring, baseline discovery, and baseline migration preview endpoints require:

- authenticated Tool Host context;
- active site mode `dorks-and-dice`;
- explicit membership in the requested campaign;
- campaign-scoped `DM` role.

Anonymous campaign requests return unauthorized. An authenticated nonmember receives not-found behavior. A campaign Player receives forbidden. A DM operating outside Dorks & Dice mode receives not-found behavior because the campaign Rules Layer is not active in that mode.

Source access remains an independent axis for both workflows. Rules Lawyer or DM authority permits working with Rules Layer identities and decisions, but it does not reveal restricted source descriptive metadata or source-backed preview content. A matching `user_source_grant` is still required for those source details. Conversely, a source grant never grants Rules Lawyer or campaign DM change authority.

## Publication state

For the global layer, a concept has unpublished changes when its latest append-only decision is not the decision referenced by the latest published global ruleset revision. Previewing does not change that state. Saving a different decision makes it pending; publishing a ruleset containing that decision clears the pending state.

For a campaign, baseline state and campaign-decision state are independent. `hasUnpublishedBaselineChange` compares the latest baseline selection with the baseline selection pinned by the latest campaign publication. `hasUnpublishedOverrideChange` compares the latest campaign decision for a current-baseline concept with the campaign decision pinned for that concept by the latest campaign publication. `needsPublication` is derived from those two sources of change.

All of this state is derived from immutable decision, selection, and publication records rather than maintained as mutable draft flags, so it can not drift from publication history.
