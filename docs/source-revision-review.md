# Source revision review

Rules Core source entities are revisioned immutably. Global rule decisions deliberately pin an exact `source_entity_revision`, so importing a newer source revision can not silently change the active or pending Rules Layer.

The source revision review flow makes that drift visible to a Rules Lawyer without changing the decision automatically.

## Pending update discovery

`GET /api/global/rules/source-updates` returns the latest global decision for each concept when the source entity selected by that decision has one or more newer immutable revisions.

The result reports:

- concept identity and current global decision provenance;
- source package, edition, entity, and source code;
- the currently selected source revision and its fingerprint/import time;
- the latest source revision and its fingerprint/import time;
- the number of newer revisions between the selected and latest revision.

Only source packages that the current Rules Lawyer may independently read are considered. Public packages are visible normally. Restricted packages require a `user_source_grant`. A Rules Lawyer role alone does not reveal restricted source metadata.

The review list is based on the latest saved global decision, not only the most recently published decision. This prevents an already-updated pending decision from continuing to appear stale while it waits for publication.

## Read-only update preview

`GET /api/global/rules/source-updates/{conceptId}/preview` compares the currently resolved rule with the result that the latest source revision would produce if the current decision semantics were replayed against it.

For `select-source`, the latest source document becomes the candidate resolved document. For `json-merge-patch` and `json-rule-patch`, Rules Core applies the exact stored patch to the latest source revision and generates the normal structural diff against the current resolved rule.

Structured patches are intentionally strict. If a stored array selector, JSON Pointer, or other patch assumption no longer matches the latest source document, preview reports the patch as incompatible instead of guessing how the rule should be migrated. The Rules Lawyer must then review and adapt the rule manually.

Preview is read-only. It does not create a `global_rule_decision`, publish a `ruleset_revision`, change the Source Layer, or acknowledge/dismiss the update.

## Hosted authoring UI

When pending source updates exist, the Global Rules view shows a **Source updates to review** panel. Each row identifies the selected and latest source revisions and opens a read-only comparison.

The comparison shows the current resolved rule, the candidate result when compatible, and the structural changes. The Rules Lawyer can then open the ordinary rule editor and deliberately save whatever new decision is appropriate. Publication remains a separate explicit action.

This keeps the same invariant used elsewhere in Rules Core: source changes are discoverable and easy to evaluate, but only an authorized human decision changes the Rules Layer.
