# Structured SRD JSON promotion

The implementation now prioritizes direct reuse of the pinned 5e.tools-shaped SRD corpus rather than reconstructing the structured definition from PDF text. The publisher remains the authority for verification and correction. This is not a blanket import of the full 5e.tools proprietary-book dataset.

The first batch adds typed time, range, components, duration, entries, higher-level entries and available descriptive/mechanical tags to all 319 SRD 5.1 and 339 SRD 5.2.1 spell records. It also restores the original structured trait/action/bonus/reaction/spellcasting arrays for all 95 modern Animals. Independently parsed numerical animal statistics, official page references, source layout and previous reviewed records remain intact. Native IDs and catalog counts do not change.

Run `node tools/promote_srd_json.mjs` to populate the selected existing native definitions, or `node tools/promote_srd_json.mjs --check` to verify their selected fields against the pinned source. The original PDF extraction checks independently verify publisher evidence. The staged corpus inventory workflow continues to verify pinned source hashes.

Metadata and media such as reprint references, fluff and external artwork are not promoted. Structured source pointers identify the original edition and occurrence. Source provenance explicitly distinguishes verified names/levels/schools or core stat-block fields from pending entry-body review. No body/table semantic certification, complete inline reference resolution, executable spell engine, publication approval or downstream compatibility is claimed. Known source discrepancies must be corrected in the promotion layer as they are reviewed.

The subsequent [structured bestiary promotion](srd-structured-bestiary-promotion.md) imports the remaining base stat blocks and applies known spell-name corrections. Other structured categories follow the same approach.
