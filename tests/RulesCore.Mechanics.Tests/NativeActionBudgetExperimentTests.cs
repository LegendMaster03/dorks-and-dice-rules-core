using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeActionBudgetExperimentTests
{
    private static NativeActionBudgetState Initial(string policy)
    {
        var result = NativeActionBudgetExperiment.Resolve(
            policy, NativeActionBudgetExperiment.Initialize);
        Assert.Equal("resolved", result.Status);
        return Assert.IsType<NativeActionBudgetState>(result.State);
    }

    private static NativeActionBudgetState Step(NativeActionBudgetState state, string command)
    {
        var result = NativeActionBudgetExperiment.Resolve(state.PolicyId, command, state);
        Assert.Equal("resolved", result.Status);
        return Assert.IsType<NativeActionBudgetState>(result.State);
    }

    [Theory]
    [InlineData(NativeActionBudgetExperiment.Independent)]
    [InlineData(NativeActionBudgetExperiment.SharedEndTurn)]
    public void BonusActionRequiresTurnAndItsBudget(string policy)
    {
        var state = Initial(policy);
        Assert.Equal("invalid-timing",
            NativeActionBudgetExperiment.Resolve(policy,
                NativeActionBudgetExperiment.UseBonusAction, state).Status);
        state = Step(state, NativeActionBudgetExperiment.StartTurn);
        state = Step(state, NativeActionBudgetExperiment.UseBonusAction);
        var denied = NativeActionBudgetExperiment.Resolve(
            policy, NativeActionBudgetExperiment.UseBonusAction, state);
        Assert.Equal("unavailable", denied.Status);
        Assert.Equal(state, denied.State);
    }

    [Fact]
    public void IndependentPolicyKeepsReactionsAndBonusActionsSeparate()
    {
        var state = Initial(NativeActionBudgetExperiment.Independent);
        state = Step(state, NativeActionBudgetExperiment.StartTurn);
        state = Step(state, NativeActionBudgetExperiment.UseBonusAction);
        Assert.True(state.ReactionAvailable);
        state = Step(state, NativeActionBudgetExperiment.EndTurn);
        state = Step(state, NativeActionBudgetExperiment.UseReaction);
        Assert.False(state.ReactionAvailable);
        Assert.False(state.BonusActionAvailable);

        state = Step(state, NativeActionBudgetExperiment.StartTurn);
        Assert.True(state.ReactionAvailable);
        Assert.True(state.BonusActionAvailable);
        state = Step(state, NativeActionBudgetExperiment.UseBonusAction);
        Assert.False(state.BonusActionAvailable);
    }

    [Fact]
    public void SharedEndTurnPolicyMakesOutOfTurnReactionCostNextBonusAction()
    {
        var state = Initial(NativeActionBudgetExperiment.SharedEndTurn);
        state = Step(state, NativeActionBudgetExperiment.StartTurn);
        state = Step(state, NativeActionBudgetExperiment.UseBonusAction);
        Assert.False(state.ReactionAvailable);
        state = Step(state, NativeActionBudgetExperiment.EndTurn);
        Assert.True(state.ReactionAvailable);
        Assert.True(state.BonusActionAvailable);

        state = Step(state, NativeActionBudgetExperiment.UseReaction);
        Assert.False(state.ReactionAvailable);
        Assert.False(state.BonusActionAvailable);
        Assert.Equal("unavailable",
            NativeActionBudgetExperiment.Resolve(state.PolicyId,
                NativeActionBudgetExperiment.UseReaction, state).Status);

        state = Step(state, NativeActionBudgetExperiment.StartTurn);
        Assert.False(state.BonusActionAvailable);
        Assert.Equal("unavailable",
            NativeActionBudgetExperiment.Resolve(state.PolicyId,
                NativeActionBudgetExperiment.UseBonusAction, state).Status);

        state = Step(state, NativeActionBudgetExperiment.EndTurn);
        Assert.True(state.ReactionAvailable);
        Assert.True(state.BonusActionAvailable);
    }

    [Fact]
    public void SharedReactionOnOwnTurnConsumesCurrentBonusAction()
    {
        var state = Step(Initial(NativeActionBudgetExperiment.SharedEndTurn),
            NativeActionBudgetExperiment.StartTurn);
        state = Step(state, NativeActionBudgetExperiment.UseReaction);
        Assert.False(state.BonusActionAvailable);
        state = Step(state, NativeActionBudgetExperiment.EndTurn);
        Assert.True(state.BonusActionAvailable);
    }

    [Theory]
    [InlineData(NativeActionBudgetExperiment.Independent)]
    [InlineData(NativeActionBudgetExperiment.SharedEndTurn)]
    public void AReactionRequiresSeparateTriggerAuthorizationFromTheCaller(string policy)
    {
        // The resource kernel does not determine if an opportunity attack,
        // a defensive spell, or a cooperative reaction is actually permitted.
        var state = Initial(policy);
        var reaction = Step(state, NativeActionBudgetExperiment.UseReaction);
        Assert.False(reaction.ReactionAvailable);
        Assert.Equal("unavailable",
            NativeActionBudgetExperiment.Resolve(policy,
                NativeActionBudgetExperiment.UseReaction, reaction).Status);
    }

    [Fact]
    public void PolicyCannotChangeMidSequence()
    {
        var state = Initial(NativeActionBudgetExperiment.Independent);
        var result = NativeActionBudgetExperiment.Resolve(
            NativeActionBudgetExperiment.SharedEndTurn,
            NativeActionBudgetExperiment.StartTurn, state);
        Assert.Equal("invalid-state", result.Status);
        Assert.Equal(state, result.State);
    }

    [Fact]
    public void InvalidTransitionsDoNotChangeTheState()
    {
        var state = Initial(NativeActionBudgetExperiment.SharedEndTurn);
        Assert.Equal("invalid-timing",
            NativeActionBudgetExperiment.Resolve(state.PolicyId,
                NativeActionBudgetExperiment.EndTurn, state).Status);
        var started = Step(state, NativeActionBudgetExperiment.StartTurn);
        var result = NativeActionBudgetExperiment.Resolve(started.PolicyId,
            NativeActionBudgetExperiment.StartTurn, started);
        Assert.Equal("invalid-timing", result.Status);
        Assert.Equal(started, result.State);
    }

    [Fact]
    public void MissingAndInconsistentStatesAreRejected()
    {
        var state = Initial(NativeActionBudgetExperiment.SharedEndTurn);
        Assert.Equal("invalid-policy",
            NativeActionBudgetExperiment.Resolve("unknown",
                NativeActionBudgetExperiment.Initialize).Status);
        Assert.Equal("invalid-state",
            NativeActionBudgetExperiment.Resolve(state.PolicyId,
                NativeActionBudgetExperiment.UseReaction).Status);
        Assert.Equal("invalid-state",
            NativeActionBudgetExperiment.Resolve(state.PolicyId,
                NativeActionBudgetExperiment.UseReaction,
                state with { BonusActionAvailable = false }).Status);
        Assert.Equal("invalid-state",
            NativeActionBudgetExperiment.Resolve(state.PolicyId,
                NativeActionBudgetExperiment.Initialize, state).Status);
    }

    [Theory]
    [InlineData(NativeActionBudgetExperiment.Independent)]
    [InlineData(NativeActionBudgetExperiment.SharedEndTurn)]
    public void EachCombatantTracksTheirOwnAllowanceInEitherInitiativeMode(string policy)
    {
        // The kernel does not know the order, alliance, or creature model.
        var attacker = Step(Initial(policy), NativeActionBudgetExperiment.StartTurn);
        var ally = Initial(policy);
        attacker = Step(attacker, NativeActionBudgetExperiment.UseBonusAction);
        Assert.True(ally.BonusActionAvailable);
        Assert.True(ally.ReactionAvailable);
        ally = Step(ally, NativeActionBudgetExperiment.UseReaction);
        Assert.False(ally.ReactionAvailable);
        Assert.True(attacker.OnTurn);
    }
}
