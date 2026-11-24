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
| POST | `/experimental/combat/action-budget` | Opt-in resource-accounting comparison, not an approved action-economy rule |
| POST | `/experimental/combat/attack-check` | Native roll + condition + Normal/Touch/Flat-Footed AC preview; no damage or encounter authorization |
| POST | `/experimental/combat/damage-preview` | Percentage mitigation, applicable DR, and optional noncommitting HP proposal; fractional outputs remain unresolved |

The private mechanics surface additionally maps `GET /catalog/rules` and `POST /catalog/resolve`. Newer or incomplete mechanics must be verified against code and tests rather than inferred from this navigation table.

## Experimental combat contract (no active gameplay rules)

The `/experimental/combat/action-budget`, `/experimental/combat/attack-check`, and `/experimental/combat/damage-preview` operations use the existing delegated/private authenticated mechanics boundaries, ruleset IDs and catalog fingerprints. They return deterministic calculations and **do not persist combat state, prove attacker ownership, validate whether an ability may be used, or apply damage**. `/experimental/combat/action-budget` requires explicit `independent` or `shared-end-turn` selection for each encounter; its state is caller-owned and neither policy is an approved default. The attack check takes caller-supplied attack bonus, actual die faces, native defense profile and trusted defender contributions; it composes applicable typed conditions. Missing predicates fail closed. Natural critical candidates require separate adjudication. The damage preview accepts independently verified incoming damage, signed resistance/vulnerability percentage and already-applicable non-bypassed DR entries. It returns `rounding-unresolved` where fractional damage would require an unapproved rounding rule, and never mutates HP or applies death/defeat logic. See the [first combat-engine slice](rewrite/combat-kernel-first-slice-20261124.md) and [action-budget protocol](rewrite/combat-action-budget-playtest.md).

## Related design

- [Equipment qualification contract](rewrite/equipment-qualification-contract.md)
- [Current native rules design](rewrite/native-rules-design-specification.md)
- [Authenticated consumer catalog](native-authenticated-consumer-catalog.md)

Do not reinstate retired tool-as-Competency or legacy source-translation semantics when implementing these endpoints. Preserve independent equipment qualifications, native skills, and native Competencies, with their respective approved progression rules.
