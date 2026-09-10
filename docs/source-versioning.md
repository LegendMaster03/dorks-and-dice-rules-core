# Cross-version detection, lineage, and consolidation

Rules Core exists in part to recognize that the same conceptual rule can have multiple implementations across source revisions, publications, playtests, and D&D editions, and then let a human deliberately consolidate those implementations into the Dorks & Dice rule.

## Separate facts

Three facts remain independent:

1. Source identity records where an implementation appeared.
2. Rule-concept binding records that source implementations are expressions of the same Dorks & Dice concept.
3. Source lineage records how source implementations relate historically or developmentally.

Lineage never makes a later rule automatically authoritative. Publication order is evidence for review, not resolver precedence.

## Revision versus lineage

A new immutable `SourceEntityRevision` means the same source entity was reimported with changed content. A rule appearing in another UA release, book, printing treated as a distinct release, or D&D edition is normally another `SourceEntity`. If it descends from an earlier implementation, the entities are connected by lineage rather than collapsed into one revision chain.

## Lineage relationships

The first framework supports directional relationships:

- `predecessor-of`
- `playtest-of`
- `revised-as`
- `renamed-as`
- `split-into`
- `combined-into`
- `related-version`

Relationships are independent rows, so one-to-one, one-to-many, many-to-one, and many-to-many histories are representable. A mistaken relationship is voided rather than erased.

## Detection

Version detection is advisory. It may use confirmed concept bindings, confirmed lineage, normalized names, explicit upstream references such as reprint metadata, document shape, and textual similarity. A confidence score and reasons are returned to the Rules Lawyer.

Detection does not create concepts, bind sources, create lineage, make a rule decision, or publish anything automatically.

## Manual consolidation

A consolidation workspace presents every source implementation bound to a concept, including accessible immutable revisions and confirmed lineage. A Rules Lawyer selects one exact source revision as the base and can use the existing merge/structured patch system to construct the Dorks & Dice result.

Additional source revisions can be recorded as:

- `incorporated` — material from that source revision deliberately influenced the consolidated rule;
- `reference` — the source revision was deliberately reviewed as context but not necessarily incorporated.

Those contribution records are provenance only. They do not change patch semantics or make an inaccessible source readable.

The resulting decision remains append-only and is not live until the normal global publication step.

## Source access

Every source-backed comparison, contribution, lineage authoring operation, and detected-version binding independently enforces Rules Core source access. Rules Lawyer authority does not grant access to restricted source content.
