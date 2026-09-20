# Source normalization suggestions

Imported Source Layer entities are not automatically treated as the same rule merely because they have similar names. Rules Core keeps source identity separate from Rules Layer concept identity and requires a Rules Lawyer to confirm normalization.

Canonical source identity is an earlier, different operation. Two representation-specific source entities may be recognized as the same **occurrence in the same publication** without creating or changing a Dorks & Dice rule concept. For example, a 5e.tools entity and a PDF-derived entity can point to one canonical PHB occurrence while remaining separate gated source records. That source-level association does not imply that an SRD occurrence, a later-edition occurrence, or a third-party occurrence is the same Rules Layer concept.

## Purpose

The normalization workflow reduces repetitive concept creation and source binding without turning heuristics into authoritative rules decisions. It operates only on source entities the current user may independently access and that do not already have a Rules Layer binding.

`GET /api/global/rules/normalization/candidates` returns reviewed candidates. Optional `entityType`, `q`, and `limit` parameters narrow the list. The endpoint requires normal Dorks & Dice global Rules Lawyer authority.

For each candidate, Rules Core derives a deterministic suggested concept key from the source entity type and name. For example, a `skill` entity named `Arcana` suggests `skill.arcana`. Punctuation and whitespace are normalized into stable key segments. The suggestion does not inspect or infer rule semantics from the source document.

A candidate is classified as:

- `new-concept` when the suggested key does not exist;
- `existing-concept` when the suggested key already exists with the same entity type;
- `conflict` when the suggested key exists for a different entity type.

The browser displays the proposed key and classification before any mutation occurs.

## Accepting a suggestion

`POST /api/global/rules/normalization/entities/{sourceEntityId}/accept` accepts one reviewed suggestion.

For a `new-concept` candidate, acceptance creates the stable Rules Layer concept and binds the source entity to it in one transaction. For an `existing-concept` candidate, it reuses that concept and creates only the source binding. Repeating an already accepted suggestion is idempotent.

A conflicting suggestion is rejected. The Rules Lawyer can instead use the existing manual concept/source-binding workflow when the deterministic name-based suggestion is not appropriate.

Acceptance does not infer equivalence from the normalization suggestion itself and never publishes a ruleset revision. After the deliberate binding is recorded, Rules Core may run its separate conservative compatibility check across the latest bound editions. That check can create an automatic global decision when the bound implementations are mechanically identical or when their differences can be combined as a verified non-destructive additive result. Publication remains explicit.

## Cross-edition behavior

The workflow is useful for common names repeated across editions. If a 2014 `Arcana` entity is accepted first, Rules Core can create `skill.arcana`. A later accessible 2024 `Arcana` entity then proposes the existing `skill.arcana` concept rather than another concept.

This is still a binding suggestion, not an automatic Rules Layer association. Canonical source-occurrence matching only deduplicates representations of the same occurrence in the same publication; it does not cross this boundary. The Rules Lawyer must accept each Rules Layer binding. Only after those deliberate bindings exist may the compatibility resolver compare the exact source revisions.

Mechanically identical implementations can auto-resolve. Purely additive/subtractive differences can also auto-resolve when shared rule-bearing values do not conflict. If one exact source already contains the complete compatible result, Rules Core selects that source. If neither source is complete alone—for example, the older monster contains ability A while the newer monster drops A but adds compatible ability B—Rules Core may create a deterministic additive patch whose resolved result retains both A and B.

If shared values conflict, text changes, same-named entries disagree, rule-bearing entries are incompatibly reordered, or an array change is too ambiguous to classify safely, the concept remains for manual adjudication. Differently named implementations, exceptional cases, aliases, split concepts, or entities that should map elsewhere also remain available through manual authoring.

## Authorization and source access

Normalization preserves the two independent authorization axes used throughout Rules Core:

- Dorks & Dice `Rules Lawyer` authority determines whether the user may create concepts and bindings.
- Rules Core source grants determine whether the user may see and normalize restricted source metadata.

A Rules Lawyer without a grant does not receive a restricted source as a candidate and receives not-found behavior if they try to accept that restricted entity by ID. A source grant by itself does not grant Rules Lawyer authority.

The candidate endpoint returns descriptive source metadata but not the raw source document. It still enforces source access because even restricted package/entity names and provenance must not leak through Rules Layer convenience APIs.

Canonical publication/occurrence tables do not weaken this rule. They are internal identity indexes, not a source-content API and not a substitute for package grants.

## Design boundary

Normalization and automatic compatibility resolution are intentionally conservative:

- no unattended or bulk automatic acceptance by server heuristics;
- no automatic concept binding by server heuristics;
- an authenticated human or Rules Lawyer agent may deliberately inspect and explicitly accept one deterministic suggestion, with the normal actor identity and audit provenance recorded;
- no content-similarity binding into Rules Layer concepts;
- no mutation of Source Layer records;
- no automatic publication; publication remains a separate explicit Rules Lawyer action;
- no blind JSON union across conflicting or ambiguous versions;
- no replacement of an existing manual or patched ruling;
- no silent resolution of key conflicts;
- an automatic decision is allowed only after deliberate binding and deterministic verification that the bound revisions are identical or can be combined without replacing shared rule-bearing content.

Future normalization helpers may offer aliases or richer similarity suggestions, but they must remain explainable and reviewable rather than becoming an implicit source of truth.
