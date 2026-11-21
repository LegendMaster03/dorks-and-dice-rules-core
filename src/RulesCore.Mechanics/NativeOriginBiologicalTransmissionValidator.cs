using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Authenticated, evaluated biological transmission intent. Only the construction
/// engine can confirm whether a chosen contributor occurrence is actually
/// available. These IDs describe intended heredity, not effective or latent grants.
/// </summary>
internal sealed record NativeOriginBiologicalTransmissionReview(
    bool Explicit,
    IReadOnlySet<string> CandidateIds,
    IReadOnlyList<NativeOriginContractIssue> Issues);

internal static class NativeOriginBiologicalTransmissionValidator
{
    private const string RuleId = "rule.creature-origination";

    public static NativeOriginBiologicalTransmissionReview Review(
        NativeOriginProposal proposal,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        if (proposal.BiologicalTransmission is null)
            return new(false, new HashSet<string>(StringComparer.Ordinal), []);

        var selected = new HashSet<string>(StringComparer.Ordinal);
        var issues = new List<NativeOriginContractIssue>();
        void Invalid(string code, string path, string detail) =>
            issues.Add(new(code, path, NativeOriginIssueSeverity.Invalid, detail));
        void Unresolved(string code, string path, string detail) =>
            issues.Add(new(code, path, NativeOriginIssueSeverity.Unresolved, detail));

        if (proposal.Intent != NativeOriginIntent.Mixed ||
            proposal.Method?.RuleId != RuleId ||
            proposal.Method.MethodId != "biological")
        {
            Invalid("biological-transmission-method-invalid", "biologicalTransmission",
                "An explicit biological transmission plan requires mixed biological origination.");
            return new(true, selected, issues);
        }
        if (proposal.BiologicalTransmission.CandidateIds is null)
        {
            Invalid("biological-transmission-list-missing", "biologicalTransmission.candidateIds",
                "An explicit plan needs a nonnull candidate list, which may be empty.");
            return new(true, selected, issues);
        }
        var candidates = (proposal.Candidates ?? [])
            .Where(x => x?.Id is not null)
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (var id in proposal.BiologicalTransmission.CandidateIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Invalid("biological-transmission-candidate-id-invalid",
                    "biologicalTransmission.candidateIds",
                    "A transmitted candidate ID must be nonempty.");
                continue;
            }
            if (!selected.Add(id))
            {
                Invalid("biological-transmission-candidate-duplicate",
                    "biologicalTransmission.candidateIds." + id,
                    "The same contributor offer cannot be transmitted twice by duplicating its ID.");
                continue;
            }
            if (!candidates.TryGetValue(id, out var candidate) ||
                candidate.ContributorId is null ||
                candidate.Identity is null ||
                candidate.Identity.Kind is not
                    (NativeOriginCharacteristicKind.Intrinsic or NativeOriginCharacteristicKind.Trait))
            {
                Invalid("biological-transmission-candidate-unavailable",
                    "biologicalTransmission.candidateIds." + id,
                    "A transmitted characteristic must refer to a real contributor-local offer.");
                continue;
            }

            var native = candidate.SelectionBasis?.Eligibility == NativeOriginEligibility.Eligible &&
                candidate.SelectionBasis.RuleReference == RuleId;
            var ruling = verified("candidate", id) ??
                verified("source", candidate.Identity.Occurrence.Source.Id);
            var dm = candidate.SelectionBasis?.Eligibility == NativeOriginEligibility.DmAuthorized &&
                candidate.SelectionBasis.TableDecisionId == ruling?.Id &&
                ruling?.EffectiveOutcome.ValueKind == JsonValueKind.Object &&
                ruling.EffectiveOutcome.TryGetProperty("eligibility", out var eligibility) &&
                eligibility.ValueKind == JsonValueKind.String &&
                eligibility.GetString() == "eligible";
            if (!native && !dm)
                Unresolved("biological-transmission-candidate-not-authorized",
                    "biologicalTransmission.candidateIds." + id,
                    "A contributor's offer needs native biological eligibility or an authenticated scoped table ruling.");
        }

        return new(true, selected, issues);
    }

    /// <summary>
    /// With no approved persistent latent-gene policy, every transmitted offer
    /// must actually participate in an effective inherent grant. Failed
    /// expression stays unresolved; it never silently becomes a heritable gene.
    /// </summary>
    public static IReadOnlyList<NativeOriginContractIssue> ReviewExpression(
        NativeOriginBiologicalTransmissionReview transmission,
        IReadOnlyList<NativeOriginEffectiveGrant> grants)
    {
        if (!transmission.Explicit) return [];

        var expressed = (grants ?? []).Where(x => x?.Synergy is null)
            .SelectMany(x => x.SourceCandidateIds)
            .ToHashSet(StringComparer.Ordinal);
        return transmission.CandidateIds
            .Where(id => !expressed.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new NativeOriginContractIssue(
                "biological-transmission-expression-unresolved",
                "biologicalTransmission.candidateIds." + id,
                NativeOriginIssueSeverity.Unresolved,
                "This proposed transmitted source is not represented in an effective inherent grant; no persistent latent policy is authorized."))
            .ToArray();
    }
}
