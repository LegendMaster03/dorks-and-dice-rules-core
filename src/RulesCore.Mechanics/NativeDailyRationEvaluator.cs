namespace RulesCore.Mechanics;

/// <summary>
/// Applies the SRD 5.1 (2014) food-deprivation interval to one daily ration.
/// The caller owns elapsed-day accounting, authenticated ration consumption,
/// and the canonical Exhaustion condition. This pure evaluator never modifies
/// inventory, rests, power availability, or a character's conditions.
/// </summary>
public static class NativeDailyRationEvaluator
{
    public static NativeDailyRationAssessment AssessDay(
        int constitutionModifier, int previousDaysWithoutFullRation,
        bool fullRationSupplied)
    {
        if (previousDaysWithoutFullRation < 0)
            throw new ArgumentOutOfRangeException(
                nameof(previousDaysWithoutFullRation));

        // A creature can go 3 + Constitution modifier days without food,
        // with a minimum of one day. Further days cause one Exhaustion level
        // each, using the existing universal condition.
        var allowedDays = (int)Math.Clamp(
            3L + constitutionModifier, 1L, int.MaxValue);
        var missedDays = fullRationSupplied ? 0
            : previousDaysWithoutFullRation == int.MaxValue
                ? int.MaxValue : previousDaysWithoutFullRation + 1;
        return new(missedDays, allowedDays,
            !fullRationSupplied && missedDays > allowedDays ? 1 : 0,
            "condition.exhaustion");
    }
}

public sealed record NativeDailyRationAssessment(
    int ConsecutiveDaysWithoutFullRation, int DaysAllowedWithoutRation,
    int ExhaustionLevelsGained, string AppliedConditionId);
