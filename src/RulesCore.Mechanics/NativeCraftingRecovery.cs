namespace RulesCore.Mechanics;

public enum NativeRecoveryOperation
{
    Salvage,
    Disenchant
}

/// <summary>
/// A source-backed recovery attempt. Ordinary disenchanting follows the
/// Warcraft rarity qualification ladder; salvaging keeps source-defined rank
/// requirements. Neither operation creates a payout or rewrites any item.
/// </summary>
public sealed record NativeRecoveryQualificationRequest(
    NativeRecoveryOperation Operation,
    string? ItemRarity,
    int? OperatorRanks,
    int? SourceMinimumRanks,
    bool? HasAuthorizedTechnique,
    bool? HasAccessibleItem,
    bool? HasDefinedRecoveryOutputs,
    bool? HasRequiredEquipment,
    bool? IsPermanent = null,
    bool? IsAttunedToAnother = null,
    bool ExceptionalDisenchantingAuthorized = false,
    int? MinutesSpent = null);

public sealed record NativeRecoveryQualificationResult(
    string Status, bool? Eligible, int? MinimumRanks,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Eligibility only. A real inventory transaction must enforce provenance,
/// item ownership, recipe-specific loss/yield, checks and atomic consumption.
/// </summary>
public static class NativeCraftingRecovery
{
    public static NativeRecoveryQualificationResult Evaluate(
        NativeRecoveryQualificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var missing = new List<string>();
        var denied = new List<string>();
        int? minimum = null;
        var ordinaryDisenchanting = request.Operation ==
            NativeRecoveryOperation.Disenchant;

        if (!Enum.IsDefined(request.Operation) ||
            request.SourceMinimumRanks is < 0 or > 1_000_000)
            return new("unresolved", null, null, ["invalid-recovery-contract"]);

        if (request.Operation == NativeRecoveryOperation.Salvage)
        {
            minimum = request.SourceMinimumRanks;
            if (minimum is null)
                missing.Add("salvage-rank-requirement-unresolved");
        }
        else
        {
            if (!NativeCraftingQualification.TryParseRarity(
                request.ItemRarity, out var rarity))
                missing.Add("item-rarity-unresolved");
            else
            {
                // Ordinary disenchanting does not apply to Legendary items or
                // Artifacts. An explicitly authored exceptional procedure may
                // define its own requirement; the source must still authorize
                // the output and its exceptional use.
                if (rarity is NativeCraftingItemRarity.Legendary or
                    NativeCraftingItemRarity.Artifact)
                {
                    if (!request.ExceptionalDisenchantingAuthorized)
                        denied.Add("rarity-excluded-from-ordinary-disenchanting");
                    else if (request.SourceMinimumRanks is null)
                        missing.Add("exceptional-recovery-rank-unresolved");
                    else
                    {
                        // An exceptional recovery is a different authored
                        // procedure. Do not impose ordinary disenchanting's
                        // permanence, attunement or timing rules on it.
                        ordinaryDisenchanting = false;
                        minimum = request.SourceMinimumRanks;
                    }
                }
                else
                    minimum = (int)rarity + 1;
            }

            if (ordinaryDisenchanting)
            {
                if (request.IsPermanent is null)
                    missing.Add("item-permanence-unresolved");
                else if (!request.IsPermanent.Value)
                    denied.Add("item-not-permanent");

                if (request.IsAttunedToAnother is null)
                    missing.Add("item-attunement-unresolved");
                else if (request.IsAttunedToAnother.Value)
                    denied.Add("item-attuned-to-another");

                // Warcraft's ordinary disenchanting requires one hour.
                // Salvage and exceptional procedures define their own time.
                if (request.MinutesSpent is null ||
                    request.MinutesSpent > 1_000_000)
                    missing.Add("disenchanting-time-unresolved");
                else if (request.MinutesSpent < 60)
                    denied.Add("disenchanting-time-insufficient");
            }
        }

        if (minimum is int level && request.SourceMinimumRanks is int authored)
            minimum = Math.Max(level, authored);
        if (request.OperatorRanks is not int ranks || ranks is < 0 or > 1_000_000)
            missing.Add("operator-rank-unresolved");
        else if (minimum is int threshold && ranks < threshold)
            denied.Add("operator-rank-too-low");

        Check(request.HasAuthorizedTechnique, "technique", denied, missing);
        Check(request.HasAccessibleItem, "item", denied, missing);
        Check(request.HasDefinedRecoveryOutputs, "defined-recovery-output",
            denied, missing);
        Check(request.HasRequiredEquipment, "equipment", denied, missing);

        if (denied.Count > 0)
            return new("ineligible", false, minimum,
                denied.Concat(missing).ToArray());
        if (missing.Count > 0)
            return new("unresolved", null, minimum, missing);
        return new("eligible", true, minimum, []);
    }

    private static void Check(bool? value, string label,
        List<string> denied, List<string> missing)
    {
        if (value is null) missing.Add(label + "-unresolved");
        else if (!value.Value) denied.Add(label + "-missing");
    }
}
