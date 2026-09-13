# Source Format Adapters

Rules Core must not assume that structured datasets such as 5e.tools are the first or authoritative representation of a publication.

A source file can be the first time Rules Core has encountered both the publication and the rules content it contains. This is expected for third-party material, including publications that are absent from 5e.tools or only partially represented there.

## Core rule

Source ingestion and cross-representation entitlement are separate concerns.

A compatible source must be ingestible even when:

- no canonical publication already exists;
- no 5e.tools source code is known;
- no structured representation of the publication exists;
- no matching source entity or rule concept exists yet.

When another representation already exists, canonical identity may link the two representations. That linkage is deduplication/provenance evidence; it is not a prerequisite for accepting the new source.

## Adapter responsibilities

Each source-format adapter converts one physical representation into Source Layer material. The adapter is responsible for:

1. validating that it can read the supplied representation;
2. preserving enough provenance to identify the physical source and its extraction method;
3. extracting publication-level evidence such as title, publisher, edition, publication date, identifiers, or format-specific aliases when available;
4. extracting source entities or source fragments without inventing unsupported mechanics;
5. retaining unclassified material that is useful source evidence instead of silently dropping it;
6. producing stable local identities and fingerprints so reimport is idempotent;
7. allowing canonical publication and occurrence matching to happen after ingestion.

An adapter must not require a pre-existing canonical publication or source code.

## PDF behavior

A text-readable PDF is a first-class source representation, not merely proof that the account owns another representation.

For a PDF that Rules Core has never seen before, the intended flow is:

1. create a restricted source package for the uploaded PDF;
2. grant that package only to the uploading account;
3. extract document metadata and readable page/layout text;
4. create or match the canonical publication from the available bibliographic evidence;
5. extract recognized rule-bearing entities when the adapter has enough evidence to classify them;
6. preserve remaining material as source fragments with page/location provenance;
7. allow later normalization, manual classification, or improved extraction to turn those fragments into more specific rule entities without rewriting the original source evidence.

The uploaded PDF package remains independently usable even when there is no other representation of the publication anywhere in Rules Core.

### No guessing

PDF layout is not a reliable rules schema. The adapter must prefer an unclassified fragment over an invented monster, spell, feat, item, or mechanical field.

Scan-only PDFs are a separate capability. The initial PDF adapter may reject documents with no usable text layer. OCR can be added later behind the same adapter boundary without changing Source Layer identity rules.

## Relationship to structured representations

A structured representation such as 5e.tools can provide higher-quality entity boundaries than a PDF, but it is only another source representation.

If Rules Core later determines that a PDF publication and a 5e.tools publication are the same canonical publication:

- both source packages remain distinct provenance records;
- their source entities may bind to the same canonical publication/occurrences when evidence supports that match;
- the uploader's PDF grant does not automatically become a grant to an unrelated multi-publication package;
- any policy that allows one verified publication representation to unlock another representation must operate at canonical-publication scope, not package scope.

This prevents a PDF for one book from granting every book contained in a broad 5e.tools package.

## Source entity granularity

Adapters should emit the most specific source entity that can be identified safely.

Preferred examples include:

- monster/stat block;
- spell;
- feat;
- class/subclass feature;
- species/race feature;
- item;
- condition;
- explicit rules section;
- table or other mechanically meaningful block.

When the adapter can not establish one of those boundaries confidently, it should emit a generic source fragment carrying at least:

- publication identity/evidence;
- page number or equivalent locator;
- extracted text;
- a stable fragment identity;
- extraction provenance.

Generic fragments are intentionally valid Source Layer records. They represent what the source says before Rules Core has enough information to classify it more narrowly.

## Canonical identity

Canonical publication identity is representation-neutral. It may be created from any adapter.

The first encountered representation can therefore establish the canonical publication. Later representations add aliases and corroborating bibliographic/content evidence rather than replacing the first source.

Canonical occurrence matching should remain conservative. Failure to match two representations must create parallel source evidence for later review, not force a merge.

## Access model

The access grant created by a private upload applies to the uploaded source package. This is sufficient for a previously unknown PDF to become usable immediately.

A future canonical-publication entitlement can be used only when Rules Core intentionally exposes an alternate representation of the same publication. It is not part of the minimum PDF-ingestion path.

## Architectural implication

`CurrentUserSourceCompatibility` is currently a 5e.tools JSON compatibility check. It should evolve into format-adapter dispatch rather than becoming a list of special cases that all have to produce 5e.tools JSON.

The Source Layer importer should likewise accept adapter-produced source entities independently of the physical input format. The existing 5e.tools importer can remain as one adapter/compatibility path while the generalized import contract is introduced.
