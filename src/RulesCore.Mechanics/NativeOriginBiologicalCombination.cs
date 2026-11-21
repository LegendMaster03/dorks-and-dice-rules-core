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
    IReadOnlyList<NativeOriginEffectiveGrant> Expressions);

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

public sealed record NativeOriginBiologicalCombination(
    string ProposalId,
    IReadOnlyList<NativeOriginBiologicalCharacteristicGroup> Characteristics,
    bool ExplicitTransmissionPlan = false,
    IReadOnlyList<NativeOriginBiologicalInheritedInstance>? InheritedInstances = null);

internal static class NativeOriginBiologicalCombinationProjector
{
    private const string RuleId = "rule.creature-origination";

    public static NativeOriginBiologicalCombination? Build(
        NativeOriginConstructionResult result)
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
            .Where(manifestation => manifestation is not null)
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
                // A derived synergy may cite one of these offers as an input,
                // but it is not an expression of the source gene itself.
                var expressions = effective.Where(grant =>
                        grant.Synergy is null &&
                        grant.Identity.Kind == group.Key.Kind &&
                        (group.Key.Kind == NativeOriginCharacteristicKind.Trait
                            ? grant.Identity.MechanicalDefinitionId == group.Key.Id
                            : grant.Identity.IntrinsicSlotId == group.Key.Id) &&
                        grant.SourceCandidateIds.Any(ids.Contains))
                    .OrderBy(grant => grant.Id, StringComparer.Ordinal).ToArray();
                var candidateOffers = candidates.Select(candidate =>
                    new NativeOriginBiologicalOffer(
                        candidate,
                        attempts.GetValueOrDefault(candidate.Id) ?? [],
                        expressions.Where(grant =>
                            grant.SourceCandidateIds.Contains(candidate.Id, StringComparer.Ordinal))
                            .Select(grant => grant.Id).ToArray(),
                        proposed.Contains(candidate.Id)))
                    .ToArray();
                return new NativeOriginBiologicalCharacteristicGroup(
                    group.Key.Kind, group.Key.Id, candidateOffers, expressions);
            })
            .ToArray();
        var inheritedInstances = (result.Profile?.Underlying.BiologicalGenes ?? [])
            .Select(gene => new NativeOriginBiologicalInheritedInstance(
                gene,
                effective.Where(grant =>
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
            proposal.BiologicalTransmission is not null, inheritedInstances);
    }
}
