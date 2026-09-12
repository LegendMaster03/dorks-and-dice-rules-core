using System.Text.Json;

namespace RulesCore.Application.Sources;

public sealed record Import5eToolsDocumentRequest(
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
    DateOnly? PublicationDate = null);

public static class SourceImportPreviewActions
{
    public const string NewEntity = "new-entity";
    public const string NewRevision = "new-revision";
    public const string Unchanged = "unchanged";
}

public sealed record SourceImportPreviewEntity(
    Guid? EntityId,
    string EntityType,
    string Name,
    string SourceCode,
    int? CurrentRevisionNumber,
    string Fingerprint,
    string Action);

public sealed record SourceImportPreviewResult(
    string PackageKey,
    string PackageDisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    string? GameEdition,
    string? ReleaseKind,
    DateOnly? PublicationDate,
    bool CanImport,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Warnings,
    int EntityCount,
    int NewEntityCount,
    int NewRevisionCount,
    int UnchangedCount,
    IReadOnlyList<SourceImportPreviewEntity> Entities);

public sealed record ImportedSourceEntity(
    Guid EntityId,
    string EntityType,
    string Name,
    string SourceCode,
    int RevisionNumber,
    string Fingerprint,
    bool CreatedRevision);

public sealed record SourceImportResult(
    Guid PackageId,
    Guid WorkId,
    Guid EditionId,
    IReadOnlyList<ImportedSourceEntity> Entities,
    string? GameEdition = null,
    string? ReleaseKind = null,
    DateOnly? PublicationDate = null);

public sealed record SourcePackageSummary(
    Guid Id,
    string Key,
    string DisplayName,
    string Provider,
    string? License,
    bool IsPublic);

public sealed record SourceEntitySummary(
    Guid EntityId,
    string EntityType,
    string Name,
    string SourceCode,
    int LatestRevisionNumber,
    string LatestFingerprint,
    DateTimeOffset LatestImportedAt,
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName);

public sealed record SourceEntityView(
    Guid EntityId,
    string EntityType,
    string Name,
    string SourceCode,
    int RevisionNumber,
    string Fingerprint,
    DateTimeOffset ImportedAt,
    string PackageKey,
    string PackageDisplayName,
    string WorkKey,
    string WorkDisplayName,
    string EditionKey,
    string EditionDisplayName,
    JsonElement Document);

public interface ISourceImportService
{
    Task<SourceImportPreviewResult> Preview5eToolsDocumentAsync(
        Import5eToolsDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<SourceImportResult> Import5eToolsDocumentAsync(
        Import5eToolsDocumentRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISourceCatalogService
{
    Task<IReadOnlyList<SourcePackageSummary>> GetAccessiblePackagesAsync(
        string? userId,
        CancellationToken cancellationToken = default);

    Task<SourceEntityView?> GetLatestAccessibleEntityAsync(
        Guid entityId,
        string? userId,
        CancellationToken cancellationToken = default);
}

public interface ISourceEntitySearchService
{
    Task<IReadOnlyList<SourceEntitySummary>> SearchAccessibleAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceEntitySummary>> SearchAccessiblePageAsync(
        string? userId,
        string? entityType = null,
        string? query = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);
}

public interface ISourceGrantService
{
    Task GrantAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default);

    Task<bool> HasGrantAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default);
}