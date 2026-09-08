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

## Published rulesets

`ruleset_revision` is an immutable published snapshot of the latest global decision for every concept that currently has a decision. `ruleset_revision_entry` records the concept, decision, and exact source revision included in that publication.

Publication computes a SHA-256 fingerprint over the ordered effective decision set. Publishing again without changing the effective set returns the existing latest revision instead of creating a duplicate. A changed decision set creates the next ruleset revision.

This establishes a stable point-in-time global ruleset that later campaign layers can deliberately adopt or override.

## Mutation authority

Global Rules Layer mutation is accepted only when the request arrived through the authenticated Dorks & Dice Tool gateway and the redeemed context satisfies both conditions:

1. active site mode is `dorks-and-dice`;
2. effective global roles include `Rules Lawyer`.

Direct/standalone mutation requests return unauthorized. An authenticated user without Rules Lawyer authority, or a Rules Lawyer operating in another site mode, receives forbidden.

The stable site user ID from the redeemed host context is stored on concept creation, bindings, decisions, and publication for auditability.

Source-content access remains independent. Global mutation responses use source/revision identifiers and decision metadata; binding responses do not disclose restricted source names or text.

## Resolved rules

`GET /api/rules/{conceptKey}` resolves against the latest published global ruleset. The response includes:

- concept identity;
- published ruleset revision and fingerprint;
- global decision provenance;
- exact source entity/revision provenance;
- package/work/edition provenance;
- the preserved source document.

Before returning the source document, Rules Core applies Source Layer access control. Public source packages are readable anonymously. Restricted packages require a `user_source_grant` for the stable hosted user ID. A Rules Lawyer without that grant receives the same not-found result as a user querying a missing rule. Conversely, a user with a source grant may read the resolved rule without gaining Rules Lawyer mutation authority.

## Current API

Global mutation endpoints:

- `POST /api/global/rules/concepts`
- `POST /api/global/rules/concepts/{conceptId}/bindings`
- `PUT /api/global/rules/concepts/{conceptId}/decision`
- `POST /api/global/rules/publish`

Resolved read endpoint:

- `GET /api/rules/{conceptKey}`

This slice intentionally implements only the global `select-source` decision. Merge/override semantics, campaign Rules Layers, diffs, rollback UI, and deliberate campaign migration remain later work built on the same immutable revision model.
