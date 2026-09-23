using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class CurrentUserSourceService : ICurrentUserSourceService
{
    private static readonly HttpClient SharedHttpClient =
        CurrentUserSourceRemoteResolver.CreateSharedHttpClient();

    private readonly RulesCoreDbContext dbContext;
    private readonly INormalizedSourceImportService importer;
    private readonly ISourceFormatAdapterRegistry adapters;
    private readonly ISourceGrantService grants;
    private readonly CurrentUserSourceRemoteResolver remoteResolver;
    private readonly Func<CurrentUserSourceImportProgress, CancellationToken, Task>? progressReporter;

    // Compatibility constructor retained for existing integration/bootstrap callers while
    // current-user ingestion moves to the normalized adapter pipeline.
    public CurrentUserSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService legacyImporter,
        ISourceGrantService grants)
        : this(
            dbContext,
            new NormalizedSourceImportService(dbContext),
            CreateDefaultRegistry(),
            grants,
            SharedHttpClient)
    {
        _ = legacyImporter;
    }

    public CurrentUserSourceService(
        RulesCoreDbContext dbContext,
        ISourceImportService legacyImporter,
        ISourceGrantService grants,
        HttpClient httpClient)
        : this(
            dbContext,
            new NormalizedSourceImportService(dbContext),
            CreateDefaultRegistry(),
            grants,
            httpClient)
    {
        _ = legacyImporter;
    }

    public CurrentUserSourceService(
        RulesCoreDbContext dbContext,
        INormalizedSourceImportService importer,
        ISourceFormatAdapterRegistry adapters,
        ISourceGrantService grants,
        HttpClient httpClient,
        Func<CurrentUserSourceImportProgress, CancellationToken, Task>? progressReporter = null)
    {
        this.dbContext = dbContext;
        this.importer = importer;
        this.adapters = adapters;
        this.grants = grants;
        remoteResolver = new CurrentUserSourceRemoteResolver(adapters, httpClient);
        this.progressReporter = progressReporter;
    }

    public async Task<IReadOnlyList<CurrentUserSourceView>> ListAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        await EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    current_user_source_id,
                    source_kind,
                    display_name,
                    source_url,
                    source_package_id,
                    source_codes_json::text AS source_codes_json,
                    entity_count,
                    added_at,
                    refreshed_at
                FROM current_user_source
                WHERE user_id = @user_id
                ORDER BY added_at DESC, current_user_source_id;
                """;
            AddParameter(command, "@user_id", userId);

            var results = new List<CurrentUserSourceView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadView(reader));
            }
            return results;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<CurrentUserSourceView> AddAsync(
        string currentUserId,
        AddCurrentUserSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = RequireUserId(currentUserId);
        var kind = NormalizeKind(request.Kind);

        string displayName;
        string? sourceUrl;
        string originIdentity;
        IReadOnlyList<NormalizedSourceRepresentation> representations;

        if (kind == CurrentUserSourceKinds.Upload)
        {
            var bytes = ReadUploadBytes(request);
            var fileName = NormalizeOptional(request.FileName, 500)
                ?? (request.Content is not null ? "Uploaded source.json" : "Uploaded source");
            var hash = Fingerprint(bytes);
            var artifact = new SourceRepresentationArtifact(
                fileName,
                bytes,
                $"upload:{hash}",
                MediaType: null);
            var representation = adapters.TryRead(artifact)
                ?? throw new InvalidDataException("The uploaded file is not compatible with Rules Core.");
            representations = [representation];
            displayName = fileName;
            sourceUrl = null;
            originIdentity = artifact.OriginIdentity;
        }
        else
        {
            var uri = RequireWebSourceUri(request.Url);
            representations = await remoteResolver.ResolveAsync(uri, cancellationToken);
            if (representations.Count == 0)
            {
                throw new InvalidDataException("The Web source did not contain any compatible files.");
            }
            displayName = WebSourceDisplayName(uri);
            sourceUrl = uri.AbsoluteUri;
            originIdentity = $"web:{NormalizeWebOrigin(uri)}";
        }

        var originKey = Fingerprint(Encoding.UTF8.GetBytes(originIdentity));
        var packageKey = SharedPackageKey(originIdentity);
        var packageDisplayName = $"Shared user source {originKey[..16]}";
        const string packageProvider = "user-source";

        var reusable = await TryReadReusablePackageAsync(
            packageKey,
            representations,
            cancellationToken);
        if (reusable is not null)
        {
            if (progressReporter is not null)
            {
                await progressReporter(
                    new CurrentUserSourceImportProgress(
                        "finalizing",
                        reusable.Value.EntityCount,
                        reusable.Value.EntityCount,
                        "Reused an existing identical source package",
                        RecordsDiscovered: reusable.Value.EntityCount,
                        RecordsTranslated: reusable.Value.EntityCount,
                        EntitiesPersisted: reusable.Value.EntityCount,
                        UnchangedEntities: reusable.Value.EntityCount,
                        RepresentationsStored: 0,
                        RepresentationsReused: representations.Count),
                    cancellationToken);
            }

            await grants.GrantAsync(userId, reusable.Value.PackageId, cancellationToken);
            await EnsureSchemaAsync(cancellationToken);
            if (kind == CurrentUserSourceKinds.Web && sourceUrl is not null)
            {
                await new IncompleteCurrentUserSourceImportCleanupService(dbContext)
                    .CleanupWebAddAsync(userId, sourceUrl, cancellationToken);
            }
            return await UpsertRegistrationAsync(
                userId,
                kind,
                displayName,
                sourceUrl,
                reusable.Value.PackageId,
                originKey,
                reusable.Value.SourceCodes,
                reusable.Value.EntityCount,
                cancellationToken);
        }

        await TryAdoptLegacyPackageAsync(
            userId,
            originIdentity,
            packageKey,
            packageDisplayName,
            representations,
            cancellationToken);

        Guid? packageId = null;
        var entityCount = 0;
        var publicationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalRecords = representations.Sum(value => value.Records.Count);
        var totalPublications = representations.Sum(CountPublicationGroups);
        var translatedOffset = 0;
        var persistedOffset = 0;
        var publicationOffset = 0;
        var reconciliationIssueOffset = 0;
        var newEntitiesOffset = 0;
        var unchangedEntitiesOffset = 0;
        var newRevisionsOffset = 0;
        var translationOnlyUpdatesOffset = 0;
        var representationsStoredOffset = 0;
        var representationsReusedOffset = 0;

        for (var representationIndex = 0; representationIndex < representations.Count; representationIndex++)
        {
            var representation = representations[representationIndex];
            CurrentUserSourceImportProgress? latestRepresentationProgress = null;

            async Task ReportRepresentationProgressAsync(
                CurrentUserSourceImportProgress progress,
                CancellationToken progressCancellationToken)
            {
                latestRepresentationProgress = progress;
                if (progressReporter is null) return;
                await progressReporter(
                    AggregateRepresentationProgress(
                        progress,
                        representation,
                        representationIndex,
                        representations.Count,
                        totalRecords,
                        totalPublications,
                        translatedOffset,
                        persistedOffset,
                        publicationOffset,
                        reconciliationIssueOffset,
                        newEntitiesOffset,
                        unchangedEntitiesOffset,
                        newRevisionsOffset,
                        translationOnlyUpdatesOffset,
                        representationsStoredOffset,
                        representationsReusedOffset),
                    progressCancellationToken);
            }

            var importRequest = new ImportNormalizedSourceRequest(
                packageKey,
                packageDisplayName,
                packageProvider,
                License: null,
                IsPublic: false,
                representation);
            if (progressReporter is not null)
            {
                importRequest = importRequest with
                {
                    ProgressReporter = ReportRepresentationProgressAsync
                };
            }

            var imported = await importer.ImportAsync(importRequest, cancellationToken);
            packageId ??= imported.PackageId;
            if (packageId != imported.PackageId)
            {
                throw new InvalidOperationException(
                    "One added source unexpectedly resolved to multiple source packages.");
            }

            entityCount += imported.Entities.Count;
            foreach (var key in imported.SourceCodes)
            {
                publicationKeys.Add(key);
            }

            translatedOffset += representation.Records.Count;
            persistedOffset += latestRepresentationProgress?.EntitiesPersisted ?? imported.Entities.Count;
            publicationOffset += latestRepresentationProgress?.PublicationsProcessed
                ?? CountPublicationGroups(representation);
            reconciliationIssueOffset += latestRepresentationProgress?.ReconciliationIssueCount
                ?? imported.ReconciliationIssues.Count;
            newEntitiesOffset += latestRepresentationProgress?.NewEntities ?? 0;
            unchangedEntitiesOffset += latestRepresentationProgress?.UnchangedEntities ?? 0;
            newRevisionsOffset += latestRepresentationProgress?.NewRevisions ?? 0;
            translationOnlyUpdatesOffset += latestRepresentationProgress?.TranslationOnlyUpdates ?? 0;
            representationsStoredOffset += latestRepresentationProgress?.RepresentationsStored ?? 0;
            representationsReusedOffset += latestRepresentationProgress?.RepresentationsReused ?? 0;
        }

        if (packageId is null || entityCount == 0)
        {
            throw new InvalidDataException(
                kind == CurrentUserSourceKinds.Web
                    ? "The Web source did not contain any compatible files."
                    : "The uploaded file is not compatible with Rules Core.");
        }

        if (kind == CurrentUserSourceKinds.Web && sourceUrl is not null)
        {
            await new IncompleteCurrentUserSourceImportCleanupService(dbContext)
                .CleanupWebAddAsync(userId, sourceUrl, cancellationToken);
        }

        await grants.GrantAsync(userId, packageId.Value, cancellationToken);
        await EnsureSchemaAsync(cancellationToken);
        return await UpsertRegistrationAsync(
            userId,
            kind,
            displayName,
            sourceUrl,
            packageId.Value,
            originKey,
            publicationKeys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            entityCount,
            cancellationToken);
    }

    private static CurrentUserSourceImportProgress AggregateRepresentationProgress(
        CurrentUserSourceImportProgress progress,
        NormalizedSourceRepresentation representation,
        int representationIndex,
        int representationCount,
        int totalRecords,
        int totalPublications,
        int translatedOffset,
        int persistedOffset,
        int publicationOffset,
        int reconciliationIssueOffset,
        int newEntitiesOffset,
        int unchangedEntitiesOffset,
        int newRevisionsOffset,
        int translationOnlyUpdatesOffset,
        int representationsStoredOffset,
        int representationsReusedOffset)
    {
        var current = progress.Current;
        var total = progress.Total;
        if (string.Equals(progress.Stage, "translating", StringComparison.Ordinal))
        {
            current = AggregateCount(translatedOffset, progress.Current, totalRecords);
            total = totalRecords;
        }
        else if (string.Equals(progress.Stage, "persisting", StringComparison.Ordinal)
            || string.Equals(progress.Stage, "finalizing", StringComparison.Ordinal))
        {
            current = AggregateCount(persistedOffset, progress.Current, totalRecords);
            total = totalRecords;
        }
        else if (string.Equals(progress.Stage, "reconciling", StringComparison.Ordinal))
        {
            current = AggregateCount(publicationOffset, progress.Current, totalPublications);
            total = totalPublications;
        }

        var sourceSet = $"Source set {representationIndex + 1} of {representationCount}: {representation.Artifact.FileName}";
        var detail = string.IsNullOrWhiteSpace(progress.Detail)
            ? sourceSet
            : $"{sourceSet} · {progress.Detail}";
        var completedImportUnits = string.Equals(progress.Stage, "finalizing", StringComparison.Ordinal)
            ? representationIndex + 1
            : representationIndex;

        return progress with
        {
            Current = current,
            Total = total,
            Detail = detail,
            ImportUnitsProcessed = completedImportUnits,
            ImportUnitTotal = representationCount,
            RecordsDiscovered = totalRecords,
            RecordsTranslated = AggregateCount(
                translatedOffset,
                progress.RecordsTranslated,
                totalRecords),
            EntitiesPersisted = AggregateCount(
                persistedOffset,
                progress.EntitiesPersisted,
                totalRecords),
            NewEntities = newEntitiesOffset + (progress.NewEntities ?? 0),
            UnchangedEntities = unchangedEntitiesOffset + (progress.UnchangedEntities ?? 0),
            NewRevisions = newRevisionsOffset + (progress.NewRevisions ?? 0),
            TranslationOnlyUpdates = translationOnlyUpdatesOffset
                + (progress.TranslationOnlyUpdates ?? 0),
            PublicationsProcessed = AggregateCount(
                publicationOffset,
                progress.PublicationsProcessed,
                totalPublications),
            PublicationTotal = totalPublications,
            ReconciliationIssueCount = reconciliationIssueOffset
                + (progress.ReconciliationIssueCount ?? 0),
            RepresentationsStored = representationsStoredOffset
                + (progress.RepresentationsStored ?? 0),
            RepresentationsReused = representationsReusedOffset
                + (progress.RepresentationsReused ?? 0)
        };
    }

    private static int AggregateCount(int offset, int? current, int total) =>
        Math.Min(total, checked(offset + (current ?? 0)));

    private static int CountPublicationGroups(NormalizedSourceRepresentation representation) =>
        (representation.Publications ?? [])
            .Select(value => value.LocalKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    private async Task<bool> TryAdoptLegacyPackageAsync(
        string userId,
        string originIdentity,
        string sharedPackageKey,
        string sharedDisplayName,
        IReadOnlyList<NormalizedSourceRepresentation> representations,
        CancellationToken cancellationToken)
    {
        if (await dbContext.SourcePackages
            .AsNoTracking()
            .AnyAsync(value => value.Key == sharedPackageKey, cancellationToken))
        {
            return false;
        }

        var legacyKey =
            $"user-source-{Fingerprint(Encoding.UTF8.GetBytes($"{userId}\n{originIdentity}"))[..24]}";
        var legacy = await dbContext.SourcePackages
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Key == legacyKey, cancellationToken);
        if (legacy is null) return false;

        var stored = await dbContext.SourceRepresentations
            .AsNoTracking()
            .Where(value => value.SourcePackageId == legacy.Id)
            .Select(value => new { value.FormatKey, value.OriginIdentity, value.ContentSha256 })
            .ToArrayAsync(cancellationToken);
        if (stored.Length == 0) return false;

        var expected = representations
            .Select(RepresentationStorageIdentity)
            .ToHashSet(StringComparer.Ordinal);
        if (stored.Any(value => !expected.Contains(
                RepresentationStorageIdentity(
                    value.FormatKey,
                    value.OriginIdentity,
                    value.ContentSha256))))
        {
            return false;
        }

        var affected = await dbContext.SourcePackages
            .Where(value => value.Id == legacy.Id && value.Key == legacyKey)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(value => value.Key, sharedPackageKey)
                    .SetProperty(value => value.DisplayName, sharedDisplayName)
                    .SetProperty(value => value.Provider, "user-source")
                    .SetProperty(value => value.License, (string?)null),
                cancellationToken);
        dbContext.ChangeTracker.Clear();
        return affected == 1;
    }

    private async Task<(Guid PackageId, int EntityCount, string[] SourceCodes)?> TryReadReusablePackageAsync(
        string packageKey,
        IReadOnlyList<NormalizedSourceRepresentation> representations,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.SourcePackages
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is null) return null;

        var stored = await dbContext.SourceRepresentations
            .AsNoTracking()
            .Where(value => value.SourcePackageId == package.Id)
            .Select(value => new { value.FormatKey, value.OriginIdentity, value.ContentSha256 })
            .ToArrayAsync(cancellationToken);
        var actual = stored
            .Select(value => RepresentationStorageIdentity(
                value.FormatKey,
                value.OriginIdentity,
                value.ContentSha256))
            .ToHashSet(StringComparer.Ordinal);
        var expected = representations
            .Select(RepresentationStorageIdentity)
            .ToArray();
        if (expected.Any(value => !actual.Contains(value))) return null;

        var currentEntities = representations
            .SelectMany(value => value.Records.Select(record =>
                $"{value.FormatKey}\n{record.NativeKey}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (currentEntities.Length == 0) return null;

        var sourceCodes = representations
            .SelectMany(value => value.Records)
            .Select(value => value.SourceCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return (package.Id, currentEntities.Length, sourceCodes);
    }

    internal static string SharedPackageKey(string originIdentity) =>
        $"user-origin-{Fingerprint(Encoding.UTF8.GetBytes(originIdentity))}";

    internal static string PackageOriginIdentity(string representationOriginIdentity)
    {
        if (representationOriginIdentity.StartsWith("web:", StringComparison.Ordinal))
        {
            var separator = representationOriginIdentity.IndexOf('#', 4);
            return separator < 0
                ? representationOriginIdentity
                : representationOriginIdentity[..separator];
        }

        return representationOriginIdentity;
    }

    private static string RepresentationStorageIdentity(NormalizedSourceRepresentation representation)
    {
        var contentHash = Fingerprint(representation.Artifact.Content);
        return RepresentationStorageIdentity(
            representation.FormatKey,
            representation.Artifact.OriginIdentity,
            contentHash);
    }

    private static string RepresentationStorageIdentity(
        string formatKey,
        string originIdentity,
        string contentHash) =>
        $"{formatKey}\n{originIdentity}\n{contentHash}";

    public async Task<CurrentUserSourceView?> RefreshAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        if (currentUserSourceId == Guid.Empty)
        {
            throw new ArgumentException("Source ID can not be empty.", nameof(currentUserSourceId));
        }

        var existing = (await ListAsync(userId, cancellationToken))
            .SingleOrDefault(value => value.Id == currentUserSourceId);
        if (existing is null)
        {
            return null;
        }
        if (!string.Equals(existing.Kind, CurrentUserSourceKinds.Web, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(existing.Url))
        {
            throw new InvalidOperationException(
                "Uploaded files are immutable snapshots and can not be refreshed. Upload the newer file as a source instead.");
        }

        return await AddAsync(
            userId,
            new AddCurrentUserSourceRequest(CurrentUserSourceKinds.Web, Url: existing.Url),
            cancellationToken);
    }

    private async Task<CurrentUserSourceView> UpsertRegistrationAsync(
        string userId,
        string kind,
        string displayName,
        string? sourceUrl,
        Guid packageId,
        string originKey,
        IReadOnlyList<string> publicationKeys,
        int entityCount,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sourceCodesJson = JsonSerializer.Serialize(publicationKeys);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO current_user_source (
                    current_user_source_id,
                    user_id,
                    source_kind,
                    display_name,
                    source_url,
                    source_package_id,
                    origin_key,
                    source_codes_json,
                    entity_count,
                    added_at,
                    refreshed_at)
                VALUES (
                    @id,
                    @user_id,
                    @kind,
                    @display_name,
                    @source_url,
                    @package_id,
                    @origin_key,
                    CAST(@source_codes_json AS jsonb),
                    @entity_count,
                    @added_at,
                    @refreshed_at)
                ON CONFLICT (user_id, origin_key)
                DO UPDATE SET
                    source_kind = EXCLUDED.source_kind,
                    display_name = EXCLUDED.display_name,
                    source_url = EXCLUDED.source_url,
                    source_package_id = EXCLUDED.source_package_id,
                    source_codes_json = EXCLUDED.source_codes_json,
                    entity_count = EXCLUDED.entity_count,
                    refreshed_at = EXCLUDED.refreshed_at
                RETURNING
                    current_user_source_id,
                    source_kind,
                    display_name,
                    source_url,
                    source_package_id,
                    source_codes_json::text AS source_codes_json,
                    entity_count,
                    added_at,
                    refreshed_at;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@user_id", userId);
            AddParameter(command, "@kind", kind);
            AddParameter(command, "@display_name", displayName);
            AddNullableParameter(command, "@source_url", sourceUrl);
            AddParameter(command, "@package_id", packageId);
            AddParameter(command, "@origin_key", originKey);
            AddParameter(command, "@source_codes_json", sourceCodesJson);
            AddParameter(command, "@entity_count", entityCount);
            AddParameter(command, "@added_at", now);
            AddParameter(command, "@refreshed_at", now);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Added source was not readable after it was saved.");
            }
            return ReadView(reader);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private Task EnsureSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(CurrentUserSourceSchemaSql, cancellationToken);

    private static byte[] ReadUploadBytes(AddCurrentUserSourceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ContentBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(request.ContentBase64);
                if (bytes.Length == 0)
                {
                    throw new InvalidDataException("Uploaded source file can not be empty.");
                }
                return bytes;
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("The uploaded file content is not valid Base64.", exception);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            return Encoding.UTF8.GetBytes(request.Content);
        }
        throw new InvalidDataException("Uploaded source file can not be empty.");
    }

    private static CurrentUserSourceView ReadView(DbDataReader reader)
    {
        var sourceCodesJson = reader.GetString(reader.GetOrdinal("source_codes_json"));
        var sourceCodes = JsonSerializer.Deserialize<string[]>(sourceCodesJson) ?? [];
        return new CurrentUserSourceView(
            reader.GetGuid(reader.GetOrdinal("current_user_source_id")),
            reader.GetString(reader.GetOrdinal("source_kind")),
            reader.GetString(reader.GetOrdinal("display_name")),
            GetNullableString(reader, "source_url"),
            reader.GetGuid(reader.GetOrdinal("source_package_id")),
            sourceCodes.Length,
            reader.GetInt32(reader.GetOrdinal("entity_count")),
            sourceCodes,
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("added_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("refreshed_at")));
    }

    private static ISourceFormatAdapterRegistry CreateDefaultRegistry() =>
        new SourceFormatAdapterRegistry([
            new FiveEToolsSourceFormatAdapter(),
            new PcGenSourceFormatAdapter(),
            new PdfSourceFormatAdapter()
        ]);

    private static Uri RequireWebSourceUri(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Web source URL must be an absolute HTTPS URL.", nameof(value));
        }
        if (!string.IsNullOrEmpty(uri.UserInfo)
            || string.Equals(uri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.AbsoluteUri.Length > 2000)
        {
            throw new ArgumentException("Web source URL is not allowed.", nameof(value));
        }
        return uri;
    }

    private static string WebSourceDisplayName(Uri uri)
    {
        if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                return $"{segments[0]}/{segments[1]}";
            }
        }
        return uri.Host + uri.AbsolutePath.TrimEnd('/');
    }

    private static string NormalizeWebOrigin(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };
        return builder.Uri.AbsoluteUri;
    }

    private static string NormalizeKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Source kind can not be blank.", nameof(value));
        }
        var normalized = value.Trim().ToLowerInvariant();
        if (!CurrentUserSourceKinds.IsSupported(normalized))
        {
            throw new ArgumentException("Source kind must be 'upload' or 'web'.", nameof(value));
        }
        return normalized;
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("User ID can not exceed 200 characters.", nameof(value));
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string Fingerprint(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string? GetNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddNullableParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }







    private const string CurrentUserSourceSchemaSql = """
        CREATE TABLE IF NOT EXISTS current_user_source (
            current_user_source_id uuid NOT NULL,
            user_id varchar(200) NOT NULL,
            source_kind varchar(20) NOT NULL,
            display_name varchar(500) NOT NULL,
            source_url varchar(2000) NULL,
            source_package_id uuid NOT NULL,
            origin_key varchar(64) NOT NULL,
            source_codes_json jsonb NOT NULL,
            entity_count integer NOT NULL,
            added_at timestamp with time zone NOT NULL,
            refreshed_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_current_user_source PRIMARY KEY (current_user_source_id),
            CONSTRAINT fk_current_user_source_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE,
            CONSTRAINT ck_current_user_source_kind CHECK (source_kind IN ('upload', 'web')));
        CREATE UNIQUE INDEX IF NOT EXISTS ux_current_user_source_user_origin
            ON current_user_source(user_id, origin_key);
        CREATE INDEX IF NOT EXISTS ix_current_user_source_user
            ON current_user_source(user_id, added_at DESC);
        ALTER TABLE current_user_source
            ADD COLUMN IF NOT EXISTS upstream_version varchar(500) NULL;
        ALTER TABLE current_user_source
            ADD COLUMN IF NOT EXISTS last_checked_at timestamp with time zone NULL;
        ALTER TABLE current_user_source
            ADD COLUMN IF NOT EXISTS last_refresh_error varchar(1000) NULL;
        CREATE INDEX IF NOT EXISTS ix_current_user_source_due_web_refresh
            ON current_user_source(last_checked_at)
            WHERE source_kind = 'web';
        """;
}