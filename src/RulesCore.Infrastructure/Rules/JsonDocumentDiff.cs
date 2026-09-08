using System.Text.Json;
using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules;

public static class JsonDocumentDiff
{
    public static IReadOnlyList<RuleDocumentChangeView> Compare(
        JsonElement before,
        JsonElement after)
    {
        var changes = new List<RuleDocumentChangeView>();
        CompareValue(before, after, string.Empty, changes);
        return changes;
    }

    private static void CompareValue(
        JsonElement before,
        JsonElement after,
        string path,
        ICollection<RuleDocumentChangeView> changes)
    {
        if (JsonElement.DeepEquals(before, after))
        {
            return;
        }

        if (before.ValueKind == JsonValueKind.Object
            && after.ValueKind == JsonValueKind.Object)
        {
            CompareObjects(before, after, path, changes);
            return;
        }

        if (before.ValueKind == JsonValueKind.Array
            && after.ValueKind == JsonValueKind.Array)
        {
            changes.Add(new RuleDocumentChangeView(
                path,
                RuleDocumentChangeKinds.Replace,
                before.Clone(),
                after.Clone()));
            return;
        }

        changes.Add(new RuleDocumentChangeView(
            path,
            RuleDocumentChangeKinds.Replace,
            before.Clone(),
            after.Clone()));
    }

    private static void CompareObjects(
        JsonElement before,
        JsonElement after,
        string path,
        ICollection<RuleDocumentChangeView> changes)
    {
        var beforeProperties = before
            .EnumerateObject()
            .ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        var afterProperties = after
            .EnumerateObject()
            .ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

        foreach (var propertyName in beforeProperties.Keys
                     .Union(afterProperties.Keys, StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var propertyPath = path + "/" + EscapePointerSegment(propertyName);
            var hasBefore = beforeProperties.TryGetValue(propertyName, out var beforeValue);
            var hasAfter = afterProperties.TryGetValue(propertyName, out var afterValue);

            if (!hasBefore)
            {
                changes.Add(new RuleDocumentChangeView(
                    propertyPath,
                    RuleDocumentChangeKinds.Add,
                    null,
                    afterValue.Clone()));
                continue;
            }

            if (!hasAfter)
            {
                changes.Add(new RuleDocumentChangeView(
                    propertyPath,
                    RuleDocumentChangeKinds.Remove,
                    beforeValue.Clone(),
                    null));
                continue;
            }

            CompareValue(beforeValue, afterValue, propertyPath, changes);
        }
    }

    private static string EscapePointerSegment(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
}
