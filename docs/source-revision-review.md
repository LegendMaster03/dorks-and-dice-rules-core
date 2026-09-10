# Source revision review

Rules Core source entities are revisioned immutably. Global rule decisions deliberately pin an exact `source_entity_revision`, so importing a newer source revision can not silently change the active or pending Rules Layer.

The source revision review flow makes that drift visible to a Rules Lawyer without changing the decision automatically.

## Pending update discovery

`GET /api/global/rules/source-updates` returns the latest global decision for each concept when the source entity selected by that decision has one or more newer immutable revisions that have not been deliberately rejected for that exact decision and latest revision.

The result reports:

- concept identity and current global decision provenance;
- source package, edition, entity, and source code;
- the currently selected source revision and its fingerprint/import time;
- the latest source revision and its fingerprint/import time;
- the number of newer revisions between the selected and latest revision.

Only source packages that the current Rules Lawyer may independently read are considered. Public packages are visible normally. Restricted packages require a `user_source_grant`. A Rules Lawyer role alone does not reveal restricted source metadata.

The review list is based on the latest saved global decision, not only the most recently published decision. This prevents an already-updated pending decision from continuing to appear stale while it waits for publication.

A rejection suppresses only the exact pair of global decision and latest source revision that was reviewed. If a new global decision is created while retaining the older source revision, the update becomes reviewable again in the context of that new decision. If a still-newer source revision is imported, that newer revision also becomes reviewable automatically.

## Read-only update preview

`GET /api/global/rules/source-updates/{conceptId}/preview` compares the currently resolved rule with the result that the latest source revision would produce if the current decision semantics were replayed against it.

For `select-source`, the latest source document becomes the candidate resolved document. For `json-merge-patch` and `json-rule-patch`, Rules Core applies the exact stored patch to the latest source revision and generates the normal structural diff against the current resolved rule.

Structured patches are intentionally strict. If a stored array selector, JSON Pointer, or other patch assumption no longer matches the latest source document, preview reports the patch as incompatible instead of guessing how the rule should be migrated. The Rules Lawyer must then review and adapt the rule manually or deliberately reject that source revision for the current decision.

Preview is read-only. It does not create a `global_rule_decision`, publish a `ruleset_revision`, change the Source Layer, or record a rejection.

## Deliberate adoption

After a compatible preview, `POST /api/global/rules/source-updates/{conceptId}/adopt` can create the next append-only global decision against the reviewed latest source revision. The operation carries forward the exact current decision kind, stored patch, patch fingerprint, note, and consolidation contribution provenance; only the pinned source revision and decision provenance change.

The request carries optimistic review tokens for the global decision ID, latest source revision ID, and latest source fingerprint. If another Rules Lawyer changes the decision, or another source revision is imported after the preview, adoption fails with a conflict and requires a fresh review. Restricted source access is checked again during the mutation.

Adoption is rejected when the stored decision can not be replayed cleanly against the latest source revision. In particular, Rules Core does not rewrite broken structured-patch selectors or infer replacement array operations.

Adoption creates a pending global decision only. It never publishes a `ruleset_revision`; publication remains a separate Rules Lawyer action. Repeating the same reviewed adoption request after it succeeds also conflicts because the expected global decision is no longer current.

## Deliberate rejection

`POST /api/global/rules/source-updates/{conceptId}/reject` records that the Rules Lawyer reviewed the current latest source revision and deliberately chose not to adopt it for the current global decision.

A rejection is review provenance, not a Rules Layer decision. It does not create or modify a `global_rule_decision`, does not publish a ruleset, does not change the selected source revision, and does not modify Source Layer content.

The request includes the same optimistic review tokens used for adoption plus a required reason. Rules Core validates the current global decision ID, latest source revision ID, and latest source fingerprint, and rechecks restricted-source access before recording the rejection. Stale review tokens conflict rather than silently dismissing a different update.

Rejections are append-only audit records keyed to the exact `(global rule decision, source revision)` review target. Repeating the same rejection with the same reason is idempotent. Attempting to replace the reason for an already-recorded exact rejection conflicts rather than mutating history.

This scope is intentionally narrow:

- rejecting source revision 2 for global decision 4 suppresses only that exact review target;
- creating global decision 5 while still selecting the older source revision causes revision 2 to appear again because the decision context changed;
- importing source revision 3 causes revision 3 to appear because the source review target changed;
- an earlier rejection remains in the audit history even after either event.

This prevents a rejection from becoming an accidental permanent ignore rule.

## Hosted authoring UI

When pending source updates exist, the Global Rules view shows a **Source updates to review** panel. Each row identifies the selected and latest source revisions and opens a comparison.

The comparison shows the current resolved rule, the candidate result when compatible, and the structural changes. A compatible preview exposes **Adopt latest source revision**, which requires an explicit confirmation and creates the pending decision described above. The review also exposes **Reject latest source revision**, which requires a reason and records only the exact review target. The Rules Lawyer can instead open the ordinary rule editor whenever the update needs a different decision or patch. Publication remains a separate explicit action.

This keeps the same invariant used elsewhere in Rules Core: source changes are discoverable and easy to evaluate, but only an authorized human decision changes the Rules Layer, and publication is never implied by review, adoption, or rejection.
