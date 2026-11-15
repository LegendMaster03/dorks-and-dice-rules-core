# Character State Inheritance — Increment 3: manifestations and transitions

**Scope:** Shared Rules Core mechanics contracts and a synthetic werewolf vertical slice. This is a host-verified transition *planner* and read-only effective-state evaluator, **not** a Character Sheet mutation endpoint, persistence migration, production lycanthropy implementation, or playable first-party form.

## Ownership and lifecycle

- `NativeCharacterState` continues to own the primary mechanics, persistent nested alternate sheets and acquired influences. No existing source/progression/grant ownership was moved.
- `NativeManifestationLedger` represents a separately persisted character-scoped journal with a revision, processed event identities, activation instances, resource owners and authenticated overlap decisions.
- A `NativeManifestationInstance` explicitly records `CharacterId` as well as its owned persistent sheet, exact transformation source/occurrence, optional acquired influence, activation receipt and event identity. The instance's character owner must match both the authoritative `NativeCharacterState.CharacterId` and its enclosing `NativeManifestationLedger.CharacterId`; structurally invalid or foreign-owned instances are rejected before transition authorization. It stores only activation-time captures, ending conditions, resource policies, lifecycle and effective-contribution state, **not a cloned sheet**.
- `Active` versus `Ended` is independent from `Contributing` versus `Suppressed`. Suppression does not erase an activation or persistent advancement. Ending a manifestation does not destroy its owner sheet, erase earned progression, or automatically resume a previously ended instance.
- `NativeCharacterManifestationResolver.Resolve` chooses an explicitly contributing instance and resolves its current sheet through both existing inheritance and progressive influence resolvers. The result retains sheet-owned and influence-owned grant provenance independently. Every resolution uses current authoritative primary inputs, so Class increases, reconstructed Origin inputs and influence expression changes are not undone on reversion.

A simultaneous contributing-form conflict has no default first/last-wins rule. One source- and scope-verified interaction decision can explicitly select the effective instance with `Suppress` or `Replace`; `Coexist` remains an unresolved mechanical combination without a supported composition rule. `Terminate` requires actual authorized `End` steps instead of implicitly rewriting lifecycle. Existing influence-versus-sheet characteristic conflicts are still resolved by the Increment 2 evaluator, which continues to fail closed.

## Planning and transaction boundary

`NativeCharacterManifestationTransitionPlanner.Plan` accepts a `NativeManifestationTransitionRequest` with character identity, **expected revision**, **stable event ID**, and ordered steps. Steps can `Activate`, `End`, `Suppress`, and `Restore`. For example, an atomic Wolf-to-Hybrid switch is an `End(Wolf)` and `Activate(Hybrid)` pair under one event ID. Reversion ends the current manifestation, then ordinary resolution uses the latest primary input; no activation snapshot is restored.

The planner validates the current journal and the existing sheet/influence graph, rejects stale revisions, event replays, invalid form references, illegal lifecycle changes and invalid source/evidence shapes, and requires the host to authorize every transition. New manifestation resources and interactions are independently authenticated. It returns a **detached proposed ledger** at `expectedRevision + 1`, without changing the supplied journal or character. An unresolved simultaneous manifestation plan returns no proposal. No permissive authority implementation exists in the library.

The planner now **reevaluates the proposed effective character before returning a commit candidate**. It resolves the proposed target against the latest primary mechanics, verified persistent influences, and explicit resource policies. It refuses an unresolved target-form/influence conflict or resource policy by returning no proposed state. A successful `NativeManifestationTransitionPlan` carries the detached `Proposed` ledger and its `ProposedEffective` read-only preview. The preview does not authenticate the uncommitted next revision as already persisted: the current ledger is verified first, followed by the newly introduced transition steps, resources, and interactions. As before, `partial` on a valid alternate form identifies unimplemented Class/HP/Origin use policies, not a mechanically playable character.

Each `End` step must declare an `EndingConditionId`. A condition listed in the activation's own `EndingConditions` still requires host verification of the event, rule occurrence, evidence, scope, and trigger via `AuthorizesTransition`. An unlisted ending may instead come from a later source-authored event or properly scoped DM ruling, but must pass the additional mandatory `AuthorizesUnlistedEnding` host verifier. An unknown label is never implicit permission to end a form; nor is an original list of ending conditions a structural irreversibility rule. `Terminate` interaction metadata must correspond to an explicit `End` step in the same proposed transaction. The interaction decision alone is not a write, a reversion policy, or a universal precedence rule.

The production Character Sheet host must implement `INativeCharacterManifestationAuthority` and verify exact form ownership/unlocks, occurrence, rules or scoped DM decisions, evidence, identity and current revision against its own records. The host must atomically compare-and-swap the character revision and commit all proposed steps, instance/resource records, processed-event identities and decisions, or commit none. **A successful planning result is not a committed transaction**; event replay prevention at the database boundary and durable history retention remain host responsibilities.

