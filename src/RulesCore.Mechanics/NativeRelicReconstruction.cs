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


/// <summary>
/// Resolves the explicitly attempted assembly of a previously qualified relic.
/// Ordinary relics require a source-defined History DC (15 for Warcraft); a
/// failed attempt cannot repeat until another matching fragment is acquired.
/// Six qualified magic fragments instead assemble an item of the DM's chosen
/// eligible rarity, without a separate History assembly roll.
/// </summary>
public sealed record NativeRelicAssemblyRequest(
    NativeRelicReconstructionRequest Reconstruction,
    int? HistoryCheckTotal = null,
    int HistoryDifficultyClass = 15,
    bool PreviousAttemptFailed = false,
    int? AdditionalMatchingFragmentsSinceFailure = null);

public sealed record NativeRelicAssemblyResult(
    string Status, bool? Assembled, int? DifficultyClass,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Read-only adjudication. No check is rolled and no relic, magical item,
/// fragment, or retry state is awarded or consumed. All item choices,
/// fragment provenance and prior failed-attempt history remain caller-owned.
/// </summary>
public static class NativeRelicAssembly
{
    public static NativeRelicAssemblyResult Resolve(NativeRelicAssemblyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Reconstruction is null ||
            request.HistoryDifficultyClass is < 1 or > 100_000 ||
            request.HistoryCheckTotal is < -1_000_000 or > 1_000_000 ||
            request.AdditionalMatchingFragmentsSinceFailure is < 0 or > 1_000_000)
            return new("unresolved", null, null, ["invalid-relic-assembly-input"]);

        var qualification = NativeRelicReconstruction.Evaluate(request.Reconstruction);
        if (qualification.CanAttempt != true)
            return new(qualification.Status, null, null, qualification.Reasons);

        if (request.Reconstruction.MagicalResult)
            return new("resolved", true, null, []);

        // The Warcraft ordinary-relic retry rule concerns additional matching
        // fragments acquired AFTER the failure, not the original six fragments
        // still present in the character's inventory.
        if (request.PreviousAttemptFailed)
        {
            if (request.AdditionalMatchingFragmentsSinceFailure is null)
                return new("unresolved", null, request.HistoryDifficultyClass,
                    ["additional-fragment-history-unresolved"]);
            if (request.AdditionalMatchingFragmentsSinceFailure == 0)
                return new("ineligible", null, request.HistoryDifficultyClass,
                    ["additional-matching-fragment-required-after-failure"]);
        }

        if (request.HistoryCheckTotal is null)
            return new("unresolved", null, request.HistoryDifficultyClass,
                ["history-check-unresolved"]);

        return request.HistoryCheckTotal >= request.HistoryDifficultyClass
            ? new("resolved", true, request.HistoryDifficultyClass, [])
            : new("resolved", false, request.HistoryDifficultyClass,
                ["failed-history-check"]);
    }
}
