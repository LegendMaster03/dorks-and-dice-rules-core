using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RulesCore.Infrastructure.Rules;

internal sealed record RuleAdditiveMergeResult(
    bool Compatible,
    string? MergedJson,
    string Reason);

internal static class RuleSemanticCompatibility
{
    private static readonly HashSet<string> IgnoredRootProperties = new(
        [
            "name",
            "source",
            "page",
            "id",
            "uniqueId",
            "reprintedAs",
            "otherSources",
            "additionalSources",
            "previousVersion",
            "previousVersions",
            "versions",
            "seeAlso",
            "edition",
            "srd",
            "srd52",
            "basicRules",
            "basicRules2024",
            "freeRules2024"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ArrayIdentityProperties = ["name", "id", "key"];

    public static string ComputeFingerprint(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalRuleContent(writer, document.RootElement, isRoot: true);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    public static RuleAdditiveMergeResult TryCreateAdditiveUnion(
        string preferredBaseJson,
        IEnumerable<string> otherSourceJson)
    {
        var target = JsonNode.Parse(preferredBaseJson) as JsonObject;
        if (target is null)
        {
            return Incompatible("A rule document must have a JSON object root.");
        }

        foreach (var sourceJson in otherSourceJson)
        {
            var source = JsonNode.Parse(sourceJson) as JsonObject;
            if (source is null)
            {
                return Incompatible("Every rule document must have a JSON object root.");
            }

            if (!TryMergeObjects(target, source, "$", isRoot: true, out var reason))
            {
                return Incompatible(reason);
            }
        }

        return new RuleAdditiveMergeResult(
            Compatible: true,
            target.ToJsonString(),
            "The bound editions can be combined without replacing or contradicting rule-bearing content.");
    }

    public static NormalizedJsonMergePatch? CreateAdditivePatch(string baseJson, string mergedJson)
    {
        var source = JsonNode.Parse(baseJson) as JsonObject
            ?? throw new InvalidDataException("A rule document must have a JSON object root.");
        var target = JsonNode.Parse(mergedJson) as JsonObject
            ?? throw new InvalidDataException("A merged rule document must have a JSON object root.");

        if (!TryBuildMergePatch(source, target, "$", out var changed, out var patch, out var reason))
        {
            throw new InvalidOperationException(reason);
        }
        if (!changed)
        {
            return null;
        }

        using var document = JsonDocument.Parse(patch!.ToJsonString());
        return JsonMergePatch.Normalize(document.RootElement);
    }

    private static bool TryMergeObjects(
        JsonObject target,
        JsonObject source,
        string path,
        bool isRoot,
        out string reason)
    {
        foreach (var property in source)
        {
            if (isRoot && IgnoredRootProperties.Contains(property.Key))
            {
                continue;
            }

            var propertyPath = AppendPath(path, property.Key);
            if (!target.TryGetPropertyValue(property.Key, out var targetValue))
            {
                if (property.Value is null)
                {
                    reason = $"Property '{propertyPath}' is null in only one edition and can not be added safely.";
                    return false;
                }

                target[property.Key] = property.Value.DeepClone();
                continue;
            }

            if (!TryMergeNodes(targetValue, property.Value, propertyPath, out var mergedValue, out reason))
            {
                return false;
            }
            target[property.Key] = mergedValue;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryMergeNodes(
        JsonNode? target,
        JsonNode? source,
        string path,
        out JsonNode? merged,
        out string reason)
    {
        if (JsonNode.DeepEquals(target, source))
        {
            merged = target?.DeepClone();
            reason = string.Empty;
            return true;
        }

        if (target is JsonObject targetObject && source is JsonObject sourceObject)
        {
            var result = (JsonObject)targetObject.DeepClone();
            if (!TryMergeObjects(result, sourceObject, path, isRoot: false, out reason))
            {
                merged = null;
                return false;
            }

            merged = result;
            return true;
        }

        if (target is JsonArray targetArray && source is JsonArray sourceArray)
        {
            var result = (JsonArray)targetArray.DeepClone();
            if (!TryMergeArrays(result, sourceArray, path, out reason))
            {
                merged = null;
                return false;
            }

            merged = result;
            return true;
        }

        merged = null;
        reason = $"Rule-bearing value '{path}' differs between editions and requires adjudication.";
        return false;
    }

    private static bool TryMergeArrays(
        JsonArray target,
        JsonArray source,
        string path,
        out string reason)
    {
        if (JsonNode.DeepEquals(target, source))
        {
            reason = string.Empty;
            return true;
        }

        if (TryGetIdentifiedArray(target, out var targetItems)
            && TryGetIdentifiedArray(source, out var sourceItems))
        {
            return TryMergeIdentifiedArrays(target, targetItems, sourceItems, path, out reason);
        }

        if (IsExactOrderedSubset(source, target))
        {
            reason = string.Empty;
            return true;
        }

        if (IsExactOrderedSubset(target, source))
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item?.DeepClone());
            }
            reason = string.Empty;
            return true;
        }

        reason = $"Array '{path}' contains incompatible replacements, reorderings, or anonymous additions and requires adjudication.";
        return false;
    }

    private static bool TryMergeIdentifiedArrays(
        JsonArray target,
        IReadOnlyList<IdentifiedArrayItem> targetItems,
        IReadOnlyList<IdentifiedArrayItem> sourceItems,
        string path,
        out string reason)
    {
        if (targetItems.Select(value => value.Identity).Distinct(StringComparer.Ordinal).Count() != targetItems.Count
            || sourceItems.Select(value => value.Identity).Distinct(StringComparer.Ordinal).Count() != sourceItems.Count)
        {
            reason = $"Array '{path}' contains duplicate semantic identities and can not be merged automatically.";
            return false;
        }

        var targetById = targetItems.ToDictionary(value => value.Identity, StringComparer.Ordinal);
        var sourceById = sourceItems.ToDictionary(value => value.Identity, StringComparer.Ordinal);
        var common = targetById.Keys.Intersect(sourceById.Keys, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var targetCommon = targetItems.Where(value => common.Contains(value.Identity)).Select(value => value.Identity).ToArray();
        var sourceCommon = sourceItems.Where(value => common.Contains(value.Identity)).Select(value => value.Identity).ToArray();
        if (!targetCommon.SequenceEqual(sourceCommon, StringComparer.Ordinal))
        {
            reason = $"Array '{path}' reorders shared rule-bearing entries and requires adjudication.";
            return false;
        }

        var mergedCommon = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var identity in common)
        {
            var targetItem = targetById[identity];
            var sourceItem = sourceById[identity];
            if (!TryMergeNodes(
                targetItem.Value,
                sourceItem.Value,
                $"{path}[{identity}]",
                out var merged,
                out reason))
            {
                return false;
            }
            mergedCommon[identity] = merged;
        }

        var targetIds = targetItems.Select(value => value.Identity).ToArray();
        var sourceIds = sourceItems.Select(value => value.Identity).ToArray();
        var mergedIds = ShortestCommonSupersequence(targetIds, sourceIds);

        target.Clear();
        foreach (var identity in mergedIds)
        {
            if (mergedCommon.TryGetValue(identity, out var merged))
            {
                target.Add(merged?.DeepClone());
            }
            else if (targetById.TryGetValue(identity, out var targetItem))
            {
                target.Add(targetItem.Value?.DeepClone());
            }
            else
            {
                target.Add(sourceById[identity].Value?.DeepClone());
            }
        }

        reason = string.Empty;
        return true;
    }

    private static IReadOnlyList<string> ShortestCommonSupersequence(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        var lcs = new int[left.Count + 1, right.Count + 1];
        for (var i = left.Count - 1; i >= 0; i--)
        {
            for (var j = right.Count - 1; j >= 0; j--)
            {
                lcs[i, j] = string.Equals(left[i], right[j], StringComparison.Ordinal)
                    ? 1 + lcs[i + 1, j + 1]
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var result = new List<string>(left.Count + right.Count);
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Count && rightIndex < right.Count)
        {
            if (string.Equals(left[leftIndex], right[rightIndex], StringComparison.Ordinal))
            {
                result.Add(left[leftIndex]);
                leftIndex++;
                rightIndex++;
            }
            else if (lcs[leftIndex + 1, rightIndex] >= lcs[leftIndex, rightIndex + 1])
            {
                result.Add(left[leftIndex++]);
            }
            else
            {
                result.Add(right[rightIndex++]);
            }
        }

        while (leftIndex < left.Count) result.Add(left[leftIndex++]);
        while (rightIndex < right.Count) result.Add(right[rightIndex++]);
        return result;
    }

    private static bool TryGetIdentifiedArray(
        JsonArray array,
        out IReadOnlyList<IdentifiedArrayItem> items)
    {
        var result = new List<IdentifiedArrayItem>(array.Count);
        foreach (var item in array)
        {
            if (item is not JsonObject obj || !TryGetObjectIdentity(obj, out var identity))
            {
                items = [];
                return false;
            }
            result.Add(new IdentifiedArrayItem(identity, item));
        }

        items = result;
        return true;
    }

    private static bool TryGetObjectIdentity(JsonObject value, out string identity)
    {
        foreach (var propertyName in ArrayIdentityProperties)
        {
            if (!value.TryGetPropertyValue(propertyName, out var property)
                || property is null
                || property is JsonObject
                || property is JsonArray)
            {
                continue;
            }

            identity = $"{propertyName}:{CanonicalizeNode(property)}";
            return true;
        }

        identity = string.Empty;
        return false;
    }

    private static bool IsExactOrderedSubset(JsonArray subset, JsonArray superset)
    {
        var next = 0;
        foreach (var subsetItem in subset)
        {
            var matched = false;
            while (next < superset.Count)
            {
                if (JsonNode.DeepEquals(subsetItem, superset[next]))
                {
                    matched = true;
                    next++;
                    break;
                }
                next++;
            }

            if (!matched)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryBuildMergePatch(
        JsonNode? source,
        JsonNode? target,
        string path,
        out bool changed,
        out JsonNode? patch,
        out string reason)
    {
        if (JsonNode.DeepEquals(source, target))
        {
            changed = false;
            patch = null;
            reason = string.Empty;
            return true;
        }

        if (source is JsonObject sourceObject && target is JsonObject targetObject)
        {
            var result = new JsonObject();
            foreach (var sourceProperty in sourceObject)
            {
                if (!targetObject.ContainsKey(sourceProperty.Key))
                {
                    changed = false;
                    patch = null;
                    reason = $"Automatic additive resolution can not remove property '{AppendPath(path, sourceProperty.Key)}'.";
                    return false;
                }
            }

            foreach (var targetProperty in targetObject)
            {
                var propertyPath = AppendPath(path, targetProperty.Key);
                if (!sourceObject.TryGetPropertyValue(targetProperty.Key, out var sourceValue))
                {
                    if (targetProperty.Value is null)
                    {
                        changed = false;
                        patch = null;
                        reason = $"Automatic additive resolution can not add null property '{propertyPath}' with JSON Merge Patch semantics.";
                        return false;
                    }
                    result[targetProperty.Key] = targetProperty.Value.DeepClone();
                    continue;
                }

                if (!TryBuildMergePatch(
                    sourceValue,
                    targetProperty.Value,
                    propertyPath,
                    out var childChanged,
                    out var childPatch,
                    out reason))
                {
                    changed = false;
                    patch = null;
                    return false;
                }
                if (childChanged)
                {
                    result[targetProperty.Key] = childPatch;
                }
            }

            changed = result.Count > 0;
            patch = changed ? result : null;
            reason = string.Empty;
            return true;
        }

        if (target is null)
        {
            changed = false;
            patch = null;
            reason = $"Automatic additive resolution can not replace '{path}' with null because JSON Merge Patch would interpret that as deletion.";
            return false;
        }

        changed = true;
        patch = target.DeepClone();
        reason = string.Empty;
        return true;
    }

    private static string CanonicalizeNode(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string AppendPath(string path, string propertyName) =>
        path == "$" ? $"$.{propertyName}" : $"{path}.{propertyName}";

    private static void WriteCanonicalRuleContent(
        Utf8JsonWriter writer,
        JsonElement element,
        bool isRoot)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                    .EnumerateObject()
                    .Where(value => !isRoot || !IgnoredRootProperties.Contains(value.Name))
                    .OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalRuleContent(writer, property.Value, isRoot: false);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteCanonicalRuleContent(writer, child, isRoot: false);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteCanonical(writer, child);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static RuleAdditiveMergeResult Incompatible(string reason) =>
        new(false, null, reason);

    private sealed record IdentifiedArrayItem(string Identity, JsonNode? Value);
}
