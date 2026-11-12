using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// One catalog-backed construction evaluator for direct, mixed, nested and table-authored
/// Origin proposals. No invocation executes a transformation or changes native content.
/// </summary>
public sealed class NativeOriginConstructionEngine(NativeCatalogSnapshot catalog)
{
    private readonly NativeOriginContractValidator _validator = new(catalog);
    private readonly NativeOriginSourceDeconstructor _deconstructor = new(catalog);
    private readonly NativeOriginReconciler _reconciler = new(catalog);

    public NativeOriginConstructionResult Evaluate(NativeOriginConstructionRequest request,
        INativeOriginAuthority? authority = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proposal);
        var (discovered, discoveryIssues) = _deconstructor.Deconstruct(request.Proposal);
        var issues = new List<NativeOriginContractIssue>(discoveryIssues);
        var candidates = discovered.Candidates.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var manifests = (discovered.Manifestations ?? []).ToList();
        var selected = new HashSet<string>(manifests.Select(x => x.CandidateId),
            StringComparer.Ordinal);

        // Only unambiguous directly selected native defaults can be manifested
        // automatically. A choice, a mixed contribution, or a restricted source
        // always needs its own actual selection and authorization.
        if (request.AutoManifestDirectDefaults && discovered.Intent == NativeOriginIntent.Direct)
        {
            foreach (var candidate in discovered.Candidates)
            {
                if (candidate.ContributorId is not null ||
                    candidate.SelectionBasis.Eligibility != NativeOriginEligibility.Eligible ||
                    selected.Contains(candidate.Id) ||
                    (candidate.RequiredChoices ?? []).Any(x =>
                        x.Timing == NativeOriginChoiceTiming.Creation))
                    continue;
                if (candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                    candidate.OfferedValue is null) continue;
                manifests.Add(new("auto:" + candidate.Id, candidate.Id,
                    candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                        ? candidate.OfferedValue : null));
                selected.Add(candidate.Id);
            }
        }

        // A finite choice for an intrinsic slot is the selected intrinsic value,
        // not a separate mechanical source. Preserve the explicit choice record.
        for (var i = 0; i < manifests.Count; i++)
        {
            var manifest = manifests[i];
            if (manifest.SelectedValue is not null ||
                !candidates.TryGetValue(manifest.CandidateId, out var candidate) ||
                candidate.Identity.Kind != NativeOriginCharacteristicKind.Intrinsic)
                continue;
            var choice = manifest.Choices?.SingleOrDefault();
            var requirement = candidate.RequiredChoices?.SingleOrDefault();
            if (choice is not null && requirement is not null &&
                choice.RequirementId == requirement.Id && choice.SelectedOptionIds.Count == 1 &&
                requirement.FiniteOptions?.Contains(choice.SelectedOptionIds[0], StringComparer.Ordinal) == true)
                manifests[i] = manifest with
                {
                    SelectedValue = JsonSerializer.SerializeToElement(choice.SelectedOptionIds[0])
                };
        }

        var proposal = discovered with { Manifestations = manifests.ToArray() };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var accepted = new List<NativeOriginSelectedGrant>();
        var usedDm = false;

        NativeTableRuleDecision? Verified(string kind, string id)
        {
            if (authority is null || request.TrustedScope is null) return null;
            foreach (var decision in proposal.TableDecisions ?? [])
            {
                if (decision is null || decision.Target is null || decision.Scope is null ||
                    decision.Target.Kind != kind || decision.Target.Id != id ||
                    !ScopeMatches(decision.Scope, request.TrustedScope, proposal.Id))
                    continue;
                if (authority.Authorizes(decision, request.TrustedScope))
                    return decision;
            }
            return null;
        }

        // Replace untrusted eligibility claims only after verifying the exact decision.
        // The structural validator then inspects the actual table-authorized basis.
        proposal = proposal with { Candidates = proposal.Candidates.Select(candidate =>
        {
            var d = Verified("candidate", candidate.Id) ??
                Verified("source", candidate.Identity.Occurrence.Source.Id);
            return d is not null &&
                JsonString(d.EffectiveOutcome, "eligibility") == "eligible" &&
                selected.Contains(candidate.Id)
                ? candidate with { SelectionBasis = new(NativeOriginEligibility.DmAuthorized,
                    TableDecisionId: d.Id) } : candidate;
        }).ToArray() };
        candidates = proposal.Candidates.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var structural = _validator.ReviewProposal(proposal);
        issues.AddRange(structural.Issues.Where(x => !UnselectedOfferIssue(x, selected)));

