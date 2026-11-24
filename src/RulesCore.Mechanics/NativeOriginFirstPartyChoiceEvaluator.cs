using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Supplies first-party membership evidence for creation choices whose catalog
/// eligibility is fully decidable from the current immutable native snapshot.
/// This never authenticates a DM, awards a separately earned grant, or grants
/// access to unapproved content or character-specific feat prerequisites.
/// </summary>
public sealed class NativeOriginFirstPartyChoiceEvaluator(NativeCatalogSnapshot catalog)
{
    private readonly NativeOriginConstructionEngine _construction = new(catalog);
    private readonly NativeOriginSourceDeconstructor _sources = new(catalog);

    public NativeOriginConstructionResult Evaluate(NativeOriginProposal submitted,
        INativeOriginFeatChoiceAuthority? featAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(submitted);

        // No first-party choice evidence may be minted for mixed, custom, or
        // transformational constructions until their trusted sources exist.
        if (submitted.Intent != NativeOriginIntent.Direct)
            return _construction.Evaluate(new(submitted));

        var (discovered, _) = _sources.Deconstruct(submitted);
        return EvaluateChoices(new(submitted,
            TrustedScope: new("first-party-catalog", catalog.RulesetId)),
            discovered, featAuthority, null);
    }

    /// <summary>
    /// Verified mixed creation uses exactly the same catalog predicates and
    /// construction engine as direct creation. Discover against independently
    /// loaded parent revisions before normalizing any new selections. Parent
    /// snapshots are never taken from the client or reopened as fresh menus.
    /// </summary>
    public NativeOriginConstructionResult EvaluateVerifiedMixed(
        NativeOriginConstructionRequest request,
        INativeOriginManifestedContributorAuthority manifestedAuthority,
        INativeOriginFeatChoiceAuthority? featAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(manifestedAuthority);
        if (request.Proposal.Intent != NativeOriginIntent.Mixed || request.TrustedScope is null)
            throw new ArgumentException("A scoped mixed creation request is required.", nameof(request));
        var discovery = _construction.Evaluate(request,
            manifestedContributorAuthority: manifestedAuthority);
        return EvaluateChoices(request, discovery.EvaluatedProposal, featAuthority, manifestedAuthority);
    }

    private NativeOriginConstructionResult EvaluateChoices(
        NativeOriginConstructionRequest request, NativeOriginProposal discovered,
        INativeOriginFeatChoiceAuthority? featAuthority,
        INativeOriginManifestedContributorAuthority? manifestedAuthority)
    {
        var submitted = request.Proposal;
        var candidates = discovered.Candidates.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var proofId = ProofForCatalog(catalog);
        // Feat proofs are minted only after independently verifying the
        // actual candidate/feat pair. They are scoped to this evaluation.
        var approvedFeatProofs = new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyList<NativeOriginChoiceSelection>? Normalize(
            string candidateId, IReadOnlyList<NativeOriginChoiceSelection>? choices)
        {
            if (choices is null || choices.Any(choice => choice is null) ||
                !candidates.TryGetValue(candidateId, out var candidate) ||
                candidate.SelectionBasis.Eligibility != NativeOriginEligibility.Eligible ||
                candidate.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
                (submitted.Intent == NativeOriginIntent.Direct && candidate.ContributorId is not null))
                return choices;

            var requirements = (candidate.RequiredChoices ?? []).ToDictionary(x => x.Id, StringComparer.Ordinal);
            return choices.Select(choice =>
            {
                if (!requirements.TryGetValue(choice.RequirementId, out var req) ||
                    req.Timing != NativeOriginChoiceTiming.Creation || req.OptionCatalogId is null)
                    return choice;
                if (choice.SelectedOptionIds is null ||
                    choice.SelectedOptionIds.Count < req.Minimum ||
                    choice.SelectedOptionIds.Count > req.Maximum ||
                    choice.SelectedOptionIds.Distinct(StringComparer.Ordinal).Count() != choice.SelectedOptionIds.Count ||
                    !choice.SelectedOptionIds.All(id => NativeOriginChoiceCatalog.IsMember(catalog, req.OptionCatalogId, id)))
                    return choice with { CatalogEligibilityEvidenceId = null };
                if (req.OptionCatalogId == "feat:origin")
                {
                    if (choice.SelectedOptionIds.Count != 1 ||
                        featAuthority?.Authorizes(candidate, choice.SelectedOptionIds[0]) != true)
                        return choice with { CatalogEligibilityEvidenceId = null };
                    var featProof = proofId + ":feat:" + candidate.Id;
                    approvedFeatProofs[featProof] = choice.SelectedOptionIds[0];
                    return choice with { CatalogEligibilityEvidenceId = featProof };
                }
                return choice with { CatalogEligibilityEvidenceId = proofId };
            }).ToArray();
        }

        var normalized = (submitted.Manifestations ?? []).Select(manifest =>
            manifest with { Choices = Normalize(manifest.CandidateId, manifest.Choices) }).ToArray();
        var transmission = submitted.BiologicalTransmission;
        if (transmission?.LatentConfigurations is { } configurations)
            transmission = transmission with
            {
                LatentConfigurations = configurations.Select(configuration => configuration with
                { Choices = Normalize(configuration.CandidateId, configuration.Choices) }).ToArray()
            };

        // The catalog adapter does not authorize table decisions. Keep the
        // original character scope, allocation gate and independently
        // verified contributor authority for the actual construction.
        return _construction.Evaluate(request with
        {
            Proposal = submitted with { Manifestations = normalized, BiologicalTransmission = transmission }
        }, new NativeCatalogOnlyAuthority(catalog, proofId, approvedFeatProofs, featAuthority), manifestedAuthority);
    }

    private static string ProofForCatalog(NativeCatalogSnapshot snapshot) =>
        "native-catalog:" + snapshot.RulesetId + ":" + snapshot.Fingerprint;

    private sealed class NativeCatalogOnlyAuthority(
        NativeCatalogSnapshot catalog, string expectedProofId,
        IReadOnlyDictionary<string, string> approvedFeatProofs,
        INativeOriginFeatChoiceAuthority? featAuthority) : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope trustedScope) => false;

        public bool AuthorizesInheritedFeat(NativeOriginCandidate candidate, string featId) =>
            NativeOriginChoiceCatalog.IsMember(catalog, "feat:origin", featId) &&
            featAuthority?.Authorizes(candidate, featId) == true;

        public bool AuthorizesCatalogChoice(
            string catalogId, string optionId, string evidenceId) =>
            NativeOriginChoiceCatalog.IsMember(catalog, catalogId, optionId) &&
            (catalogId == "feat:origin"
                ? approvedFeatProofs.TryGetValue(evidenceId, out var featId) && featId == optionId
                : evidenceId == expectedProofId);
    }
}
