using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

internal static class TrustedSourceLineageRegistry
{
    private sealed record Lineage(
        string Scheme,
        string FormatKey,
        string Host,
        string Owner,
        string Repository);

    private static readonly Lineage[] Lineages =
    [
        new(
            "pcgen-org-pcgen",
            PcGenSourceFormatAdapter.Format,
            "raw.githubusercontent.com",
            "PCGen",
            "pcgen"),
        new(
            "pcgen-org-pcgen-newsources",
            PcGenSourceFormatAdapter.Format,
            "raw.githubusercontent.com",
            "PCGen",
            "pcgen-newsources"),
        new(
            "5etools-mirror-3-5etools-src",
            FiveEToolsSourceFormatAdapter.Format,
            "raw.githubusercontent.com",
            "5etools-mirror-3",
            "5etools-src")
    ];

    public static bool IsRegisteredScheme(string aliasScheme)
    {
        var normalized = CanonicalSourceIdentity.NormalizeIdentityPart(aliasScheme);
        return Lineages.Any(value => string.Equals(value.Scheme, normalized, StringComparison.Ordinal));
    }

    public static bool TryResolveScheme(
        string formatKey,
        string? sourceUri,
        out string scheme)
    {
        scheme = string.Empty;
        if (string.IsNullOrWhiteSpace(formatKey)
            || !Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length < 3)
        {
            return false;
        }

        foreach (var lineage in Lineages)
        {
            if (!string.Equals(lineage.FormatKey, formatKey, StringComparison.Ordinal)
                || !string.Equals(lineage.Host, uri.Host, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(lineage.Owner, segments[0], StringComparison.OrdinalIgnoreCase)
                || !string.Equals(lineage.Repository, segments[1], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scheme = lineage.Scheme;
            return true;
        }

        return false;
    }
}
