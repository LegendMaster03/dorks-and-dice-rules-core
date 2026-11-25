using System.Collections.Immutable;
using System.Text.Json;

namespace RulesCore.Content;

/// <summary>Consumer-safe package attribution, distinct from individual entity provenance.</summary>
public sealed record NativePackageMetadata(
    string RulesetId, string? DisplayName, string? SourceSystem, string? SourceEdition,
    string? SourcePublication, string? SourceVersion, string? LicenseIdentifier,
    string? LicenseUrl, string? AttributionStatement, ImmutableArray<string> CopyrightNotices,
    ImmutableArray<string> OriginalSources, ImmutableArray<string> ModificationNotices,
    ImmutableArray<string> Acknowledgments)
{
    internal static NativePackageMetadata FromManifest(string id, JsonElement manifest)
    {
        // Explicit allowlist. Never forward the manifest or distribution configuration.
        var license = Text(manifest, "license");
        var url = HttpsUrl(Text(manifest, "licenseUrl"));
        return new(id, Text(manifest, "name"), Text(manifest, "sourceSystem"),
            Text(manifest, "sourceEdition"), Text(manifest, "sourcePublication"),
            Text(manifest, "sourceVersion"), license, url,
            Text(manifest, "attribution"), Strings(manifest, "copyrightNotices"),
            Strings(manifest, "originalSources").Where(s => HttpsUrl(s) is not null).ToImmutableArray(),
            Strings(manifest, "modificationNotices"), Strings(manifest, "acknowledgments"));
    }

    private static string? Text(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString() : null;

    private static string? HttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Length > 0 && string.IsNullOrEmpty(uri.UserInfo) ? value : null;

    private static ImmutableArray<string> Strings(JsonElement element, string key) =>
        element.TryGetProperty(key, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!).Where(v => !string.IsNullOrWhiteSpace(v)).ToImmutableArray()
            : ImmutableArray<string>.Empty;
}
