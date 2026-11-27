using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// One biological contributor's offer of a canonical gene or physical intrinsic.
/// The candidate retains the complete native configuration and source occurrence.
/// A manifestation attempt is not a grant, nor proof of latent inheritance.
/// </summary>
public sealed record NativeOriginBiologicalOffer(
    NativeOriginCandidate Candidate,
    IReadOnlyList<string> AttemptedManifestationIds,
    IReadOnlyList<string> EffectiveGrantIds,
    bool ProposedForTransmission = false);

/// <summary>
/// Combines eligible occurrences of one mechanical identity without erasing
/// contributor-specific parameters or multiplying the resulting benefit.
/// Expressions refer only to grants surviving the universal reconciliation and
/// dependency closure; no extra characteristics are minted here.
/// </summary>
public sealed record NativeOriginBiologicalCharacteristicGroup(
    NativeOriginCharacteristicKind Kind,
    string CharacteristicId,
    IReadOnlyList<NativeOriginBiologicalOffer> Offers,
    IReadOnlyList<NativeOriginEffectiveGrant> Expressions,
    NativeOriginBiologicalCharacteristicPresentation? Presentation = null);

/// <summary>
/// Bounded player-facing metadata from the canonical native definition.
/// Recombination keys are authored permission evidence, not a proof that a
/// particular selected composition passes the construction reconciler.
/// </summary>
public sealed record NativeOriginBiologicalCharacteristicPresentation(
    string Name, string? Description, string? Category,
    IReadOnlyList<string> RecombinableParameters);

/// <summary>
/// Source-preserving Combine output for biological origination. Availability,
/// proposed inheritance, evaluated hereditary instances, and effective grants
/// remain independently represented. Latency is derived from the final grant
/// set rather than treated as a separate, assignable trait.
/// </summary>
/// <summary>
/// Expression is evaluated against effective grants; absence is the derived
/// latent state. Neither state is persisted as an independent gene property.
/// </summary>
public sealed record NativeOriginBiologicalInheritedInstance(
    NativeOriginBiologicalGeneInstance Gene,
    IReadOnlyList<string> ExpressionGrantIds)
{
    public bool IsLatent => ExpressionGrantIds.Count == 0;
}

/// <summary>
/// Read-only candidate pools for a native authored inherited interaction.
/// A full distinct input selection is still checked by the same evaluator,
/// including competing expressions, dependencies and allocation.
/// </summary>
public sealed record NativeOriginBiologicalInteractionInput(
    int Count, IReadOnlyList<string> CandidateIds);
public sealed record NativeOriginBiologicalInteractionOption(
    string RuleOwnerId, string InteractionId, string ResultTraitId, string ResultName,
    IReadOnlyList<NativeOriginBiologicalInteractionInput> Inputs);

public sealed record NativeOriginBiologicalCombination(
    string ProposalId,
    IReadOnlyList<NativeOriginBiologicalCharacteristicGroup> Characteristics,
    bool ExplicitTransmissionPlan = false,
    IReadOnlyList<NativeOriginBiologicalInheritedInstance>? InheritedInstances = null,
    int? MaxInheritedInstancesPerKind = null,
    int? MaxExpressedInstancesPerKind = null,
    IReadOnlyList<NativeOriginBiologicalInteractionOption>? InteractionOptions = null);

internal static class NativeOriginBiologicalCombinationProjector
{
    private const string RuleId = "rule.creature-origination";

