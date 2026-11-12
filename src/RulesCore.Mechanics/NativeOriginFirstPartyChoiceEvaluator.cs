using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Supplies first-party membership evidence for creation choices whose catalog
/// eligibility is fully decidable from the current immutable native snapshot.
/// This never authenticates a DM, awards a separately earned grant, or grants
/// access to unimplemented choice catalogs (notably Origin Feats).
/// </summary>
public sealed class NativeOriginFirstPartyChoiceEvaluator(NativeCatalogSnapshot catalog)
{
    private readonly NativeOriginConstructionEngine _construction = new(catalog);
    private readonly NativeOriginSourceDeconstructor _sources = new(catalog);

    public NativeOriginConstructionResult Evaluate(NativeOriginProposal submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);

        // No first-party choice evidence may be minted for mixed, custom, or
        // transformational constructions until their trusted sources exist.
        if (submitted.Intent != NativeOriginIntent.Direct)
            return _construction.Evaluate(new(submitted));

        var (discovered, _) = _sources.Deconstruct(submitted);
        var candidates = discovered.Candidates.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var proofId = ProofForCatalog(catalog);
        var normalized = (submitted.Manifestations ?? []).Select(manifest =>
        {
            if (manifest.Choices is null ||
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
                            IsAuthoritativelyEligible(catalog, req.OptionCatalogId, id)))
                        return choice;
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
            new NativeCatalogOnlyAuthority(catalog, proofId));
    }

    private static string ProofForCatalog(NativeCatalogSnapshot snapshot) =>
        "native-catalog:" + snapshot.RulesetId + ":" + snapshot.Fingerprint;

    /// <summary>
    /// This is deliberately a closed set of *fully authored* catalog predicates.
    /// A missing feat category or a future source-specific prerequisite cannot
    /// be inferred from a general name/category match. New predicates require
    /// their own reviewed native eligibility rules and tests.
    /// </summary>
    private static bool IsAuthoritativelyEligible(NativeCatalogSnapshot snapshot,
        string catalogId, string optionId)
    {
        if (catalogId != "any-native-skill" ||
            !snapshot.TryGetById(optionId, out var option) || option is null ||
            option.Category != "skill")
            return false;

        // A native Skill with an explicit selectable=false is not a creation
        // choice. No local invented hierarchy/rank formula is applied here.
        var definition = option.Definition;
        return !definition.TryGetProperty("selectable", out var selectable) ||
            selectable.ValueKind != System.Text.Json.JsonValueKind.False;
    }

    private sealed class NativeCatalogOnlyAuthority(
        NativeCatalogSnapshot catalog, string expectedProofId) : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope trustedScope) => false;

        public bool AuthorizesCatalogChoice(
            string catalogId, string optionId, string evidenceId) =>
            evidenceId == expectedProofId &&
            IsAuthoritativelyEligible(catalog, catalogId, optionId);
    }
}
