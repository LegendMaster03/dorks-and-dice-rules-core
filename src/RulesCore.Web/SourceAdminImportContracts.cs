using RulesCore.Application.Sources;

namespace RulesCore.Web;

public sealed record SourceAdminImportRequest(
    string PackageKey,
    string PackageDisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string Json,
    string? GameEdition = null,
    string? ReleaseKind = null,
    DateOnly? PublicationDate = null,
    IReadOnlyList<string>? IncludedSourceCodes = null);

public sealed record SourceAdminImportPreparation(
    Import5eToolsDocumentRequest LogicalRequest,
    IReadOnlyList<string> AvailableSourceCodes,
    IReadOnlyList<string> IncludedSourceCodes,
    IReadOnlyList<string> Warnings);

public static class SourceAdminImportPartitioner
{
    public static SourceAdminImportPreparation Prepare(SourceAdminImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Json))
        {
            throw new ArgumentException("Source JSON can not be blank.", nameof(request));
        }

        var available = FiveEToolsDocumentInspector.DiscoverSourceCodes(
            request.Json,
            request.EditionKey);
        var includedSet = FiveEToolsDocumentInspector.NormalizeSourceCodes(
            request.IncludedSourceCodes);
        var included = includedSet
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warnings = new List<string>();
        string logicalJson;

        if (included.Length == 0)
        {
            logicalJson = request.Json;
            if (available.Count > 1)
            {
                warnings.Add(
                    $"The submitted aggregate contains multiple source codes ({string.Join(", ", available)}). All imported entities will receive the requested work/release metadata. If those source codes represent different logical publications or releases, preview and import each partition separately with IncludedSourceCodes.");
            }
        }
        else
        {
            var missing = included
                .Where(code => !available.Contains(code, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (missing.Length > 0)
            {
                warnings.Add(
                    $"The requested source-code filter contains codes not present in this document: {string.Join(", ", missing)}.");
            }

            logicalJson = FiveEToolsDocumentInspector.FilterBySourceCodes(
                request.Json,
                request.EditionKey,
                included,
                out var selectedEntityCount);
            if (selectedEntityCount == 0)
            {
                throw new InvalidDataException(
                    $"The source-code filter did not select any importable entities. Available source codes: {string.Join(", ", available)}.");
            }

            warnings.Add(
                $"Source-code partition active: only entities from {string.Join(", ", included)} are included in this preview/import. The submitted aggregate remains unchanged outside Rules Core.");
        }

        return new SourceAdminImportPreparation(
            new Import5eToolsDocumentRequest(
                request.PackageKey,
                request.PackageDisplayName,
                request.Provider,
                request.License,
                request.IsPublic,
                request.WorkKey,
                request.WorkDisplayName,
                request.EditionKey,
                request.EditionDisplayName,
                logicalJson,
                request.GameEdition,
                request.ReleaseKind,
                request.PublicationDate),
            available,
            included,
            warnings);
    }

    public static IReadOnlyList<string> InspectSourceCodes(
        string json,
        string? fallbackSourceCode = null) =>
        FiveEToolsDocumentInspector.DiscoverSourceCodes(
            json,
            fallbackSourceCode ?? "uploaded-document");
}