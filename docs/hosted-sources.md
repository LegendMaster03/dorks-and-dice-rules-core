# Hosted source definitions

Hosted source definitions let Rules Core use a canonical live location for commonly imported source material instead of accepting and retaining another submitted copy every time a user encounters the same upstream dataset.

## Authority and lifecycle

A user with the global **Rules Lawyer** role can create and revise hosted source definitions, preview the current remote material, and refresh it into the Source Layer. Definition changes are append-only: saving changed configuration creates a new definition revision, while saving identical configuration is idempotent.

Refreshing a hosted source does **not** make resolved rules depend on the remote server. Rules Core fetches the current remote documents, filters them to the definition's logical source codes, and passes the resulting entities through the normal immutable Source Layer importer. Identical entities remain unchanged; changed entities receive a new immutable source revision.

The Dev-only manual source-import workflow checks the selected source-code partition against enabled hosted definitions before previewing an uploaded/pasted JSON copy. When registered hosted definitions fully cover the selected partition, the UI stops the normal manual-import path and points the user to the canonical hosted source. A deliberate manual override remains available for curated exceptions.

## Supported resource kinds

The initial hosted-source adapter targets the existing 5e.tools-shaped JSON importer and supports three remote resource shapes:

- `direct-json` — one HTTPS JSON document, for example `https://5e.tools/data/feats.json`.
- `json-index` — an HTTPS JSON index whose string values contain child `.json` paths or URLs. This supports indexes such as `https://5e.tools/data/bestiary/index.json` and `https://5e.tools/data/spells/index.json`.
- `github-tree` — a public GitHub tree URL such as `https://github.com/5etools-mirror-3/5etools-src/tree/main/data`. Rules Core uses the public GitHub tree API to enumerate JSON files below the configured path and fetches their raw contents.

A definition may combine multiple root resources. This is useful when one logical release is represented by several direct files and/or indexed collections.

## Logical source identity

A hosted source definition records the same logical provenance required by manual import:

- package key/display name, provider, license, and public/restricted visibility;
- work key/display name;
- release key/display name;
- canonical D&D game edition and release kind when known;
- publication date when known;
- optional 5e.tools source-code filter.

The physical URL is acquisition provenance, not durable rule identity. Changing from a 5e.tools URL to a GitHub mirror does not require a different package/work/release identity when both locations represent the same logical source.

Mixed 5e.tools aggregate files should normally use `IncludedSourceCodes`. The same physical `feats.json`, for example, may contain entities from many books/releases; a definition should select only the source codes that belong to its logical release.

## Remote-source safety

Hosted resources must use HTTPS and can not contain embedded credentials. Before every fetch, Rules Core rejects loopback, link-local, private, carrier-grade NAT, multicast, and other non-public network addresses. Redirects are not followed; a Rules Lawyer should register the final canonical HTTPS URL instead. Individual remote documents are capped at 64 MiB, and an index/tree expansion is capped at 2,000 JSON documents.

## Import-format boundary

Hosted-source resolution is deliberately separate from import-format parsing. The first implementation resolves live resources into the existing `5etools-json` adapter. Additional import formats can later implement their own adapters without changing the persistent hosted-definition model.
