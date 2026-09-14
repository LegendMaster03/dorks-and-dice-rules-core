namespace RulesCore.Application.Sources;

public sealed record SourceRepresentationArtifact(
    string FileName,
    byte[] Content,
    string OriginIdentity,
    string? SourceUri = null,
    string? MediaType = null);

public sealed record NormalizedSourceRecord(
    string EntityType,
    string Name,
    string? SourceCode,
    string NativeKey,
    string RawJson,
    string? LocatorKey = null,
    string? PublicationLocalKey = null,
    string NativeIdentityJson = "{}",
    string? SemanticJson = null,
    IReadOnlyDictionary<string, string>? CanonicalAliases = null);

public sealed record NormalizedSourcePublication(
    string LocalKey,
    string DisplayName,
    string? Publisher = null,
    string? GameEdition = null,
    DateOnly? PublicationDate = null,
    IReadOnlyDictionary<string, string>? ExternalIdentifiers = null);

public sealed record NormalizedSourceRepresentation(
    string FormatKey,
    SourceRepresentationArtifact Artifact,
    IReadOnlyList<NormalizedSourceRecord> Records,
    IReadOnlyList<NormalizedSourcePublication>? Publications = null,
    string? MetadataJson = null);

public interface ISourceFormatAdapter
{
    string FormatKey { get; }

    bool IsCandidate(string? fileName, ReadOnlySpan<byte> content);

    NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact);
}

public interface ISourceFormatBatchAdapter : ISourceFormatAdapter
{
    IReadOnlyList<NormalizedSourceRepresentation> TryReadMany(
        IReadOnlyList<SourceRepresentationArtifact> artifacts);
}

public interface ISourceFormatAdapterRegistry
{
    bool IsCandidateFileName(string? fileName);

    NormalizedSourceRepresentation? TryRead(SourceRepresentationArtifact artifact);

    IReadOnlyList<NormalizedSourceRepresentation> TryReadMany(
        IReadOnlyList<SourceRepresentationArtifact> artifacts);
}

public sealed record ImportNormalizedSourceRequest(
    string PackageKey,
    string PackageDisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    NormalizedSourceRepresentation Representation);

public sealed record ImportedNormalizedPublication(
    Guid CanonicalPublicationId,
    string LocalKey,
    string DisplayName,
    int EntityCount);

public sealed record NormalizedSourceImportResult(
    Guid PackageId,
    IReadOnlyList<ImportedNormalizedPublication> Publications,
    IReadOnlyList<ImportedSourceEntity> Entities,
    IReadOnlyList<string> SourceCodes);

public interface INormalizedSourceImportService
{
    Task<NormalizedSourceImportResult> ImportAsync(
        ImportNormalizedSourceRequest request,
        CancellationToken cancellationToken = default);
}
