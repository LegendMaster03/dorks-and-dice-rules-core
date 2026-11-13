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
| POST | `/conditions/roll-mode` | Conditional roll-mode effects |
| GET | `/creatures/{entityId}` | Supported native creature facts |
| POST | `/resources/resolve` | Resource expenditure or recovery |
| POST | `/experimental/combat/action-budget` | Explicit, stateless action-resource comparison (**not an approved combat rule**); see [playtest guide](rewrite/combat-action-budget-playtest.md) |

The private mechanics surface additionally maps `GET /catalog/rules` and `POST /catalog/resolve`. Newer or incomplete mechanics must be verified against code and tests rather than inferred from this navigation table.

**Experimental resource evaluator:** The `/experimental/combat/action-budget` operation is exposed behind the existing authenticated mechanics boundary for controlled playtesting. An encounter must explicitly choose `independent` or `shared-end-turn`; the API does not choose a default, verify activity eligibility, store combatant state, or activate either policy for existing games. It rejects mismatched content fingerprints. Do not infer production readiness or balance from its presence.

## Related design

- [Equipment qualification contract](rewrite/equipment-qualification-contract.md)
- [Current native rules design](rewrite/native-rules-design-specification.md)
- [Authenticated consumer catalog](native-authenticated-consumer-catalog.md)

Do not reinstate retired tool-as-Competency or legacy source-translation semantics when implementing these endpoints. Preserve independent equipment qualifications, native skills, and native Competencies, with their respective approved progression rules.
