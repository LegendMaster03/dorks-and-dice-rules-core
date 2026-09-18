# Rules Library workspace

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

Collection routes include `/monsters`, `/spells`, `/classes`, `/subclasses`, `/prestige-classes`, `/feats`, `/races`, `/species`, `/items`, `/conditions`, and `/skills`. A direct deep link resolves independently of authoring authority and defaults to the published global rule. Browser back/forward navigation reparses the current tool-relative route and rerenders through the normal explicit application lifecycle.

The normal Rules Library uses a persistent list/detail workspace: compact type/search/scope controls above a dense concept list on the left, with the selected rule on the right. The page header is intentionally compact: persistent instructional prose is omitted so the list and detail panes receive the available viewport height, while keyboard help remains discoverable on the search control. The list is concept-based rather than source-record-based, so a rule that exists in several editions still appears once. The effective Dorks & Dice rule is the default detail tab; accessible source versions appear as adjacent tabs. Version tabs prefer canonical game-edition labels (for example 3e, 3.5e, 5e, and 5.5e), adding the source code only when more than one accessible version shares the same edition. The effective-rule view also shows whether the rule is the Dorks & Dice ruling, a campaign override, or inherited from Dorks & Dice. Users with the relevant Rules Lawyer or campaign-DM authority receive a direct edit action from that status bar; read-only users see the same state without an edit control. Version comparison labels each side with its edition/source identity and presents primitive differences side by side; structured values remain available in compact code blocks. Import, source acquisition, and source-record inspection do not occupy this workspace. They live under the separate `/sources` Sources view. The index uses incremental loading rather than numbered pages: the first slice is fetched immediately, additional slices load as the user approaches the end of the list, and a visible Load more control remains as an explicit fallback. Rule-family switching is a compact horizontal strip on wide layouts and a select on narrow layouts. The family list is extended from catalog entity-type facets, so newly imported or third-party rule families remain discoverable even when the frontend did not know their type name in advance. Unknown families use the generic `/types/{entityType}` collection route so their library views remain addressable and survive browser navigation. Campaign scope is carried in the browser URL as a `scope=campaign:{id}` query parameter, so refresh, back/forward navigation, and shared rule links do not silently fall back to the global Dorks & Dice ruleset. Changing scope preserves the currently selected concept even when that concept lies beyond the first incrementally loaded list page; if its row is loaded later, the list selection state catches up without replacing the detail view. If the concept does not exist in the target published scope, the browser returns to that family's collection state instead of leaving a dead detail URL. Additional filtering is collapsed behind a Filters button inside the list pane; the first filters are source code and campaign-overrides-only, so filtering does not consume a permanent sidebar. On narrow viewports the workspace becomes a list/detail drill-in rather than stacking the entire list above the selected rule: collection routes land on the list, selecting a rule opens the detail pane, and Back to list preserves the active family and filters.

The catalog also returns compact browser-index fields derived from the published effective mechanical document without returning the document itself. The list chooses columns by entity family, for example monster Type/CR, spell Level/School, class Hit Die, race/species Ability/Size, and skill Ability. Subclasses use the published `parent-class` relationship for their Class column. The response includes the filtered total count so the search strip can show the visible range without loading the complete catalog.

## Global catalog

`GET /api/rules` returns the latest published global ruleset revision and the rules from that revision whose effective source package is accessible to the current request identity. Optional `entityType`, `q`, `source`, `limit`, and `offset` parameters filter/page the catalog. Entity-type and source facets are populated on the first page (`offset=0`); incremental pages omit the repeated facet payload.

A direct or anonymous request can list rules backed by public source packages. A hosted Dorks & Dice request may additionally list restricted rules for which the stable authenticated user ID has an explicit Rules Core source grant.

The catalog returns stable rule identity, browser link target, effective decision kind, source revision identity, and accessible source provenance. It deliberately does not duplicate the resolved document. `GET /api/rules/{conceptKey}` performs the same independent source-access check before returning the full resolved document and its `browserLink`.

`GET /api/rules/{conceptKey}/versions` returns the accessible bound source versions for that same stable concept. Equivalent source representations of one canonical version are collapsed to one version entry rather than becoming duplicate browser tabs. Each returned version includes its latest immutable mechanical document and source revision identity; normal source grants still determine which versions the caller may see.

## Campaign catalog and baseline

Campaign lifecycle is owned outside Rules Core. Rules Core does not create campaigns, accept join requests, issue invitations, or assign DM/Player membership. The Dorks & Dice Tool Host supplies the authenticated account's campaign memberships and roles through the host API; Rules Core consumes that context for browsing and authorization. If the host supplies no memberships, campaign scopes are simply absent and global browsing continues independently.

`GET /api/campaigns/{campaignId}/rules` returns the latest **published** campaign ruleset. It never reflects an unpublished baseline selection or unpublished campaign decision. It accepts the same `entityType`, `q`, `source`, `limit`, and `offset` catalog controls as the global endpoint, plus `overridesOnly=true` for a campaign-override-only view. Facets are returned on the first page and omitted from subsequent incremental pages.

Campaign catalog access requires normal campaign membership from the Dorks & Dice Tool Host context. Members may browse the campaign's published rules even when they can not adjudicate that campaign. Nonmembers receive not-found behavior and anonymous requests are unauthorized.

When campaign scope is active, the detail page distinguishes three facts:

1. the campaign publication's pinned global baseline;
2. the campaign-specific decision/override, when present;
3. the resulting effective campaign rule.

