# Resolved rules browser

Rules Core exposes read-only catalogs for published global and campaign rules so ordinary authenticated users can browse the effective rules without receiving authoring authority.

## Global catalog

`GET /api/rules` returns the latest published global ruleset revision and the rules from that revision whose effective source package is accessible to the current request identity. Optional `entityType`, `q`, and `limit` parameters filter the catalog.

A direct or anonymous request can list rules backed by public source packages. A hosted Dorks & Dice request may additionally list restricted rules for which the stable authenticated user ID has an explicit Rules Core source grant.

The catalog returns rule identity, effective decision kind, source revision identity, and accessible source provenance. It deliberately does not include the resolved source document. The existing `GET /api/rules/{conceptKey}` endpoint performs the same independent source-access check before returning the full resolved document.

## Campaign catalog

`GET /api/campaigns/{campaignId}/rules` returns the latest **published** campaign ruleset. It never reflects an unpublished baseline selection or unpublished DM decision.

Campaign catalog access requires normal campaign membership from the Dorks & Dice Tool Host context. Both Players and DMs may browse the campaign's published rules. Nonmembers receive not-found behavior and anonymous requests are unauthorized.

Source access remains independent from campaign membership. A campaign may contain a restricted rule that one member can read and another can not. The inaccessible rule is omitted from that member's catalog rather than exposing its concept, source, or package metadata. The full rule endpoint follows the same boundary.

Each campaign catalog item identifies whether its effective result is a campaign override. An explicit `inherit-global` decision is not presented as an override because the effective rule still follows the selected global baseline.

## Embedded UI

The hosted module exposes a `Rules Browser` view to authenticated users in Dorks & Dice mode, including users with no global authoring role. Users can:

- browse the published global ruleset;
- switch to any campaign returned by the Tool Host membership API;
- filter by entity type or text;
- inspect rule/source provenance;
- open the full resolved document when their independent source access permits it.

Rules Lawyers and campaign DMs retain their existing authoring navigation. The browser is read-only and does not surface unpublished decision state to ordinary users.

The first browser renderer intentionally displays the resolved JSON document rather than attempting entity-specific presentation. Future spell, class, skill, creature, and other renderers can sit on top of the same resolved catalog/detail API without changing publication or authorization semantics.

## Authorization invariant

The browser preserves the existing separation between Dorks & Dice authority and Rules Core source licensing:

- global published public rules can be read without Rules Lawyer authority;
- campaign rules require campaign membership but not DM authority;
- restricted source-backed rules require an explicit source grant regardless of global or campaign role;
- source grants never confer mutation authority.

Catalog filtering is enforced in the backend. Hiding rows in the browser is never treated as an authorization boundary.
