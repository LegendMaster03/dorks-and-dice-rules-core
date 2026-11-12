using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Phase 1 source-conservative shape and reference validation. This service never
/// assembles candidates, chooses defaults, applies table rulings, or issues grants.
/// Phase 2 must resolve authoritative definitions, selections and whole-profile
/// dependencies before accepting a profile as mechanically effective.
/// </summary>
public sealed class NativeOriginContractValidator(NativeCatalogSnapshot catalog)
{
    /// <summary>
    /// Resolve a method directly from the selected native rule document. Unknown
    /// methods are not silently treated as banned or as supported native methods.
    /// </summary>
    public NativeOriginMethodDefinition? FindMethod(NativeOriginMethodReference reference)
    {
        if (reference is null || string.IsNullOrWhiteSpace(reference.RuleId) ||
            string.IsNullOrWhiteSpace(reference.MethodId) ||
            !catalog.TryGetById(reference.RuleId, out var entity) ||
            entity?.Category != "rule" ||
            !entity.Definition.TryGetProperty("methodDefinitions", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in definitions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                Text(item, "id") != reference.MethodId) continue;
            return new NativeOriginMethodDefinition(reference, Text(item, "name") ?? reference.MethodId,
                Text(item, "specializes"), Text(item, "stage"),
                Bool(item, "createsNewIndividual"), Text(item, "operation"), item.Clone());
        }
        return null;
    }

    public NativeOriginContractReview ReviewProposal(NativeOriginProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var issues = new List<NativeOriginContractIssue>();
        void Add(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));

        if (Blank(proposal.Id)) Add("proposal-id-required", "id", NativeOriginIssueSeverity.Invalid,
            "Each proposal requires a stable local identity.");
        if (proposal.Method is null || Blank(proposal.Method.RuleId) || Blank(proposal.Method.MethodId))
            Add("method-reference-required", "method", NativeOriginIssueSeverity.Invalid,
                "A method must have a native rule reference and method ID.");
        else if (FindMethod(proposal.Method) is null)
            Add("method-not-in-native-catalog", "method", NativeOriginIssueSeverity.Unresolved,
                "No selected-native method definition was found. A table-authored method requires explicit adjudication.");

        if (proposal.Contributors is null || proposal.Candidates is null || proposal.Manifestations is null)
        {
            Add("required-collections-missing", "proposal", NativeOriginIssueSeverity.Invalid,
                "Contributors, candidates and manifestations are required; empty lists are valid.");
            return Result(issues);
        }

        var contributors = Index(proposal.Contributors, c => c?.Id, "contributors", Add);
        var candidates = Index(proposal.Candidates, c => c?.Id, "candidates", Add);
        var manifests = Index(proposal.Manifestations, m => m?.Id, "manifestations", Add);
        var decisions = Index(proposal.TableDecisions ?? [], d => d?.Id, "tableDecisions", Add);
        var relations = Index(proposal.Relationships ?? [], r => r?.Id, "relationships", Add);

        foreach (var (id, contributor) in contributors)
        {
            var path = "contributors." + id;
            if (contributor.Role is null || contributor.Source is null ||
                Blank(contributor.Source.Id) ||
                (contributor.Role?.Kind == NativeOriginInputKind.ProcessSpecific &&
                 Blank(contributor.Role.ProcessRoleId)))
                Add("contributor-source-or-role-invalid", path, NativeOriginIssueSeverity.Invalid,
                    "Every input needs a source and a typed role; process-specific roles need an ID.");
            if (contributor.ManifestedProfile is { } previous)
            {
                if (Blank(previous.ProfileId) || previous.InherentGrants is null)
                    Add("nested-current-profile-required", path, NativeOriginIssueSeverity.Invalid,
                        "A nested contributor must identify its current manifested configuration.");
                if (contributor.Source?.Kind != NativeOriginSourceKind.ManifestedProfile)
                    Add("nested-source-mismatch", path, NativeOriginIssueSeverity.Invalid,
                        "A contributed current profile must be attributed to a manifested-profile source.");
            }
        }

