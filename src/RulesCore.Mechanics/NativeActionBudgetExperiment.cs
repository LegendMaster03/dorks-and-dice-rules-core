namespace RulesCore.Mechanics;

/// <summary>
/// A per-combatant resource snapshot for comparing alternative action budgets.
/// This does not grant an Action, Bonus Action, Reaction, or any ability.
/// Consumers must independently authorize the activity and its trigger.
/// </summary>
public sealed record NativeActionBudgetState(
    string PolicyId,
    bool OnTurn,
    bool BonusActionAvailable,
    bool ReactionAvailable);

/// <summary>
/// Immutable, deterministic result. Rules Core does not persist encounter state.
/// A failed command leaves the supplied state unchanged.
/// </summary>
public sealed record NativeActionBudgetResult(
    string Status,
    NativeActionBudgetState? State = null,
    string? Reason = null);

/// <summary>
/// Experimental resource-accounting models, not approved native action-economy rules.
/// The encounter selects exactly one policy; each combatant retains an independent
/// snapshot under that policy, even when using Block Initiative.
/// </summary>
public static class NativeActionBudgetExperiment
{
    public const string Independent = "independent";
    public const string SharedEndTurn = "shared-end-turn";

    public const string Initialize = "initialize";
    public const string StartTurn = "start-turn";
    public const string EndTurn = "end-turn";
    public const string UseBonusAction = "bonus-action";
    public const string UseReaction = "reaction";

    public static NativeActionBudgetResult Resolve(
        string? policyId, string? command, NativeActionBudgetState? state = null)
    {
        if (policyId is not (Independent or SharedEndTurn))
            return new("invalid-policy", state, "Select an explicit experimental policy.");

        if (command == Initialize)
            return state is null
                ? new("resolved", new(policyId, false, true, true))
                : new("invalid-state", state, "Initialize requires no prior state.");

        if (command is not (StartTurn or EndTurn or UseBonusAction or UseReaction))
            return new("invalid-command", state);

        if (state is null || state.PolicyId != policyId ||
            (policyId == SharedEndTurn &&
             state.BonusActionAvailable != state.ReactionAvailable))
            return new("invalid-state", state, "State must match the selected policy.");

        // Independent mirrors the familiar 5.x refresh: both budgets refresh
        // when the individual combatant starts their own turn.
        // SharedEndTurn refreshes one combined budget when that turn ends.
        switch (command)
        {
            case StartTurn:
                if (state.OnTurn)
                    return new("invalid-timing", state, "Turn has already started.");
                return new("resolved", policyId == Independent
                    ? state with
                    {
                        OnTurn = true,
                        BonusActionAvailable = true,
                        ReactionAvailable = true
                    }
                    : state with { OnTurn = true });

            case EndTurn:
                if (!state.OnTurn)
                    return new("invalid-timing", state, "No turn is active.");
                return new("resolved", policyId == SharedEndTurn
                    ? state with
                    {
                        OnTurn = false,
                        BonusActionAvailable = true,
                        ReactionAvailable = true
                    }
                    : state with { OnTurn = false });

            case UseBonusAction:
                if (!state.OnTurn)
                    return new("invalid-timing", state,
                        "Bonus Actions require the combatant's turn.");
                if (!state.BonusActionAvailable)
                    return new("unavailable", state, "Bonus Action budget is spent.");
                return new("resolved", policyId == SharedEndTurn
                    ? state with
                    {
                        BonusActionAvailable = false,
                        ReactionAvailable = false
                    }
                    : state with { BonusActionAvailable = false });

            case UseReaction:
                // Only accounts for the budget. The invoking encounter tool
                // must check that a feature grants this reaction and its trigger
                // is legal; this prototype cannot make that adjudication.
                if (!state.ReactionAvailable)
                    return new("unavailable", state, "Reaction budget is spent.");
                return new("resolved", policyId == SharedEndTurn
                    ? state with
                    {
                        BonusActionAvailable = false,
                        ReactionAvailable = false
                    }
                    : state with { ReactionAvailable = false });

            default:
                return new("invalid-command", state);
        }
    }
}
