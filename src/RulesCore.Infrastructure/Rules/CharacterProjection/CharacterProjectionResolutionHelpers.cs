using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Rules.CharacterProjection;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Shared mechanics-resolution helpers used by focused Character projection resolver modules.
/// </summary>
internal static class CharacterProjectionResolutionHelpers
{
    internal static bool TryResolvedNumeric(
        CharacterProjectionContext context,
        string key,
        out int value)
    {
        value = default;
        if (!context.Mechanics.TryGetValue(key, out var mechanic)
            || !string.Equals(
                mechanic.State,
                CharacterResolutionStates.Resolved,
                StringComparison.Ordinal)
            || mechanic.NumericValue is not int numeric)
        {
            return false;
        }

        value = numeric;
        return true;
    }

    internal static CharacterResolvedMechanicView Unresolved(
        string key,
        string kind,
        string displayName,
        string state,
        IReadOnlyList<string>? missingInputs = null,
        IReadOnlyList<string>? missingCapabilities = null,
        IReadOnlyList<string>? requiredChoices = null,
        IReadOnlyList<string>? requiredRolls = null,
        CharacterMechanicProvenanceView? provenance = null) =>
        new(
            key,
            kind,
            displayName,
            state,
            null,
            null,
            null,
            missingInputs ?? [],
            missingCapabilities ?? [],
            requiredChoices ?? [],
            requiredRolls ?? [],
            [],
            provenance ?? CharacterProjectionContext.EmptyProvenance());

    internal static CharacterMechanicContributionView Contribution(
        string key,
        string label,
        int value) =>
        new(
            key,
            label,
            CharacterEffectOperations.Add,
            value,
            null,
            null,
            CharacterProjectionContext.EmptyProvenance());

    internal static CharacterMechanicProvenanceView EffectiveProvenance(
        ResolvedRuleCatalogItemView rule)
    {
        var attribution = new CharacterMechanicSourceAttributionView(
            rule.PackageKey,
            rule.PackageDisplayName,
            null,
            rule.SourceCode,
            rule.SourceRevisionNumber,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            false);
        return new CharacterMechanicProvenanceView([], [], [attribution]);
    }

    internal static CharacterMechanicProvenanceView Provenance(
        IReadOnlyList<CharacterMechanicSourceAttributionView> attributions) =>
        new(attributions, attributions, attributions);
}