        if (proposal.Intent == NativeOriginIntent.Direct)
            foreach (var candidate in proposal.Candidates.Where(x =>
                x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                x.ContributorId is null && !selected.Contains(x.Id) &&
                (x.RequiredChoices ?? []).Any(y => y.Timing == NativeOriginChoiceTiming.Creation)))
                Add("required-origin-selection-missing", candidate.Id,
                    NativeOriginIssueSeverity.Incomplete,
                    "The directly selected source requires a persistent character-creation choice.");

        foreach (var manifest in manifests.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (!seen.Add(manifest.CandidateId))
            {
                Add("candidate-selected-more-than-once", manifest.CandidateId,
                    NativeOriginIssueSeverity.Invalid,
                    "One candidate cannot produce multiple independent grants without an authored rule.");
                continue;
            }
            if (!candidates.TryGetValue(manifest.CandidateId, out var candidate)) continue;
            var source = candidate.Identity.Occurrence.Source;
            var nativeSource = source.Kind == NativeOriginSourceKind.Native &&
                catalog.TryGetById(source.Id, out var native) && native is not null;
            var direct = proposal.Intent == NativeOriginIntent.Direct &&
                candidate.ContributorId is null &&
                (source.Id == proposal.SelectedSpeciesId || source.Id == proposal.SelectedAncestryId);
            var baseEligible = nativeSource && direct &&
                candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible;
            var permission = Verified("candidate", candidate.Id) ?? Verified("source", source.Id);
            var decisionEligible = permission is not null &&
                JsonString(permission.EffectiveOutcome, "eligibility") == "eligible";
            if (!baseEligible && !decisionEligible)
            {
                Add("selection-eligibility-unresolved", candidate.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "Contributor or custom availability is not an approved effective grant.");
                continue;
            }
            if (decisionEligible) usedDm = true;

            var needsDecision = !nativeSource && !decisionEligible;
            if (needsDecision) continue;

            var requirements = candidate.RequiredChoices ?? [];
            var choices = manifest.Choices ?? [];
            var incomplete = false;
            foreach (var req in requirements)
            {
                var choice = choices.FirstOrDefault(x => x.RequirementId == req.Id);
                if (req.Timing == NativeOriginChoiceTiming.OnUse) continue;
                if (choice is null)
                {
                    if (req.Minimum > 0)
                    {
                        Add("dependent-choice-incomplete", candidate.Id + ":" + req.Id,
                            NativeOriginIssueSeverity.Incomplete,
                            "The selected trait requires an additional persistent choice.");
                        incomplete = true;
                    }
                    continue;
                }
                if (choice.SelectedOptionIds is null ||
                    choice.SelectedOptionIds.Count < req.Minimum ||
                    choice.SelectedOptionIds.Count > req.Maximum ||
                    choice.SelectedOptionIds.Distinct(StringComparer.Ordinal).Count() !=
                        choice.SelectedOptionIds.Count ||
                    (req.FiniteOptions is not null &&
                     choice.SelectedOptionIds.Any(x => !req.FiniteOptions.Contains(x, StringComparer.Ordinal))))
                {
                    Add("dependent-choice-invalid", candidate.Id + ":" + req.Id,
                        NativeOriginIssueSeverity.Invalid, "Selection is outside authored choice bounds.");
                    incomplete = true;
                }
                if (req.OptionCatalogId is not null &&
                    (string.IsNullOrWhiteSpace(choice.CatalogEligibilityEvidenceId) ||
                     authority is null || request.TrustedScope is null ||
                     choice.SelectedOptionIds.Any(x => !authority.AuthorizesCatalogChoice(
                         req.OptionCatalogId, x, choice.CatalogEligibilityEvidenceId!))))
                {
                    Add("choice-authorization-unresolved", candidate.Id + ":" + req.Id,
                        NativeOriginIssueSeverity.Unresolved,
                        "Open-catalog selection lacks trusted membership and eligibility verification.");
                    incomplete = true;
                }
            }
            if (choices.Any(x => !requirements.Any(y => y.Id == x.RequirementId &&
                y.Timing != NativeOriginChoiceTiming.OnUse)))
            {
                Add("unrecognized-persistent-choice", candidate.Id,
                    NativeOriginIssueSeverity.Invalid, "An on-use or unoffered choice was persisted.");
                incomplete = true;
            }
            if (incomplete) continue;

            if (candidate.UnresolvedEvidence?.Count > 0)
            {
                Add("unsupported-source-binding", candidate.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    string.Join("; ", candidate.UnresolvedEvidence));
                continue;
            }
            var value = manifest.SelectedValue ?? candidate.OfferedValue;
            if (candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                value is null)
            {
                Add("intrinsic-value-incomplete", candidate.Id,
                    NativeOriginIssueSeverity.Incomplete,
                    "The intrinsic slot needs an explicit selected value.");
                continue;
            }
            var overridesValue = manifest.SelectedValue is not null &&
                candidate.OfferedValue is not null &&
                !SameJson(manifest.SelectedValue.Value, candidate.OfferedValue.Value);
            var overridesParameters = manifest.SelectedParameters?.Any(pair =>
                candidate.OfferedParameters is null ||
                !candidate.OfferedParameters.TryGetValue(pair.Key, out var offered) ||
                !SameJson(pair.Value, offered)) == true;
            NativeTableRuleDecision? modification = null;
            if (overridesValue || overridesParameters || manifest.TableDecisionId is not null)
            {
                modification = proposal.TableDecisions?.FirstOrDefault(x =>
                    x.Id == manifest.TableDecisionId && x.Target is not null &&
                    ((x.Target.Kind == "manifestation" && x.Target.Id == manifest.Id) ||
                     (x.Target.Kind == "candidate" && x.Target.Id == candidate.Id)));
                if (modification is null || request.TrustedScope is null || authority is null ||
                    !ScopeMatches(modification.Scope, request.TrustedScope, proposal.Id) ||
                    !authority.Authorizes(modification, request.TrustedScope) ||
                    (overridesValue && (!modification.EffectiveOutcome.TryGetProperty("value",
                        out var dmValue) || !SameJson(dmValue, value!.Value))) ||
                    (overridesParameters && !MatchingParameters(modification.EffectiveOutcome,
                        manifest.SelectedParameters!)))
                {
                    Add("manifestation-override-unauthorized", manifest.Id,
                        NativeOriginIssueSeverity.Unresolved,
                        "A selected mechanical alteration requires the exact verified table outcome.");
                    continue;
                }
                usedDm = true;
            }
            // A selected value on a choice-driven slot must agree with its finite
            // option; it cannot inject an unrelated intrinsic value.
            if (candidate.OfferedValue is null &&
                candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                requirements.Count > 0 && !choices.Any(x =>
                    x.SelectedOptionIds.Any(id => value?.ValueKind == JsonValueKind.String &&
                        value.Value.GetString() == id)))
            {
                Add("intrinsic-choice-value-mismatch", candidate.Id,
                    NativeOriginIssueSeverity.Invalid, "Intrinsic value differs from the selected option.");
                continue;
            }

            var attribution = modification ?? (decisionEligible ? permission : null);
            var basis = attribution is null
                ? new NativeOriginDecisionBasis(NativeOriginAdjudication.Supported, source.Id)
                : new NativeOriginDecisionBasis(NativeOriginAdjudication.DmAuthorized,
                    TableDecisionId: attribution.Id);
            var grant = new NativeOriginEffectiveGrant("grant:" + manifest.Id, manifest.Id,
                candidate.Identity, [candidate.Id], value,
                manifest.SelectedParameters ?? candidate.OfferedParameters, choices, basis);
            accepted.Add(new(candidate, manifest, grant));
        }

