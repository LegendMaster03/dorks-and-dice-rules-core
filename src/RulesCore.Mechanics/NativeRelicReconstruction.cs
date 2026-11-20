namespace RulesCore.Mechanics;

/// <summary>
/// A source-verified fragment holding. Magical and ordinary fragments of
/// different origins are never interchangeable. This is not an inventory item
/// definition or a source of new fragments.
/// </summary>
public sealed record NativeRelicFragment(
    string OriginId, bool Magical, int Quantity);

/// <summary>
/// Reconstruct a source-defined historical object from matching fragments.
/// Six fragments is the Warcraft-inspired default; the authored procedure may
/// choose another positive count. A magical result additionally obeys the
/// regular item-rarity-to-Competency-rank requirement.
/// </summary>
public sealed record NativeRelicReconstructionRequest(
    string OriginId,
    bool MagicalResult,
    string? ResultItemRarity,
    int? RestorerRanks,
    IReadOnlyList<NativeRelicFragment> AvailableFragments,
    bool? HasQualifiedHistoricalMethod,
    bool? HasRequiredKnowledge,
    bool? HasRequiredFacilities,
    int RequiredFragments = 6);

public sealed record NativeRelicReconstructionResult(
    string Status,
    bool? CanAttempt,
    int MatchingFragments,
    int RequiredFragments,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Validates relic reconstruction requirements only; it does not discover
/// fragments during travel, award an item, roll History checks, or consume
/// anything. The DM or source recipe chooses the output and any checks.
/// </summary>
public static class NativeRelicReconstruction
{
    public static NativeRelicReconstructionResult Evaluate(
        NativeRelicReconstructionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.OriginId) ||
            request.OriginId.Length > 160 ||
            request.RequiredFragments is < 1 or > 1_000_000 ||
            request.AvailableFragments is null ||
            request.AvailableFragments.Count > 500 ||
            request.AvailableFragments.Any(f => f is null ||
                string.IsNullOrWhiteSpace(f.OriginId) ||
                f.OriginId.Length > 160 || f.Quantity is < 1 or > 1_000_000))
            return new("unresolved", null, 0, request.RequiredFragments,
                ["invalid-reconstruction-contract"]);

        var matching = request.AvailableFragments
            .Where(f => f.OriginId == request.OriginId &&
                f.Magical == request.MagicalResult)
            .Sum(f => (long)f.Quantity);
        var count = (int)Math.Min(1_000_000, matching);
        var denied = new List<string>();
        var missing = new List<string>();

        if (matching < request.RequiredFragments)
            denied.Add("insufficient-matching-fragments");

        Check(request.HasQualifiedHistoricalMethod, "historical-method",
            denied, missing);
        Check(request.HasRequiredKnowledge, "restoration-knowledge",
            denied, missing);
        Check(request.HasRequiredFacilities, "restoration-facilities",
            denied, missing);

        if (request.MagicalResult)
        {
            // Reuse the approved rank gate. Fragment holdings satisfy only
            // the material part of this check; they never confer the recipe.
            var itemQualification = NativeCraftingQualification.Evaluate(
                new(request.ResultItemRarity, request.RestorerRanks,
                    request.HasRequiredKnowledge,
                    matching >= request.RequiredFragments));
            foreach (var reason in itemQualification.Reasons)
            {
                if (reason is "required-materials-missing" or
                    "materials-unresolved") continue;
                if (itemQualification.Eligible == false)
                    denied.Add(reason);
                else
                    missing.Add(reason);
            }
        }

        if (denied.Count > 0)
            return new("ineligible", false, count, request.RequiredFragments,
                denied.Distinct().Concat(missing.Distinct()).ToArray());
        if (missing.Count > 0)
            return new("unresolved", null, count, request.RequiredFragments,
                missing.Distinct().ToArray());
        return new("eligible", true, count, request.RequiredFragments, []);
    }

    private static void Check(bool? value, string label,
        List<string> denied, List<string> missing)
    {
        if (value is null) missing.Add(label + "-unresolved");
        else if (!value.Value) denied.Add(label + "-missing");
    }
}