    public static NativeOriginBiologicalCombination? Build(
        NativeOriginConstructionResult result, NativeCatalogSnapshot catalog,
        int? maxInherited, int? maxExpressed,
        IReadOnlyList<NativeOriginBiologicalInteractionOption>? interactionOptions)
    {
        var proposal = result.EvaluatedProposal;
        if (proposal.Intent != NativeOriginIntent.Mixed ||
            proposal.Method?.RuleId != RuleId ||
            proposal.Method.MethodId != "biological")
            return null;

        // The construction engine has already replaced untrusted claims with
        // catalog or authenticated table eligibility. A merely offered,
        // restricted, or unreviewed characteristic is not in this eligible pool.
        var offers = (proposal.Candidates ?? []).Where(candidate =>
            candidate?.ContributorId is not null &&
            candidate.Identity is not null &&
            (candidate.Identity.Kind == NativeOriginCharacteristicKind.Trait &&
                !string.IsNullOrWhiteSpace(candidate.Identity.MechanicalDefinitionId) ||
             candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                !string.IsNullOrWhiteSpace(candidate.Identity.IntrinsicSlotId)) &&
            (candidate.SelectionBasis?.Eligibility == NativeOriginEligibility.Eligible &&
                candidate.SelectionBasis.RuleReference == RuleId ||
             candidate.SelectionBasis?.Eligibility == NativeOriginEligibility.DmAuthorized))
            .ToArray();

        var attempts = (proposal.Manifestations ?? [])
            .Where(manifestation => manifestation is not null &&
                !string.IsNullOrWhiteSpace(manifestation.CandidateId))
            .GroupBy(manifestation => manifestation.CandidateId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyList<string>)group.Select(x => x.Id)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var effective = result.Profile?.Underlying.InherentGrants ?? [];
        var proposed = (proposal.BiologicalTransmission?.CandidateIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var groups = offers
            .GroupBy(candidate => (
                candidate.Identity.Kind,
                Id: candidate.Identity.Kind == NativeOriginCharacteristicKind.Trait
                    ? candidate.Identity.MechanicalDefinitionId!
                    : candidate.Identity.IntrinsicSlotId!))
            .OrderBy(group => group.Key.Kind)
            .ThenBy(group => group.Key.Id, StringComparer.Ordinal)
            .Select(group =>
            {
                var candidates = group.OrderBy(x => x.ContributorId, StringComparer.Ordinal)
                    .ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
                var ids = candidates.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                // Ordinary synergies are not gene expressions. Explicitly
                // selected inherited combinations express all their inputs
                // through one replacement phenotype, even if both were latent.
                var expressions = effective.Where(grant =>
                        grant.Synergy is null &&
                        grant.Identity.Kind == group.Key.Kind &&
                        (group.Key.Kind == NativeOriginCharacteristicKind.Trait
                            ? grant.Identity.MechanicalDefinitionId == group.Key.Id
                            : grant.Identity.IntrinsicSlotId == group.Key.Id) &&
                        grant.SourceCandidateIds.Any(ids.Contains) ||
                        grant.Synergy?.InheritedInputs?.Any(input =>
                            ids.Contains(input.Gene.SourceCandidateId)) == true)
                    .OrderBy(grant => grant.Id, StringComparer.Ordinal).ToArray();
                var candidateOffers = candidates.Select(candidate =>
                    new NativeOriginBiologicalOffer(
                        candidate,
                        attempts.GetValueOrDefault(candidate.Id) ?? [],
                        expressions.Where(grant =>
                            grant.Synergy?.InheritedInputs?.Any(input =>
                                input.Gene.SourceCandidateId == candidate.Id) == true ||
                            grant.Synergy is null &&
                                grant.SourceCandidateIds.Contains(candidate.Id,
                                    StringComparer.Ordinal))
                            .Select(grant => grant.Id).ToArray(),
                        proposed.Contains(candidate.Id)))
                    .ToArray();
                return new NativeOriginBiologicalCharacteristicGroup(
                    group.Key.Kind, group.Key.Id, candidateOffers, expressions,
                    Presentation(catalog, group.Key.Kind, group.Key.Id));
            })
            .ToArray();
        var inheritedInstances = (result.Profile?.Underlying.BiologicalGenes ?? [])
            .Select(gene => new NativeOriginBiologicalInheritedInstance(
                gene,
                effective.Where(grant =>
                    grant.Synergy?.InheritedInputs?.Any(input =>
                        input.Gene.Id == gene.Id) == true ||
                    grant.Synergy is null &&
                    grant.SourceCandidateIds.Contains(gene.SourceCandidateId,
                        StringComparer.Ordinal) &&
                    grant.Identity.Kind == gene.Identity.Kind &&
                    (gene.Identity.Kind == NativeOriginCharacteristicKind.Trait
                        ? grant.Identity.MechanicalDefinitionId ==
                            gene.Identity.MechanicalDefinitionId
                        : grant.Identity.IntrinsicSlotId ==
                            gene.Identity.IntrinsicSlotId))
                    .Select(grant => grant.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(instance => instance.Gene.Id, StringComparer.Ordinal)
            .ToArray();
        return new NativeOriginBiologicalCombination(proposal.Id, groups,
            proposal.BiologicalTransmission is not null, inheritedInstances,
            maxInherited, maxExpressed, interactionOptions);
    }

    // Do not expose the full native definition or internal authoring notes.
    // The canonical definition, not the submitted offer, owns these fields.
    private static NativeOriginBiologicalCharacteristicPresentation? Presentation(
        NativeCatalogSnapshot catalog, NativeOriginCharacteristicKind kind, string id)
    {
        if (kind != NativeOriginCharacteristicKind.Trait ||
            !catalog.TryGetById(id, out var entity) || entity is null ||
            entity.Category != "origin-trait" ||
            entity.Definition.ValueKind != JsonValueKind.Object ||
            !entity.Definition.TryGetProperty("trait", out var trait) ||
            trait.ValueKind != JsonValueKind.Object)
            return null;

        static string? Text(JsonElement source, string key, int maximum) =>
            source.TryGetProperty(key, out var item) &&
            item.ValueKind == JsonValueKind.String &&
            item.GetString() is { Length: > 0 } value && value.Length <= maximum &&
            !string.IsNullOrWhiteSpace(value) ? value : null;

        var name = Text(trait, "name", 160) ?? Text(entity.Definition, "name", 160);
        if (name is null) return null;
        string[] keys = [];
        if (entity.Definition.TryGetProperty("originComposition", out var composition) &&
            composition.ValueKind == JsonValueKind.Object &&
            composition.TryGetProperty("recombinableParameters", out var parameters) &&
            parameters.ValueKind == JsonValueKind.Array &&
            parameters.GetArrayLength() <= 32 &&
            parameters.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String &&
                x.GetString() is { Length: > 0 and <= 160 } value &&
                !string.IsNullOrWhiteSpace(value)))
            keys = parameters.EnumerateArray().Select(x => x.GetString()!)
                .Distinct(StringComparer.Ordinal).ToArray();

        // Presentation grouping is not a mechanic or inheritance rule.
        // Prefer explicit authored category; otherwise recognize typed fields
        // and leave unclassified definitions to the consumer's generic group.
        var category = Text(entity.Definition, "category", 160) ??
            (trait.TryGetProperty("sense", out _) ? "Senses" :
             trait.TryGetProperty("spell", out _) ? "Innate magic" :
             trait.TryGetProperty("damageResistance", out _) ? "Defenses" :
             trait.TryGetProperty("activation", out _) ? "Active abilities" : null);
        return new(name, Text(trait, "description", 4000) ??
            Text(entity.Definition, "description", 4000), category, keys);
    }
}
