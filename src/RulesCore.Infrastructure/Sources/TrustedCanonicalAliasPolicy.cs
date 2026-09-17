using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

internal static class TrustedCanonicalAliasPolicy
{
    public const string PublicationSourceCodeScheme = "rules-core-source-code";
    private const string PcGenSourceShortScheme = "pcgen-source-short";

    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        // Exact competency translations are an importer concern, not a trusted-lineage concern.
        // Apply them for every supported representation before adding any source-lineage alias.
        record = ExactCompetencyTranslationPolicy.Apply(representation, record);

        if (string.IsNullOrWhiteSpace(record.ContentJson)
            || !TrustedSourceLineageRegistry.TryResolveScheme(
                representation.FormatKey,
                representation.Artifact.SourceUri,
                out var scheme))
        {
            return record;
        }

        var aliases = record.CanonicalAliases is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(record.CanonicalAliases, StringComparer.Ordinal);
        aliases[scheme] = record.NativeKey;
        return record with { CanonicalAliases = aliases };
    }

    public static NormalizedSourcePublication ApplyPublication(
        NormalizedSourceRepresentation representation,
        NormalizedSourcePublication publication)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(publication);

        if (!string.Equals(representation.FormatKey, PcGenSourceFormatAdapter.Format, StringComparison.Ordinal)
            || !TrustedSourceLineageRegistry.TryResolveScheme(
                representation.FormatKey,
                representation.Artifact.SourceUri,
                out _))
        {
            return publication;
        }

        var sourceCode = publication.ExternalIdentifiers?
            .FirstOrDefault(value => string.Equals(
                value.Key,
                PcGenSourceShortScheme,
                StringComparison.OrdinalIgnoreCase))
            .Value;
        var canonicalSourceCode = ReviewedPcGenSrdSourceCode(
            publication,
            sourceCode,
            representation.Artifact.SourceUri);
        if (canonicalSourceCode is null)
        {
            return publication;
        }

        // pcgen-source-short remains preserved in the source representation metadata and each
        // SourceEntity.SourceCode. Once the official PCGen SRD identity is reviewed, do not also
        // persist that contextual abbreviation as a globally reusable canonical-publication alias.
        var identifiers = publication.ExternalIdentifiers is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : publication.ExternalIdentifiers
                .Where(value => !string.Equals(
                    value.Key,
                    PcGenSourceShortScheme,
                    StringComparison.OrdinalIgnoreCase))
                .ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
        identifiers[PublicationSourceCodeScheme] = canonicalSourceCode;
        return publication with { ExternalIdentifiers = identifiers };
    }

    private static string? ReviewedPcGenSrdSourceCode(
        NormalizedSourcePublication publication,
        string? sourceCode,
        string? sourceUri)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)
            || !Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var publisher = CanonicalSourceIdentity.NormalizeIdentityPart(publication.Publisher ?? string.Empty);
        if (!string.IsNullOrEmpty(publisher)
            && !string.Equals(publisher, "wizards-of-the-coast", StringComparison.Ordinal))
        {
            return null;
        }

        var normalizedCode = sourceCode.Trim().ToLowerInvariant();
        var edition = CanonicalSourceIdentity.NormalizeIdentityPart(publication.GameEdition ?? string.Empty);
        var title = CanonicalSourceIdentity.NormalizeIdentityPart(publication.DisplayName);
        var path = Uri.UnescapeDataString(uri.AbsolutePath).Replace('\\', '/').ToLowerInvariant();

        return (normalizedCode, edition, title) switch
        {
            ("srd", "3e", "system-reference-document")
                when path.Contains("/data/3e/", StringComparison.Ordinal) => "SRD3",
            ("rsrd", "3-5e", "revised-v-3-5-system-reference-document")
                when path.Contains("/data/35e/", StringComparison.Ordinal) => "SRD35",
            ("rsrd", "3-5e", "revised-system-reference-document")
                when path.Contains("/data/35e/", StringComparison.Ordinal) => "SRD35",
            _ => null
        };
    }
}
