# Rules authoring workflow

Rules Core exposes a stateless global authoring workflow for the Dorks & Dice Rules Lawyer UI. The workflow deliberately separates browsing, previewing, saving, and publishing so that inspecting a candidate can never mutate the active ruleset.

## Global authoring sequence

A Rules Lawyer operating in the `dorks-and-dice` site mode uses the following sequence:

1. `GET /api/global/rules/authoring` to browse rule concepts and publication state.
2. `GET /api/global/rules/authoring/concepts/{conceptId}` to inspect one concept, its source bindings, exact accessible source revisions, latest saved decision, and whether that decision is still unpublished.
3. Construct a `SetGlobalRuleDecisionRequest` using one exact bound source revision and either no patch, `mergePatch`, or `structuredPatch`.
4. `POST /api/global/rules/concepts/{conceptId}/preview` to resolve the candidate and inspect its deterministic diff without saving it.
5. `PUT /api/global/rules/concepts/{conceptId}/decision` to append the approved decision.
6. `POST /api/global/rules/publish` to publish a new immutable global ruleset revision when the accumulated saved decisions are ready.

The authoring endpoints do not create draft database records. The client may freely abandon a candidate after preview. Saved decisions remain append-only and publication remains a separate deliberate operation.

## Authoring overview

`GET /api/global/rules/authoring` returns:

- the latest published global ruleset revision, when one exists;
- the total number of concepts;
- how many concepts currently have at least one decision;
- how many latest decisions differ from the decision present in the latest publication;
- one summary per concept with binding count, latest decision identity/kind, published decision identity, and `hasUnpublishedChanges`.

This lets the UI distinguish concepts that have never been adjudicated, concepts whose latest decision is already published, and concepts with pending saved work.

## Concept authoring state

`GET /api/global/rules/authoring/concepts/{conceptId}` returns the concept and all Rules Layer source-binding records. Binding IDs and source-entity IDs are Rules Layer metadata and remain visible to an authorized Rules Lawyer.

Source descriptive metadata is filtered separately through Rules Core source grants. `accessibleSources` includes names, source codes, package/work/edition provenance, and exact revision IDs only for bound source entities the current user may access. `restrictedBindingCount` reports how many bindings exist without exposing the restricted source names or content.

Each accessible source includes its immutable revisions ordered newest first. The UI can therefore deliberately choose the exact revision that will be pinned by a candidate decision.

The concept response also includes the latest saved global decision, the decision currently present in the latest published ruleset, the latest publication number, and whether the latest saved decision is unpublished.

## Authorization boundaries

The authoring endpoints require the same global change authority as global mutation:

- authenticated Tool Host context;
- active site mode `dorks-and-dice`;
- effective global role `Rules Lawyer`.

Anonymous requests return unauthorized. Authenticated users without Rules Lawyer authority, including a Rules Lawyer operating in another site mode, return forbidden.

Source access remains an independent axis. Rules Lawyer authority permits working with Rules Layer identities and decisions, but it does not reveal restricted source descriptive metadata or source-backed preview content. A matching `user_source_grant` is still required for those source details. Conversely, a source grant never grants Rules Lawyer change authority.

## Publication state

A concept has unpublished changes when its latest append-only decision is not the decision referenced by the latest published global ruleset revision. Previewing does not change that state. Saving a different decision makes it pending; publishing a ruleset containing that decision clears the pending state.

This state is derived from immutable decision/publication records rather than maintained as a mutable draft flag, so it can not drift from publication history.