        foreach (var (id, candidate) in candidates)
        {
            var path = "candidates." + id;
            if (candidate.Identity?.Occurrence?.Source is null ||
                Blank(candidate.Identity.Occurrence.Source.Id) ||
                Blank(candidate.Identity.Occurrence.OccurrencePath))
                Add("source-occurrence-required", path, NativeOriginIssueSeverity.Invalid,
                    "A candidate needs an exact authored source occurrence, even if identity mapping is unknown.");
            else if (candidate.Identity.MechanicalDefinitionId is null &&
                     candidate.Identity.IntrinsicSlotId is null)
                Add("mechanical-identity-unresolved", path, NativeOriginIssueSeverity.Unresolved,
                    "A complete mechanical identity has not yet been established.");

            if (candidate.ContributorId is not null && !contributors.ContainsKey(candidate.ContributorId))
                Add("unknown-contributor", path, NativeOriginIssueSeverity.Invalid,
                    "Candidate provenance references an unknown contributor.");
            if (candidate.SelectionBasis is null)
                Add("selection-basis-required", path, NativeOriginIssueSeverity.Invalid,
                    "Availability evidence must be separate from an actual grant.");
            else
            {
                if (candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                    Blank(candidate.SelectionBasis.RuleReference))
                    Add("candidate-rule-evidence-required", path,
                        NativeOriginIssueSeverity.Unresolved,
                        "Claimed eligibility needs an authoritative rule reference.");
                if (candidate.SelectionBasis.Eligibility is NativeOriginEligibility.Unreviewed or
                    NativeOriginEligibility.Unresolved or NativeOriginEligibility.Restricted)
                    Add("candidate-eligibility-unresolved", path, NativeOriginIssueSeverity.Unresolved,
                        "Candidate availability has not been established as an effective rule.");
                if (candidate.SelectionBasis.Eligibility == NativeOriginEligibility.DmAuthorized)
                    RequireDecision(candidate.SelectionBasis.TableDecisionId, path, decisions, Add);
            }
            if (candidate.UnresolvedEvidence?.Any(x => !Blank(x)) == true)
                Add("unresolved-source-evidence", path, NativeOriginIssueSeverity.Unresolved,
                    "Unmodeled authored properties must survive into later evaluation.");

            var choices = Index(candidate.RequiredChoices ?? [], c => c?.Id, path + ".requiredChoices", Add);
            foreach (var (choiceId, req) in choices)
            {
                if (req.Minimum < 0 || req.Maximum < req.Minimum ||
                    req.Occurrence?.Source is null || Blank(req.Occurrence.Source.Id) ||
                    Blank(req.Occurrence.OccurrencePath))
                    Add("choice-schema-invalid", path + "." + choiceId, NativeOriginIssueSeverity.Invalid,
                        "Choice bounds and authored occurrence must be valid.");
                if (req.Timing == NativeOriginChoiceTiming.Unknown)
                    Add("choice-timing-unresolved", path + "." + choiceId, NativeOriginIssueSeverity.Unresolved,
                        "Unspecified timing is not a creation-time entitlement.");
                if (req.FiniteOptions is null && Blank(req.OptionCatalogId) &&
                    req.Timing != NativeOriginChoiceTiming.OnUse)
                    Add("choice-catalog-unresolved", path + "." + choiceId, NativeOriginIssueSeverity.Unresolved,
                        "No authoritative finite menu or catalog selection source was provided.");
            }
        }

