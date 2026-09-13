using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RulesCore.Application.Sources;

public sealed record CanonicalPublicationEvidence(
    string DisplayName,
    string? Publisher = null,
    string? GameEdition = null,
    DateOnly? PublicationDate = null,
    IReadOnlyDictionary<string, string>? Aliases = null,
    IReadOnlyCollection<string>? OccurrenceFingerprints = null);

public sealed record CanonicalPublicationIdentityView(
    Guid Id,
    string Key,
    string DisplayName,
    string? Publisher,
    string? GameEdition,
    DateOnly? PublicationDate,
    string MatchKind,
    double Confidence);

public sealed record CanonicalSourceOccurrenceEvidence(
    string EntityType,
    string Name,
    string? LocatorKey,
    string SemanticFingerprint);

public static class CanonicalSourceIdentity
{
    private static readonly HashSet<string> ProvenanceProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "source",
        "page",
        "otherSources",
        "reprintedAs",
        "srd",
        "basicRules",
        "basicRules2024",
        "edition",
        "uniqueId",
        "id"
    };

    public static string NormalizeIdentityPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingDash = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(character);
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }
        return builder.ToString();
    }

    public static string BibliographicFingerprint(CanonicalPublicationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var value = string.Join('\n', new[]
        {
            NormalizeIdentityPart(evidence.Publisher ?? string.Empty),
            NormalizeIdentityPart(evidence.DisplayName),
            NormalizeIdentityPart(evidence.GameEdition ?? string.Empty),
            evidence.PublicationDate?.ToString("yyyy-MM-dd") ?? string.Empty
        });
        return Fingerprint(value);
    }

    public static string OccurrenceKey(string entityType, string name, string? identitySuffix = null)
    {
        var normalizedType = NormalizeIdentityPart(entityType);
        var normalizedName = NormalizeIdentityPart(name);
        if (string.IsNullOrEmpty(normalizedType) || string.IsNullOrEmpty(normalizedName))
        {
            throw new ArgumentException("Occurrence identity requires a non-blank entity type and name.");
        }

        var normalizedSuffix = NormalizeIdentityPart(identitySuffix ?? string.Empty);
        return string.IsNullOrEmpty(normalizedSuffix)
            ? $"{normalizedType}|{normalizedName}"
            : $"{normalizedType}|{normalizedName}|{normalizedSuffix}";
    }

    public static string SemanticFingerprint(string document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            throw new ArgumentException("Source occurrence document can not be blank.", nameof(document));
        }

        using var parsed = JsonDocument.Parse(document);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteSemanticCanonical(writer, parsed.RootElement, isRoot: true);
        }
        return Fingerprint(Encoding.UTF8.GetString(stream.ToArray()));
    }

    public static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void WriteSemanticCanonical(Utf8JsonWriter writer, JsonElement element, bool isRoot)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .Where(property => !isRoot || !ProvenanceProperties.Contains(property.Name))
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteSemanticCanonical(writer, property.Value, isRoot: false);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteSemanticCanonical(writer, child, isRoot: false);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
