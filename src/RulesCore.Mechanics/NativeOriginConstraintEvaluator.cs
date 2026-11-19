using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Enforces only authored or authenticated whole-profile constraints. Unverified
/// assertions never become native rules, and unresolved dependencies prevent
/// affected manifestations from entering the effective grant set.
/// </summary>
internal sealed class NativeOriginConstraintEvaluator(NativeCatalogSnapshot catalog)
{
    public (IReadOnlyList<NativeOriginSelectedGrant> Allowed,
        IReadOnlyList<NativeOriginContractIssue> Issues, bool UsedDm) Apply(
        NativeOriginProposal proposal, IReadOnlyList<NativeOriginSelectedGrant> selected,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        var issues = new List<NativeOriginContractIssue>();
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var usedDm = false;
        foreach (var constraint in (proposal.Constraints ?? [])
            .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (constraint is null || constraint.Subjects is null ||
                constraint.Conditions is null) continue;
            var subjects = selected.Where(x => constraint.Subjects.Any(s => Matches(s, x)))
                .DistinctBy(x => x.Candidate.Id).ToArray();
            var conditions = selected.Where(x => constraint.Conditions.Any(s => Matches(s, x)))
                .DistinctBy(x => x.Candidate.Id).ToArray();
            // A cardinality constraint is relevant even with zero selected subjects.
            if (subjects.Length == 0 &&
                constraint.Kind != NativeOriginRelationshipKind.SelectionCardinality)
                continue;

            var decision = verified("constraint", constraint.Id);
            var dmAllow = decision is not null &&
                ReadBool(decision.EffectiveOutcome, "allow") == true;
            var dmEnforce = decision is not null &&
                ReadBool(decision.EffectiveOutcome, "enforce") == true;
            var authored = NativeConstraintAuthorizes(proposal, constraint);
            if (decision is not null && (dmAllow || dmEnforce)) usedDm = true;
            if (dmAllow) continue;
            if (!dmEnforce && !authored)
            {
                Block("constraint-policy-unresolved", NativeOriginIssueSeverity.Unresolved,
                    "No executable authored or verified table constraint is available.");
                continue;
            }

            switch (constraint.Kind)
            {
                case NativeOriginRelationshipKind.Requires:
                    var required = constraint.Minimum ?? constraint.Conditions.Count;
                    if (conditions.Length < required)
                        Block("required-mechanic-missing", NativeOriginIssueSeverity.Incomplete,
                            "A selected characteristic depends on missing characteristics.");
                    break;
                case NativeOriginRelationshipKind.Exclusion:
                    if (conditions.Length > 0)
                        Block("authored-exclusion", NativeOriginIssueSeverity.Conflict,
                            "Mutually exclusive selected mechanics cannot both be effective.");
                    break;
                case NativeOriginRelationshipKind.SelectionCardinality:
                    if (constraint.Minimum is int minimum && subjects.Length < minimum)
                        Block("selection-cardinality-incomplete", NativeOriginIssueSeverity.Incomplete,
                            "Too few eligible characteristics were selected.");
                    if (constraint.Maximum is int maximum && subjects.Length > maximum)
                        Block("selection-cardinality-conflict", NativeOriginIssueSeverity.Conflict,
                            "More characteristics were selected than the authored limit.");
                    break;
                case NativeOriginRelationshipKind.LinkedParameters:
                    if (subjects.Length == 0 || conditions.Length == 0 ||
                        constraint.LinkedParameterPaths is null ||
                        constraint.LinkedParameterPaths.Count == 0)
                    {
                        Block("linked-parameter-incomplete", NativeOriginIssueSeverity.Incomplete,
                            "Linked mechanical choices require both ends and authored parameter paths.");
                        break;
                    }
                    foreach (var (source, target) in constraint.LinkedParameterPaths)
                    {
                        foreach (var s in subjects)
                        foreach (var t in conditions)
                        {
                            if (s.Grant.Parameters?.TryGetValue(source, out var a) != true ||
                                t.Grant.Parameters?.TryGetValue(target, out var b) != true)
                            {
                                Block("linked-parameter-unbound",
                                    NativeOriginIssueSeverity.Incomplete,
                                    "The selected manifestations lack a required parameter binding.");
                                continue;
                            }
                            if (!JsonElement.DeepEquals(a, b))
                                Block("linked-parameter-conflict", NativeOriginIssueSeverity.Conflict,
                                    "The linked parameter values do not agree.");
                        }
                    }
                    break;
                default:
                    Block("constraint-semantics-unsupported", NativeOriginIssueSeverity.Unresolved,
                        "The named constraint has no approved executable interpretation.");
                    break;
            }

            void Block(string code, NativeOriginIssueSeverity level, string detail)
            {
                issues.Add(new(code, "constraints." + constraint.Id, level, detail));
                foreach (var entry in subjects) blocked.Add(entry.Candidate.Id);
                // A conflicting exclusion or link also invalidates the other participants.
                if (level == NativeOriginIssueSeverity.Conflict)
                    foreach (var entry in conditions) blocked.Add(entry.Candidate.Id);
            }
        }

        return (selected.Where(x => !blocked.Contains(x.Candidate.Id)).ToArray(),
            issues, usedDm);
    }


