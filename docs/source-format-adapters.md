# Source Format Adapters

Rules Core does not assume that structured datasets such as 5e.tools are the first or authoritative representation of a publication.

A source file can be the first time Rules Core has encountered both the publication and the rules content it contains. This is expected for third-party material, including publications that are absent from 5e.tools or only partially represented there.

## Core rule

Source ingestion and cross-representation entitlement are separate concerns.

A compatible source is ingestible even when:

- no canonical publication already exists;
- no 5e.tools source code is known;
- no structured representation of the publication exists;
- no matching source entity or rule concept exists yet.

When another representation already exists, canonical identity may link the two representations. That linkage is deduplication/provenance evidence; it is not a prerequisite for accepting the new source and it never grants access to another package.

## Implemented ingestion pipeline

The normal current-user path is now:

`bytes -> ISourceFormatAdapter -> normalized publication/records -> Source Layer -> canonical reconciliation`

`ISourceFormatAdapterRegistry` selects a compatible adapter. The first implemented adapters are:

- `FiveEToolsSourceFormatAdapter` for the existing 5e.tools-shaped JSON entity format;
- `PdfSourceFormatAdapter` for PDFs with a usable text layer.

`INormalizedSourceImportService` persists adapter output independently of the physical input format. The older `ISourceImportService` remains for existing bundled/administrative 5e.tools import paths; it is no longer the fundamental current-user Add Source model.

Each imported artifact is retained separately in `source_representation`, including its format, origin identity, file name, source URL when applicable, media type, SHA-256, byte length, original bytes, adapter metadata, and import time. `source_representation_publication` records which canonical publication a particular representation was associated with.

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

The first-pass PDF adapter:

1. reads the PDF text layer with PdfPig;
2. rejects a PDF when no usable page text can be extracted;
3. extracts explicit publication evidence when available, including PDF title metadata, labeled title/publisher/system/publication-date fields, and ISBN;
4. establishes a new canonical publication when no sufficiently strong existing identity matches;
5. retains every readable page as a `source-fragment` with its original page number and extraction provenance;
6. associates a later representation with an existing canonical publication only when canonical identity evidence is sufficiently strong;
7. preserves the original PDF bytes and representation metadata regardless of whether canonical deduplication succeeds.

The adapter intentionally does not infer monsters, spells, feats, or other mechanical entity types from weak layout/text cues. Generic fragments are valid Source Layer records and can be classified more specifically by later extraction work without rewriting the original source evidence.

OCR is not implemented in this slice. Scan-only PDFs with no usable text layer are therefore incompatible for now.

## Publisher provenance and conflicts

Publisher evidence is stored on the source edition produced by the adapter and is also supplied to canonical publication reconciliation.

When canonical identity is established by stronger evidence such as an ISBN alias, later missing canonical publisher/date/edition fields may be filled from the new representation. Conflicting non-null evidence is not silently overwritten. The canonical value is retained and the observation is recorded in `canonical_publication_evidence_conflict`, linked to the source entity that supplied the conflicting evidence.

## Relationship to structured representations

A structured representation such as 5e.tools can provide higher-quality entity boundaries than a PDF, but it is only another source representation.

If Rules Core determines that a PDF publication and a 5e.tools publication are the same canonical publication:

- both source packages remain distinct provenance records;
- their source entities may bind to the same canonical publication/occurrences when evidence supports that match;
- the uploader's PDF grant does not automatically become a grant to an unrelated multi-publication package;
- canonical identity itself does not confer source access.

This prevents a PDF for one book from granting every book contained in a broad 5e.tools package.

## Source entity granularity

Adapters emit the most specific source entity that can be identified safely. Preferred future PDF classifications include monster/stat block, spell, feat, class/subclass feature, species/race feature, item, condition, explicit rules section, table, or another mechanically meaningful block.

When the adapter can not establish one of those boundaries confidently, it emits a generic source fragment carrying at least publication evidence, page number or equivalent locator, extracted text, a stable fragment identity, and extraction provenance.

## Canonical identity

Canonical publication identity is representation-neutral. The first encountered representation can establish the canonical publication. Later representations add aliases and corroborating bibliographic/content evidence rather than replacing the first source.

Exact external aliases such as ISBN or a representation-specific source identifier are strongest evidence. Bibliographic matching uses title plus available publisher/system/date evidence. Sparse title-only evidence is deliberately content-disambiguated so two unrelated same-titled publications are not silently merged. Ambiguous evidence remains parallel source material for later review.

Canonical source occurrences remain separate from Rules Layer concepts. Same-named rules across publications or editions are not automatically treated as one Dorks & Dice semantic rule.

## Access model

The access grant created by a private upload applies to the uploaded source package. This is sufficient for a previously unknown PDF to become usable immediately.

Canonical publication and occurrence records are identity/provenance metadata. They do not make restricted source content globally readable and do not broaden a package grant to another account or another aggregate package.

## Web refresh

Moving Web sources retain independent account registrations while sharing a version probe by normalized URL during each automatic refresh pass. GitHub tree sources use commit SHA; other HTTP sources use `ETag` and/or `Last-Modified` when available. A full source pull is performed only when the version changed or no usable version signal exists.

The refresh coordinator is an ASP.NET hosted `BackgroundService`, not fire-and-forget process work. It scans hourly for registrations whose previous check is at least 24 hours old. Manual Refresh remains available. Refresh re-enters the same normalized adapter pipeline and does not run the legacy 5e.tools canonical indexer afterward.
