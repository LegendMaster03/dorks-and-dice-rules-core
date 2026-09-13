# Rules Library and entity presentation

Rules Core keeps immutable source material separate from adjudicated and published rules. The normal browsing experience reflects that boundary explicitly:

- **Rules Library** browses accessible Source Layer publications and source entities.
- **Published Rules** browses the resolved Rules Layer that Dorks & Dice has deliberately published.
- **Rules Lawyer**, cross-version review, campaign authoring, and maintenance remain authority-dependent workspaces rather than alternate source browsers.

## Rules Library hierarchy

The Rules Library is publication-first. Its landing page lists accessible publications, summarizes their available entity categories, and can search source entities across every accessible publication. Opening a publication exposes its categories and entries without requiring Rules Layer normalization or publication.

The four baseline SRDs are hydrated by baseline bootstrap when configured:

- 3e SRD (`SRD3`)
- 3.5e SRD (`SRD35`)
- SRD 5.1 / 5e (`SRD51`)
- SRD 5.2.1 / 5.5e (`SRD52`)

Current-user uploads and Web sources use the existing **Add Source** workflow on the Rules Library landing page. Import, refresh, cleanup, staged progress, adapter selection, canonical publication reconciliation, and source grants remain Source Layer concerns; this browser redesign does not replace those pipelines.

## Stable Source Library routes

The Tool Host owns the mount point and Rules Core owns the route beneath it. Source Library routes use immutable Source Layer IDs to prevent collisions between same-named material from different publications:

- `/library` — library landing page and cross-publication search;
- `/library/monsters` — one category across accessible publications;
- `/library/{sourceEditionId}` — one source publication;
- `/library/{sourceEditionId}/monsters` — one category within a publication;
- `/library/{sourceEditionId}/monsters/{sourceEntityId}` — one immutable source entity;
- `/library/{sourceEditionId}/types/{entityType}` — fallback for an entity type without a named route family.

Search and pagination are URL query state. Back/Forward, reload, bookmarks, and new-tab navigation therefore restore the same browser state instead of depending on transient JavaScript filters.

Source Library API views expose a browser-link contract carrying the `rules-core` tool slug, tool-relative path, and a stable Source Layer route identity. Source entities remain independently authorized by public-package visibility or explicit source grant.

Published Rules use their existing Rules Layer concept identities and routes, such as `/monsters/{conceptIdentity}`. A source entity and a published rule can therefore refer to related content without sharing or conflating route identity.

## Entity-specific presentation

Source Library and Published Rules use the same renderer registry over their respective source/resolved documents. A renderer only changes presentation; it does not rewrite the immutable source record, change adjudication, or infer a new canonical identity.

Specialized renderer families currently cover:

- monsters and stat blocks;
- spells and psionic powers;
- items;
- feats and divine abilities;
- classes, subclasses, class features, prestige classes, and NPC classes;
- races and species;
- conditions;
- skills, domains, house rules, source fragments, generic rules, and unknown entity types through a readable document fallback.

The registry remains open-ended because 5e.tools-compatible sources preserve top-level collection names as entity types. A new adapter or third-party publication can therefore introduce a type that remains browseable before a specialized presentation is added.

Raw immutable JSON and provenance remain available behind secondary disclosures instead of dominating the normal reading view.

## Monster rendering and legacy editions

Monsters use a conventional stat-block hierarchy with compact combat statistics, 5.5-style ability presentation such as `STR 18 (+4)`, defenses and senses, traits, spellcasting, actions, bonus actions, reactions, legendary actions, mythic actions, and edition-specific statistics when available.

Legacy 3e/3.5e normalized bodies can also expose fields such as touch AC, flat-footed AC, Base Attack/Grapple, Fortitude/Reflex/Will, Space/Reach, special attacks and qualities, environment, organization, treasure, advancement, and level adjustment. Those values are presentation projections from the retained source document; the original body remains unchanged.

The legacy monster projection is centralized in the shared renderer. In particular, a 3.5-style line such as `Hit Dice: 10d10+30 (85 hp)` derives the displayed hit points from the Hit Dice field when a dedicated Hit Points value is absent. The renderer does not treat the lowercase `hp` inside that parenthetical as a new field label, avoiding the prior `HP = ")"` failure without duplicating another browser-specific parser.

## Kaiju Fighting presentation

Kaiju Fighting is represented as a structural monster presentation variant rather than a separate unrelated rules family. The renderer activates from explicit kaiju metadata or kaiju-specific fields and can present mechanics such as:

- Chaos Threshold;
- Vulnerable Areas with their own statistics;
- Behaviors and their triggers/effects;
- Finishing Blow and Death Rattle rules;
- XP milestones;
- kaiju-specific actions or features.

This presentation model follows the information hierarchy of Loot Tavern's publicly released Kaiju Fighting Lite rules while using Rules Core's own visual language. It does not copy third-party art, typography, page decoration, or trade dress.

## Rules-layer boundary

Adding or browsing a source never makes that material a table rule. Source entities remain available for browsing, normalization, version review, concept binding, consolidation, and explicit Rules Lawyer decisions. Publication remains explicit, and same-name material from different editions or publications is not silently treated as equivalent or superseded.

Application-owned DOM continues to use the explicit render lifecycle documented in `frontend-render-lifecycle.md`; the routed browsers do not introduce `MutationObserver` as a lifecycle mechanism.