    /// <summary>
    /// Close authored or authenticated prerequisites over the *effective* grant
    /// set. Initial selection is not enough: reconciliation, parameter binding,
    /// resource binding and other prerequisite failures may have removed an
    /// initially selected companion. Repeat until no dependent grant is left
    /// relying on a subsequently removed grant.
    /// </summary>
    public (IReadOnlyList<NativeOriginEffectiveGrant> Grants,
        IReadOnlyList<NativeOriginContractIssue> Issues) VerifyEffectiveRequirements(
        NativeOriginProposal proposal, IReadOnlyList<NativeOriginEffectiveGrant> effective,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        var remaining = effective.ToList();
        var issues = new List<NativeOriginContractIssue>();
        var offered = (proposal.Candidates ?? [])
            .Where(x => x is not null)
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var requirements = (proposal.Constraints ?? [])
            .Where(x => x is not null &&
                x.Kind == NativeOriginRelationshipKind.Requires &&
                x.Subjects is { Count: > 0 } && x.Conditions is not null)
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();

        bool changed;
        do
        {
            changed = false;
            foreach (var requirement in requirements)
            {
                // A caller's rule pointer cannot authorize new prerequisites.
                // The initial evaluator has already marked unsupported claims
                // unresolved; this pass must not turn them into executable rules.
                var decision = verified("constraint", requirement.Id);
                if (ReadBool(decision?.EffectiveOutcome ?? default, "allow") == true)
                    continue;
                if (ReadBool(decision?.EffectiveOutcome ?? default, "enforce") != true &&
                    !NativeConstraintAuthorizes(proposal, requirement))
                    continue;

                var subjects = remaining.Where(grant =>
                    requirement.Subjects.Any(selector =>
                        MatchesEffective(selector, grant, offered))).ToArray();
                if (subjects.Length == 0)
                    continue;

                var conditions = remaining.Count(grant =>
                    requirement.Conditions.Any(selector =>
                        MatchesEffective(selector, grant, offered)));
                if (conditions >= (requirement.Minimum ?? requirement.Conditions.Count))
                    continue;

                issues.Add(new("effective-required-mechanic-missing",
                    "constraints." + requirement.Id, NativeOriginIssueSeverity.Incomplete,
                    "A required characteristic is absent from the final effective grant set."));
                var blocked = subjects.Select(x => x.Id)
                    .ToHashSet(StringComparer.Ordinal);
                remaining.RemoveAll(x => blocked.Contains(x.Id));
                changed = true;
            }
        } while (changed);

        return (remaining, issues);
    }

    private static bool MatchesEffective(
        NativeOriginCharacteristicSelector selector, NativeOriginEffectiveGrant grant,
        IReadOnlyDictionary<string, NativeOriginCandidate> offered)
    {
        if (selector is null || grant.SourceCandidateIds is null)
            return false;
        if (selector.ParameterKey is not null &&
            (grant.Parameters?.TryGetValue(selector.ParameterKey, out var parameter) != true ||
             selector.ExpectedValue is { } expected &&
                !JsonElement.DeepEquals(expected, parameter)))
            return false;

        // Resolve every selector field against ONE verified offer. A consolidated
        // grant may cite several sources, but fields from unrelated offers must
        // not be combined into a forged selector match.
        return grant.SourceCandidateIds.Any(id =>
            offered.TryGetValue(id, out var candidate) &&
            candidate.Identity.Kind == grant.Identity.Kind &&
            (selector.CandidateId is null || selector.CandidateId == id) &&
            (selector.MechanicalDefinitionId is null ||
                selector.MechanicalDefinitionId == candidate.Identity.MechanicalDefinitionId) &&
            (selector.IntrinsicSlotId is null ||
                selector.IntrinsicSlotId == candidate.Identity.IntrinsicSlotId) &&
            (selector.SourceId is null ||
                selector.SourceId == candidate.Identity.Occurrence.Source.Id));
    }

