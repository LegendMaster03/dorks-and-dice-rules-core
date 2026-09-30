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

A Wiki reference can exist before a `RuleConcept` or published Rules Layer decision exists. Canonical source-only histories receive stable Core-owned `canonical:{canonicalEntityId}` identities. An accessible source occurrence that is intentionally still unresolved by canonical reconciliation receives a deterministic provisional `occurrence:{canonicalSourceOccurrenceId}` identity. Both use `/references/{referenceIdentity}` as their browser route. Creating or reading either identity does not create a Rules Layer concept or decision, and unresolved occurrences are not grouped by loose name matching.

Reference histories group canonical entities connected by authoritative `revision` or `rename` relationships. Those relationships are traversed across the complete history component, so an older or newer member is still the same evolving logical history. `variant` and `reprint` relationships do not grant same-history resolution semantics or collapse into that evolving history. Every variation retains its exact source revision and provenance.

A variation has one canonical mechanical category. Terminology aliases such as Race -> Species and Subrace -> Subspecies normalize into one canonical mechanical category. The immutable source record still retains its original source terminology, but the Wiki contract exposes only the canonical `Category` rather than a second `NativeEntityType` presentation field.

A logical concept history may also span mechanically distinct categories such as Prestige Class and Subclass. Those categories remain distinct; the selected effective variation determines the effective category for the current rules scope. The Rules Layer concept keeps its original type as stable anchor metadata, while the resolved consumer `EntityType` and Wiki `EffectiveCategory` describe the selected variation.

Search, Source, Package, Edition, category, and campaign-override filtering are evaluated over the complete accessible reference set in Rules Core. Facets and counts come from that same server-authoritative set rather than from one client page. Full mechanical JSON is loaded only for the variation projected into each requested catalog row or for the variations in a requested detail/history view. In `categoryMode=any`, that row projection comes from a variation matching the requested historical category; in `categoryMode=effective`, it comes from the effective variation.

## Rules Layer participation and effective/default selection

Direct `rule_concept_source_binding` remains type-coherent. A `prestigeClass` source can not be directly bound to a `subclass` concept merely because the names match.

A mechanically different variation may nevertheless participate in that concept when authoritative canonical identity evidence places it in the same evolving history as a directly bound canonical entity. Current same-history evidence is the transitive `revision`/`rename` component. `variant` and `reprint` alone do not make a different-category source selectable for the concept.

This keeps arbitrary mismatched-type binding invalid while allowing legitimate edition evolution such as:

```text
3.5e  prestigeClass
  -> revision history
5e    subclass
  -> rename/revision history
5.5e  subclass
```

The logical history representative is semantic, not chronological authoring metadata. When legacy data contains more than one RuleConcept bound within the same revision/rename component, Rules Core prefers a RuleConcept bound to a root canonical entity in the directed history graph. Stable concept-key ordering is used only as a deterministic compatibility fallback when a unique rooted binding is not available. Decision creation timestamps do not select which RuleConcept represents the history.

A published global decision on the authoritative history anchor selects one exact source revision from that authoritative concept history. Its selected source determines the effective category. Changing the decision from the 3.5e variation to the 5e variation therefore changes the effective category from `prestigeClass` to `subclass` without rewriting the historical categories or mutating the concept's anchor type.

A campaign is evaluated against its pinned global baseline. An explicit published campaign `select-source` override on the authoritative history anchor may select another revision/rename-history member of the same concept, including one with a different mechanical category. The campaign effective category follows that selected variation. `inherit-global` continues to follow the pinned baseline rather than the newest global publication.

Reference browsing and Rules Layer resolution are separate facts: the Wiki may show every accessible historical variation, while the resolved consumer API exposes only the effective/default variation for the selected scope.

If no published selection applies, the reference service chooses a deterministic accessible fallback from authoritative publication/version metadata. The fallback is labeled `unresolved-fallback`; it is a browsing/default result only and is never persisted as a Rules Layer decision by a read. Its effective category is the category of the selected fallback variation.

This Wiki fallback does not broaden `/api/rules` into a complete source browser. Source-only references with no Rules Layer concept remain outside the effective consumer catalog merely because they can be browsed in Rules Wiki.

## Historical browse projection

`WikiReferenceItemView` distinguishes two variation roles:

- `EffectiveVariation` — the current global/campaign/default variation selected by Rules Layer or fallback semantics;
- `BrowseVariation` — the variation whose source/edition/browser-field metadata should populate the current catalog row.

In Effective mode these are the same variation. In Any-variation mode, the browse variation is selected from the requested historical category and then narrowed by the current Source, Package, and Edition filters where applicable. `EffectiveCategory` remains the effective rules type even when the row is displayed under a different historical family.

This separation is required for type-specific client filters and columns. A Prestige Class result whose effective rule is a 5e Subclass must expose Prestige-Class fields, source, and edition while browsing Prestige Classes; otherwise the UI would filter a historical match using the wrong document.

## Source-access boundary

Reference rows, detail, history, facets, counts, and comparison all apply the same Source Layer access boundary. Anonymous callers see public packages only. Authenticated callers additionally see restricted packages for which their stable Dorks & Dice user ID has a Rules Core grant.

Canonical recognition never grants access. A restricted variation that the caller can not read is not exposed indirectly through a facet, count, history entry, comparison request, or fallback choice. Likewise, unresolved reconciliation does not hide an otherwise accessible imported occurrence merely because `canonical_entity_id` is null.

## Historical Tool mount links

Some downstream persisted state can contain a previously materialized absolute browser href such as `/tools/rules-core/conditions/exhaustion` instead of the structured `browserLink` contract. New responses do not emit that destination, but deployment must preserve those historical links without requiring downstream Tools to understand the split.

The Site should provide a legacy browser-route alias or redirect from `/tools/rules-core/{**toolRelativePath}` to `/tools/rules-wiki/{**toolRelativePath}`, preserving the trailing path and query string. This compatibility route is a Site deployment concern; it must not turn the headless Rules Core service registration back into a navigable UI Tool.
