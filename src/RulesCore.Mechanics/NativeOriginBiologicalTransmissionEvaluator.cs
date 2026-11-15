using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Source-neutral, catalog-authored eligibility for intentional biological
/// inheritance. This evaluates offers, never grants or chooses traits. It does
/// not apply to plant, construct, spawning, custom or unauthenticated nested
/// contributors. Other biological characteristics remain table-authorized until
/// their complete transmission/allocation policies are authored.
/// </summary>
internal sealed class NativeOriginBiologicalTransmissionEvaluator(
    NativeCatalogSnapshot catalog)
{
    private const string RuleId = "rule.creature-origination";

    public NativeOriginProposal Evaluate(NativeOriginProposal proposal)
    {
        if (proposal.Intent != NativeOriginIntent.Mixed ||
            proposal.Method.RuleId != RuleId ||
            proposal.Method.MethodId != "biological" ||
            !catalog.TryGetById(RuleId, out var rule) ||
            rule is null || rule.Category != "rule" ||
            !rule.Definition.TryGetProperty("biologicalTransmission", out var policy) ||
            policy.ValueKind != JsonValueKind.Object ||
            !policy.TryGetProperty("heritableIntrinsicSlots", out var slots) ||
            slots.ValueKind != JsonValueKind.Array)
            return proposal;

        var intrinsicSlots = slots.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        return proposal with
        {
            Candidates = (proposal.Candidates ?? []).Select(candidate =>
            {
                if (candidate is null || candidate.ContributorId is null ||
                    candidate.SelectionBasis?.Eligibility is not
                        (NativeOriginEligibility.Unreviewed or NativeOriginEligibility.Restricted))
                    return candidate;

                var contributor = proposal.Contributors?.FirstOrDefault(x =>
                    x.Id == candidate.ContributorId);
                if (contributor?.Role.Kind != NativeOriginInputKind.BiologicalParent ||
                    contributor.Source.Kind != NativeOriginSourceKind.Native ||
                    !catalog.TryGetById(contributor.Source.Id, out var parent) ||
                    parent is null || parent.Category is not ("species" or "ancestry") ||
                    !SupportsBiologicalOrigination(parent) ||
                    parent.Definition.TryGetProperty("selectable", out var selectable) &&
                    selectable.ValueKind == JsonValueKind.False ||
                    parent.Definition.TryGetProperty("acquisitionMethods", out var acquisition) &&
                    acquisition.ValueKind == JsonValueKind.Array &&
                    !acquisition.EnumerateArray().Any(x =>
                        x.ValueKind == JsonValueKind.String &&
                        x.GetString() == "biological") ||
                    candidate.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                    !catalog.TryGetById(candidate.Identity.Occurrence.Source.Id, out var source) ||
                    source is null || source.Category is not ("species" or "ancestry") ||
                    source.Definition.TryGetProperty("acquisitionMethods", out var sourceAcquisition) &&
                    sourceAcquisition.ValueKind == JsonValueKind.Array &&
                    !sourceAcquisition.EnumerateArray().Any(x =>
                        x.ValueKind == JsonValueKind.String &&
                        x.GetString() == "biological"))
                    return candidate;

                // A selectable leaf can legitimately inherit a nonselectable
                // shared Species or Ancestry-family definition (Dragonborn's
                // base Species is one example). The *donor* must be selectable,
                // and any source-specific acquisition restriction still applies.
                var eligible = candidate.Identity.Kind switch
                {
                    NativeOriginCharacteristicKind.Intrinsic =>
                        candidate.Identity.IntrinsicSlotId is { } slot &&
                        intrinsicSlots.Contains(slot),
                    NativeOriginCharacteristicKind.Trait =>
                        IsAuthoredBiologicalTrait(candidate),
                    _ => false
                };
                return eligible ? candidate with
                {
                    SelectionBasis = new(NativeOriginEligibility.Eligible,
                        RuleReference: RuleId,
                        Explanation: "Explicitly player-selected biological transmission.")
                } : candidate;
            }).ToArray()
        };
    }

    /// <summary>
    /// Origination-method compatibility is checked against the actual selected
    /// donor, not assumed from the offspring or the BiologicalParent role.
    /// A specific Species/Ancestry method list overrides the inherited
    /// creature-type default; an absent list inherits through the native
    /// ancestry tree. This keeps future Construct assembly definitions outside
    /// biological inheritance unless their own content expressly permits it.
    /// </summary>
    private bool SupportsBiologicalOrigination(NativeRuleEntity donor)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        NativeRuleEntity? entity = donor;
        while (entity is not null && visited.Add(entity.Id) && visited.Count <= 64)
        {
            if (entity.Definition.TryGetProperty("originationMethods", out var authored))
                return authored.ValueKind == JsonValueKind.Array &&
                    authored.EnumerateArray().Any(item =>
                        item.ValueKind == JsonValueKind.String &&
                        item.GetString() == "biological");

            if (entity.Definition.TryGetProperty("creatureType", out var type) &&
                type.ValueKind == JsonValueKind.String)
            {
                var id = type.GetString();
                if (string.IsNullOrWhiteSpace(id)) return false;
                if (!id.StartsWith("creature-type.", StringComparison.Ordinal))
                    id = "creature-type." + id;
                return new NativeOriginMethodAvailabilityEvaluator(catalog)
                    .Resolve(id, new(RuleId, "biological")).Status ==
                    NativeOriginMethodDefaultStatus.Available;
            }

            if (entity.Category != "ancestry" ||
                !entity.Definition.TryGetProperty("parent", out var parent) ||
                parent.ValueKind != JsonValueKind.String ||
                !catalog.TryGetById(parent.GetString()!, out var next) ||
                next is null || next.Category is not ("species" or "ancestry"))
                return false;
            entity = next;
        }
        return false;
    }

    private bool IsAuthoredBiologicalTrait(NativeOriginCandidate candidate)
    {
        var id = candidate.Identity.MechanicalDefinitionId;
        return id is not null && catalog.TryGetById(id, out var definition) &&
            definition is not null && definition.Category == "origin-trait" &&
            definition.Definition.TryGetProperty("inheritanceMethods", out var methods) &&
            methods.ValueKind == JsonValueKind.Array &&
            methods.EnumerateArray().Any(x =>
                x.ValueKind == JsonValueKind.String && x.GetString() == "biological");
    }
}
