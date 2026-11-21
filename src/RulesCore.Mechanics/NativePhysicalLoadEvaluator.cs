namespace RulesCore.Mechanics;

/// <summary>
/// A source of maximum gross moving load for any mechanically supported body.
/// This is not automatically a movement speed, a creature's carrying capacity,
/// or an entitlement to combine independent locomotion mechanisms.
/// </summary>
public sealed record NativePhysicalLoadSupport(
    string SourceId, long? MaximumGrossLoadPounds, bool Operating,
    bool SharesLoad = false);

/// <summary>
/// A generic, read-only physical loading assessment suitable for creatures,
/// constructed equipment, or vehicles. Known body mass may be only a lower
/// bound: a measured shortfall proves overloading, while apparent headroom
/// never proves sufficient capacity until body and payload mass are complete.
/// </summary>
public sealed record NativePhysicalLoadAssessment(
    string Status, long KnownMinimumGrossLoadPounds,
    long? VerifiedSupportCapacityPounds, long? MinimumDeficitPounds,
    long? MaximumPossibleHeadroomPounds, bool BodyMassComplete,
    string Explanation)
{
    public bool? WithinSupportCapacity =>
        Status == "supported" ? true : Status == "overloaded" ? false : null;
}

public static class NativePhysicalLoadEvaluator
{
    public static NativePhysicalLoadAssessment Evaluate(
        long knownMinimumBodyMassPounds, bool bodyMassComplete,
        long? carriedPayloadMassPounds, IReadOnlyList<NativePhysicalLoadSupport> supports)
    {
        ArgumentNullException.ThrowIfNull(supports);
        ArgumentOutOfRangeException.ThrowIfNegative(knownMinimumBodyMassPounds);
        if (carriedPayloadMassPounds is < 0)
            throw new ArgumentOutOfRangeException(nameof(carriedPayloadMassPounds));
        if (supports.Any(x => x is null ||
            string.IsNullOrWhiteSpace(x.SourceId) ||
            x.MaximumGrossLoadPounds is < 0))
            throw new ArgumentException(
                "Support sources need identities and nonnegative authored capacity.", nameof(supports));

        // Unknown payload does not count as zero when proving that the
        // configured assembly is within its support capacity.
        long minimumLoad;
        try
        {
            minimumLoad = checked(knownMinimumBodyMassPounds +
                (carriedPayloadMassPounds ?? 0));
        }
        catch (OverflowException)
        {
            return Unknown(knownMinimumBodyMassPounds, null, "gross-load-overflow");
        }
        var operating = supports.Where(x => x.Operating).ToArray();
        if (operating.Length == 0)
            return Unknown(minimumLoad, null, "no-operating-load-support");

        if (operating.Any(x => x.MaximumGrossLoadPounds is null))
            return Unknown(minimumLoad, null, "load-support-capacity-unresolved");

        if (operating.Length > 1 && operating.Any(x => !x.SharesLoad))
            return Unknown(minimumLoad, null, "load-support-combination-unresolved");

        long capacity;
        try
        {
            capacity = checked(operating.Sum(x => x.MaximumGrossLoadPounds!.Value));
        }
        catch (OverflowException)
        {
            return Unknown(minimumLoad, null, "load-support-capacity-overflow");
        }

        // A lower bound above a verified maximum is already sufficient to
        // prove that these exact operational supports cannot move this load.
        // The body may still have other movement modes or external help.
        if (minimumLoad > capacity)
            return new("overloaded", minimumLoad, capacity,
                minimumLoad - capacity, 0, bodyMassComplete,
                "Known load exceeds the selected operating supports' combined capacity.");

        if (!bodyMassComplete || carriedPayloadMassPounds is null)
            return Unknown(minimumLoad, capacity, "gross-load-mass-incomplete");

        return new("supported", minimumLoad, capacity, 0,
            capacity - minimumLoad, true,
            "The measured gross load does not exceed the selected supports' capacity.");

        NativePhysicalLoadAssessment Unknown(long minimum, long? maximum,
            string explanation) => new("unresolved", minimum, maximum, null,
                maximum is long c ? Math.Max(0, c - minimum) : null,
                bodyMassComplete, explanation);
    }
}

/// <summary>
/// Source-identified, passive physical coverage. Material identity, rigidity
/// and flammability are evidence only: no AC, DR, damage resistance, stacking
/// or other combat grant is inferred from the existence of a surface.
/// </summary>
public sealed record NativePhysicalSurface(
    string InstanceId, string ComponentDefinitionId, string MaterialSlotId,
    string MaterialDefinitionId, string Role,
    string? Rigidity, string? Combustibility, bool Operational,
    // Explicit installed region identity is an arbitrary physical segment,
    // not a body-wide protection entitlement or mandatory anatomy.
    string? ProtectedRegionId = null, int? CandidateFlatDamageReduction = null);