## Resource policies

`NativeManifestationResourceState` identifies exactly one authoritative resource owner: `Primary`, `Sheet`, `Influence` or `Manifestation`. Each pool has a stable ID, source occurrence, proof receipt and persisted current/recorded maximum. A form must explicitly declare each accessible pool and its authenticated `NativeManifestationResourcePolicy`:

- `Inherit` reads the latest pool owned elsewhere without copying its balance.
- `Separate` exposes a separately owned pool.
- `CaptureMaximum` evaluates an authenticated numeric activation capture, which stays fixed for that instance.
- `AdjustMaximum` calculates the effective maximum relative to the latest authoritative recorded maximum.

These policies never refill, clamp or spend the current quantity. A capacity reduction may leave current quantity above the displayed effective maximum; no unapproved transfer rule is inferred. New instance-owned pools may be proposed only as explicit, source-authorized activation resources. No first-party current HP transfer, damage, healing or recovery behavior is implemented. Character-level resources remain authoritative mutations outside this read-only evaluator.

## Projection limitation

The opt-in `NativeCharacterManifestationProjector.Project` runs an authenticated effective-character request through the existing `NativeCharacterProjector` numerical kernel. Its `NativeManifestationProjectedCharacter` reports the effective source/ownership view alongside a native numerical projection. Verified alternate forms use current inherited ability scores and Class levels, but **computed Class features and Class resource pools are withheld**, and a resolved normal-form Hit Point maximum is marked unsupported pending form-specific policy. A regular primary-sheet projection retains its normal native calculated features. Catalog version mismatches remain invalid.

Alternate-form outputs are always `partial`: this implementation composes verified numeric sheet characteristics and applicable persistent influences but **withholds raw current-resource values from the projected character request**, and explicitly marks complete Class-feature, Class-resource, HP and underlying Origin grant applicability unresolved. The separately verified `Resources` view is resource policy information, not a substitute for complete combat rules or all Class resources. An unmanifested sheet is never implicitly selected. No monster/statblock name can authorize a form.

The `Underlying` Origin remains authoritative and the `Current` Origin compatibility field is unchanged. Permanent reconstruction continues to use its existing independent engine. Wild Shape, Polymorph and full simultaneous mechanical combination are optional later consumers.

## Synthetic tests

`tests/RulesCore.Mechanics.Tests/NativeCharacterManifestationTests.cs` exercises:

1. Persistent Werewolf, Wolf and Hybrid sheets without a manifestation; verified Wolf entry, reversion, reentry and Wolf-to-Hybrid switching.
2. Shared versus form-local grant ownership, latest Class level and reconstructed baseline propagation, primary and explicitly alternate-targeted reversible influences.
3. Primary, sheet and activation-owned resources; stable activation captures; live maximum adjustment without refilling current quantities.
4. Active/suppressed/ended lifecycles, lack of automatic reactivation, revision and replay checks, unauthorized transitions and ownership, invalid graph/source/resource inputs.
5. Unresolved simultaneous forms, authenticated single-form suppression, unsupported coexistence and repeated non-mutating deterministic reads.
6. Native numerical projection of normal and active forms, with form-specific Class feature/resource withholding, class-level propagation, stale catalog rejection and source/receipt denial.
7. Pre-commit evaluation of an actual Wolf characteristic/influence collision and an invalid resource policy; both return `unresolved` without a commit candidate.
8. Source-named ending events, independently authorized later ending events, rejected mismatched/missing ending evidence, and verified `Terminate` interactions requiring explicit `End` steps.
9. Negative checks for unauthorized inherited progression/grants, duplicate grant ownership, malformed source occurrences and empty/foreign manifestation `CharacterId` values at the manifestation planner boundary. Both existing forged ledger records and proposed activations with foreign ownership fail closed before transition authorization.
10. An independently authenticated reconstructed `NativeOriginProfile.Underlying` projected through the existing Origin baseline adapter during an active Wolf manifestation, followed by reversion using the latest Underlying rather than stale `Current`. This is an adapter-level synthetic test, **not** an executed Rite or new reconstruction engine.

Validation commands (execute in a .NET 10 environment):

```sh
dotnet restore RulesCore.slnx
dotnet build RulesCore.slnx -c Release --no-restore
dotnet test RulesCore.slnx -c Release --no-build
python3 -m unittest discover -s tests -p 'test_*.py' -v
python3 tools/audit_native_baseline.py --native data/rulesets/first-party-001/native --strict
python3 tools/validate_ruleset_split.py
```

## Remaining integration

The next increment should connect this ledger and proposed transactional commit to an authenticated Character Sheet host with durable revision and unique-event constraints, source-qualified form eligibility, interaction authorization and resource ownership verification. Next, add a complete, authored form-specific applicability/HP/resource policy slice and actual first-party werewolf progression only after independent gameplay review. Do not represent this library increment as production transformation support.
