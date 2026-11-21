namespace RulesCore.Mechanics;

/// <summary>
/// Read-only evidence of source-identified physical surfaces covering a region.
/// No numeric protection, layer stacking, AC or DR is inferred from materials.
/// </summary>
public sealed record NativePhysicalProtectionPreview(
    string Status, string RegionId,
    IReadOnlyList<NativePhysicalSurface> ContributingSurfaces, string Explanation);

/// <summary>
/// Identifies installed protective surfaces for a declared physical region.
/// Actual protective effects require separately approved combat/material rules.
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
            return new("none", regionId, [],
                "No explicitly declared protective surface covers this queried region.");

        // Composite construction remains unresolved until a real coverage rule
        // defines how materials in one installed component interact.
        if (relevant.GroupBy(x => x.InstanceId, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
            return new("unresolved", regionId, relevant,
                "A composite protective surface needs an authored coverage rule.");

        return new("present", regionId, relevant,
            "Installed regional surfaces are identified; no defensive benefit is inferred.");
    }
}
