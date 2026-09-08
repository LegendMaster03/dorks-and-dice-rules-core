using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules;

public sealed record NormalizedJsonRulePatch(
    string Json,
    string Fingerprint);

public static class JsonRulePatch
{
    public static NormalizedJsonRulePatch Normalize(RuleStructuredPatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operations = request.ArrayOperations ?? Array.Empty<RuleArrayOperationRequest>();
        if (operations.Count == 0)
        {
            throw new ArgumentException(
                "A structured rule patch must contain at least one array operation.",
                nameof(request));
        }

        var root = new JsonObject();
        if (request.MergePatch.HasValue)
        {
            var mergePatch = JsonMergePatch.Normalize(request.MergePatch.Value);
            root["merge"] = JsonNode.Parse(mergePatch.Json);
        }

        var operationArray = new JsonArray();
        foreach (var operation in operations)
        {
            operationArray.Add(NormalizeOperation(operation));
        }
        root["arrays"] = operationArray;

        using var document = JsonDocument.Parse(root.ToJsonString());
        var canonicalJson = Canonicalize(document.RootElement);
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)))
            .ToLowerInvariant();
        return new NormalizedJsonRulePatch(canonicalJson, fingerprint);
    }

    public static JsonElement ParsePatch(string? patchJson)
    {
        if (string.IsNullOrWhiteSpace(patchJson))
        {
            throw new ArgumentException("A structured rule patch is required.", nameof(patchJson));
        }

        using var patch = JsonDocument.Parse(patchJson);
        if (patch.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A stored structured rule patch must have a JSON object root.");
        }
        return patch.RootElement.Clone();
    }

    public static JsonElement Apply(JsonElement source, string? patchJson)
    {
        var patch = ParsePatch(patchJson);
        var resolved = source.Clone();

        if (patch.TryGetProperty("merge", out var mergePatch))
        {
            resolved = JsonMergePatch.Apply(resolved, mergePatch.GetRawText());
        }

        if (!patch.TryGetProperty("arrays", out var operations)
            || operations.ValueKind != JsonValueKind.Array
            || operations.GetArrayLength() == 0)
        {
            throw new InvalidDataException(
                "A stored structured rule patch must contain at least one array operation.");
        }

        var root = JsonNode.Parse(resolved.GetRawText())
            ?? throw new InvalidDataException("A resolved rule document can not be JSON null.");
        foreach (var operation in operations.EnumerateArray())
        {
            ApplyOperation(root, operation);
        }

        using var mergedDocument = JsonDocument.Parse(root.ToJsonString());
        return mergedDocument.RootElement.Clone();
    }

    private static JsonObject NormalizeOperation(RuleArrayOperationRequest operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var operationKind = RequireText(operation.Operation, nameof(operation.Operation)).ToLowerInvariant();
        if (!RuleArrayOperationKinds.All.Contains(operationKind))
        {
            throw new ArgumentException(
                $"Unsupported array operation '{operation.Operation}'.",
                nameof(operation.Operation));
        }

        var path = RequireText(operation.Path, nameof(operation.Path));
        if (!path.StartsWith('/', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "An array operation path must be an RFC 6901 JSON Pointer beginning with '/'.",
                nameof(operation.Path));
        }

        var normalized = new JsonObject
        {
            ["op"] = operationKind,
            ["path"] = path
        };

        switch (operationKind)
        {
            case RuleArrayOperationKinds.Append:
                RequireValue(operation.Value, operationKind);
                RejectSelector(operation.Match, nameof(operation.Match), operationKind);
                RejectSelector(operation.Anchor, nameof(operation.Anchor), operationKind);
                normalized["value"] = ToNode(operation.Value!.Value);
                break;

            case RuleArrayOperationKinds.Remove:
                RejectValue(operation.Value, operationKind);
                RejectSelector(operation.Anchor, nameof(operation.Anchor), operationKind);
                normalized["match"] = NormalizeSelector(
                    operation.Match,
                    operationKind,
                    requireKey: false);
                break;

            case RuleArrayOperationKinds.ReplaceByKey:
                RequireValue(operation.Value, operationKind);
                RejectSelector(operation.Anchor, nameof(operation.Anchor), operationKind);
                normalized["match"] = NormalizeSelector(
                    operation.Match,
                    operationKind,
                    requireKey: true);
                normalized["value"] = ToNode(operation.Value!.Value);
                break;

            case RuleArrayOperationKinds.InsertBefore:
            case RuleArrayOperationKinds.InsertAfter:
                RequireValue(operation.Value, operationKind);
                RejectSelector(operation.Match, nameof(operation.Match), operationKind);
                normalized["anchor"] = NormalizeSelector(
                    operation.Anchor,
                    operationKind,
                    requireKey: false);
                normalized["value"] = ToNode(operation.Value!.Value);
                break;
        }

        return normalized;
    }

    private static JsonObject NormalizeSelector(
        RuleArrayItemSelectorRequest? selector,
        string operationKind,
        bool requireKey)
    {
        if (selector is null)
        {
            throw new ArgumentException(
                $"Array operation '{operationKind}' requires a selector.");
        }
        if (selector.Value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException(
                $"Array operation '{operationKind}' selector value is required.");
        }

        var key = string.IsNullOrWhiteSpace(selector.Key) ? null : selector.Key.Trim();
        if (requireKey && key is null)
        {
            throw new ArgumentException(
                $"Array operation '{operationKind}' requires a keyed selector.");
        }

        var normalized = new JsonObject();
        if (key is not null)
        {
            normalized["key"] = key;
        }
        normalized["value"] = ToNode(selector.Value);
        return normalized;
    }

    private static void ApplyOperation(JsonNode root, JsonElement operation)
    {
        if (operation.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A structured rule patch array operation must be an object.");
        }

        var kind = GetRequiredString(operation, "op");
        var path = GetRequiredString(operation, "path");
        var target = ResolveArray(root, path);

        switch (kind)
        {
            case RuleArrayOperationKinds.Append:
                target.Add(ParseRequiredNode(operation, "value"));
                return;

            case RuleArrayOperationKinds.Remove:
            {
                var selector = GetSelector(operation, "match");
                var index = FindUniqueIndex(target, selector, kind, path);
                target.RemoveAt(index);
                return;
            }

            case RuleArrayOperationKinds.ReplaceByKey:
            {
                var selector = GetSelector(operation, "match");
                if (selector.Key is null)
                {
                    throw new InvalidDataException("A replace-by-key operation requires a keyed selector.");
                }
                var index = FindUniqueIndex(target, selector, kind, path);
                target[index] = ParseRequiredNode(operation, "value");
                return;
            }

            case RuleArrayOperationKinds.InsertBefore:
            case RuleArrayOperationKinds.InsertAfter:
            {
                var selector = GetSelector(operation, "anchor");
                var index = FindUniqueIndex(target, selector, kind, path);
                if (kind == RuleArrayOperationKinds.InsertAfter)
                {
                    index++;
                }
                target.Insert(index, ParseRequiredNode(operation, "value"));
                return;
            }

            default:
                throw new InvalidDataException($"Unsupported stored array operation '{kind}'.");
        }
    }

    private static JsonArray ResolveArray(JsonNode root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in ParsePointer(path))
        {
            current = current switch
            {
                JsonObject obj when obj.TryGetPropertyValue(segment, out var child) => child,
                JsonArray array when int.TryParse(
                    segment,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var index)
                    && index >= 0
                    && index < array.Count => array[index],
                _ => throw new InvalidDataException(
                    $"Array operation path '{path}' does not resolve to an existing value.")
            };
        }

        return current as JsonArray
            ?? throw new InvalidDataException(
                $"Array operation path '{path}' does not resolve to a JSON array.");
    }

    private static IReadOnlyList<string> ParsePointer(string path)
    {
        if (!path.StartsWith('/', StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Stored array operation path '{path}' is not a valid JSON Pointer.");
        }

        return path.Split('/').Skip(1).Select(DecodePointerSegment).ToArray();
    }

    private static string DecodePointerSegment(string segment)
    {
        var result = new StringBuilder(segment.Length);
        for (var index = 0; index < segment.Length; index++)
        {
            if (segment[index] != '~')
            {
                result.Append(segment[index]);
                continue;
            }

            if (index + 1 >= segment.Length)
            {
                throw new InvalidDataException("A JSON Pointer segment contains an incomplete escape.");
            }

            result.Append(segment[index + 1] switch
            {
                '0' => '~',
                '1' => '/',
                _ => throw new InvalidDataException("A JSON Pointer segment contains an invalid escape.")
            });
            index++;
        }
        return result.ToString();
    }

    private static int FindUniqueIndex(
        JsonArray array,
        ArraySelector selector,
        string operationKind,
        string path)
    {
        var matches = new List<int>();
        for (var index = 0; index < array.Count; index++)
        {
            if (Matches(array[index], selector))
            {
                matches.Add(index);
            }
        }

        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"Array operation '{operationKind}' at '{path}' requires exactly one matching item; found {matches.Count}.");
        }
        return matches[0];
    }

    private static bool Matches(JsonNode? item, ArraySelector selector)
    {
        if (selector.Key is null)
        {
            return JsonNode.DeepEquals(item, selector.Value);
        }

        return item is JsonObject obj
            && obj.TryGetPropertyValue(selector.Key, out var actual)
            && JsonNode.DeepEquals(actual, selector.Value);
    }

    private static ArraySelector GetSelector(JsonElement operation, string propertyName)
    {
        if (!operation.TryGetProperty(propertyName, out var selector)
            || selector.ValueKind != JsonValueKind.Object
            || !selector.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"A stored array operation is missing its '{propertyName}' selector.");
        }

        string? key = null;
        if (selector.TryGetProperty("key", out var keyElement))
        {
            if (keyElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(keyElement.GetString()))
            {
                throw new InvalidDataException("A stored array selector key must be a nonblank string.");
            }
            key = keyElement.GetString()!;
        }

        return new ArraySelector(key, ToNode(value));
    }

    private static string GetRequiredString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException(
                $"A stored structured rule patch is missing string property '{propertyName}'.");
        }
        return property.GetString()!;
    }

    private static JsonNode ParseRequiredNode(JsonElement operation, string propertyName)
    {
        if (!operation.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidDataException(
                $"A stored array operation is missing property '{propertyName}'.");
        }
        return ToNode(value);
    }

    private static JsonNode ToNode(JsonElement value) =>
        JsonNode.Parse(value.GetRawText())
        ?? JsonValue.Create((string?)null)!;

    private static void RequireValue(JsonElement? value, string operationKind)
    {
        if (!value.HasValue || value.Value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException(
                $"Array operation '{operationKind}' requires a value.");
        }
    }

    private static void RejectValue(JsonElement? value, string operationKind)
    {
        if (value.HasValue)
        {
            throw new ArgumentException(
                $"Array operation '{operationKind}' does not accept a replacement value.");
        }
    }

    private static void RejectSelector(
        RuleArrayItemSelectorRequest? selector,
        string parameterName,
        string operationKind)
    {
        if (selector is not null)
        {
            throw new ArgumentException(
                $"Array operation '{operationKind}' does not accept {parameterName}.");
        }
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        return value.Trim();
    }

    private static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, element);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
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

    private sealed record ArraySelector(string? Key, JsonNode Value);
}
