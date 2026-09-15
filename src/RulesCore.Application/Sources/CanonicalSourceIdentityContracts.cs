using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RulesCore.Domain.Sources;

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
    private const string ExactCompetencyIdentityVersion = "rules-core-exact-competency-v1";
    private const string ExactCompetencyIdentityProperty = "exactCompetencyIdentity";

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
        var publisher = NormalizeIdentityPart(evidence.Publisher ?? string.Empty);
        var title = NormalizeIdentityPart(evidence.DisplayName);
        var gameEdition = NormalizeIdentityPart(evidence.GameEdition ?? string.Empty);
        var publicationDate = evidence.PublicationDate?.ToString("yyyy-MM-dd") ?? string.Empty;

        // A title by itself is not strong enough evidence to merge publications. When no
        // publisher, system/edition, or publication date is known, include stable content
        // evidence so same-titled but different works remain distinct until reviewed.
        var sparseContentIdentity = string.Empty;
        if (string.IsNullOrEmpty(publisher)
            && string.IsNullOrEmpty(gameEdition)
            && string.IsNullOrEmpty(publicationDate))
        {
            sparseContentIdentity = string.Join(
                '|',
                (evidence.OccurrenceFingerprints ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal));
        }

        return Fingerprint(string.Join('\n', new[]
        {
            publisher,
            title,
            gameEdition,
            publicationDate,
            sparseContentIdentity
        }));
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

        var mechanicalDocument = RulesMechanicalContent.ForRules(document);
        using var parsed = JsonDocument.Parse(mechanicalDocument);
        if (TryReadExactCompetencyIdentity(parsed.RootElement, out var exactCompetencyKey))
        {
            // Reviewed competency translations deliberately make edition-specific source
            // mechanics share one canonical competency identity. Raw/native revisions remain
            // distinct and available for provenance and mechanical comparison.
            return Fingerprint($"{ExactCompetencyIdentityVersion}\n{exactCompetencyKey}");
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteSemanticCanonical(writer, parsed.RootElement, isRoot: true);
        }
        return Fingerprint(Encoding.UTF8.GetString(stream.ToArray()));
    }

    public static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool TryReadExactCompetencyIdentity(JsonElement root, out string key)
    {
        key = string.Empty;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("_rulesCore", out var extension)
            || extension.ValueKind != JsonValueKind.Object
            || !extension.TryGetProperty(ExactCompetencyIdentityProperty, out var identity)
            || identity.ValueKind != JsonValueKind.Object
            || !identity.TryGetProperty("version", out var version)
            || version.ValueKind != JsonValueKind.String
            || !string.Equals(version.GetString(), ExactCompetencyIdentityVersion, StringComparison.Ordinal)
            || !identity.TryGetProperty("key", out var keyElement)
            || keyElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(keyElement.GetString()))
        {
            return false;
        }

        key = keyElement.GetString()!.Trim().ToLowerInvariant();
        return true;
    }

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