        foreach (var (id, manifest) in manifests)
        {
            var path = "manifestations." + id;
            if (!candidates.TryGetValue(manifest.CandidateId ?? "", out var candidate))
            {
                Add("manifestation-candidate-missing", path, NativeOriginIssueSeverity.Invalid,
                    "A manifestation must reference an available candidate occurrence.");
                continue;
            }
            if (manifest.TableDecisionId is not null)
                RequireDecision(manifest.TableDecisionId, path, decisions, Add);
            var requirements = (candidate.RequiredChoices ?? [])
                .Where(x => x is not null && !Blank(x.Id))
                .ToDictionary(x => x.Id, StringComparer.Ordinal);
            var selected = Index(manifest.Choices ?? [], c => c?.RequirementId,
                path + ".choices", Add);
            foreach (var (choiceId, choice) in selected)
            {
                if (!requirements.TryGetValue(choiceId, out var req))
                {
                    Add("unexpected-choice", path + "." + choiceId, NativeOriginIssueSeverity.Invalid,
                        "Only a choice authored for the selected candidate may be supplied.");
                    continue;
                }
                if (req.Timing == NativeOriginChoiceTiming.OnUse)
                {
                    Add("on-use-choice-not-persistent", path + "." + choiceId,
                        NativeOriginIssueSeverity.Invalid, "An on-use choice is not a permanent Origin selection.");
                    continue;
                }
                var options = choice.SelectedOptionIds ?? [];
                if (options.Count < req.Minimum || options.Count > req.Maximum ||
                    options.Any(Blank) || options.Distinct(StringComparer.Ordinal).Count() != options.Count ||
                    (req.FiniteOptions is not null &&
                     options.Any(o => !req.FiniteOptions.Contains(o, StringComparer.Ordinal))))
                    Add("choice-selection-invalid", path + "." + choiceId, NativeOriginIssueSeverity.Invalid,
                        "Choice count, distinctness and authored finite options must be respected.");
                if (req.OptionCatalogId is not null && Blank(choice.CatalogEligibilityEvidenceId))
                    Add("choice-catalog-proof-required", path + "." + choiceId,
                        NativeOriginIssueSeverity.Unresolved,
                        "Open catalog selections require independent trusted eligibility evidence.");
            }
            foreach (var (choiceId, req) in requirements)
                if (req.Timing == NativeOriginChoiceTiming.Creation &&
                    req.Minimum > 0 && !selected.ContainsKey(choiceId))
                    Add("choice-incomplete", path + "." + choiceId, NativeOriginIssueSeverity.Incomplete,
                        "A required persistent choice has not been supplied.");
            if (candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                !manifest.SelectedValue.HasValue)
                Add("intrinsic-value-incomplete", path, NativeOriginIssueSeverity.Incomplete,
                    "An intrinsic manifestation needs an explicit selected value.");
            foreach (var key in manifest.SelectedParameters?.Keys ?? Enumerable.Empty<string>())
                if (candidate.OfferedParameters is null ||
                    !candidate.OfferedParameters.ContainsKey(key))
                    Add("parameter-binding-unresolved", path + "." + key,
                        NativeOriginIssueSeverity.Unresolved,
                        "Selected parameter has no verified authored offer or table-effective binding.");
        }

        foreach (var (id, relation) in relations)
        {
            if (relation.CandidateIds is null || relation.CandidateIds.Count == 0 ||
                relation.CandidateIds.Any(key => !candidates.ContainsKey(key)))
                Add("relationship-evidence-invalid", "relationships." + id,
                    NativeOriginIssueSeverity.Invalid, "Relations must reference exact candidate IDs.");
        }
        var resolutions = Index(proposal.Resolutions ?? [], r => r?.RelationshipId, "resolutions", Add);
        foreach (var (id, resolution) in resolutions)
        {
            if (!relations.ContainsKey(id))
                Add("resolution-relationship-missing", "resolutions." + id,
                    NativeOriginIssueSeverity.Invalid,
                    "A disposition must reference the reviewed relationship.");
            if (resolution.Basis is null)
                Add("resolution-basis-required", "resolutions." + id,
                    NativeOriginIssueSeverity.Invalid, "A disposition needs its own adjudication evidence.");
            else
            {
                if (resolution.Basis.Adjudication == NativeOriginAdjudication.DmAuthorized)
                    RequireDecision(resolution.Basis.TableDecisionId, "resolutions." + id, decisions, Add);
                if (resolution.Basis.Adjudication == NativeOriginAdjudication.Supported &&
                    Blank(resolution.Basis.NativeRuleReference))
                    Add("supported-rule-reference-required", "resolutions." + id,
                        NativeOriginIssueSeverity.Unresolved,
                        "Supported first-party outcomes require a known rule reference.");
                if (resolution.Basis.Adjudication is NativeOriginAdjudication.Unresolved or
                    NativeOriginAdjudication.Conflict ||
                    resolution.Action == NativeOriginResolutionAction.Defer)
                    Add("relationship-unresolved", "resolutions." + id,
                        NativeOriginIssueSeverity.Unresolved, "The proposed relation has no resolved outcome.");
            }
        }
        foreach (var id in relations.Keys)
            if (!resolutions.ContainsKey(id))
                Add("relationship-awaits-decision", "relationships." + id,
                    NativeOriginIssueSeverity.Unresolved,
                    "Relationship evidence alone is not a duplicate, stacking or replacement rule.");

