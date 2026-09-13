# Cross-version detection, lineage, and consolidation

Rules Core exists in part to recognize that the same conceptual rule can have multiple implementations across source revisions, publications, playtests, and D&D editions, and then consolidate those implementations into the Dorks & Dice rule without forcing unnecessary manual work.

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

Detection by itself does not create concepts, bind sources, create lineage, make a rule decision, or publish anything automatically. A Rules Lawyer must first confirm that implementations belong to the same concept.

## Automatic compatible resolution

After a source implementation is deliberately bound to a concept, Rules Core checks whether the latest accessible implementations span multiple D&D editions and are mechanically compatible. The comparison canonicalizes object-property ordering and ignores only top-level identity/provenance markers such as name, source code, page, edition flags, basic-rules flags, and reprint/reference metadata.

Two automatic cases are allowed:

1. **No-change:** every bound implementation contains exactly the same rule-bearing content.
2. **Non-destructive additive:** one complete source revision is a semantic superset of every other bound implementation. Shared scalar values must match exactly; object properties may be added; and array entries may be added without changing the relative order or contents of existing entries. This covers cases such as a monster version that is otherwise unchanged but adds an ability, or a newer version that removes an otherwise compatible older ability. The superset revision is retained so an omission does not destructively remove compatible content.

The additive rule is intentionally a subset/superset test, not a generic union. If one edition removes one rule element while adding a different one, changes a number, rewrites text, renames an entry, reorders rule-bearing array content, or otherwise leaves neither implementation as a semantic superset of the other, Rules Core requires manual adjudication.

When the bound implementations qualify, Rules Core automatically creates an append-only `select-source` global decision. For identical implementations, the newest publication is used as the representative base because the rule content is equivalent. For additive implementations, the semantic superset is selected even when it comes from the older publication. Other reviewed revisions are recorded as reference provenance. Publication remains a separate explicit action.

Automatic decisions are revalidated against the current latest revision of every bound implementation. If a later import or newly bound edition introduces a conflict or causes the selected revision to stop being the semantic superset, the earlier automatic decision remains in append-only history but is no longer treated as the current ruling in authoring. Publication is blocked until the concept is resolved again. If the comparison remains identical or additive, Rules Core can append a refreshed automatic decision whose provenance covers the current comparison set.

If an existing exact-source manual decision already selects the same compatible result, it is retained without creating decision churn. Existing patched/manual rulings are never replaced automatically. Inaccessible bound sources, missing edition metadata, conflicting values, or other ambiguity leave the concept for normal manual adjudication.

This is intentionally conservative: false negatives create extra review work, while false positives could silently change table rules.

## Manual consolidation

When implementations differ beyond the safe compatible cases, a consolidation workspace presents every source implementation bound to a concept, including accessible immutable revisions and confirmed lineage. A Rules Lawyer selects one exact source revision as the base and can use the existing merge/structured patch system to construct the Dorks & Dice result.

Additional source revisions can be recorded as:

- `incorporated` — material from that source revision deliberately influenced the consolidated rule;
- `reference` — the source revision was deliberately reviewed as context but not necessarily incorporated.

Those contribution records are provenance only. They do not change patch semantics or make an inaccessible source readable.

The resulting decision remains append-only and is not live until the normal global publication step.

## Source access

Every source-backed comparison, contribution, lineage authoring operation, detected-version binding, and automatic compatibility check independently enforces Rules Core source access. Rules Lawyer authority does not grant access to restricted source content.
