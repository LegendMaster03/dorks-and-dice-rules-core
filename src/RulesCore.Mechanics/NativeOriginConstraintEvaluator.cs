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
            var authored = NativeConstraintAuthorizes(constraint);
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

    private bool NativeConstraintAuthorizes(NativeOriginConstraint constraint)
    {
        if (constraint.NativeRuleReference is null ||
            !catalog.TryGetById(constraint.NativeRuleReference, out var rule) ||
            rule?.Category != "rule" ||
            !rule.Definition.TryGetProperty("originConstraints", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Array) return false;
        return definitions.EnumerateArray().Any(item =>
            Text(item, "id") == constraint.Id &&
            Text(item, "kind") == constraint.Kind.ToString());
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
