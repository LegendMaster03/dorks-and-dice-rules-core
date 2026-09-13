# Account sources

The normal source workflow is deliberately simple. Source-package identity, work/release records, source-code partitioning, grants, and immutable revision handling are backend concerns and are not exposed as prerequisites to an ordinary account.

## Add Source

Any signed-in Dorks & Dice account can add source material from the Rules Library with **Add Source**. No Dev, Rules Lawyer, campaign DM, or campaign Player role is required beyond being signed in to the Dorks & Dice site.

The user chooses one of two source types:

- **Upload file** — select a source file and choose **Add source**.
- **Web source** — paste an HTTPS source URL and choose **Add source**. GitHub tree URLs are supported, including a source such as `https://github.com/5etools-mirror-3/5etools-src/tree/main/data`.

That is the complete normal user workflow.

Rules Core validates compatibility before importing anything. If an uploaded file is not compatible, the request returns an error. If a Web source contains no compatible files beneath the selected source location, the request returns an error rather than creating an empty source.

Compatibility is intentionally the contract instead of JSON itself. The current compatibility implementation supports the existing 5e.tools-shaped JSON entity format. Additional source formats can be added later without changing the user-facing **Upload file** and **Web source** workflow.

Rules Core then performs the internal work automatically:

1. validates and reads the submitted source;
2. discovers the contained source identities required by the compatible format;
3. partitions an aggregate corpus by source code internally so unrelated publications are not assigned one logical work identity;
4. imports complete entity objects into immutable Source Layer revisions;
5. creates a restricted source package for the account's added source;
6. grants that signed-in account access to the package;
7. records the user-facing source registration so it can be listed later;
8. for a Web source, remembers the URL so the source can be refreshed.

Another account does not receive access merely because one account added a source. Adding a source also does not grant Dev, Rules Lawyer, campaign DM, or any other mutation authority.

## Web-source safety

Web sources must use HTTPS and can not contain embedded credentials. Rules Core rejects loopback, private, link-local, carrier-grade NAT, multicast, and other non-public destinations. Redirects are not followed. GitHub tree sources are enumerated beneath the selected tree path. Files that are not compatible with a supported source format are ignored while resolving the tree; if none are compatible, the Web source is rejected.

The current compatibility implementation considers 5e.tools-shaped JSON entity files. Narrative book/adventure JSON and other JSON documents that do not contain compatible entities are not treated as compatible source files merely because they are valid JSON.

## Upload snapshots and Web source refresh

An uploaded file is an immutable snapshot. To use a newer file, add the newer file.

A Web source retains its URL and can be refreshed. Refreshing re-reads the source and imports only the resulting immutable revisions; existing Source Layer history is not overwritten. A refresh that no longer resolves any compatible files fails instead of replacing the source with an empty import.

## Advanced administration remains separate

The existing Dev and Rules Lawyer source-administration screens remain available for maintainers who need to establish canonical shared hosted definitions, inspect provenance, manage advanced package metadata, or debug ingestion behavior. Those controls are not part of the normal **Add Source** procedure.

Likewise, acquisition-provenance records remain available as administrative/audit information where needed, but an ordinary user does not have to separately record an acquisition and then separately grant the same account access after choosing **Add Source**.
