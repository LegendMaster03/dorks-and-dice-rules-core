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
}
