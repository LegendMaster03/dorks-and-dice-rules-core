namespace RulesCore.Mechanics;

/// <summary>
/// Read-only protection potential for one explicitly identified physical region.
/// It never creates an AC, flat DR, damage resistance or damage-type grant.
/// </summary>
public sealed record NativePhysicalProtectionPreview(
    string Status, string RegionId, long KnownCandidateFlatDamageReduction,
    IReadOnlyList<NativePhysicalSurface> ContributingSurfaces, string Explanation);

/// <summary>
/// Compares only source-identified protective layers known to cover the queried
/// region. The generic combat DR resolver still owns actual damage handling,
/// applicability, bypass and stacking after the protection rules are approved.
/// </summary>
public static class NativePhysicalProtectionEvaluator
{
    public static NativePhysicalProtectionPreview Evaluate(
        string regionId, IReadOnlyList<NativePhysicalSurface> surfaces)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentNullException.ThrowIfNull(surfaces);
        if (surfaces.Any(s => s is null ||
            string.IsNullOrWhiteSpace(s.InstanceId) ||
            string.IsNullOrWhiteSpace(s.ComponentDefinitionId) ||
            string.IsNullOrWhiteSpace(s.MaterialDefinitionId) ||
            string.IsNullOrWhiteSpace(s.MaterialSlotId)))
            throw new ArgumentException("Physical layers must retain native source identity.",
                nameof(surfaces));

        var relevant = surfaces.Where(x => x.Role == "protective-surface" &&
            x.ProtectedRegionId == regionId)
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal)
            .ThenBy(x => x.MaterialSlotId, StringComparer.Ordinal).ToArray();
        if (relevant.Length == 0)
            return new("none", regionId, 0, [],
                "No explicitly declared protective surface covers this queried region.");

        // One composite surface containing multiple materials is not
        // automatically several independently stackable protective layers.
        if (relevant.GroupBy(x => x.InstanceId, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
            return new("unresolved", regionId, 0, relevant,
                "A composite protective surface needs its own coverage and material interaction rule.");

        if (relevant.Any(x => x.CandidateFlatDamageReduction is null or < 0))
            return new("unresolved", regionId, 0, relevant,
                "At least one covering surface lacks a valid material protection value.");

        try
        {
            var potential = checked(relevant.Sum(x =>
                (long)x.CandidateFlatDamageReduction!.Value));
            return new("candidate", regionId, potential, relevant,
                "Provisional covering-layer total only; damage mitigation, bypass and effective coverage are not authorized.");
        }
        catch (OverflowException)
        {
            return new("unresolved", regionId, 0, relevant,
                "Potential protection total exceeds supported quantities.");
        }
    }
}
