using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Read-only projection of already selected native Tremorsense characteristics.
/// Each independent grant retains its entire authored effect, including contact
/// limits, activation, duration, and the source of any use allowance. It does
/// not activate a sense, spend uses, merge modes, or authorize inheritance.
/// </summary>
public static class NativeOriginTremorsenseProjector
{
    public sealed record GrantedSense(
        string GrantId, string DefinitionId, string SourceId, string OccurrencePath,
        IReadOnlyList<string> SourceCandidateIds, int RangeFeet, string Activation,
        string? RequiresContact, IReadOnlyList<string> RequiresSharedContact,
        int? DurationMinutes, string? UsesFormula, string? Recharge,
        string? ResourcePoolOwnerGrantId, JsonElement CompleteEffect);

    public sealed record View(
        string Status, IReadOnlyList<GrantedSense> Senses,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true);

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) =>
        ProjectCore(catalog, result, approvedMixed: false);

    public static View ProjectApprovedMixed(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) =>
        ProjectCore(catalog, result, approvedMixed: true);

    private static View ProjectCore(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result, bool approvedMixed)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(result);
        var foundation = approvedMixed
            ? NativeOriginApprovedMixedIntrinsicProjector.Project(result)
            : NativeOriginIntrinsicProjector.Project(result);
        if (foundation.Status != "resolved" || result.Profile is not { } profile)
            return Unresolved(approvedMixed
                ? "mixed-native-foundation-unverified"
                : "direct-native-foundation-unverified");

        var senses = new List<GrantedSense>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in profile.Underlying.InherentGrants)
        {
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
                continue;

            var id = grant.Identity.MechanicalDefinitionId;
            NativeRuleEntity? definition = null;
            if (id is not null)
                catalog.TryGetById(id, out definition);
            var hasDefinition = definition is not null &&
                definition.Category == "origin-trait" &&
                definition.Definition.TryGetProperty("trait", out var trait) &&
                trait.ValueKind == JsonValueKind.Object;
            var hasEffectiveSense = IsTremorsense(grant.EffectiveValue);
            var hasCanonicalSense = hasDefinition &&
                IsTremorsense(definition!.Definition.GetProperty("trait"));
            if (!hasEffectiveSense && !hasCanonicalSense)
                continue;

            // Both directions matter: an altered saved value may not suppress
            // an authored sense, or invent one on an unrelated/unknown trait.
            if (!hasDefinition || !hasCanonicalSense || !hasEffectiveSense ||
                id is null || !seen.Add(id) ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } actual ||
                !JsonElement.DeepEquals(
                    definition!.Definition.GetProperty("trait"), actual) ||
                grant.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                !catalog.TryGetById(grant.Identity.Occurrence.Source.Id, out var source) ||
                source is null || source.Category is not ("species" or "ancestry") ||
                !VerifiedGrant(grant))
                return Unresolved("tremorsense-grant-not-verified:" + (id ?? grant.Id));

            var canonical = definition!.Definition.GetProperty("trait");
            // Neither authored Tremorsense mode has configurable bindings or
            // shared-use dependencies. A saved extra parameter/owner needs a
            // separate native rule review, not a silently presumed pool.
            if (grant.Parameters is { Count: > 0 } ||
                grant.ResourceOwners is { Count: > 0 })
                return Unresolved("tremorsense-parameter-or-resource-owner-unrecognized:" + id);
            if (!canonical.TryGetProperty("rangeFeet", out var distance) ||
                distance.ValueKind != JsonValueKind.Number ||
                !distance.TryGetInt32(out var feet) || feet is <= 0 or > 1_000_000 ||
                !canonical.TryGetProperty("activation", out var activation) ||
                activation.ValueKind != JsonValueKind.String)
                return Unresolved("tremorsense-grammar-unrecognized:" + id);

            var mode = activation.GetString();
            string? contact = null;
            string[] shared = [];
            int? duration = null;
            string? usesFormula = null;
            string? recharge = null;

            if (mode == "passive")
            {
                if (!canonical.TryGetProperty("requiresSharedContact", out var materials) ||
                    materials.ValueKind != JsonValueKind.Array ||
                    materials.GetArrayLength() == 0 ||
                    materials.EnumerateArray().Any(x =>
                        x.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(x.GetString())) ||
                    canonical.TryGetProperty("requiresContact", out _) ||
                    canonical.TryGetProperty("durationMinutes", out _) ||
                    canonical.TryGetProperty("uses", out _))
                    return Unresolved("tremorsense-passive-grammar-unrecognized:" + id);
                shared = materials.EnumerateArray().Select(x => x.GetString()!).ToArray();
            }
            else if (mode == "bonus-action")
            {
                if (!canonical.TryGetProperty("requiresContact", out var surface) ||
                    surface.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(surface.GetString()) ||
                    !canonical.TryGetProperty("durationMinutes", out var time) ||
                    time.ValueKind != JsonValueKind.Number ||
                    !time.TryGetInt32(out var minutes) ||
                    minutes is <= 0 or > 1_000_000 ||
                    !canonical.TryGetProperty("uses", out var uses) ||
                    uses.ValueKind != JsonValueKind.Object ||
                    !uses.TryGetProperty("value", out var amount) ||
                    amount.ValueKind != JsonValueKind.String ||
                    amount.GetString() != "proficiency-bonus" ||
                    !uses.TryGetProperty("recharge", out var rest) ||
                    rest.ValueKind != JsonValueKind.String ||
                    rest.GetString() != "long-rest" ||
                    canonical.TryGetProperty("requiresSharedContact", out _))
                    return Unresolved("tremorsense-activated-grammar-unrecognized:" + id);

                contact = surface.GetString();
                duration = minutes;
                usesFormula = amount.GetString();
                recharge = rest.GetString();
            }
            else
                return Unresolved("tremorsense-activation-unrecognized:" + id);

            var occurrence = grant.Identity.Occurrence;
            senses.Add(new(grant.Id, id, occurrence.Source.Id,
                occurrence.OccurrencePath, grant.SourceCandidateIds.ToArray(),
                feet, mode!, contact, shared, duration, usesFormula, recharge,
                usesFormula is null ? null : grant.Id, canonical.Clone()));
        }

        // Distinct native definitions can coexist only when construction has
        // already explicitly authorized them. No maximum range or common use
        // pool is inferred from their common sense name.
        return new(senses.Count == 0 ? "none" : "resolved",
            senses.OrderBy(x => x.DefinitionId, StringComparer.Ordinal)
                .ThenBy(x => x.GrantId, StringComparer.Ordinal).ToArray(), []);

        bool VerifiedGrant(NativeOriginEffectiveGrant grant)
        {
            if (approvedMixed)
                return NativeOriginApprovedMixedIntrinsicProjector
                    .HasVerifiedContributingEvidence(result, grant);
            return grant.Authorization?.Adjudication == NativeOriginAdjudication.Supported &&
                grant.Authorization.NativeRuleReference ==
                    grant.Identity.Occurrence.Source.Id &&
                grant.SourceCandidateIds.Count == 1 &&
                result.EvaluatedProposal.Candidates.Any(candidate =>
                    candidate.Id == grant.SourceCandidateIds[0] &&
                    candidate.ContributorId is null &&
                    candidate.Identity == grant.Identity &&
                    candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                    candidate.OfferedValue is { } offered &&
                    grant.EffectiveValue is { } effective &&
                    JsonElement.DeepEquals(offered, effective) &&
                    result.EvaluatedProposal.Manifestations.Count(manifest =>
                        manifest.CandidateId == candidate.Id) == 1);
        }
    }

    private static bool IsTremorsense(JsonElement? effect) =>
        effect is { ValueKind: JsonValueKind.Object } value &&
        value.TryGetProperty("sense", out var sense) &&
        sense.ValueKind == JsonValueKind.String &&
        sense.GetString() == "tremorsense";

    private static View Unresolved(string reason) =>
        new("unresolved", [], [reason]);
}
