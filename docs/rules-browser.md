# Resolved rules browser

Rules Core is the canonical browser for the resolved Dorks & Dice ruleset. The resolved Dorks & Dice result is the primary content; source material remains immutable evidence and provenance behind that result.

## Stable browser routes

Rules Core owns a **tool-relative** route. The Dorks & Dice Tool Host owns the mount point. The browser therefore treats these as separate values:

- Tool Host mount: `/tools/rules-core`
- Rules Core route: `/monsters/ancient-red-dragon`
- Site URL assembled by the host/browser: `/tools/rules-core/monsters/ancient-red-dragon`

The module consumes the host-provided `toolBasePath` and `toolRoute` values. Rules Core does not hard-code MVC routes or assume that `/tools/rules-core` is its permanent deployment prefix.

Every catalog item and resolved rule exposes a `browserLink` API contract containing:

- `toolSlug`;
- `toolRelativePath`;
- `routeIdentity`.

`routeIdentity` is the stable Rules Layer concept key. The path is derived from that identity, not mutable display text. Known families use readable paths such as `/monsters/{identity}` and `/spells/{identity}`; unrecognized entity families use `/rules/{conceptKey}`. Other Dorks & Dice tools should consume this contract rather than reconstructing Rules Core URLs.

Collection routes include `/monsters`, `/spells`, `/classes`, `/feats`, `/races`, `/species`, `/items`, and `/conditions`. A direct deep link resolves independently of authoring authority and defaults to the published global rule.

## Global catalog

`GET /api/rules` returns the latest published global ruleset revision and the rules from that revision whose effective source package is accessible to the current request identity. Optional `entityType`, `q`, `limit`, and `offset` parameters filter/page the catalog.

A direct or anonymous request can list rules backed by public source packages. A hosted Dorks & Dice request may additionally list restricted rules for which the stable authenticated user ID has an explicit Rules Core source grant.

The catalog returns stable rule identity, browser link target, effective decision kind, source revision identity, and accessible source provenance. It deliberately does not duplicate the resolved document. `GET /api/rules/{conceptKey}` performs the same independent source-access check before returning the full resolved document and its `browserLink`.

## Campaign catalog and baseline

`GET /api/campaigns/{campaignId}/rules` returns the latest **published** campaign ruleset. It never reflects an unpublished baseline selection or unpublished campaign decision.

Campaign catalog access requires normal campaign membership from the Dorks & Dice Tool Host context. Members may browse the campaign's published rules even when they can not adjudicate that campaign. Nonmembers receive not-found behavior and anonymous requests are unauthorized.

When campaign scope is active, the detail page distinguishes three facts:

1. the campaign publication's pinned global baseline;
2. the campaign-specific decision/override, when present;
3. the resulting effective campaign rule.

`GET /api/campaigns/{campaignId}/rules/{conceptKey}/global-baseline` resolves the exact global baseline pinned by the published campaign revision. It does not substitute the latest global publication. The endpoint independently rechecks access to the baseline source and all recorded consolidation contributions. If the effective campaign override is visible but its underlying global baseline is restricted, the baseline endpoint returns not-found rather than leaking the restricted document.

Source access remains independent from campaign membership and adjudication authority. A campaign may contain a restricted rule that one member can read and another can not. The inaccessible rule is omitted from that member's catalog, and inaccessible baseline/source content is not exposed through provenance views.

## Entity renderers

The browser uses a renderer registry over the common resolved-rule contract. Entity families are not forced through one presentation component.

The first specialized vertical slice is **monsters**. The monster renderer projects common stat-block information from the resolved rule document, including:

- AC, HP, hit dice, initiative, speed, and challenge rating;
- STR, DEX, CON, INT, WIS, and CHA with modifiers;
- saves and skills;
- vulnerabilities, resistances, damage immunities, and condition immunities;
- senses and languages;
- traits and spellcasting;
- actions, bonus actions, reactions, legendary actions, and mythic actions.

Legacy normalized monster bodies retain a compatibility projection for common scalar statistics. The immutable normalized JSON remains available behind a disclosure, but it is no longer the primary monster presentation.

Additional entity renderers can be registered without changing routing, publication, source authorization, or the resolved-rule API contract.

## Adjudication scope control

The workspace exposes a persistent adjudication scope control when the account has mutation authority. Scope choices are derived from authorization rather than from arbitrary campaign membership:

- `Global Rules` appears as an adjudication target only for a Rules Lawyer;
- a campaign scope appears as an adjudication target only when the current account has the campaign-scoped `DM` role;
- in this architecture, that `DM` role is the campaign owner authority;
- campaign Players remain in browse mode for that campaign.

Server endpoints independently authorize the requested scope. UI selection is never an authorization boundary.

`GET /api/workspace/scopes` exposes browse/adjudication capability for the current global and campaign scopes. Semantic comparison requests carry an explicit scope object and are rejected when that scope is not authorized.

## Semantic comparison

Manual adjudication uses a common semantic comparison model rather than asking the user to discover differences in two raw JSON documents.

`POST /api/workspace/comparison` compares two source revisions already bound to the same Rules Layer concept. The request includes the intended adjudication scope plus the concept and exact source revision identities. Both source revisions must remain independently accessible to the caller.

The comparison model reports:

- unchanged values as a count rather than visual noise;
- source/provenance-only metadata differences;
- additions;
- omissions that can be non-destructively retained;
- compatible additive array/object differences;
- contradictions that require a human decision.

The final `canResolveAutomatically` result is delegated to the same conservative compatibility policy used by automatic cross-edition resolution. The explanatory diff does not broaden what Rules Core is allowed to auto-resolve. A scalar replacement, changed same-named entry, incompatible ordering, ambiguous array change, or other contradiction remains manual.

The comparison UI is reusable in both global and campaign adjudication because the comparison semantics do not depend on where the resulting decision will be stored. Scope changes authority and destination, not the meaning of the source difference.

## Publication and authority invariants

The browser/workspace preserves the existing separation of facts:

- source identity and immutable source revisions are not Rules Layer decisions;
- concept binding is not adjudication;
- automatic compatibility does not publish;
- global decisions are append-only and require Rules Lawyer authority;
- campaign decisions are append-only within that campaign and require that campaign's DM/owner authority;
- campaign decisions never mutate the global Rules Layer;
- publication remains explicit at each scope;
- newer publication date never silently grants higher mechanical authority;
- source grants remain independent from global or campaign mutation authority;
- existing manual/patched decisions remain authoritative within their scope until deliberately changed.

The route, renderer, scope, and comparison contracts are intentionally extensible so later entity families and additional adjudication scopes do not require another hosting or persistence redesign.
