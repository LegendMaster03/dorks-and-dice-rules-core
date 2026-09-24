using System.Security.Cryptography;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

public sealed class SecureDiceRoller : IDiceRoller
{
    public const int MinimumSides = 2;
    public const int MaximumSides = 1_000_000;
    public const int MaximumRepeat = 1_000;
    public const int MaximumAbsoluteModifier = 1_000_000;

    public DiceRollBatchView Roll(DiceRollRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Sides < MinimumSides || request.Sides > MaximumSides)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Sides),
                $"Dice must have between {MinimumSides} and {MaximumSides} sides.");
        }
        if (request.Repeat < 1 || request.Repeat > MaximumRepeat)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Repeat),
                $"Repeat must be between 1 and {MaximumRepeat}.");
        }
        if (Math.Abs((long)request.Modifier) > MaximumAbsoluteModifier)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Modifier),
                $"Modifier must be between {-MaximumAbsoluteModifier} and {MaximumAbsoluteModifier}.");
        }

        var mode = DiceRollSelector.NormalizeMode(request.SelectionMode);
        if (mode == DiceRollSelectionModes.Emphasis && request.Sides != 20)
        {
            throw new ArgumentException(
                "Emphasis is defined for a d20 because its selection pivot is 10.",
                nameof(request.SelectionMode));
        }

        var rawCount = DiceRollSelector.RequiredRollCount(mode);
        var outcomes = new DiceRollOutcomeView[request.Repeat];

        for (var outcomeIndex = 0; outcomeIndex < request.Repeat; outcomeIndex++)
        {
            var rolls = new int[rawCount];
            for (var rollIndex = 0; rollIndex < rawCount; rollIndex++)
            {
                rolls[rollIndex] = RandomNumberGenerator.GetInt32(1, request.Sides + 1);
            }

            var selection = DiceRollSelector.Select(mode, rolls);
            var total = selection.SelectedRoll is int selected
                ? checked(selected + request.Modifier)
                : (int?)null;

            outcomes[outcomeIndex] = new DiceRollOutcomeView(
                selection.Rolls,
                selection.SelectedRollIndex,
                selection.SelectedRoll,
                selection.CandidateRollIndices,
                selection.RequiresChoice,
                total);
        }

        return new DiceRollBatchView(
            request.Sides,
            mode,
            request.Modifier,
            request.Repeat,
            outcomes);
    }
}
