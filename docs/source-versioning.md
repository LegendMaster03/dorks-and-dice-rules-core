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
2. **Non-destructive additive:** the bound implementations can be combined without replacing or contradicting any shared rule-bearing value. Shared scalar values must match exactly. Object properties may be added. Named array entries, such as monster traits or actions, may be added or omitted so long as entries with the same identity remain compatible and shared ordering is not contradictory. Anonymous or ambiguous array changes remain manual.

The additive case covers all of these common patterns:

- a newer monster is otherwise unchanged and adds an ability;
- a newer monster is otherwise unchanged and omits an older compatible ability, in which case the omission does not destructively remove that ability from the Dorks & Dice result;
- the older version contains ability A while the newer version contains ability B, with all shared mechanics unchanged, in which case the resolved result can retain both A and B.

This is not a generic JSON union. A changed number, rewritten text, changed same-named ability, incompatible type change, conflicting array order, or other replacement is a conflict and requires manual adjudication.

When compatible implementations qualify, Rules Core creates an append-only automatic global decision:

- if an exact bound source revision already contains the complete compatible result, Rules Core records a normal `select-source` decision for that revision, even when the complete revision is the older edition;
- if no one source contains the complete compatible result, Rules Core selects the preferred source revision as the base and records a deterministic `json-merge-patch` containing only the verified additive result.

Source revisions that actually contribute compatible content to an automatic additive merge are recorded as `incorporated`; revisions that were reviewed but add nothing beyond the selected base are recorded as `reference`. Publication remains a separate explicit action.

Automatic decisions are revalidated against the current latest revision of every bound implementation. If a later import or newly bound edition introduces a conflict, the earlier automatic decision remains in append-only history but is no longer treated as the current ruling in authoring. Publication is blocked until the concept is resolved again. If the comparison remains identical or additively compatible, Rules Core can append a refreshed automatic decision whose provenance covers the current comparison set.

An existing manual or patched ruling is never replaced automatically. An existing exact-source manual decision may be retained without decision churn only when that exact source already contains the complete compatible result. Inaccessible bound sources, missing edition metadata, conflicting values, or other ambiguity leave the concept for normal manual adjudication.

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