        var (resolved, reconciliationIssues, reconciliationDm) =
            _reconciler.Reconcile(proposal, accepted, Verified);
        issues.AddRange(reconciliationIssues);
        usedDm |= reconciliationDm;

        // Phase 2 does not execute permanent or temporary form transitions. A
        // supplied baseline remains authoritative, and the active effective form
        // remains separate. Reconstructed grants are not unioned into a form.
        if (proposal.Intent == NativeOriginIntent.Transformation && manifests.Count > 0)
            Add("transformation-execution-deferred", "intent", NativeOriginIssueSeverity.Unresolved,
                "The form transition or permanent reconstruction needs its later executable policy.");
        if (proposal.ExistingConfiguration is not null && resolved.Count > 0)
            Add("baseline-replacement-deferred", "existingConfiguration",
                NativeOriginIssueSeverity.Unresolved,
                "A current baseline cannot be overwritten by an unexecuted reconstruction.");

        var configure = Verified("configuration", proposal.Id);
        var configuredSpecies = proposal.Intent == NativeOriginIntent.Direct ?
            proposal.SelectedSpeciesId : null;
        var configuredAncestry = proposal.Intent == NativeOriginIntent.Direct ?
            proposal.SelectedAncestryId : null;
        if (configure is not null)
        {
            var outcome = configure.EffectiveOutcome;
            if (outcome.ValueKind == JsonValueKind.Object &&
                (outcome.TryGetProperty("speciesId", out _) ||
                 outcome.TryGetProperty("ancestryId", out _)))
            {
                configuredSpecies = JsonString(outcome, "speciesId");
                configuredAncestry = JsonString(outcome, "ancestryId");
                usedDm = true;
            }
        }