    private bool NativeConstraintAuthorizes(
        NativeOriginProposal proposal, NativeOriginConstraint constraint)
    {
        if (constraint.NativeRuleReference is null ||
            !catalog.TryGetById(constraint.NativeRuleReference, out var rule) ||
            rule is null) return false;
        if (rule.Category == "rule" &&
            rule.Definition.TryGetProperty("originConstraints", out var definitions) &&
            definitions.ValueKind == JsonValueKind.Array &&
            definitions.EnumerateArray().Any(item =>
                Text(item, "id") == constraint.Id &&
                Text(item, "kind") == constraint.Kind.ToString()))
            return true;
        // Source-owned linked selections must match exactly the native
        // contributor-local candidates. A caller-authored Requires assertion
        // is not authority to bind another parent's trait or waive a drawback.
        if (rule.Category is not ("species" or "ancestry") ||
            constraint.Kind != NativeOriginRelationshipKind.Requires ||
            constraint.Minimum != 1 ||
            constraint.Subjects.Count != 1 || constraint.Conditions.Count != 1 ||
            constraint.NativeRuleReference != rule.Id ||
            !rule.Definition.TryGetProperty("originLinkedSelections", out var links) ||
            links.ValueKind != JsonValueKind.Array) return false;

        var subjectId = constraint.Subjects[0].CandidateId;
        var conditionId = constraint.Conditions[0].CandidateId;
        var subject = proposal.Candidates.FirstOrDefault(x => x.Id == subjectId);
        var condition = proposal.Candidates.FirstOrDefault(x => x.Id == conditionId);
        if (subject is null || condition is null ||
            subject.Id == condition.Id ||
            subject.Identity.Kind != NativeOriginCharacteristicKind.Intrinsic ||
            condition.Identity.Kind != NativeOriginCharacteristicKind.Trait ||
            subject.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
            condition.Identity.Occurrence.Source.Kind != NativeOriginSourceKind.Native ||
            subject.Identity.Occurrence.Source.Id != rule.Id ||
            condition.Identity.Occurrence.Source.Id != rule.Id ||
            subject.ContributorId != condition.ContributorId)
            return false;

        return links.EnumerateArray().Any(link =>
            Text(link, "scope") == "same-contributor" &&
            Text(link, "kind") == nameof(NativeOriginRelationshipKind.Requires) &&
            Text(link, "triggerIntrinsicSlot") == subject.Identity.IntrinsicSlotId &&
            Text(link, "requiresTraitDefinitionId") == condition.Identity.MechanicalDefinitionId &&
            constraint.Id == NativeOriginNativeLinkedSelectionEvaluator.ConstraintId(
                subject.Id, Text(link, "id")!) &&
            constraint.Subjects[0] == new NativeOriginCharacteristicSelector(
                CandidateId: subject.Id) &&
            constraint.Conditions[0] == new NativeOriginCharacteristicSelector(
                CandidateId: condition.Id));
    }

    private static bool Matches(NativeOriginCharacteristicSelector selector,
        NativeOriginSelectedGrant grant)
    {
        if (selector is null) return false;
        var candidate = grant.Candidate;
        if (selector.CandidateId is not null && selector.CandidateId != candidate.Id ||
            selector.MechanicalDefinitionId is not null &&
                selector.MechanicalDefinitionId != candidate.Identity.MechanicalDefinitionId ||
            selector.IntrinsicSlotId is not null &&
                selector.IntrinsicSlotId != candidate.Identity.IntrinsicSlotId ||
            selector.SourceId is not null &&
                selector.SourceId != candidate.Identity.Occurrence.Source.Id)
            return false;
        if (selector.ParameterKey is null) return true;
        if (grant.Grant.Parameters?.TryGetValue(selector.ParameterKey, out var value) != true)
            return false;
        return selector.ExpectedValue is null ||
            JsonElement.DeepEquals(selector.ExpectedValue.Value, value);
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static bool? ReadBool(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : null;
}
