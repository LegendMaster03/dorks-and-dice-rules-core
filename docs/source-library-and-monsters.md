# Source Library and monster beta integration

Rules Core keeps imported source material separate from adjudicated/published rules. The Source Library is the normal entry point for browsing that material and for hydrating the built-in SRDs.

## Built-in SRD hydration

The four built-in definitions are registered by baseline bootstrap:

- 3e SRD (`SRD3`)
- 3.5e SRD (`SRD35`)
- SRD 5.1 / 5e (`SRD51`)
- SRD 5.2.1 / 5.5e (`SRD52`)

Registration does not perform network I/O. A Rules Lawyer can import or refresh each source from the Library. The Library reports whether local source entities exist, approximate entity counts, detected monster counts, and the most recent import time visible in the current result window. Counts shown with `+` reached the browser query cap and are intentionally presented as lower bounds.

The existing Hosted Source Definitions screen remains available under **Advanced** for definition maintenance. Manual JSON import and source-access controls also remain available under **Advanced**.

## Monster discovery contract

Block Initiative and other tools can use the existing Source Layer API without depending on the Rules Layer:

1. Search accessible monster entities:

   `GET /api/sources/entities?entityType=monster&q=<name-or-source>&limit=100`

2. Retrieve the latest accessible immutable source document:

   `GET /api/sources/entities/{entityId}`

The detail document preserves the imported source object. Structured 5e/5.5e bestiary records therefore retain their normal armor class, hit point, speed, ability-score, challenge-rating, action, trait, and other fields. Legacy 3e/3.5e entities preserve the original normalized stat-block body and provenance.

The Library monster inspector projects common fields (AC, HP, hit dice, initiative/Dex modifier, speed, challenge rating, and ability scores) for beta testing. That projection is presentation/integration assistance only; it does not replace the immutable source document or adjudicate cross-edition meaning.

## Rules-layer boundary

Importing an SRD never automatically makes those entities table rules. Source entities remain available for browsing, normalization, version review, concept binding, consolidation, and explicit Rules Lawyer publication. This preserves the rule that same-name material from different editions is not automatically treated as equivalent or superseded.
