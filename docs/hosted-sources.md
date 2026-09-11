# Hosted source definitions

Hosted source definitions let Rules Core use a canonical live location for commonly imported source material instead of accepting and retaining another submitted copy every time a user encounters the same upstream dataset.

## Authority and lifecycle

A user with the global **Rules Lawyer** role can create and revise hosted source definitions, preview the current remote material, and refresh it into the Source Layer. Definition changes are append-only: saving changed configuration creates a new definition revision, while saving identical configuration is idempotent.

Refreshing a hosted source does **not** make resolved rules depend on the remote server. Rules Core fetches the current remote documents, normalizes them through the definition's import adapter, and passes the resulting entities through the normal immutable Source Layer importer. Identical entities remain unchanged; changed entities receive a new immutable source revision.

The Dev-only manual 5e.tools source-import workflow checks the selected source-code partition against enabled hosted definitions before previewing an uploaded/pasted JSON copy. When registered 5e.tools hosted definitions fully cover the selected partition, the UI stops the normal manual-import path and points the user to the canonical hosted source. A deliberate manual override remains available for curated exceptions. Legacy SRD text definitions are acquired through their hosted definitions rather than matched against pasted 5e.tools JSON.

## Supported formats and resource kinds

Rules Core currently supports two hosted formats.

### `5etools-json`

The structured 5e/5.5e adapter supports:

- `direct-json` — one HTTPS JSON document;
- `json-index` — an HTTPS JSON index whose string values contain child `.json` paths or URLs;
- `github-tree` — a public GitHub tree URL. Rules Core enumerates JSON blobs below the configured path and fetches their raw contents.

A definition may combine multiple roots so a logical release can include explicit files plus indexed collections.

### `legacy-srd-text`

The 3e/3.5e adapter normalizes historical HTML or Markdown into the same canonical entity JSON consumed by the immutable Source Layer. It supports:

- `html-index` — an HTTPS index page whose same-host, same-corpus `.htm`/`.html` links are expanded and parsed. This is used for the surviving Dragon.ee 3.0 SRD representation;
- `github-tree` — a public GitHub tree whose Markdown blobs beneath the configured path are enumerated and parsed. This is used for the `olimot/srd-v3.5-md` representation.

Legacy parsing preserves each document URI and heading/body provenance, assigns deterministic normalized identities, and classifies resolver-relevant entities such as classes, prestige classes, NPC classes, races, skills, feats, spells, monsters, and magic items where the source structure supports that classification. Other headings remain generic `rule` entities rather than being discarded.

The legacy adapter uses normalized source labels `SRD3` and `SRD35`. These are Rules Core labels for partitioning and provenance; they are not claimed to be historical Wizards source codes.

## Logical source identity

A hosted source definition records the same logical provenance required by manual import:

- package key/display name, provider, license, and public/restricted visibility;
- work key/display name;
- release key/display name;
- canonical D&D game edition and release kind when known;
- publication date when known;
- source partition/normalization code when applicable.

The physical URL is acquisition provenance, not durable rule identity. Changing representation or mirror does not require a different package/work/release identity when both locations represent the same logical source.

For 5e.tools aggregate files, `IncludedSourceCodes` partitions mixed documents. For legacy SRDs, one normalized source label is attached to the corpus after the separately recorded authority establishes what belongs to that SRD.

## Authority versus representation

Hosted acquisition locations are not automatically corpus-membership authorities.

For 3e, Rules Core records the archived SRD 3.0 distribution index as the membership authority and uses `https://www.dragon.ee/30srd/` only as the parseable HTML representation.

For 3.5e, Rules Core records the archived official Wizards Revised 3.5 SRD ZIP as the membership authority and uses `olimot/srd-v3.5-md` only as the Markdown representation.

For 5e and 5.5e, the official SRD PDFs remain the membership authorities while the hosted JSON is a structured representation.

## Remote-source safety

Hosted resources must use HTTPS and can not contain embedded credentials. Before every fetch, Rules Core rejects loopback, link-local, private, carrier-grade NAT, multicast, and other non-public network addresses. Redirects are not followed; a Rules Lawyer should register the final canonical HTTPS URL instead. Individual remote documents are capped at 64 MiB, and an index/tree expansion is capped at 2,000 documents.

GitHub-tree acquisition rejects truncated recursive tree responses instead of silently importing a partial corpus.

## Runtime boundary

Hosted-source resolution is an acquisition operation, not a runtime dependency. Startup registers definitions and authority metadata but performs no remote fetch. A Rules Lawyer explicitly previews or refreshes a hosted definition; the resulting normalized entities are then stored as immutable Source Layer revisions. Rules resolution reads those local revisions and continues to work when the representation host is unavailable.

The 3e/3.5e adapter deliberately converges on the same `ISourceImportService` used by 5e/5.5e. The resolver therefore does not have separate legacy-edition logic: edition choice remains Rules Lawyer adjudication over ordinary source entities and revisions.
