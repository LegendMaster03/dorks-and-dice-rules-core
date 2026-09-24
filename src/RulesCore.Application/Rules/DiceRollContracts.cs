namespace RulesCore.Application.Rules;

public sealed record DiceRollRequest(
    int Sides = 20,
    string SelectionMode = "normal",
    int Modifier = 0,
    int Repeat = 1);

public sealed record DiceRollOutcomeView(
    IReadOnlyList<int> Rolls,
    int? SelectedRollIndex,
    int? SelectedRoll,
    IReadOnlyList<int> CandidateRollIndices,
    bool RequiresChoice,
    int? Total);

public sealed record DiceRollBatchView(
    int Sides,
    string SelectionMode,
    int Modifier,
    int Repeat,
    IReadOnlyList<DiceRollOutcomeView> Outcomes);

public interface IDiceRoller
{
    DiceRollBatchView Roll(DiceRollRequest request);
}
