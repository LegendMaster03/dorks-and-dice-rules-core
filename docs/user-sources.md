# Account sources

The normal source workflow is deliberately simple. Source-package identity, canonical publication identity, work/release records, source-code partitioning, grants, and immutable revision handling are backend concerns and are not exposed as prerequisites to an ordinary account.

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
3. partitions an aggregate corpus by source code internally so unrelated publications are not assigned one representation-specific work identity;
4. imports complete entity objects into immutable Source Layer revisions;
5. derives canonical publication and source-occurrence identities separately from the representation that supplied them;
6. creates a restricted source package for the account's added source;
7. grants that signed-in account access to the package;
8. records the user-facing source registration so it can be listed later;
9. for a Web source, remembers the URL and upstream version information so it can be checked for updates.

Another account does not receive access merely because one account added a source. Canonical identity metadata does not grant access to source content. Adding a source also does not grant Dev, Rules Lawyer, campaign DM, or any other mutation authority.

## Web-source safety

Web sources must use HTTPS and can not contain embedded credentials. Rules Core rejects loopback, private, link-local, carrier-grade NAT, multicast, and other non-public destinations. Redirects are not followed. GitHub tree sources are enumerated beneath the selected tree path. Files that are not compatible with a supported source format are ignored while resolving the tree; if none are compatible, the Web source is rejected.

The current compatibility implementation considers 5e.tools-shaped JSON entity files. Narrative book/adventure JSON and other JSON documents that do not contain compatible entities are not treated as compatible source files merely because they are valid JSON.

## Upload snapshots and Web source refresh

An uploaded file is an immutable snapshot. To use a newer file, add the newer file.

A Web source is checked automatically once it is due for a check **24 hours after the previous check**. The background coordinator scans for due sources hourly, so a normally running instance checks a source approximately every 24–25 hours. The user-facing **Refresh** action remains available and performs an immediate check.

A check is deliberately cheaper than a content pull when the upstream provider exposes a stable version signal:

- GitHub tree sources are checked against the referenced branch/tag/commit's current commit SHA.
- Other Web sources use HTTP `ETag` and/or `Last-Modified` metadata when the server provides them.
- If the upstream version is unchanged, Rules Core records the check without downloading and re-importing the source corpus.
- If the version changed, Rules Core resolves the compatible files again and imports the resulting immutable revisions.
- If the server exposes no usable version metadata, Rules Core performs a full source read when the check is due; unchanged entity fingerprints still do not create new Source Layer revisions.

Registrations that use the same Web source URL share the upstream version probe during an automatic check. Access and import records remain account-specific even when the URL or canonical material is shared.

A refresh that no longer resolves any compatible files fails instead of replacing the source with an empty import. A transient refresh failure is recorded for that registration and does not delete the last successfully imported material.

## Canonical identity does not bypass access

Account imports are the content-availability boundary. A source package and its grant record why a particular account may read a particular imported representation. Canonical publication and occurrence records contain identity metadata and fingerprints, not a second globally readable copy of restricted source content.

If two accounts independently add representations that Rules Core recognizes as the same publication, their source packages, source entities, and grants remain separate. They may point to the same canonical publication and canonical source occurrence, but one account's grant never gives the other account access to the first account's source representation.

## Advanced administration remains separate

The existing Dev and Rules Lawyer source-administration screens remain available for maintainers who need to establish canonical shared hosted definitions, inspect provenance, manage advanced package metadata, or debug ingestion behavior. Those controls are not part of the normal **Add Source** procedure.

Likewise, acquisition-provenance records remain available as administrative/audit information where needed, but an ordinary user does not have to separately record an acquisition and then separately grant the same account access after choosing **Add Source**.
