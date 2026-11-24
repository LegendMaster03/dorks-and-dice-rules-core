using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>One native effective source and its actual inherited cultural parents.</summary>
internal static class NativeLifeExperienceSourceChain
{
    internal static IReadOnlyList<NativeRuleEntity>? Resolve(NativeCatalogSnapshot catalog,
        string id, string category, out NativeIssue? issue)
    {
        issue = null;
        var chain = new List<NativeRuleEntity>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = id;
        while (true)
        {
            if (string.IsNullOrWhiteSpace(current) || current.Length > 160 ||
                !seen.Add(current) || chain.Count >= 16 ||
                !catalog.TryGetById(current, out var entity) || entity?.Category != category ||
                (chain.Count == 0 && (!entity.Definition.TryGetProperty("selectable", out var selectable) ||
                    selectable.ValueKind != JsonValueKind.True)))
            {
                issue = new("invalid-life-experience-source", current ?? id,
                    "Direct sources must be selectable; inherited parents must exist and be acyclic.");
                return null;
            }
            chain.Add(entity);
            if (!entity.Definition.TryGetProperty("parent", out var parent)) break;
            if (parent.ValueKind != JsonValueKind.String || category != "upbringing")
            {
                issue = new("invalid-life-experience-parent", current,
                    "Only an authored Upbringing parent chain is supported.");
                return null;
            }
            current = parent.GetString()!;
        }
        chain.Reverse();
        return chain;
    }
}
