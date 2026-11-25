# Ruleset package metadata and attribution

Workstream F; base 6a300933435c633b51f1d446fca09b9810dceb9e. This is not a publication approval.

The existing package manifest is the authority. The catalog loader projects only
name, sourceSystem, sourceEdition, sourcePublication, sourceVersion, license,
licenseUrl, attribution, copyrightNotices, originalSources, modificationNotices
and acknowledgments into an immutable snapshot. RulesetId is the validated
package identity. The index descriptors at /api/private/rulesets,
/api/rulesets and /api/consumer/v1/rulesets gain an additive metadata object
(camel-case names: rulesetId, displayName, sourceSystem, sourceEdition,
sourcePublication, sourceVersion, licenseIdentifier, licenseUrl,
attributionStatement, copyrightNotices, originalSources, modificationNotices,
acknowledgments). Missing strings are null and missing arrays are empty. Only
HTTPS links without userinfo are projected. Existing manifests without fields
remain loadable; absence is not permission or an inferred license. The
first-party package declares no package-wide license.

Metadata is distinct from entity provenance; a package notice does not grant
rights to every separately sourced entity or independently authored addition.
Clients must use the descriptor for the exact authorized ruleset and
fingerprint; do not substitute another package or cache across users. Render
notices as text and links safely. No filesystem roots, full manifests, private
review references or distribution settings are serialized. Metadata is authored
consumer-facing text, not a destination for secrets. The existing content digest
remains unchanged in meaning (native entities, not manifest display fields).
Rediscover the descriptor when opening a reader; publication review must also
review metadata independently. Do not infer approval from a license.

## Existing access boundary (unchanged)

Site owns tester access to the Rules Wiki. The private reference tunnel and
the existing Core endpoint policies remain authoritative. The private catalog
index currently applies the existing foreign-package distribution approval
check; this work does not change that behavior. No additional reviewer roster,
preview entitlement, package eligibility configuration or authorization path is
in scope. Metadata is returned only for descriptors already permitted by
the existing endpoint. Fixture tests exercise the existing approval path
without enabling production packages.

## Future campaign rulesets

Keep base package identity and entity source provenance separate. A future
authoritative campaign revision may provide a collection of inherited package
notices and separately attributed modifications. Do not copy notices from
arbitrary client selections or assign a single license to a composite ruleset.
No campaign inheritance or publication behavior is introduced.
