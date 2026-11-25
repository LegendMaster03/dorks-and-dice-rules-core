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

## Private access finding and proposed contract (approval required)

PrivateReferenceTicketValidator redeems one-time Site tickets, checks target
rules-core, contract version, user ID, dorks-and-dice mode and rules-wiki tunnel
source. PrivateOnly ingress maps the reference routes. NativeReferenceEndpoints
requires the first-party active domain and then uses NativePublicEndpoints.MayRead
for index/search/detail: authenticated first-party or exact package publication
approval. Direct authenticated consumer and anonymous public routes are separate.

Propose a private Wiki preview entitlement, not a publication bypass. The
validated Site subject and source must still pass all current checks; a
Core-owned, default-deny per-package preview configuration must separately
authorize the exact snapshot fingerprint and explicit review subjects (or an
owner-approved reviewer entitlement from Site). No client header, browser role,
network membership or license identifier grants preview. Apply the same decision
to index/search/detail, conceal denied catalogs, preserve first-party access,
retain the existing publication path for already-approved foreign packages.
Do not expose preview configuration, change public/direct consumer routes, or
allow preview exports. Require isolated acceptance tests for wrong source,
subject, mode, missing entitlement, stale fingerprint, unrelated package,
public/direct consumer denial and first-party regression. Isolated fixtures can
exercise the existing approval path without changing deployment settings.

This proposal is not implemented. Owner must approve scope of preview
subjects/entitlements and environment policy before implementation.

## Future campaign rulesets

Keep base package identity and entity source provenance separate. A future
authoritative campaign revision may provide a collection of inherited package
notices and separately attributed modifications. Do not copy notices from
arbitrary client selections or assign a single license to a composite ruleset.
No campaign inheritance or publication behavior is introduced.
