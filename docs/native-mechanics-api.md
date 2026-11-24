# Native mechanics API — current implementation

The native mechanics API is authenticated and versioned. It evaluates the current validated, immutable ruleset snapshot; it does not expose the legacy source-normalized Character projection or old `/api/rules` contracts. The API and DTO source of truth is `src/RulesCore.Host/NativeMechanicsEndpoints.cs` and `src/RulesCore.Mechanics/`.

## Access and consistency

- Ordinary Tools use the `PublicOnly` `/api/mechanics/v1` route family with Site-issued authenticated delegation. The private first-party tunnel uses `PrivateOnly` `/api/private/mechanics/v1` with a separate authentication boundary.
- `GET /ruleset` provides the current ruleset ID, fingerprint, and supported capabilities. Requests that carry a fingerprint must target the same snapshot; use the API's mismatch response to refresh rather than combining data from different snapshots.
- Native mechanics routes evaluate submitted state; they do not own persisted character state, infer missing choices, grant source access, or imply that every narrative rule is executable.
- Private-only `GET /catalog/rules` and `POST /catalog/resolve` support native identity search and batch resolution. The ordinary mechanics surface includes a narrower `GET /catalog/options` for permitted option discovery, not an unrestricted source browser.

## Implemented route families

The following paths are relative to the appropriate `/api/**/mechanics/v1` base:

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/ruleset` | Ruleset identity and mechanics capabilities |
| GET | `/catalog/options` | Allowed option lookup |
| GET | `/harvesting` | Native harvesting tables |
| POST | `/harvesting/resolve`, `/harvesting/outcome` | Harvesting assessment and outcome resolution |
| POST | `/crafting/manufacturing/resolve` | Manufacturing calculation |
| POST | `/characters/starting-equipment` | Resolve initial inventory grants into item selections |
| POST | `/characters/equipment-proficiencies`, `/characters/equipment-proficiencies/coverage` | Equipment qualification grants and coverage |
| POST | `/characters/equipment-training`, `/characters/equipment-training/coverage` | Equipment training and Weapon Mastery eligibility |
| POST | `/advancements/eligibility` | Native acquisition eligibility |
| POST | `/characters/project` | Native character mechanics projection |
| POST | `/origins/preview` | Native initial Origin discovery and selection preview |
| POST | `/origins/construct/preview` | Recipe quotation, component configuration and shared Origin construction preview |
| POST | `/origins/evaluate-approved-mixed` | Public delegated, campaign-DM-scoped evaluation |
| POST | `/origins/innate-spells/ledger/initialize-preview` | Public delegated, source-matched direct Origin ledger bootstrap |
| POST | `/conditions/roll-mode` | Conditional roll-mode effects |
| GET | `/creatures/{entityId}` | Supported native creature facts |
| POST | `/resources/resolve` | Resource expenditure or recovery |

The private mechanics surface additionally maps `GET /catalog/rules` and `POST /catalog/resolve`. Newer or incomplete mechanics must be verified against code and tests rather than inferred from this navigation table.

## Character Origins preview contracts

- Submit `rulesetId`, `fingerprint` and `proposal` to the initial Origin preview. The evaluated proposal returns native candidate IDs and choice requirements; resubmit the selected manifestations, not invented grants. Initial Origin previews continue to report changed fingerprints rather than requiring an exact match; refresh before committing anything.
- Both direct and DM-scoped mixed previews return source-verified Tremorsense, Darkvision, innate spell, HP progression and passive entitlement views. Supply optional `characterLevel` for level-dependent previews; it is not proof of character advancement. Biological allocation is a read-only assessment under the current native trial pricing revision and allowance; it does not authorize traits or change the existing creation gate.
- Construct previews require the exact fingerprint and an additional `assembly` object matching `NativeConstructAssemblyRequest`. They return the recipe-derived ingredient bill, cost, component operating state, power networks and universal Origin result. Submit a draft first, then select its discovered native candidates. The starting allowance must match the authored native rule. The operating session is not serialized; movement is a read-only projection of the verified assembly. Non-initial assembly requests are modification quotations, not authorization to modify a stored character.
- Direct Wood Elf presets use the explicitly authored 35-foot Ancestry override, retaining the parent 30-foot offer as restricted source evidence rather than manifesting both values. Mixed donor reconciliation is unchanged. Re-evaluate older stored selections against the updated catalog before committing them.
- Animal Speech spell previews retain the verified configured habitat as `animalHabitat`; the resource ledger does not authorize arbitrary animal targeting.
- Private catalog resolution retains its existing source boundary. Ordinary option lookup returns identities and names only, including native Origin traits, Construct components, power sources, materials, designs and enhancements, Upbringings and Backgrounds.
- Persisted parent state, character-level authorization, permanent reconstruction, transformation execution, inventory/fuel transactions and migration require authoritative application state. None can be authorized by replaying a browser-authored profile through these stateless previews.

## Verified character state and supernatural planning

These endpoints are on the ordinary Character Sheet-delegated public mechanics surface. They are the existing Pillar 4 infrastructure integrated with the consolidated Origin engine; they are not alternate construction or transformation engines.

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/origins/underlying/verify` | Verify an existing underlying Origin through independent host authority |
| POST | `/characters/effective-state-baseline` | Project the supported baseline character state |
| POST | `/characters/effective-state` | Read verified effective character and manifestation state |
| POST | `/characters/manifestations/plan` | Plan a revision-checked manifestation transition |
| POST | `/characters/supernatural/ownership/plan` | Plan supernatural acquisition, advancement, or expression |
| POST | `/characters/supernatural/overlaps/plan` | Plan an exact, source-authorized overlap decision |

Site delegation alone does not establish character ownership, source entitlement, event evidence, or current storage revision. The independently verified provider interfaces in the host must be supplied by an authoritative application integration. The routes fail closed without that authority; successful plans do not commit state. Existing synthetic fixtures demonstrate the contracts, not production authorization or approved supernatural content. No first-party lycanthropy or vampirism rules are introduced by this infrastructure integration.

## Related design

- [Equipment qualification contract](rewrite/equipment-qualification-contract.md)
- [Current native rules design](rewrite/native-rules-design-specification.md)
- [Authenticated consumer catalog](native-authenticated-consumer-catalog.md)

Do not reinstate retired tool-as-Competency or legacy source-translation semantics when implementing these endpoints. Preserve independent equipment qualifications, native skills, and native Competencies, with their respective approved progression rules.
