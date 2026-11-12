using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Executes the deliberately narrow first-party passive vision effect from
/// the authored rule.low-light-vision, only when a completed direct Origin
/// actually grants the matching native trait occurrence. This does not imply
/// Darkvision, a range limit, ordinary visibility, or other Species senses.
/// </summary>
public static class NativeOriginVisionProjector
{
    public sealed record GrantedVision(
        string GrantId, string SourceId, string OccurrencePath,
        string RuleId, string ActualLightLevel, string PerceivedLightLevel);

    public sealed record View(
        string Status, GrantedVision? LowLightVision,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true);

    public sealed record LightPerception(string Status, string? PerceivedLightLevel);

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(result);

        // The baseline projector already rejects incomplete choices, mixed
        // inputs, active transformations, independently asserted grants and
        // post-origination reconstruction. Its result is not a permission for
        // this separate trait: we verify the individual occurrence as well.
        if (NativeOriginIntrinsicProjector.Project(result).Status != "resolved" ||
            result.Profile is not { } profile)
            return new("unresolved", null, ["origin-not-a-supported-direct-baseline"]);

        var candidates = profile.Current.InherentGrants
            .Where(grant => grant.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
                (grant.Identity.MechanicalDefinitionId?.EndsWith(
                    "#trait:low-light-vision", StringComparison.Ordinal) == true ||
                 IsLowLightRule(grant.EffectiveValue)))
            .ToArray();
        if (candidates.Length == 0)
            return new("none", null, []);

        // There is no approved stacking or upgrade rule for multiple low-light
        // occurrences, including an Ancestry that offers the same trait twice.
        if (candidates.Length != 1)
            return new("unresolved", null, ["duplicate-low-light-vision"]);

        var trait = candidates[0];
        var occurrence = trait.Identity.Occurrence;
        if (occurrence.Source.Kind != NativeOriginSourceKind.Native ||
            !catalog.TryGetById(occurrence.Source.Id, out var species) ||
            species is null || species.Category is not ("species" or "ancestry") ||
            trait.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
            trait.SourceCandidateIds.Count != 1 ||
            !result.EvaluatedProposal.Candidates.Any(offered =>
                offered.Id == trait.SourceCandidateIds[0] &&
                offered.ContributorId is null &&
                offered.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                offered.Identity == trait.Identity &&
                offered.OfferedValue is { } offeredValue &&
                trait.EffectiveValue is { } grantedValue &&
                JsonElement.DeepEquals(offeredValue, grantedValue)) ||
            trait.EffectiveValue is not { ValueKind: JsonValueKind.Object } value ||
            !IsLowLightRule(value) ||
            !OnlyTraitPresentationAndRuleFields(value) ||
            !catalog.TryGetById("rule.low-light-vision", out var rule) ||
            rule is null || rule.Category != "rule" ||
            !TryReadAuthoredEffect(rule.Definition,
                out var actual, out var perceived))
            return new("unresolved", null, ["low-light-vision-not-verified"]);

        return new("resolved",
            new(trait.Id, occurrence.Source.Id, occurrence.OccurrencePath,
                "rule.low-light-vision", actual!, perceived!), []);
    }

    /// <summary>
    /// A three-state, explicitly scoped environmental test; it does not infer
    /// distance, line of sight or whether a creature is otherwise visible.
    /// Darkness is unchanged even when low-light vision is active.
    /// </summary>
    public static LightPerception Perceive(View view, string actualLightLevel)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (actualLightLevel is not ("bright" or "dim" or "darkness") ||
            view.Status == "unresolved")
            return new("unresolved", null);
        if (view.LowLightVision is { } effect &&
            actualLightLevel == effect.ActualLightLevel)
            return new("resolved", effect.PerceivedLightLevel);
        return new("resolved", actualLightLevel);
    }

    private static bool IsLowLightRule(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Object } json &&
        json.TryGetProperty("id", out var name) &&
        name.ValueKind == JsonValueKind.String &&
        name.GetString() == "low-light-vision" &&
        json.TryGetProperty("rule", out var reference) &&
        reference.ValueKind == JsonValueKind.String &&
        reference.GetString() == "rule.low-light-vision";

    private static bool OnlyTraitPresentationAndRuleFields(JsonElement value) =>
        value.EnumerateObject().All(property =>
            property.Name is "id" or "name" or "description" or "rule");

    private static bool TryReadAuthoredEffect(
        JsonElement definition, out string? actual, out string? perceived)
    {
        actual = null;
        perceived = null;
        if (!definition.TryGetProperty("effects", out var effects) ||
            effects.ValueKind != JsonValueKind.Array ||
            effects.GetArrayLength() != 1)
            return false;
        var effect = effects[0];
        if (effect.ValueKind != JsonValueKind.Object ||
            effect.EnumerateObject().Count() != 2 ||
            !effect.TryGetProperty("actualLightLevel", out var start) ||
            !effect.TryGetProperty("perceivedLightLevel", out var end) ||
            start.ValueKind != JsonValueKind.String ||
            end.ValueKind != JsonValueKind.String ||
            start.GetString() != "dim" || end.GetString() != "bright")
            return false;
        actual = start.GetString();
        perceived = end.GetString();
        return true;
    }
}
