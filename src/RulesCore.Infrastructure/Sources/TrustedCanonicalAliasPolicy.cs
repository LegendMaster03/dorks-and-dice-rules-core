using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

internal static class TrustedCanonicalAliasPolicy
{
    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.SemanticJson)
            || !string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.Ordinal)
            || !TryGetPcGenScheme(representation.Artifact.SourceUri, out var scheme))
        {
            return record;
        }

        var aliases = record.CanonicalAliases is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(record.CanonicalAliases, StringComparer.Ordinal);
        aliases[scheme] = record.NativeKey;
        return record with { CanonicalAliases = aliases };
    }

    private static bool TryGetPcGenScheme(string? sourceUri, out string scheme)
    {
        scheme = string.Empty;
        if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri)
            || !string.Equals(
                uri.Host,
                "raw.githubusercontent.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length < 3
            || !string.Equals(segments[0], "PCGen", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(segments[1], "pcgen", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "pcgen-org-pcgen";
            return true;
        }
        if (string.Equals(segments[1], "pcgen-newsources", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "pcgen-org-pcgen-newsources";
            return true;
        }
        return false;
    }
}
