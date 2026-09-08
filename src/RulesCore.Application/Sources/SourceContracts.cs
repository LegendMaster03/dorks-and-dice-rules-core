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
    string Json);

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
    IReadOnlyList<ImportedSourceEntity> Entities);

public sealed record SourcePackageSummary(
    Guid Id,
    string Key,
    string DisplayName,
    string Provider,
    string? License,
    bool IsPublic);

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
