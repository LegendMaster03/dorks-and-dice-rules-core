namespace RulesCore.Mechanics;

/// <summary>
/// Applies the SRD 5.1 (2014) food-deprivation interval to one daily ration.
/// The caller owns elapsed-day accounting, authenticated ration consumption,
/// and the canonical Exhaustion condition. This pure evaluator never modifies
/// inventory, rests, power availability, or a character's conditions.
/// </summary>
public static class NativeDailyRationEvaluator
{
    /// <summary>
    /// The resource substituted for a normal daily food ration. This is a
    /// generic sustenance requirement: food and mechanically fueled bodies
    /// use the same deprivation and Exhaustion progression.
    /// </summary>
    public static NativeDailyRationAssessment AssessDay(
        NativeDailyRationRequirement requirement, int suppliedUnits,
        int constitutionModifier, int previousDaysWithoutFullRation)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (string.IsNullOrWhiteSpace(requirement.ResourceKind) ||
            requirement.UnitsPerDay <= 0)
            throw new ArgumentException(
                "A ration needs a resource kind and a positive daily quantity.",
                nameof(requirement));
        if (suppliedUnits < 0)
            throw new ArgumentOutOfRangeException(nameof(suppliedUnits));

        // Supply must be authenticated by the inventory/character host.
        // This read-only preview never spends the resource.
        return AssessDay(constitutionModifier, previousDaysWithoutFullRation,
            suppliedUnits >= requirement.UnitsPerDay);
    }

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


/// <summary>
/// A daily ration can be food or an approved substitute resource. The
/// requirement does not create inventory, validate ownership, or grant
/// replenishment. It identifies what the ordinary survival rules consume.
/// </summary>
public sealed record NativeDailyRationRequirement(
    string ResourceKind, int UnitsPerDay);
