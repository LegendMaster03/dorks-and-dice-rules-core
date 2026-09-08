using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RulesCore.Infrastructure.Rules;

public sealed record NormalizedJsonMergePatch(
    string Json,
    string Fingerprint);

public static class JsonMergePatch
{
    public static NormalizedJsonMergePatch Normalize(JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "A rule merge patch must have a JSON object root.",
                nameof(patch));
        }

        var canonicalJson = Canonicalize(patch);
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)))
            .ToLowerInvariant();
        return new NormalizedJsonMergePatch(canonicalJson, fingerprint);
    }

    public static JsonElement ParsePatch(string? patchJson)
    {
        if (string.IsNullOrWhiteSpace(patchJson))
        {
            throw new ArgumentException("A merge patch is required.", nameof(patchJson));
        }

        using var patch = JsonDocument.Parse(patchJson);
        return patch.RootElement.Clone();
    }

    public static JsonElement Apply(string sourceJson, string? patchJson)
    {
        using var source = JsonDocument.Parse(sourceJson);
        return Apply(source.RootElement, patchJson);
    }

    public static JsonElement Apply(JsonElement source, string? patchJson)
    {
        if (string.IsNullOrWhiteSpace(patchJson))
        {
            return source.Clone();
        }

        var targetNode = JsonNode.Parse(source.GetRawText());
        var patchNode = JsonNode.Parse(patchJson)
            ?? throw new InvalidDataException("A stored merge patch can not be JSON null.");
        if (patchNode is not JsonObject)
        {
            throw new InvalidDataException("A stored rule merge patch must have a JSON object root.");
        }

        var merged = ApplyNode(targetNode, patchNode)
            ?? throw new InvalidDataException("A rule merge patch can not remove the entire rule document.");
        using var mergedDocument = JsonDocument.Parse(merged.ToJsonString());
        return mergedDocument.RootElement.Clone();
    }

    private static JsonNode? ApplyNode(JsonNode? target, JsonNode patch)
    {
        if (patch is not JsonObject patchObject)
        {
            return patch.DeepClone();
        }

        var targetObject = target as JsonObject ?? new JsonObject();
        foreach (var property in patchObject)
        {
            if (property.Value is null)
            {
                targetObject.Remove(property.Key);
                continue;
            }

            targetObject[property.Key] = property.Value is JsonObject
                ? ApplyNode(targetObject[property.Key], property.Value)
                : property.Value.DeepClone();
        }

        return targetObject;
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
}
