# Source normalization suggestions

Imported Source Layer entities are not automatically treated as the same rule merely because they have similar names. Rules Core keeps source identity separate from Rules Layer concept identity and requires a Rules Lawyer to confirm normalization.

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

Acceptance deliberately does **not** create a global rule decision and does **not** publish a ruleset revision. Normalization answers only which source implementations correspond to a stable concept. Adjudication still occurs separately through preview, decision, and publication.

## Cross-edition behavior

The workflow is useful for common names repeated across editions. If a 2014 `Arcana` entity is accepted first, Rules Core can create `skill.arcana`. A later accessible 2024 `Arcana` entity then proposes the existing `skill.arcana` concept rather than another concept.

This is still a suggestion, not an automatic merge. The Rules Lawyer must accept each binding. Differently named implementations, exceptional cases, aliases, split concepts, or entities that should map elsewhere remain available through manual authoring.

## Authorization and source access

Normalization preserves the two independent authorization axes used throughout Rules Core:

- Dorks & Dice `Rules Lawyer` authority determines whether the user may create concepts and bindings.
- Rules Core source grants determine whether the user may see and normalize restricted source metadata.

A Rules Lawyer without a grant does not receive a restricted source as a candidate and receives not-found behavior if they try to accept that restricted entity by ID. A source grant by itself does not grant Rules Lawyer authority.

The candidate endpoint returns descriptive source metadata but not the raw source document. It still enforces source access because even restricted package/entity names and provenance must not leak through Rules Layer convenience APIs.

## Design boundary

Normalization is intentionally conservative:

- no bulk automatic acceptance;
- no content-similarity matching;
- no semantic inference from descriptions or mechanics;
- no mutation of Source Layer records;
- no automatic decision or publication;
- no silent resolution of key conflicts.

Future normalization helpers may offer aliases or richer similarity suggestions, but they must remain explainable and reviewable rather than becoming an implicit source of truth.
