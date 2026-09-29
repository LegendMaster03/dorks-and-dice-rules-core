# Browser-link and reference-browsing contracts

Rules Core remains authoritative for canonical identity, source access, Rules Layer resolution, and the API data consumed by Rules Wiki. Human-readable browser pages are owned by Rules Wiki.

## Effective consumer browser links

Resolved consumer APIs continue to expose `browserLink` with the same three-part contract:

- `toolSlug`: `rules-wiki`;
- `toolRelativePath`: a stable path such as `/monsters/ancient-red-dragon`, `/conditions/prone`, or `/rules/{conceptKey}`;
- `routeIdentity`: the canonical Rules Layer concept key.

Changing `toolSlug` does not change concept keys, source IDs, rule IDs, consumer API routes, or Rules Layer semantics. Consumers should treat `browserLink` as an opaque navigation target instead of constructing `/tools/...` URLs themselves.

Legacy route parsing remains in Rules Core where it is part of the stable link contract and regression tests. Presentation and Embedded Module route state live in Rules Wiki.

## Rules Wiki reference read model

Rules Wiki does not use `/api/rules` as its complete browsing catalog. `/api/rules` remains the effective consumer contract for game Tools.

Rules Wiki uses the first-party reference endpoints:

- `GET /api/wiki/references`
- `GET /api/wiki/references/{referenceIdentity}`
- `GET /api/campaigns/{campaignId}/wiki/references`
- `GET /api/campaigns/{campaignId}/wiki/references/{referenceIdentity}`
- `POST /api/wiki/references/comparison`

A Wiki reference can exist before a `RuleConcept` or published Rules Layer decision exists. Such a reference receives a stable Core-owned identity derived from its accessible canonical history and uses `/references/{referenceIdentity}` as its browser route. Creating or reading that identity does not create a Rules Layer concept or decision.

Reference histories group canonical entities connected by `revision` or `rename`. `variant` and `reprint` relationships do not collapse into the same evolving history. Every variation retains its exact source revision and provenance.

A variation has one canonical mechanical category. Legacy naming aliases such as Race/Species and Subrace/Subspecies normalize to the same category. Genuine mechanical category changes, such as Prestige Class to Subclass, remain distinct in history. The immutable source record still retains its original source terminology; the Wiki contract does not expose a second `NativeEntityType` presentation field.

Search, Source, Package, Edition, category, and campaign-override filtering are evaluated over the complete accessible reference set in Rules Core. Facets and counts come from that same server-authoritative set rather than from one client page. Full mechanical JSON is loaded only for effective rows on the requested page or for the variations in a requested detail/history view.

## Effective/default reference selection

A logical history can contain more than one mechanically distinct Rules Layer concept. Rules Core does not weaken concept/source entity-type validation to combine those concepts.

For global browsing, the representative effective variation is selected from applicable accessible entries in the published global ruleset. If more than one concept in the logical history is published, the most recently authored published Rules Layer decision determines the representative category and variation. Source publication date does not choose between competing published concepts.

For campaign browsing, an explicit published campaign override in the logical history takes precedence over inherited global entries. If several concepts have campaign overrides, the most recently authored published campaign decision determines the representative. Without an applicable campaign override, the campaign inherits the same published-global decision ordering from its pinned baseline.

Changing the representative from a 3.5e `prestigeClass` decision to a 5e `subclass` decision changes the effective category for that scope without rewriting either historical variation or binding one concept to a source of the wrong mechanical type.

If no published selection applies, the reference service chooses a deterministic accessible fallback from authoritative publication/version metadata. The fallback is labeled `unresolved-fallback`; it is a browsing default only and is never persisted as a Rules Layer decision by a read.

This Wiki fallback does not broaden `/api/rules`. Source-only references remain outside the effective consumer catalog until the Rules Layer establishes the corresponding consumer rule.

## Source-access boundary

Reference rows, detail, history, facets, counts, and comparison all apply the same Source Layer access boundary. Anonymous callers see public packages only. Authenticated callers additionally see restricted packages for which their stable Dorks & Dice user ID has a Rules Core grant.

Canonical recognition never grants access. A restricted variation that the caller can not read is not exposed indirectly through a facet, count, history entry, comparison request, or fallback choice.

## Historical Tool mount links

Some downstream persisted state can contain a previously materialized absolute browser href such as `/tools/rules-core/conditions/exhaustion` instead of the structured `browserLink` contract. New responses do not emit that destination, but deployment must preserve those historical links without requiring downstream Tools to understand the split.

The Site should provide a legacy browser-route alias or redirect from `/tools/rules-core/{**toolRelativePath}` to `/tools/rules-wiki/{**toolRelativePath}`, preserving the trailing path and query string. This compatibility route is a Site deployment concern; it must not turn the headless Rules Core service registration back into a navigable UI Tool.