        var requiredInputs = Index(proposal.InputRequirements ?? [], c => c?.Id,
            "inputRequirements", Add);
        foreach (var (id, req) in requiredInputs)
        {
            var path = "inputRequirements." + id;
            if (req.Role is null ||
                (req.Role?.Kind == NativeOriginInputKind.ProcessSpecific &&
                 Blank(req.Role.ProcessRoleId)) ||
                req.MinimumCount < 0 || req.MaximumCount is < 0 ||
                (req.MaximumCount.HasValue && req.MaximumCount.Value < req.MinimumCount))
                Add("input-requirement-invalid", path, NativeOriginIssueSeverity.Invalid,
                    "Process roles and minimum/maximum contributor bounds must be valid.");
            if (!Blank(req.TableDecisionId))
                RequireDecision(req.TableDecisionId, path, decisions, Add);
            else if (Blank(req.NativeRuleReference))
                Add("input-requirement-policy-unresolved", path,
                    NativeOriginIssueSeverity.Unresolved,
                    "Process input requirements need authored native or table-policy evidence.");
        }

        var constraints = Index(proposal.Constraints ?? [], c => c?.Id, "constraints", Add);
        foreach (var (id, constraint) in constraints)
        {
            var path = "constraints." + id;
            var selectors = (constraint.Subjects ?? []).Concat(constraint.Conditions ?? []);
            if (constraint.Subjects is null || constraint.Subjects.Count == 0 ||
                constraint.Conditions is null ||
                selectors.Any(s => s is null ||
                    (Blank(s.CandidateId) && Blank(s.MechanicalDefinitionId) &&
                     Blank(s.IntrinsicSlotId) && Blank(s.SourceId))))
                Add("constraint-selectors-invalid", path, NativeOriginIssueSeverity.Invalid,
                    "Constraints require nonempty subjects and identifiable selectors.");
            foreach (var selector in selectors)
                if (selector?.CandidateId is string key && !candidates.ContainsKey(key))
                    Add("constraint-candidate-missing", path, NativeOriginIssueSeverity.Invalid,
                        "Exact candidate selectors must reference this proposal.");
            if (constraint.Minimum is < 0 || constraint.Maximum is < 0 ||
                (constraint.Minimum.HasValue && constraint.Maximum.HasValue &&
                 constraint.Minimum.Value > constraint.Maximum.Value))
                Add("constraint-cardinality-invalid", path, NativeOriginIssueSeverity.Invalid,
                    "Constraint bounds must be coherent.");
            if (!Blank(constraint.TableDecisionId))
                RequireDecision(constraint.TableDecisionId, path, decisions, Add);
            else if (Blank(constraint.NativeRuleReference))
                Add("constraint-policy-unresolved", path, NativeOriginIssueSeverity.Unresolved,
                    "A constraint requires approved rule or table-policy evidence.");
        }

        foreach (var (id, decision) in decisions)
        {
            if (decision.Authority is null || Blank(decision.Authority.ActorId) ||
                Blank(decision.Authority.AuthorizationEvidenceId) ||
                decision.Scope is null || Blank(decision.Scope.Kind) || Blank(decision.Scope.Id) ||
                decision.Target is null || Blank(decision.Target.Kind) || Blank(decision.Target.Id) ||
                decision.EffectiveOutcome.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                Add("table-decision-malformed", "tableDecisions." + id,
                    NativeOriginIssueSeverity.Invalid,
                    "A table decision needs an authority reference, scope, target and effective outcome.");
        }
        foreach (var record in proposal.History ?? [])
            if (record is null || Blank(record.Id) || record.Method is null)
                Add("history-event-invalid", "history", NativeOriginIssueSeverity.Invalid,
                    "Historical events require their own identity and method reference.");

        return Result(issues);
    }

