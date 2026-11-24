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
        var candidates = discovered.Candidates.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var proofId = ProofForCatalog(catalog);
        // Feat proofs are minted only after independently verifying the
        // actual candidate/feat pair. They are scoped to this evaluation.
        var approvedFeatProofs = new Dictionary<string, string>(StringComparer.Ordinal);
        var normalized = (submitted.Manifestations ?? []).Select(manifest =>
        {
            if (manifest.Choices is null ||
                // Never derive eligibility from malformed records. Preserve
                // them unchanged so the construction validator can report
                // the invalid selection without awarding a source grant.
                manifest.Choices.Any(choice => choice is null) ||
                !candidates.TryGetValue(manifest.CandidateId, out var candidate) ||
                candidate.ContributorId is not null ||
                candidate.SelectionBasis.Eligibility != NativeOriginEligibility.Eligible ||
                candidate.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native)
                return manifest;

            var requirements = (candidate.RequiredChoices ?? [])
                .ToDictionary(x => x.Id, StringComparer.Ordinal);
            return manifest with
            {
                Choices = manifest.Choices.Select(choice =>
                {
                    if (!requirements.TryGetValue(choice.RequirementId, out var req) ||
                        req.Timing != NativeOriginChoiceTiming.Creation ||
                        req.OptionCatalogId is null ||
                        choice.SelectedOptionIds is null ||
                        choice.SelectedOptionIds.Count < req.Minimum ||
                        choice.SelectedOptionIds.Count > req.Maximum ||
                        choice.SelectedOptionIds.Distinct(StringComparer.Ordinal).Count() !=
                            choice.SelectedOptionIds.Count ||
                        !choice.SelectedOptionIds.All(id =>
                            NativeOriginChoiceCatalog.IsMember(catalog, req.OptionCatalogId, id)))
                        return choice with { CatalogEligibilityEvidenceId = null };
                    if (req.OptionCatalogId == "feat:origin")
                    {
                        // The only current native feat-choice grants one feat.
                        // Do not infer prerequisites or repeatability.
                        if (choice.SelectedOptionIds.Count != 1 ||
                            featAuthority?.Authorizes(candidate, choice.SelectedOptionIds[0]) != true)
                            return choice with { CatalogEligibilityEvidenceId = null };
                        var featProof = proofId + ":feat:" + candidate.Id;
                        approvedFeatProofs[featProof] = choice.SelectedOptionIds[0];
                        return choice with { CatalogEligibilityEvidenceId = featProof };
                    }
                    return choice with { CatalogEligibilityEvidenceId = proofId };
                }).ToArray()
            };
        }).ToArray();

        // The scope is an internal sentinel for a public first-party catalog,
        // NOT a campaign scope. Authorizes(...) always refuses table decisions.
        // The stateless ingress must independently reject all table decisions.
        return _construction.Evaluate(
            new(submitted with { Manifestations = normalized },
                TrustedScope: new("first-party-catalog", catalog.RulesetId)),
            new NativeCatalogOnlyAuthority(catalog, proofId, approvedFeatProofs));
    }

    private static string ProofForCatalog(NativeCatalogSnapshot snapshot) =>
        "native-catalog:" + snapshot.RulesetId + ":" + snapshot.Fingerprint;

    private sealed class NativeCatalogOnlyAuthority(
        NativeCatalogSnapshot catalog, string expectedProofId,
        IReadOnlyDictionary<string, string> approvedFeatProofs) : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope trustedScope) => false;

        public bool AuthorizesCatalogChoice(
            string catalogId, string optionId, string evidenceId) =>
            NativeOriginChoiceCatalog.IsMember(catalog, catalogId, optionId) &&
            (catalogId == "feat:origin"
                ? approvedFeatProofs.TryGetValue(evidenceId, out var featId) && featId == optionId
                : evidenceId == expectedProofId);
    }
}
