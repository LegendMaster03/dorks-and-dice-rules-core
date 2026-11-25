# Multi-system consumer compatibility audit

Static source inspection at the following consumer revisions. This is not an end-to-end acceptance test or a claim that the foreign systems are supported by existing consumer interfaces.

| Consumer | Revision | Current contract | Additional work |
| --- | --- | --- | --- |
| Character Sheet | `c3a5e27d95e70307901e85ee46a3faa59f069899` | Client validates ruleset identity and fingerprint; gateway still uses singleton native mechanics metadata and options endpoints. | Introduce explicit selected-ruleset routing, capability-aware UI and separate system-specific character models. |
| Block Initiative | `002954bdc97391fcc788c819e78d8de1d64608df` | Catalog reads and persisted monster IDs explicitly require first-party-001. | Parameterize selection and validate system-specific monster and condition schemas without reinterpreting existing stored IDs. |
| Rules Wiki | `0115e131d2cb4c5a9306b5cb8e6de8d3ffc1de27` | Private dispatcher accepts ruleset ID; browser explicitly finds first-party-001, routes only that ruleset. | Add selection and source-aware rendering for additional catalogs. |

## Inspected source

- Character Sheet: `src/CharacterSheet.Web/Client/src/rules-core-native-api.ts`, `src/CharacterSheet.Web/DelegatedRulesCoreGateway.cs`.
- Block Initiative: `src/BlockInitiative.Web/Client/src/integrations/rules-core/client.ts`, `monsters.ts`, `conditions.ts`.
- Rules Wiki: `src/RulesWiki.Web/RulesCorePrivateClient.cs`, `RulesCoreUiOperationDispatcher.cs`, `wwwroot/rules-reference-api.js`, `wwwroot/rules-browser-routing.js`.

The Wiki selects by ID, not by catalog ordering, so additional published catalogs do not change its default selection. Existing first-party endpoints remain unchanged. No consumer repositories were edited. Foreign package activation remains subject to independent distribution approval and the existing authorization boundary. Run real consumer integration tests before claiming compatibility.