    /// <summary>
    /// Checks the separation and linkage of effective and historical state.
    /// The caller, not this validator, is responsible for proving entitlement
    /// and granting these mechanics through a trusted construction pipeline.
    /// </summary>
    public NativeOriginContractReview ReviewProfile(NativeOriginProfile profile,
        NativeOriginProposal? proposal = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<NativeOriginContractIssue>();
        void Add(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));
        if (Blank(profile.Id) || profile.Current is null ||
            profile.Current?.InherentGrants is null ||
            profile.IndependentGrants is null || profile.History is null ||
            profile.Adjudication is null)
        {
            Add("profile-required-fields", "profile", NativeOriginIssueSeverity.Invalid,
                "Current, independent and historical state must remain separate.");
            return Result(issues);
        }
        var grants = Index(profile.Current.InherentGrants, g => g?.Id, "current.grants", Add);
        var independent = Index(profile.IndependentGrants, g => g?.Id, "independentGrants", Add);
        foreach (var id in grants.Keys)
            if (independent.ContainsKey(id))
                Add("duplicate-grant-across-sources", "profile", NativeOriginIssueSeverity.Invalid,
                    "Current inherent and independent grants must have distinct grant identities.");

        var slotGroups = grants.Values
            .Where(g => g.Identity?.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                        !Blank(g.Identity.IntrinsicSlotId))
            .GroupBy(g => g.Identity.IntrinsicSlotId, StringComparer.Ordinal);
        foreach (var group in slotGroups)
            if (group.Count() > 1)
                Add("intrinsic-slot-policy-unresolved", "current." + group.Key,
                    NativeOriginIssueSeverity.Unresolved,
                    "A repeated intrinsic slot needs explicit whole-profile reconciliation.");

        Dictionary<string, NativeOriginCandidate>? candidates = null;
        Dictionary<string, NativeOriginManifestation>? selectedManifestations = null;
        if (proposal is not null)
        {
            if (profile.ProposalId != proposal.Id)
                Add("profile-proposal-mismatch", "proposalId", NativeOriginIssueSeverity.Invalid,
                    "Profile provenance must identify the proposal it was built from.");
            candidates = (proposal.Candidates ?? [])
                .Where(c => c is not null && !Blank(c.Id))
                .GroupBy(c => c.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            selectedManifestations = (proposal.Manifestations ?? [])
                .Where(m => m is not null && !Blank(m.Id))
                .GroupBy(m => m.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }
        foreach (var grant in grants.Values)
        {
            if (grant.Authorization?.Adjudication == NativeOriginAdjudication.DmAuthorized &&
                (Blank(grant.Authorization.TableDecisionId) ||
                 proposal?.TableDecisions?.All(d => d.Id != grant.Authorization.TableDecisionId) != false))
                Add("grant-dm-decision-unverified", "current.grants." + grant.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "Grant authority needs a referenced ruling and trusted verification.");
            if (Blank(grant.ManifestationId) ||
                grant.SourceCandidateIds is null || grant.SourceCandidateIds.Count == 0 ||
                grant.Identity?.Occurrence?.Source is null)
                Add("grant-provenance-required", "current.grants." + grant.Id,
                    NativeOriginIssueSeverity.Invalid,
                    "An inherent grant must identify a manifestation, offers, and its source occurrence.");
            if (selectedManifestations is not null)
            {
                if (!selectedManifestations.TryGetValue(grant.ManifestationId ?? "", out var selectedManifestation))
                    Add("unknown-manifestation-grant", "current.grants." + grant.Id,
                        NativeOriginIssueSeverity.Invalid,
                        "An inherent grant must derive from a selected manifestation.");
                else
                {
                    if (grant.SourceCandidateIds is null ||
                        !grant.SourceCandidateIds.Contains(selectedManifestation.CandidateId, StringComparer.Ordinal))
                        Add("grant-selected-offer-mismatch", "current.grants." + grant.Id,
                            NativeOriginIssueSeverity.Invalid,
                            "The selected offer must appear in grant provenance.");
                    if (candidates is not null &&
                        candidates.TryGetValue(selectedManifestation.CandidateId, out var selected) &&
                        grant.Identity != selected.Identity)
                        Add("grant-definition-mismatch", "current.grants." + grant.Id,
                            NativeOriginIssueSeverity.Invalid,
                            "The grant identity must match its selected source occurrence.");
                }
            }
            if (candidates is not null && grant.SourceCandidateIds is not null)
                foreach (var candidateId in grant.SourceCandidateIds)
                    if (!candidates.ContainsKey(candidateId))
                        Add("unknown-grant-offer", "current.grants." + grant.Id,
                            NativeOriginIssueSeverity.Invalid,
                            "Grant evidence must name actual candidates, not invented ancestors.");
        }
        var repeatedManifestations = grants.Values.Where(g => !Blank(g.ManifestationId))
            .GroupBy(g => g.ManifestationId, StringComparer.Ordinal);
        foreach (var repeated in repeatedManifestations)
            if (repeated.Count() > 1)
                Add("manifestation-repeat-unresolved", "current.grants",
                    NativeOriginIssueSeverity.Unresolved,
                    "Multiple grants from one manifestation require a separately authored rule.");

        var historyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ev in profile.History)
        {
            if (ev is null || Blank(ev.Id) || !historyIds.Add(ev.Id) || ev.Method is null ||
                ev.ContributorIds is null)
                Add("history-event-invalid", "history", NativeOriginIssueSeverity.Invalid,
                    "Each historical event must have a unique ID, method and contributor list.");
            else if (ev.Stage == NativeOriginEventStage.Completed &&
                     ev.ReplacesConfiguration && ev.PreviousConfiguration is null)
                Add("replacement-history-incomplete", "history." + ev.Id,
                    NativeOriginIssueSeverity.Incomplete,
                    "A completed replacement must retain the former configuration as history.");
        }
        if (profile.Adjudication.Adjudication == NativeOriginAdjudication.DmAuthorized)
        {
            if (Blank(profile.Adjudication.TableDecisionId))
                Add("profile-dm-decision-required", "adjudication", NativeOriginIssueSeverity.Invalid,
                    "A DM-authorized outcome needs a scoped table-decision ID.");
            else if (proposal?.TableDecisions?.Any(d =>
                d.Id == profile.Adjudication.TableDecisionId) != true)
                Add("profile-dm-decision-unverified", "adjudication",
                    NativeOriginIssueSeverity.Unresolved,
                    "A claimed table ruling must be linked to an explicit scoped decision.");
        }
        if (profile.Adjudication.Adjudication == NativeOriginAdjudication.Unresolved)
            Add("profile-outcome-unresolved", "adjudication",
                NativeOriginIssueSeverity.Unresolved, "No supported or table-effective outcome is established.");
        if (profile.Adjudication.Adjudication == NativeOriginAdjudication.Conflict)
            Add("profile-outcome-conflict", "adjudication",
                NativeOriginIssueSeverity.Conflict, "The recorded outcome is conflicting.");
        // This verifies structural evidence only: an external authorization and
        // evaluation layer must validate any claimed rule/DM authority.
        return Result(issues);
    }

