namespace RulesCore.Mechanics;

/// <summary>A source-authored movement speed policy, independent of armor and creature type.</summary>
public sealed record NativePhysicalMovementLoadPolicy(
    string Mode, int NormalSpeedFeet, int LoadedSpeedFeet,
    int LoadedAtPercent, int OverloadedSpeedFeet);

/// <summary>An operating source provides one load-dependent movement mode.</summary>
public sealed record NativePhysicalMovementSource(
    string SourceId, bool Operating, NativePhysicalMovementLoadPolicy? Policy);

/// <summary>
/// Read-only candidate speed, not an Origin grant or a completed terrain /
/// action-economy evaluation. Unknown mass never proves unencumbered movement.
/// </summary>
public sealed record NativePhysicalMovementLoadResult(
    string Status, string? Mode, int? SpeedFeet,
    int? UpperBoundSpeedFeet, IReadOnlyList<string> SourceIds, string Explanation)
{
    public bool SpeedResolved => Status is "normal" or "loaded" or "stalled";
}

/// <summary>
/// Applies explicitly authored speed tiers to verified gross-load evidence.
/// A known mass lower bound can prove a slowdown or stall, but only a complete
/// supported load can prove a nonzero speed. Sources never add their speeds.
/// </summary>
public static class NativePhysicalMovementEvaluator
{
    public static NativePhysicalMovementLoadResult Evaluate(
        NativePhysicalLoadAssessment? load,
        IReadOnlyList<NativePhysicalMovementSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Any(x => x is null || string.IsNullOrWhiteSpace(x.SourceId)) ||
            sources.Select(x => x.SourceId).Distinct(StringComparer.Ordinal).Count() != sources.Count)
            throw new ArgumentException("Movement source identities must be distinct.", nameof(sources));

        var active = sources.Where(x => x.Operating)
            .OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray();
        var ids = active.Select(x => x.SourceId).ToArray();
        if (active.Length == 0)
            return new("unavailable", null, null, null, [],
                "No operating movement source is available.");
        if (active.Any(x => x.Policy is null))
            return Unknown("movement-policy-unresolved");

        var policy = active[0].Policy!;
        if (active.Any(x => x.Policy != policy))
            return Unknown("movement-policy-combination-unresolved");
        if (string.IsNullOrWhiteSpace(policy.Mode) ||
            policy.LoadedAtPercent is < 1 or > 100 ||
            policy.OverloadedSpeedFeet < 0 ||
            policy.LoadedSpeedFeet < policy.OverloadedSpeedFeet ||
            policy.NormalSpeedFeet < policy.LoadedSpeedFeet ||
            policy.NormalSpeedFeet is <= 0 or > 100_000)
            return Unknown("movement-policy-invalid");

        if (load is null || load.VerifiedSupportCapacityPounds is null)
            return Unknown("movement-load-unresolved", policy.Mode);

        if (load.Status == "overloaded")
            return new("stalled", policy.Mode, policy.OverloadedSpeedFeet,
                policy.OverloadedSpeedFeet, ids,
                "The known minimum gross load exceeds verified support for this movement mode.");
        if (load.Status is not ("supported" or "unresolved"))
            return Unknown("movement-load-unresolved", policy.Mode);

        var loaded = (decimal)load.KnownMinimumGrossLoadPounds * 100 >=
            (decimal)load.VerifiedSupportCapacityPounds.Value * policy.LoadedAtPercent;
        var speed = loaded ? policy.LoadedSpeedFeet : policy.NormalSpeedFeet;
        if (load.Status != "supported")
            return new("unresolved", policy.Mode, null, speed, ids,
                "The known lower bound limits possible speed, but actual gross mass is incomplete.");

        return new(loaded ? "loaded" : "normal", policy.Mode, speed, speed, ids,
            "Verified complete gross mass resolves the authored movement speed tier.");

        NativePhysicalMovementLoadResult Unknown(string reason, string? mode = null) =>
            new("unresolved", mode, null, null, ids, reason);
    }
}
