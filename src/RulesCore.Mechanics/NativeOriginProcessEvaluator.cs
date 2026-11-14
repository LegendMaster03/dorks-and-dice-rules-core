using System.Text.Json;

namespace RulesCore.Mechanics;

/// <summary>
/// Validates the process-specific contributor requirements without introducing a
/// hard-coded parent count or a second method registry. A table-authored input
/// arrangement requires authenticated, exact effective bounds.
/// </summary>
internal static class NativeOriginProcessEvaluator
{
    public static IReadOnlyList<NativeOriginContractIssue> Evaluate(
        NativeOriginProposal proposal, NativeOriginMethodDefinition? method,
        Func<string, string, NativeTableRuleDecision?> verified)
    {
        var issues = new List<NativeOriginContractIssue>();
        var methodDecision = verified("method", proposal.Method.MethodId);
        if (method is null && !(methodDecision is not null &&
            ReadBool(methodDecision.EffectiveOutcome, "allow") == true))
            issues.Add(new("method-authorization-unresolved", "method",
                NativeOriginIssueSeverity.Unresolved,
                "The method is unavailable in the selected native catalog and no verified alternative is supplied."));

        foreach (var requirement in (proposal.InputRequirements ?? [])
            .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (requirement?.Role is null) continue;
            var dm = verified("input-requirement", requirement.Id);
            var authored = method is not null && AuthoredRequirement(method.AuthoredDefinition,
                requirement);
            var waived = dm is not null && ReadBool(dm.EffectiveOutcome, "allow") == true;
            var approvedBounds = dm is not null &&
                ReadInt(dm.EffectiveOutcome, "minimumCount") == requirement.MinimumCount &&
                ReadInt(dm.EffectiveOutcome, "maximumCount") == requirement.MaximumCount;
            if (waived) continue;
            if (!authored && !approvedBounds)
            {
                issues.Add(new("process-input-policy-unresolved", "inputRequirements." + requirement.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "The contributor bounds are not supported by the selected method or a verified table rule."));
                continue;
            }
            var actual = (proposal.Contributors ?? []).Count(input =>
                input.Role?.Kind == requirement.Role.Kind &&
                (input.Role.Kind != NativeOriginInputKind.ProcessSpecific ||
                 input.Role.ProcessRoleId == requirement.Role.ProcessRoleId));
            if (actual < requirement.MinimumCount)
                issues.Add(new("process-input-incomplete", "inputRequirements." + requirement.Id,
                    NativeOriginIssueSeverity.Incomplete, "The origination process lacks required inputs."));
            if (requirement.MaximumCount is int max && actual > max)
                issues.Add(new("process-input-conflict", "inputRequirements." + requirement.Id,
                    NativeOriginIssueSeverity.Conflict, "The process has more inputs than the authored limit."));
        }
        return issues;
    }

    private static bool AuthoredRequirement(JsonElement method,
        NativeOriginInputRequirement requirement)
    {
        if (method.ValueKind != JsonValueKind.Object ||
            !method.TryGetProperty("inputRequirements", out var definitions) ||
            definitions.ValueKind != JsonValueKind.Array) return false;
        return definitions.EnumerateArray().Any(entry =>
            Text(entry, "id") == requirement.Id &&
            Text(entry, "role") == requirement.Role.Kind.ToString() &&
            (requirement.Role.Kind != NativeOriginInputKind.ProcessSpecific ||
             Text(entry, "processRoleId") == requirement.Role.ProcessRoleId) &&
            ReadInt(entry, "minimumCount") == requirement.MinimumCount &&
            ReadInt(entry, "maximumCount") == requirement.MaximumCount);
    }

    private static string? Text(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var child) &&
        child.ValueKind == JsonValueKind.String ? child.GetString() : null;

    private static int? ReadInt(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var child) &&
        child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out var value)
            ? value : null;

    private static bool? ReadBool(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var child) &&
        child.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? child.GetBoolean() : null;
}
