using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Finalizes a biological genotype independently of the effective grant set.
/// One gene identity can have multiple inherited source instances; only selected
/// manifestations that survive the ordinary whole-profile pipeline express.
/// No separate latent pool, dominance category or automatic expression exists.
/// </summary>
internal sealed class NativeOriginBiologicalInheritanceEvaluator(
    NativeCatalogSnapshot catalog)
{
    private const string RuleId = "rule.creature-origination";

    public (IReadOnlyList<NativeOriginBiologicalGeneInstance> Genes,
        IReadOnlyList<NativeOriginContractIssue> Issues,
        int? MaxInherited, int? MaxExpressed) Evaluate(
        NativeOriginProposal proposal,
        NativeOriginBiologicalTransmissionReview transmission,
        IReadOnlyList<NativeOriginEffectiveGrant> effective,
        IReadOnlySet<string> verifiedManifested,
        INativeOriginAuthority? authority,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        if (!transmission.Explicit ||
            proposal.Intent != NativeOriginIntent.Mixed ||
            proposal.Method.RuleId != RuleId ||
            proposal.Method.MethodId != "biological")
            return ([], [], null, null);

        var issues = new List<NativeOriginContractIssue>();
        void Issue(string code, string path, NativeOriginIssueSeverity severity, string message) =>
            issues.Add(new(code, path, severity, message));

        if (!catalog.TryGetById(RuleId, out var rule) || rule is null ||
            !rule.Definition.TryGetProperty("biologicalTransmission", out var policy) ||
            policy.ValueKind != JsonValueKind.Object ||
            !Int(policy, "maxInheritedInstancesPerKind", out var maxInherited) ||
            !Int(policy, "maxExpressedInstancesPerKind", out var maxExpressed) ||
            maxInherited <= 0 || maxExpressed <= 0 || maxExpressed > maxInherited)
        {
            Issue("biological-instance-policy-unavailable", "biologicalTransmission",
                NativeOriginIssueSeverity.Unresolved,
                "An authored positive inherited/expression capacity policy is required.");
            return ([], issues, null, null);
        }

        // A scoped, authenticated table rule may change the authored defaults.
        // A serialized number in a proposal is never enough to alter capacity.
        var overrideRule = verified("biological-inheritance", proposal.Id);
        if (overrideRule is not null)
        {
            if (!Int(overrideRule.EffectiveOutcome, "maxInheritedInstancesPerKind",
                    out var adjustedInherited) ||
                !Int(overrideRule.EffectiveOutcome, "maxExpressedInstancesPerKind",
                    out var adjustedExpressed) ||
                adjustedInherited <= 0 || adjustedExpressed <= 0 ||
                adjustedExpressed > adjustedInherited)
                Issue("biological-instance-policy-override-invalid",
                    "biologicalTransmission", NativeOriginIssueSeverity.Invalid,
                    "An authenticated capacity override needs positive, coherent limits.");
            else
            {
                maxInherited = adjustedInherited;
                maxExpressed = adjustedExpressed;
            }
        }

        var candidates = (proposal.Candidates ?? [])
            .Where(candidate => candidate?.Identity is not null &&
                !string.IsNullOrWhiteSpace(candidate.Id))
            .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var selected = transmission.CandidateIds
            .Where(candidates.ContainsKey)
            .Select(id => candidates[id])
            .Where(candidate => candidate.ContributorId is not null &&
                candidate.Identity.Kind is NativeOriginCharacteristicKind.Trait or
                    NativeOriginCharacteristicKind.Intrinsic)
            .ToArray();
        static string KindId(NativeOriginCandidate candidate) =>
            candidate.Identity.Kind == NativeOriginCharacteristicKind.Trait
                ? candidate.Identity.MechanicalDefinitionId ?? ""
                : candidate.Identity.IntrinsicSlotId ?? "";
        foreach (var group in selected.GroupBy(candidate =>
                     (candidate.Identity.Kind, Identity: KindId(candidate))))
            if (group.Count() > maxInherited)
                Issue("biological-inherited-capacity-exceeded",
                    "biologicalTransmission." + group.Key.Identity,
                    NativeOriginIssueSeverity.Conflict,
                    $"No more than {maxInherited} inherited instances of this gene kind are permitted.");

        // Only grants matching the same canonical mechanic count as an
        // expression; derived synergies and unrelated downstream grants do not.
        bool Matches(NativeOriginCandidate candidate, NativeOriginEffectiveGrant grant) =>
            grant.Synergy is null &&
            candidate.Identity.Kind == grant.Identity.Kind &&
            (candidate.Identity.Kind == NativeOriginCharacteristicKind.Trait
                ? candidate.Identity.MechanicalDefinitionId ==
                    grant.Identity.MechanicalDefinitionId
                : candidate.Identity.IntrinsicSlotId == grant.Identity.IntrinsicSlotId) &&
            grant.SourceCandidateIds.Contains(candidate.Id, StringComparer.Ordinal);

        var active = effective.Where(grant => grant?.Synergy is null &&
            selected.Any(candidate => Matches(candidate, grant))).ToArray();
        foreach (var group in active.GroupBy(grant =>
                     (grant.Identity.Kind, Identity:
                         grant.Identity.Kind == NativeOriginCharacteristicKind.Trait
                             ? grant.Identity.MechanicalDefinitionId
                             : grant.Identity.IntrinsicSlotId)))
            if (group.Count() > maxExpressed)
                Issue("biological-expressed-capacity-exceeded",
                    "biologicalTransmission." + group.Key.Identity,
                    NativeOriginIssueSeverity.Conflict,
                    $"No more than {maxExpressed} effective instances of this gene kind are permitted.");

        var configurations = new Dictionary<string, NativeOriginBiologicalGeneConfiguration>(
            StringComparer.Ordinal);
        foreach (var configuration in proposal.BiologicalTransmission?.LatentConfigurations ?? [])
        {
            if (configuration is null ||
                string.IsNullOrWhiteSpace(configuration.CandidateId) ||
                !configurations.TryAdd(configuration.CandidateId, configuration))
                Issue("biological-latent-configuration-duplicate",
                    "biologicalTransmission.latentConfigurations",
                    NativeOriginIssueSeverity.Invalid,
                    "Latent configurations require unique, nonempty candidate IDs.");
            else if (!transmission.CandidateIds.Contains(configuration.CandidateId))
                Issue("biological-latent-configuration-unselected",
                    "biologicalTransmission.latentConfigurations." + configuration.CandidateId,
                    NativeOriginIssueSeverity.Invalid,
                    "A configuration cannot introduce an unselected inherited gene.");
        }

        var genes = new List<NativeOriginBiologicalGeneInstance>();
        foreach (var candidate in selected.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var grants = active.Where(grant => Matches(candidate, grant)).ToArray();
            var expressed = grants.Length > 0;
            if (expressed && configurations.ContainsKey(candidate.Id))
            {
                Issue("biological-latent-configuration-expressed", candidate.Id,
                    NativeOriginIssueSeverity.Invalid,
                    "Active expression already supplies the configuration of this inherited instance.");
                continue;
            }
            var donor = (proposal.Contributors ?? []).FirstOrDefault(x =>
                x.Id == candidate.ContributorId);
            NativeOriginBiologicalGeneInstance? inheritedGene = null;
            NativeOriginEffectiveGrant? inheritedGrant = null;
            NativeOriginInheritedGeneEvidence? inheritedFrom = null;
            if (donor?.Source.Kind == NativeOriginSourceKind.ManifestedProfile &&
                verifiedManifested.Contains(donor.Id) &&
                donor.ManifestedProfile is { } parent &&
                donor.Source.Version is { } revision)
            {
                inheritedGene = (parent.BiologicalGenes ?? []).FirstOrDefault(gene =>
                    gene is not null &&
                    "nested:" + donor.Id + ":gene:" + gene.Id == candidate.Id);
                inheritedGrant = (parent.InherentGrants ?? []).FirstOrDefault(grant =>
                    grant is not null &&
                    "nested:" + donor.Id + ":" + grant.Id == candidate.Id);
                if (inheritedGene is not null || inheritedGrant is not null)
                    inheritedFrom = new NativeOriginInheritedGeneEvidence(
                        donor.Id, donor.Source.Id, revision,
                        inheritedGene?.Id ?? inheritedGrant!.Id,
                        inheritedGene is null);
            }

            // A verified donor's completed choices travel with its instance.
            // They cannot be silently reconfigured in a later generation.
            if (!expressed && (inheritedGene is not null || inheritedGrant is not null) &&
                configurations.ContainsKey(candidate.Id))
            {
                Issue("biological-inherited-gene-reconfiguration-unsupported",
                    candidate.Id, NativeOriginIssueSeverity.Invalid,
                    "A previous generation's genetic configuration cannot be changed by a latent choice claim.");
                continue;
            }
            // A newly selected native latent gene with an authored creation
            // choice needs an explicit, complete configuration.
            var choices = (expressed ? grants[0].ResolvedChoices :
                inheritedGene?.ResolvedChoices ?? inheritedGrant?.ResolvedChoices ??
                configurations.GetValueOrDefault(candidate.Id)?.Choices) ?? [];
            if (!expressed)
            {
                var requirements = candidate.RequiredChoices ?? [];
                if (inheritedGene is null && inheritedGrant is null &&
                    !ValidateChoices(candidate, requirements, choices, authority, Issue))
                    continue;
                // Authenticated parent configurations have already committed
                // their finite choices, which the deconstructor does not reopen.
                if (candidate.UnresolvedEvidence?.Count > 0)
                {
                    Issue("biological-latent-source-unresolved", candidate.Id,
                        NativeOriginIssueSeverity.Unresolved,
                        "Unresolved authoring evidence cannot become an inheritable gene.");
                    continue;
                }
            }

            // A recombined effective grant may depend on two donor instances.
            // Do not overwrite both distinct inherited configurations with
            // the one combined phenotype. A one-source expression can retain
            // its fully resolved current configuration.
            var simpleExpression = expressed && grants.Length == 1 &&
                grants[0].SourceCandidateIds.Count == 1;
            var value = simpleExpression ? grants[0].EffectiveValue :
                inheritedGene?.ConfiguredValue ?? inheritedGrant?.EffectiveValue ??
                candidate.OfferedValue;
            if (!expressed && value is null &&
                candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic)
            {
                foreach (var requirement in candidate.RequiredChoices ?? [])
                {
                    var choice = choices.FirstOrDefault(x =>
                        x.RequirementId == requirement.Id);
                    if (requirement.FiniteOptions is not null &&
                        choice?.SelectedOptionIds is { Count: 1 } options &&
                        requirement.FiniteOptions.Contains(options[0], StringComparer.Ordinal))
                    {
                        value = JsonSerializer.SerializeToElement(options[0]);
                        break;
                    }
                }
                if (value is null)
                {
                    Issue("biological-latent-intrinsic-value-incomplete", candidate.Id,
                        NativeOriginIssueSeverity.Incomplete,
                        "An inherited physical intrinsic needs a complete selected value.");
                    continue;
                }
            }
            genes.Add(new NativeOriginBiologicalGeneInstance(
                "gene:" + proposal.Id + ":" + candidate.Id, candidate.Id,
                candidate.Identity, value,
                simpleExpression ? grants[0].Parameters :
                    inheritedGene?.Parameters ?? inheritedGrant?.Parameters ??
                    candidate.OfferedParameters,
                choices, inheritedFrom));
        }

        return (genes, issues, maxInherited, maxExpressed);
    }

    private static bool Int(JsonElement source, string property, out int result)
    {
        result = 0;
        return source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out result);
    }

    private static bool ValidateChoices(
        NativeOriginCandidate candidate,
        IReadOnlyList<NativeOriginChoiceRequirement> requirements,
        IReadOnlyList<NativeOriginChoiceSelection> choices,
        INativeOriginAuthority? authority,
        Action<string, string, NativeOriginIssueSeverity, string> issue)
    {
        var valid = true;
        foreach (var choice in choices)
        {
            if (choice is null || !requirements.Any(req =>
                    req.Id == choice.RequirementId &&
                    req.Timing != NativeOriginChoiceTiming.OnUse) ||
                choices.Count(x => x is not null &&
                    x.RequirementId == choice.RequirementId) != 1)
            {
                issue("biological-latent-choice-invalid", candidate.Id,
                    NativeOriginIssueSeverity.Invalid,
                    "A latent gene can only retain unique authored persistent choices.");
                valid = false;
                continue;
            }
            var requirement = requirements.First(x => x.Id == choice.RequirementId);
            if (choice.SelectedOptionIds is null ||
                choice.SelectedOptionIds.Count < requirement.Minimum ||
                choice.SelectedOptionIds.Count > requirement.Maximum ||
                choice.SelectedOptionIds.Any(string.IsNullOrWhiteSpace) ||
                choice.SelectedOptionIds.Distinct(StringComparer.Ordinal).Count() !=
                    choice.SelectedOptionIds.Count ||
                requirement.FiniteOptions is not null &&
                    choice.SelectedOptionIds.Any(x =>
                        !requirement.FiniteOptions.Contains(x, StringComparer.Ordinal)) ||
                requirement.OptionCatalogId is not null &&
                    (string.IsNullOrWhiteSpace(choice.CatalogEligibilityEvidenceId) ||
                     authority is null ||
                     choice.SelectedOptionIds.Any(option =>
                         !authority.AuthorizesCatalogChoice(requirement.OptionCatalogId,
                             option, choice.CatalogEligibilityEvidenceId!))))
            {
                issue("biological-latent-choice-invalid", candidate.Id,
                    NativeOriginIssueSeverity.Invalid,
                    "The latent configuration fails authored bounds or trusted catalog eligibility.");
                valid = false;
            }
        }
        foreach (var requirement in requirements)
            if (requirement.Timing != NativeOriginChoiceTiming.OnUse &&
                requirement.Minimum > 0 &&
                !choices.Any(choice => choice is not null &&
                    choice.RequirementId == requirement.Id))
            {
                issue("biological-latent-choice-incomplete", candidate.Id,
                    NativeOriginIssueSeverity.Incomplete,
                    "Resolve required persistent choices before storing this latent gene.");
                valid = false;
            }
        return valid;
    }
}
