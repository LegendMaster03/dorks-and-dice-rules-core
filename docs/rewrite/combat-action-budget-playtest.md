# Experimental action-budget playtest

**Status:** Executable comparison harness; **neither budget policy is an approved Dorks & Dice combat rule**. This is a resource-state experiment, not a complete combat engine or a new 5.x conversion policy.

## Goals and boundaries

Test the effect of two encounter-wide **resource refresh policies** without changing the definitions, prerequisites, targets, effects, or timing triggers of Bonus Actions, Reactions, attacks, spells, movement, or existing character/monster features. An authorized combat tool still decides whether a requested activity is granted and whether its trigger is valid.

- `independent`: Familiar 5.x-style separate Bonus Action and Reaction availability; both refresh at the **start** of each combatant's own turn.
- `shared-end-turn`: One combined allowance for Bonus Actions or Reactions; it refreshes at the **end** of each combatant's own turn. Using a Bonus Action during the turn does not prevent a Reaction after that turn ends. Using a Reaction afterward leaves the allowance spent during the actor's next turn, until that next turn ends.
- Both use the approved **Bonus Action** terminology; source Swift Actions map to eligible Bonus Actions.
- A combatant needs a qualifying ability and legal trigger independently of an available resource. The kernel does not grant actions or resolve their effects.
- Both use **per-combatant state**, even under optional Block Initiative. Standard and Complex Combatants can coexist; their statblock class does not determine their resource policy.
- **One policy for the whole encounter.** Do not let individual players secretly use incompatible budgets in the same combat. A playtest organizer should explicitly select the policy, keep it fixed during that encounter, and persist each actor's returned state.

## Authenticated evaluation interface

`POST /api/mechanics/v1/experimental/combat/action-budget` on the delegated mechanics surface, or `POST /api/private/mechanics/v1/experimental/combat/action-budget` through the private tunnel. Both use the same existing authentication boundaries. The endpoint is **experimental**, stateless and does **not** persist campaign or combatant state.

Select a policy explicitly in the first call:

```json
{
  "rulesetId": "first-party-001",
  "fingerprint": "<current-ruleset-fingerprint>",
  "policyId": "shared-end-turn",
  "command": "initialize"
}
```

The response is `{ "status": "resolved", "state": { "policyId": "shared-end-turn", "onTurn": false, "bonusActionAvailable": true, "reactionAvailable": true }, "reason": null }`, with additional response metadata as documented by the API. Use the returned `state` in each successive request with the *same* `policyId` and a `command`:

- `start-turn`: enter the actor's own turn.
- `bonus-action`: spend an **already authorized** Bonus Action on their own turn.
- `reaction`: spend an **already authorized** Reaction when a valid trigger occurs, including outside their own turn.
- `end-turn`: leave the actor's own turn.

A failed command returns the original unchanged state and a reason. Selecting a different policy for an existing state returns `invalid-state`. A mismatched ruleset/fingerprint returns a version error. No method consumes ordinary Actions, movement, spell slots, or feature resources.

## Playtest comparison

Use equivalent characters, enemy positions, turn order, encounter objectives, and feature choices in separate runs for each policy. Log **available opportunities and rejected actions**, not only damage output. At minimum:

| Scenario | Independent | Shared end-turn |
| --- | --- | --- |
| Bonus Action on the actor's turn, then a Reaction after that turn | Both can be used | Both can be used because the shared allowance refreshes at turn end |
| Reaction after actor's turn, then Bonus Action on next turn | Both can be used | Bonus Action is unavailable until that turn ends |
| Reaction during own turn, then Bonus Action | Both can be used if abilities permit | Second use is unavailable |
| Two Reactions between the same resource refresh | Second is unavailable | Second is unavailable |
| Opportunity attack versus a defensive Reaction | Compete for one Reaction | Compete for one shared allowance |
| Character has no eligible Bonus Action | Reaction remains an option | Reaction remains an option, but this does not add new abilities |
| Cunning Action, two-weapon fighting, combo support or monster reactions | Measure unchanged action permissions | Measure which opportunities become unavailable and how often |

Also test triggering abilities just before/after a turn boundary, encounter start before a first turn, individual turn order inside Block Initiative, and simultaneous special combatant behaviors.

**Evaluation criteria:** Decision quality (meaningful alternatives), friction for 5.x users, frequency of rejected abilities, shifts in relative class effectiveness, conversion surprises, and the cost of supporting two playtest profiles. Keep logs by encounter and policy; do not compare results obtained from different encounter conditions.

## Status before real-table testing

The resolver and an authenticated evaluation endpoint are implemented in Rules Core on a separate branch. **They are not connected to Block Initiative's encounter UI or Character Sheet**, and neither policy is yet active in a deployed environment. No C# code was compiled or exercised in this session because the execution environment lacks the .NET SDK. The accompanying xUnit tests specify the expected behavior; run these commands on a .NET 10 development machine:

```sh
dotnet restore RulesCore.slnx
dotnet test tests/RulesCore.Mechanics.Tests/RulesCore.Mechanics.Tests.csproj -c Release
dotnet test tests/RulesCore.Host.Tests/RulesCore.Host.Tests.csproj -c Release
python3 tools/validate_ruleset_split.py
```

Consumer integration and deployment need separate review and approval. Neither mode should be silently activated when importing 5.x content.