    private static Dictionary<string, T> Index<T>(IEnumerable<T> entries, Func<T, string?> getId,
        string path, Action<string, string, NativeOriginIssueSeverity, string> add) where T : class
    {
        var indexed = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var id = entry is null ? null : getId(entry);
            if (id is null || Blank(id) || !indexed.TryAdd(id, entry!))
                add("duplicate-or-missing-id", path, NativeOriginIssueSeverity.Invalid,
                    "Occurrence IDs must be present and unique within their collection.");
        }
        return indexed;
    }

    private static void RequireDecision(string? id, string path,
        IReadOnlyDictionary<string, NativeTableRuleDecision> decisions,
        Action<string, string, NativeOriginIssueSeverity, string> add)
    {
        if (id is null || Blank(id) || !decisions.ContainsKey(id))
            add("table-decision-required", path, NativeOriginIssueSeverity.Invalid,
                "An explicit DM/table disposition must reference a recorded scoped decision.");
    }

    private static NativeOriginContractReview Result(List<NativeOriginContractIssue> issues)
    {
        var status = issues.Any(i => i.Severity == NativeOriginIssueSeverity.Invalid)
            ? NativeOriginReviewStatus.Invalid
            : issues.Any(i => i.Severity == NativeOriginIssueSeverity.Conflict)
                ? NativeOriginReviewStatus.Conflicting
                : issues.Any(i => i.Severity == NativeOriginIssueSeverity.Unresolved)
                    ? NativeOriginReviewStatus.Unresolved
                    : issues.Any(i => i.Severity == NativeOriginIssueSeverity.Incomplete)
                        ? NativeOriginReviewStatus.Incomplete
                        : NativeOriginReviewStatus.StructurallyConsistent;
        return new(status, issues);
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
    private static string? Text(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
    private static bool? Bool(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) ? v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        } : null;
}
