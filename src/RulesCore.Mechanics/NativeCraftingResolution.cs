using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A native implementation of the existing Loot Tavern manufacturing check.
/// Manual inputs are an explicit playtest path, not inferred training or an
/// undocumented default. Materials and results remain Character-owned state.
/// </summary>
public sealed record NativeCraftingManualCompetency(
    string DisplayName, int Contribution, bool IsQualified);

public sealed record NativeManufacturingRequest(
    string RulesetId, string Fingerprint,
    NativeCraftingManualCompetency Competency,
    bool HasQualifiedGuidance = false,
    int? D20Roll = null,
    int OtherModifier = 0,
    int? TargetDc = null,
    int? ManualAbilityModifier = null);

public sealed record NativeCraftingCheckResolution(
    int ContractVersion, string RulesetId, string Fingerprint, string Status,
    string ProcedureKey, string DisplayName, string CompetencyKey,
    string CompetencyDisplayName, bool ManualCompetency, bool IsQualified,
    string RollMode, int CompetencyContribution, int AbilityContribution,
    int OtherModifier, int? D20Roll, int? Total, int? TargetDc,
    bool? MeetsTarget, string Outcome, int? Margin, int? FlawCount,
    bool InputsConsumed, bool ProducesFunctionalOutput, string? Reason = null);

public sealed class NativeCraftingResolver(NativeCatalogSnapshot catalog)
{
    public NativeCraftingCheckResolution ResolveManufacturing(
        NativeManufacturingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var competency = request.Competency;
        var validSnapshot = request.RulesetId == catalog.RulesetId &&
            request.Fingerprint == catalog.Fingerprint;
        var valid = validSnapshot && competency is not null &&
            !string.IsNullOrWhiteSpace(competency.DisplayName) &&
            competency.DisplayName.Length <= 200 &&
            competency.Contribution is >= -100000 and <= 100000 &&
            request.ManualAbilityModifier is null or >= -100000 and <= 100000 &&
            request.OtherModifier is >= -100000 and <= 100000 &&
            request.D20Roll is null or >= 1 and <= 20 &&
            request.TargetDc is null or >= 1 and <= 100000;
        var reason = !validSnapshot
            ? "ruleset-version-mismatch"
            : !valid
                ? "invalid-manufacturing-input"
                : request.ManualAbilityModifier is null
                    ? "manual-ability-required"
                    : null;
        if (reason is not null)
            return Finish("unsupported", "pending", null, null, null, false, false,
                reason);

        // Preserves the old approved CraftingOutcomeEvaluator manufacturing
        // formula and qualified-help disadvantage rule. Dice are supplied
        // by the character workflow, not rolled or fabricated by Rules Core.
        var ability = request.ManualAbilityModifier!.Value;
        var total = request.D20Roll is int die
            ? checked(die + ability + competency!.Contribution + request.OtherModifier)
            : (int?)null;
        int? margin = total is int value && request.TargetDc is int dc
            ? checked(value - dc)
            : null;
        var outcome = margin is null ? "pending"
            : margin >= 0 ? "completed" : "failed";
        var consumes = margin is not null;
        return Finish("resolved", outcome, total, margin,
            margin is null ? null : margin >= 0,
            consumes, margin >= 0);

        NativeCraftingCheckResolution Finish(
            string status, string outcome, int? check, int? marginValue,
            bool? success, bool consumed, bool functional, string? error = null) =>
            new(1, catalog.RulesetId, catalog.Fingerprint, status,
                "manufacturing", "Manufacturing Check",
                "manual", competency?.DisplayName?.Trim() ?? "",
                true, competency?.IsQualified ?? false,
                competency is not null &&
                    !competency.IsQualified && !request.HasQualifiedGuidance
                    ? "disadvantage" : "normal",
                competency?.Contribution ?? 0,
                request.ManualAbilityModifier ?? 0,
                request.OtherModifier, request.D20Roll, check, request.TargetDc,
                success, outcome, marginValue, null, consumed, functional, error);
    }
}
