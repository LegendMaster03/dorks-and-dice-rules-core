namespace RulesCore.Mechanics;

/// <summary>
/// One biological contributor's offer of a canonical gene or physical intrinsic.
/// The candidate retains the complete native configuration and source occurrence.
/// A manifestation attempt is not a grant, nor proof of latent inheritance.
/// </summary>
public sealed record NativeOriginBiologicalOffer(
    NativeOriginCandidate Candidate,
    IReadOnlyList<string> AttemptedManifestationIds,
    IReadOnlyList<string> EffectiveGrantIds);

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
/// Read-only evidence for the Combine operation of biological origination.
/// This does not decide which genes were genetically transmitted, whether an
/// unexpressed gene can persist, or how many genes a descendant may inherit.
/// Those are separate procedure policies, not inferred from candidate offers.
/// </summary>
public sealed record NativeOriginBiologicalCombination(
    string ProposalId,
    IReadOnlyList<NativeOriginBiologicalCharacteristicGroup> Characteristics);

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
                var expressions = effective.Where(grant =>
                        grant.SourceCandidateIds.Any(ids.Contains))
                    .OrderBy(grant => grant.Id, StringComparer.Ordinal).ToArray();
                var candidateOffers = candidates.Select(candidate =>
                    new NativeOriginBiologicalOffer(
                        candidate,
                        attempts.GetValueOrDefault(candidate.Id) ?? [],
                        expressions.Where(grant =>
                            grant.SourceCandidateIds.Contains(candidate.Id, StringComparer.Ordinal))
                            .Select(grant => grant.Id).ToArray()))
                    .ToArray();
                return new NativeOriginBiologicalCharacteristicGroup(
                    group.Key.Kind, group.Key.Id, candidateOffers, expressions);
            })
            .ToArray();
        return new NativeOriginBiologicalCombination(proposal.Id, groups);
    }
}