        var profileId = "origin:" + proposal.Id;
        var underlying = proposal.ExistingConfiguration ??
            new NativeOriginCurrentConfiguration(configuredSpecies, configuredAncestry,
                resolved, profileId);
        var current = proposal.ExistingEffectiveConfiguration ?? underlying;
        var history = (proposal.History ?? []).ToList();
        if (!history.Any(x => x.Id == "construction:" + proposal.Id))
            history.Add(new("construction:" + proposal.Id, proposal.Method,
                NativeOriginEventStage.Completed,
                (proposal.Contributors ?? []).Select(x => x.Id)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                false, ResultingProfileId: profileId));

        NativeOriginConstructionStatus Status()
        {
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Invalid))
                return NativeOriginConstructionStatus.Invalid;
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Conflict))
                return NativeOriginConstructionStatus.Conflicting;
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Incomplete))
                return NativeOriginConstructionStatus.Incomplete;
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Unresolved))
                return NativeOriginConstructionStatus.Unresolved;
            return NativeOriginConstructionStatus.Authorized;
        }

        var preliminary = Status();
        var adjudication = preliminary == NativeOriginConstructionStatus.Authorized
            ? new NativeOriginDecisionBasis(usedDm ? NativeOriginAdjudication.DmAuthorized :
                NativeOriginAdjudication.Supported,
                usedDm ? null : proposal.Method.RuleId,
                usedDm ? (configure?.Id ??
                    resolved.FirstOrDefault(x => x.Authorization?.TableDecisionId is not null)?
                        .Authorization?.TableDecisionId) : null)
            : new NativeOriginDecisionBasis(preliminary == NativeOriginConstructionStatus.Conflicting ?
                NativeOriginAdjudication.Conflict : NativeOriginAdjudication.Unresolved);
        // If multiple independent rulings apply, the profile-level basis names one
        // anchor; each grant and disposition retains its exact individual decision.
        var profile = new NativeOriginProfile(profileId, underlying, current,
            request.ExistingIndependentGrants ?? [], history, adjudication, proposal.Id,
            request.PersistentInfluences, proposal.ActiveTemporaryTransformation,
            proposal.OtherActiveTemporaryTransformations,
            proposal.TemporaryTransformationPrecedence,
            proposal.ExistingEffectiveConfigurationBasis,
            request.IndependentGrantApplicability);

        // Previously acquired baseline grants belong to an earlier construction proposal.
        // Do not demand that their original manifestations appear in this proposal.
        var profileReview = _validator.ReviewProfile(profile,
            proposal.ExistingConfiguration is null ? proposal : null);
        issues.AddRange(profileReview.Issues.Where(x =>
            x.Code is not ("profile-outcome-unresolved" or "profile-outcome-conflict")));
        var status = Status();
        return new(status, proposal, status == NativeOriginConstructionStatus.Invalid ? null : profile,
            issues.OrderBy(x => x.Path, StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal).Distinct().ToArray());

        void Add(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));
    }

    private static bool ScopeMatches(NativeTableDecisionScope source,
        NativeTableDecisionScope trusted, string proposalId) =>
        source is not null &&
        ((source.Kind == trusted.Kind && source.Id == trusted.Id) ||
         (source.Kind == "proposal" && source.Id == proposalId));

    private static bool UnselectedOfferIssue(NativeOriginContractIssue issue, HashSet<string> selected)
    {
        if (issue.Code is not ("candidate-eligibility-unresolved" or
            "candidate-rule-evidence-required" or "unresolved-source-evidence" or
            "choice-catalog-unresolved" or "choice-timing-unresolved")) return false;
        return !selected.Any(id => issue.Path.StartsWith("candidates." + id,
            StringComparison.Ordinal));
    }

    private static bool SameJson(JsonElement x, JsonElement y) =>
        JsonElement.DeepEquals(x, y);

    private static string? JsonString(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var result) &&
        result.ValueKind == JsonValueKind.String ? result.GetString() : null;

    private static bool MatchingParameters(JsonElement outcome,
        IReadOnlyDictionary<string, JsonElement> selected)
    {
        if (outcome.ValueKind != JsonValueKind.Object ||
            !outcome.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Object) return false;
        return selected.All(x => parameters.TryGetProperty(x.Key, out var actual) &&
            SameJson(x.Value, actual));
    }
}
