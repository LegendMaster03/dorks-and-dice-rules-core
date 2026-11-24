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
| GET | `/origins/methods` | Authored method identities, optional creature-type defaults, executable transport scope |
| GET | `/origins/presets` | Species/Ancestry hierarchy, selectability and authored acquisition metadata |
| GET | `/origins/choice-catalog` | Fingerprint-pinned native Skill, Wizard cantrip or Origin Feat membership discovery (not ownership or feat eligibility) |
| POST | `/origins/life-experiences/preview` | Source-local Upbringing/Background selections and nonauthoritative grant/item candidates |
| POST | `/origins/life-experiences/verified-plan` | Independent source/choice/item authority, all-or-nothing earned projection proposal |
| POST | `/origins/life-experiences/scoped-check/resolve` | Apply authored scoped Upbringing contribution to a verified ordinary Skill check |
| GET | `/origins/construct/components/{definitionId}/configuration` | Native material slots, variant, operating-mode and compatible option identities |
| POST | `/origins/preview` | Native initial Origin discovery and selection preview |
| POST | `/origins/construct/preview` | Recipe quotation, component configuration and shared Origin construction preview |
| POST | `/origins/evaluate-approved-mixed` | Public delegated, campaign-DM-scoped evaluation |
| POST | `/origins/innate-spells/ledger/initialize-preview` | Public delegated, source-matched direct Origin ledger bootstrap |
| POST | `/conditions/roll-mode` | Conditional roll-mode effects |
| GET | `/creatures/{entityId}` | Supported native creature facts |
| POST | `/resources/resolve` | Resource expenditure or recovery |

The private mechanics surface additionally maps `GET /catalog/rules` and `POST /catalog/resolve`. Newer or incomplete mechanics must be verified against code and tests rather than inferred from this navigation table.

## Character Origins preview contracts

Life Experiences preview takes `rulesetId`, `fingerprint`, and a `proposal` with one `upbringingId`, one `backgroundId`, optional `selections` and `startingItemIds`. Keys identify the active native source/benefit (e.g. `background.spy/spy-field-skills`), nested option (e.g. `background.guild-merchant/guild-merchant-professional-choice/additional-trade-language`), equipment choice (`sourceId/equipment/choiceId`), or authored starting-item index (`sourceId/item/0`, pinned to the fingerprint). The inherited cultural parent chain is resolved once; default Upbringing is never automatically granted. The shared equipment resolver validates the exact selected native option path and its qualification membership. Skills, independent competency ranks/proficiencies, scoped effects, language references and manual features retain their source and benefit provenance. Candidate outputs are not earned grants. The verified plan additionally requires Character Sheet's independent source-occurrence/context/inventory provider and exact character revision. No projection is earned unless every required choice and ordinary item is verified; inventory IDs are not fabricated from display text. Item/type constraints and selected branch are checked independently of proficiency. Neither endpoint commits inventory or character state. Within the selected Life Experiences pair, duplicate Skill proficiency requires an explicit different native Skill choice (not an added rank); source occurrence and original target remain recorded. Within the selected pair, duplicate specific-tool proficiencies can likewise select a distinct eligible individual native tool type not already covered; the original source occurrence and original/replacement qualification are retained. Weapon overlap remains binary. Whole-character duplicate resolution with Class and other independently earned sources remains separate. Verified competency effects also provide distinct stable grant occurrences for the common competency reducer. Fixed ordinary items may be resolved directly by an independent inventory provider without forcing an unnecessary player choice. Vehicle operator mechanics and unreconciled learned spell execution are explicitly unavailable.

The public delegated scoped-check route accepts only character ID, current Origin revision, fingerprint, and check-event ID. Its independent host provider must resolve the actual selected Upbringing, exact inherited source occurrences, verified task/ability, ordinary total and ordinary proficiency contribution. The shared scoped contribution resolver reads the same native Upbringing chain. Stonecunning and Artificer's Lore replace the ordinary contribution with twice normal PB even on an untrained qualifying History check; only Stonecunning confers check-local proficiency. Wood Elf and Drow Survival bonuses add +2 to the existing qualified check. A mere location is not an activity, and competing proficiency replacements fail closed. No permanent training is changed.

The three read-only Origin discovery routes require the current fingerprint. Method discovery optionally accepts `creatureTypeId`; type defaults are not exclusive permissions, and reference-only methods are not executable creation paths. Preset discovery preserves nonselectable Species/Ancestry parent nodes, authored acquisition restrictions and independent default Upbringing references. Construct configuration discovery reuses the assembly evaluator's native compatibility and recipe/mode validation to return bounded choice metadata, not raw private definitions or permission to install an option. Submit the actual selected assembly to the existing preview to calculate costs and operating state.

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
| POST | `/origins/biological/verified-preview` | Resolve exact saved parent revisions and validate descendant creation under the native allocation gate |
| POST | `/origins/reconstruction/plan` | Plan a verified permanent replacement against the latest stored Origin |
| POST | `/origins/construct/operating/plan` | Plan a verified component activation or authored mode change against an installed Construct |
| POST | `/characters/effective-state-baseline` | Project the supported baseline character state |
| POST | `/characters/effective-state` | Read verified effective character and manifestation state |
| POST | `/characters/manifestations/plan` | Plan a revision-checked manifestation transition |
| POST | `/characters/supernatural/ownership/plan` | Plan supernatural acquisition, advancement, or expression |
| POST | `/characters/supernatural/overlaps/plan` | Plan an exact, source-authorized overlap decision |

