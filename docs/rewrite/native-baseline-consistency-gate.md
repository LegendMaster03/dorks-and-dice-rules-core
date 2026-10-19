# Native baseline consistency and decision gate

**This gate must be completed before SRD 5.x reconciliation or final baseline review.**

The native Dorks & Dice rules are edition-independent and authoritative. There is exactly **one operative definition of each decided mechanic**, never alternative editions, parallel candidate definitions, hidden runtime switches or a fallback to whichever candidate is listed first.

A rule with multiple proposed definitions is **not decided**. The original staging of SRD 5.1 and SRD 5.2.1 is only an inventory. A candidate found in that inventory is not automatically a native ruling.

## Resolution procedure

1. Start with the existing Dorks & Dice rules design specification and previously approved owner decisions. Express each settled mechanic as one coherent native definition, including explicitly adopted combinations of 3.x/5.x rules. Never widen partial approvals to cover unrelated effects.
2. Where differences are cosmetic or structurally different but mechanically equivalent, unify them without inventing a game-design choice. Verify actual consequences rather than assuming matching names are equivalent.
3. Where actual differences remain, use the design decisions already documented. If those do not settle the conflict, leave the rule explicitly **unresolved for the owner**; do not publish either version as the native default.
4. Ask for owner rulings with concise descriptions of the competing mechanical outcomes and their consequences. No source-by-source evidence, publication history or dual source definitions are required in the native rules or owner decision queue.
5. Keep skills distinct from competencies, stable native identities, explicit parent/child relationships and compact files for contained categories. Do not introduce duplicate mechanics while normalizing.
6. Preserve mandatory third-party license and attribution information independently of native gameplay data. Existing pinned reference material is not part of the native runtime model.
7. Validate every native entity for duplicate identities, conflicting representations, unresolved candidates, missing relationships and missing coverage. A successful inventory or import does not mean normalization is complete.

## Approved cross-category reconciliation policy

**Owner approved on 2026-10-18:** (1) combine equivalent mechanics as one native definition; (2) retain content unique to one SRD when compatible; (3) combine genuinely compatible, nonduplicated additions while respecting eligibility and costs; (4) use category-specific decisions for incompatible numerical values, never an automatic source preference or average; and (5) require a decision for fundamentally different procedures, applying existing design guidance first.

The detailed scope and limitations are recorded in `docs/rewrite/native-rules-design-specification.md`. This approval supplies the default *decision method*, not missing category-specific numerical selections or redesigned procedures. Decisions with no governing policy stay explicit and unresolved. No additional source evidence is required in the native definition.

## Release gates

**A. Structural consistency:** Native records have uniform representation and at most one authoritative definition each; undecided entries are explicit and contain no competing operative versions. This must be verified across the complete native corpus.

**B. SRD reconciliation:** Resolve the remaining mechanical differences. Apply established decisions without repeated approval requests. Obtain new decisions from the owner wherever the design specification is insufficient.

**C. 5.x baseline sign-off:** Perform a separate completeness, coherence, and gameplay review. Only after approval is the 5.x baseline considered finished, and only then does further rules-engine development begin.

**Current status — updated reconciliation (2026-10-18):** Gate A remains open. Of 2,522 logical baseline records, 1,073 still hold multiple candidate definitions and 590 hold single candidate definitions: **1,663 candidate records** remain. The original candidate backlog was 2,479. The total reduction is **816 (32.9%)**; the monster pass reduced it by 285.

**Monster pass:** 285 shared monster identities now use the newer role-specific stat block. Another 23 older creature names are mapped as aliases to newer identities (already counted as retained in the earlier pass). Eighty-four compatible older traits were incorporated. Three native families list Goblin, Hobgoblin and Bugbear roles. Another 66 single-source monster records remain retained. A separate 249-entry ledger records older capabilities needing semantic replacement or distinct-role review; these entries do not represent 249 additional unresolved dual-source monster definitions.

Native conversion of imported markup, verification of retained older abilities, and the other category decisions are still needed. Reference: `docs/rewrite/native-reconciliation-statistics.json` and `data/reference/srd/monster-legacy-variant-review.json`.