`GET /api/campaigns/{campaignId}/rules/{conceptKey}/global-baseline` resolves the exact global baseline pinned by the published campaign revision. It does not substitute the latest global publication. The endpoint independently rechecks access to the baseline source and all recorded consolidation contributions. If the effective campaign override is visible but its underlying global baseline is restricted, the baseline endpoint returns not-found rather than leaking the restricted document.

Source access remains independent from campaign membership and adjudication authority. A campaign may contain a restricted rule that one member can read and another can not. The inaccessible rule is omitted from that member's catalog, and inaccessible baseline/source content is not exposed through provenance views.

## Entity renderers

The browser uses a renderer registry over the common resolved-rule contract. Entity families are not forced through one presentation component.

The first specialized vertical slice is **monsters**. The renderer adopts the 5.5e monster-stat-block information hierarchy as a presentation grammar while continuing to render the effective mechanical document supplied by Rules Core. It does not reinterpret source editions or perform cross-edition normalization in JavaScript.

The monster presentation includes, when available:

- name, size, creature type, descriptive tags, and alignment;
- Armor Class, Hit Points, Speed, and Initiative;
- STR, DEX, CON, INT, WIS, and CHA in a compact grid showing score, modifier, and save for every ability;
- skills, senses, languages, Challenge Rating, XP, proficiency bonus, vulnerabilities, resistances, immunities, condition immunities, and gear;
- traits and spellcasting;
- actions and bonus actions;
- reactions, legendary actions, mythic actions, lair actions, and regional effects.

Fields that do not fit those headings are not dropped. Unknown rule-bearing fields are shown as additional mechanics. Source-specific `_rulesCore.pcgen` mechanics are exposed separately, and Dorks & Dice extension mechanics remain available without being coerced into 5.5e semantics. A `legendaryGroup` or other unfamiliar structural reference is therefore still visible even when the renderer does not yet have a specialized component for it.

Renderer-tag markup used by native 5e.tools content is reduced to readable display text for common attacks, hits, DCs, recharge notation, dice/damage, and entity references. That formatting does not change the underlying mechanical document.

Published monster pages put the playable stat block first. Rules Layer scope, publication revision, decision information, selected source, package, notes, and consolidation contributions move into a secondary **Rule context and provenance** disclosure. Campaign overlay and pinned-baseline behavior remain unchanged.

The separate Sources workspace uses the same monster presentation primitives against a source entity's Rules Core mechanical document while separately exposing the exact source-native record. There is no legacy prose parser, skill conversion table, or Dexterity-to-initiative fallback in the frontend.

Reusable presentation primitives introduced by this slice include the entity header, compact statistic, ability-score grid, labeled details, named rule entry, rules-text section, tags, additional-mechanics section, and expandable context/provenance disclosure. Later entity renderers can reuse those primitives without being forced into the monster layout.

## Explicit render lifecycle

Application-owned DOM continues to use the explicit Rules Core render lifecycle. The Rules Library and Sources workspace call the existing fragment-presentation hook after their own in-place result/detail updates so shell presentation is reapplied deliberately.

`MutationObserver` is not used to enhance application-owned monster, catalog, or source-detail DOM. Observer-based behavior remains reserved for genuine external boundaries.

## Adjudication scope control

The workspace exposes a persistent adjudication scope control when the account has mutation authority. Scope choices are derived from authorization rather than from arbitrary campaign membership:

- `Dorks & Dice` is the user-facing label for the global adjudication target and appears only for a Rules Lawyer;
- a campaign scope appears as an adjudication target only when the current account has the campaign-scoped `DM` role;
- in this architecture, that `DM` role is the campaign owner authority;
- campaign Players remain in browse mode for that campaign.

A user can hold these roles simultaneously across different scopes. For example, one account may be a global Rules Lawyer, DM of multiple campaigns, and Player in another campaign. Campaign authority is therefore always evaluated against the specific campaign ID and never inferred from a user-wide `DM` state.

If the host supplies no campaign memberships, Rules Core exposes no campaign browse or adjudication scopes and does not synthesize campaign authority. Global access continues independently.

Server endpoints independently authorize the requested scope. UI selection is never an authorization boundary.

`GET /api/workspace/scopes` exposes browse/adjudication capability for the current global and campaign scopes. Semantic comparison requests carry an explicit scope object and are rejected when that scope is not authorized.

## Semantic comparison

Manual adjudication uses a common semantic comparison model rather than asking the user to discover differences in two raw JSON documents.

`POST /api/rules/comparison` is the read-only comparison endpoint used by the Rules Library. It compares two accessible source revisions already bound to the same Rules Layer concept and does not require adjudication authority. Anonymous callers may compare public source versions; authenticated callers may additionally compare restricted versions for which they independently hold source access.

`POST /api/workspace/comparison` remains the adjudication endpoint. It carries an explicit global or campaign scope and requires authority to mutate that scope before returning the same semantic comparison model.

The comparison model reports:

- unchanged values as a count rather than visual noise;
- source/provenance-only metadata differences;
- additions;
- omissions that can be non-destructively retained;
- compatible additive array/object differences;
- contradictions that require a human decision.

The final `canResolveAutomatically` result is delegated to the same conservative compatibility policy used by automatic cross-edition resolution. The explanatory diff does not broaden what Rules Core is allowed to auto-resolve. A scalar replacement, changed same-named entry, incompatible ordering, ambiguous array change, or other contradiction remains manual.

The Rules Library exposes comparison as another detail tab whenever at least two accessible source versions exist. Viewing differences is read-only. When the current identity is a Rules Lawyer in global scope or the DM of the selected campaign, the comparison pane also offers a direct transition into the corresponding ruling editor. Scope changes authority and decision destination, not the meaning of the source difference.

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