Verified biological requests supply a target character ID, fingerprint, and proposal with revision-pinned manifested-profile references (not embedded parent data). The host biological provider loads the current parent records after checking access. The shared construction engine validates inherited/expressed instances and enforces the first-party initial allocation gate. Reconstruction requests supply the target character ID, current Origin revision, completion ID, fingerprint and target proposal. The host reconstruction provider independently loads the existing profile and verifies completion/decision authority; the existing permanent reconstruction engine returns a replacement proposal, never a committed write.

The Construct operating plan accepts character ID, Origin and operating revisions, ruleset identity, exact fingerprint and a complete target operating preset (instance ID, installed definition ID, enabled flag and optional authored mode per component). An independent host provider must reload the current authorized installation and its existing operating session, verify actor access, the two revisions, current power/fuel availability and the saved operating preset. A rated source is not proof of live fuel. The planner reuses the session's exact-instance transition evaluator and canonical movement projector. It requires a valid current operating baseline and a valid target preserving continuous-power loads; contextual source operations, outage recovery, resource-changing transactions and post-creation installation authorization remain separate. Returned active/inactive grants and movement are read-only proposals, not permanent Origin changes. The owning application must recheck the same revisions and resource evidence before committing preferences and consequences. No provider, persistence, or new fuel rules are supplied by Rules Core.

Site delegation alone does not establish character ownership, source entitlement, event evidence, or current storage revision. The independently verified provider interfaces in the host must be supplied by an authoritative application integration. The routes fail closed without that authority; successful plans do not commit state. Existing synthetic fixtures demonstrate the contracts, not production authorization or approved supernatural content. No first-party lycanthropy or vampirism rules are introduced by this infrastructure integration.

## Related design

- [Equipment qualification contract](rewrite/equipment-qualification-contract.md)
- [Current native rules design](rewrite/native-rules-design-specification.md)
- [Authenticated consumer catalog](native-authenticated-consumer-catalog.md)

Do not reinstate retired tool-as-Competency or legacy source-translation semantics when implementing these endpoints. Preserve independent equipment qualifications, native skills, and native Competencies, with their respective approved progression rules.


### Native open Origin choice eligibility

`GET /origins/choice-catalog?fingerprint=...&catalogId=...` uses the same closed predicates as first-party selection. Supported catalogs are `any-native-skill`, `spell:wizard`, and `feat:origin`. A native Wizard cantrip requires a canonical `spell.*` record with `kind: "spell"`, explicit `level: 0`, and `spellListIds` containing `"wizard"`. An Origin Feat membership candidate requires canonical `feat.*`, `kind: "feat"`, and `featCategory: "origin"`. Explicit unselectable definitions are excluded. Feat membership never proves character-specific prerequisites or repeatability. Empty eligible catalogs report `catalog-unavailable`, rather than a fabricated default selection. These are integration contracts for future reviewed native definitions, not publication or approval of working spell/feat content.

A direct High Elf's source-bound Arcane Cantrip projects the selected native Wizard cantrip as at-will, with the same inherited spellcasting ability, and no free-use pool or additional spell slot. The trait's level is the character unlock level, not the spell level. Rock Gnome Upbringing's independently learned Prestidigitation resolves a learned-cantrip entitlement only when the named canonical native cantrip exists; its separate ability selection is retained. Full spell execution remains a spell-domain responsibility.

The delegated `POST /origins/direct/verified-preview` accepts character ID, draft revision, exact ruleset fingerprint and the ordinary direct biological proposal. Its independent provider must authorize the draft and the exact candidate/feat choice against all character-specific prerequisite and repetition rules. The native membership predicate still applies. This route returns a preview and allocation assessment only, not a committed feat or character.

`POST /origins/innate-spells/cantrip-replacement/plan` accepts character ID, Origin and ledger revisions, fingerprint, completed Long Rest event ID, existing grant ID and replacement canonical spell ID. The host provider independently verifies the unconsumed Long Rest and loads the saved direct proposal, profile, character and ledger. The planner rebuilds the saved source, changes exactly the existing cantrip choice, verifies the rest of the profile remains identical, and applies the common Long Rest ledger recovery once, rebinding the resulting ledger fingerprint to the new spell course. The character-owning application must atomically commit the proof-free `updatedSourceProposal` (not the evaluated proposal containing minted eligibility evidence), replacement profile, recovered ledger and consumed rest event after checking the same revisions. No endpoint performs that commit. Mixed/inherited or transformed configurations and profiles with additional changes are not supported by this direct replacement path.
