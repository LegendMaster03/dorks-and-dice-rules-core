namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed record CharacterSpellPointProgression(
    int CasterLevel,
    int MaximumPoints,
    int MaximumSlotLevel);

internal static class CharacterSpellPointRules
{
    private static readonly int[] MaximumPointsByCasterLevel =
    [
        0,
        4, 6, 14, 17, 27,
        32, 38, 44, 57, 64,
        73, 73, 83, 83, 94,
        94, 107, 114, 123, 133
    ];

    private static readonly int[] MaximumSlotLevelByCasterLevel =
    [
        0,
        1, 1, 2, 2, 3,
        3, 4, 4, 5, 5,
        6, 6, 7, 7, 8,
        8, 9, 9, 9, 9
    ];

    private static readonly int[] PointCostBySlotLevel =
    [
        0,
        2, 3, 5, 6, 7,
        9, 10, 11, 13
    ];

    public const int HighLevelSlotCreationLimit = 1;

    public static bool TryGetProgression(
        int casterLevel,
        out CharacterSpellPointProgression progression)
    {
        if (casterLevel < 0 || casterLevel >= MaximumPointsByCasterLevel.Length)
        {
            progression = default!;
            return false;
        }

        progression = new CharacterSpellPointProgression(
            casterLevel,
            MaximumPointsByCasterLevel[casterLevel],
            MaximumSlotLevelByCasterLevel[casterLevel]);
        return true;
    }

    public static bool TryGetSlotCost(int spellLevel, out int pointCost)
    {
        if (spellLevel <= 0 || spellLevel >= PointCostBySlotLevel.Length)
        {
            pointCost = 0;
            return false;
        }

        pointCost = PointCostBySlotLevel[spellLevel];
        return true;
    }

    public static bool HasPerLongRestCreationLimit(int spellLevel) =>
        spellLevel is >= 6 and <= 9;
}
