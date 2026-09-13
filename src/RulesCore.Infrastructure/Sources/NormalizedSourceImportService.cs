using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class NormalizedSourceImportService(RulesCoreDbContext dbContext)
    : INormalizedSourceImportService
{
    public async Task<NormalizedSourceImportResult> ImportAsync(
        ImportNormalizedSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Representation);
        if (request.Representation.Publications.Count == 0)
        {
            throw new InvalidDataException("The source did not contain any compatible publications.");
        }

        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await SourcePublisherStore.EnsureSchemaAsync(dbContext, cancellationToken);
        await EnsureRepresentationSchemaAsync(cancellationToken);

        var packageKey = NormalizeKey(request.PackageKey, 200);
        var now = DateTimeOffset.UtcNow;
        SourcePackage package;
        var persisted = new List<PersistedPublication>();
        var allImportedEntities = new List<ImportedSourceEntity>();
        var importedPublications = new List<ImportedNormalizedPublication>();

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var existingPackage = await dbContext.SourcePackages
                .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
            if (existingPackage is null)
            {
                package = new SourcePackage
                {
                    Id = Guid.NewGuid(),
                    Key = packageKey,
                    DisplayName = Require(request.PackageDisplayName, nameof(request.PackageDisplayName), 300),
                    Provider = Require(request.Provider, nameof(request.Provider), 200),
                    License = NormalizeOptional(request.License, 300),
                    IsPublic = request.IsPublic,
                    CreatedAt = now
                };
                dbContext.SourcePackages.Add(package);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                package = existingPackage;
                EnsurePackageMatches(package, request);
            }

            foreach (var publication in request.Representation.Publications)
            {
                var localKey = Require(publication.LocalKey, nameof(publication.LocalKey), 500);
                var workKey = NormalizeKey($"publication-{localKey}", 200);
                var work = await dbContext.SourceWorks.SingleOrDefaultAsync(
                    value => value.SourcePackageId == package.Id && value.Key == workKey,
                    cancellationToken);
                if (work is null)
                {
                    work = new SourceWork
                    {
                        Id = Guid.NewGuid(),
                        SourcePackageId = package.Id,
                        Key = workKey,
                        DisplayName = Require(publication.DisplayName, nameof(publication.DisplayName), 300),
                        CreatedAt = now
                    };
                    dbContext.SourceWorks.Add(work);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                else if (!string.Equals(work.DisplayName, publication.DisplayName.Trim(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Source publication '{workKey}' is already registered as '{work.DisplayName}', not '{publication.DisplayName.Trim()}'.");
                }

                var edition = await dbContext.SourceEditions.SingleOrDefaultAsync(
                    value => value.SourceWorkId == work.Id && value.Key == "current",
                    cancellationToken);
                if (edition is null)
                {
                    edition = new SourceEdition
                    {
                        Id = Guid.NewGuid(),
                        SourceWorkId = work.Id,
                        Key = "current",
                        DisplayName = "Current source representation",
                        CreatedAt = now
                    };
                    dbContext.SourceEditions.Add(edition);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await SourceFrameworkStore.MergeEditionMetadataAsync(
                    dbContext,
                    edition.Id,
                    NormalizeOptional(publication.GameEdition, 20),
                    SourceReleaseKinds.Other,
                    publication.PublicationDate,
                    cancellationToken);
                await SourcePublisherStore.MergePublisherAsync(
                    dbContext,
                    edition.Id,
                    NormalizeOptional(publication.Publisher, 300),
                    cancellationToken);

                var importedForPublication = new List<(ImportedSourceEntity Entity, NormalizedSourceRecord Record)>();
                foreach (var record in publication.Records)
                {
                    var normalizedRecord = NormalizeRecord(record, localKey);
                    var entity = await dbContext.SourceEntities.SingleOrDefaultAsync(
                        value => value.SourceEditionId == edition.Id
                            && value.NaturalKey == normalizedRecord.NaturalKey,
                        cancellationToken);
                    if (entity is null)
                    {
                        entity = new SourceEntity
                        {
                            Id = Guid.NewGuid(),
                            SourceEditionId = edition.Id,
                            EntityType = normalizedRecord.EntityType,
                            Name = normalizedRecord.Name,
                            SourceCode = normalizedRecord.SourceCode,
                            NaturalKey = normalizedRecord.NaturalKey,
                            CreatedAt = now
                        };
                        dbContext.SourceEntities.Add(entity);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    else if (!string.Equals(entity.EntityType, normalizedRecord.EntityType, StringComparison.Ordinal)
                             || !string.Equals(entity.Name, normalizedRecord.Name, StringComparison.Ordinal)
                             || !string.Equals(entity.SourceCode, normalizedRecord.SourceCode, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Normalized source record '{normalizedRecord.NaturalKey}' conflicts with an existing immutable source identity.");
                    }

                    var fingerprint = CanonicalJsonFingerprint(normalizedRecord.RawJson);
                    var latest = await dbContext.SourceEntityRevisions
                        .Where(value => value.SourceEntityId == entity.Id)
                        .OrderByDescending(value => value.RevisionNumber)
                        .FirstOrDefaultAsync(cancellationToken);
                    var createdRevision = latest is null
                        || !string.Equals(latest.Fingerprint, fingerprint, StringComparison.Ordinal);
                    if (createdRevision)
                    {
                        latest = new SourceEntityRevision
                        {
                            Id = Guid.NewGuid(),
                            SourceEntityId = entity.Id,
                            RevisionNumber = (latest?.RevisionNumber ?? 0) + 1,
                            Fingerprint = fingerprint,
                            RawJson = normalizedRecord.RawJson,
                            ImportedAt = DateTimeOffset.UtcNow
                        };
                        dbContext.SourceEntityRevisions.Add(latest);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    var imported = new ImportedSourceEntity(
                        entity.Id,
                        entity.EntityType,
                        entity.Name,
                        entity.SourceCode,
                        latest!.RevisionNumber,
                        latest.Fingerprint,
                        createdRevision);
                    allImportedEntities.Add(imported);
                    importedForPublication.Add((imported, normalizedRecord));
                }

                persisted.Add(new PersistedPublication(publication, work.Id, edition.Id, importedForPublication));
            }

            var representationId = await StoreRepresentationAsync(
                package.Id,
                request.Representation,
                cancellationToken);

            foreach (var publication in persisted)
            {
                var fingerprints = publication.Entities
                    .Select(value => CanonicalSourceIdentity.SemanticFingerprint(value.Record.RawJson))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var evidence = new CanonicalPublicationEvidence(
                    publication.Publication.DisplayName,
                    publication.Publication.Publisher,
                    publication.Publication.GameEdition,
                    publication.Publication.PublicationDate,
                    publication.Publication.ExternalIdentifiers,
                    fingerprints);

                Guid? canonicalPublicationId = null;
                foreach (var value in publication.Entities)
                {
                    var association = await new CanonicalSourceRepresentationService(dbContext)
                        .AssociateSourceEntityAsync(
                            value.Entity.EntityId,
                            evidence,
                            new CanonicalSourceOccurrenceEvidence(
                                value.Record.EntityType,
                                value.Record.Name,
                                value.Record.LocatorKey,
                                CanonicalSourceIdentity.SemanticFingerprint(value.Record.RawJson)),
                            request.Representation.FormatKey,
                            cancellationToken);
                    canonicalPublicationId ??= association.Publication.Id;
                    if (canonicalPublicationId != association.Publication.Id)
                    {
                        throw new InvalidOperationException(
                            "One normalized publication unexpectedly resolved to multiple canonical publications.");
                    }
                }

                if (canonicalPublicationId is null)
                {
                    var identity = await new CanonicalSourceIdentityService(dbContext)
                        .ResolvePublicationAsync(evidence, cancellationToken);
                    canonicalPublicationId = identity.Id;
                }

                await new CanonicalPublicationEvidenceReconciliationService(dbContext).ReconcileAsync(
                    canonicalPublicationId.Value,
                    representationId,
                    evidence,
                    cancellationToken);

                await LinkRepresentationPublicationAsync(
                    representationId,
                    canonicalPublicationId.Value,
                    publication.WorkId,
                    publication.Publication.LocalKey,
                    cancellationToken);
                importedPublications.Add(new ImportedNormalizedPublication(
                    publication.WorkId,
                    publication.EditionId,
                    canonicalPublicationId.Value,
                    publication.Publication.DisplayName,
                    publication.Entities.Count));
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return new NormalizedSourceImportResult(
            package.Id,
            importedPublications,
            allImportedEntities,
            request.Representation.Publications
                .Select(value => value.LocalKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private async Task<Guid> StoreRepresentationAsync(
        Guid packageId,
        NormalizedSourceRepresentation representation,
        CancellationToken cancellationToken)
    {
        var artifact = representation.Artifact;
        var hash = Convert.ToHexString(SHA256.HashData(artifact.Content)).ToLowerInvariant();
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
                INSERT INTO source_representation (
                    source_representation_id,
                    source_package_id,
                    format_key,
                    origin_identity,
                    file_name,
                    source_uri,
                    media_type,
                    content_sha256,
                    content_length,
                    content_bytes,
                    metadata_json,
                    imported_at)
                VALUES (
                    @id, @package_id, @format_key, @origin_identity, @file_name,
                    @source_uri, @media_type, @hash, @length, @bytes,
                    CAST(@metadata_json AS jsonb), @imported_at)
                ON CONFLICT (source_package_id, origin_identity, content_sha256)
                DO UPDATE SET
                    metadata_json = EXCLUDED.metadata_json
                RETURNING source_representation_id;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@package_id", packageId);
            AddParameter(command, "@format_key", Require(representation.FormatKey, nameof(representation.FormatKey), 80));
            AddParameter(command, "@origin_identity", Require(artifact.OriginIdentity, nameof(artifact.OriginIdentity), 2000));
            AddParameter(command, "@file_name", Require(artifact.FileName, nameof(artifact.FileName), 500));
            AddNullableParameter(command, "@source_uri", NormalizeOptional(artifact.SourceUri, 2000));
            AddNullableParameter(command, "@media_type", NormalizeOptional(artifact.MediaType, 200));
            AddParameter(command, "@hash", hash);
            AddParameter(command, "@length", (long)artifact.Content.LongLength);
            AddParameter(command, "@bytes", artifact.Content);
            AddParameter(command, "@metadata_json", representation.MetadataJson ?? "{}");
            AddParameter(command, "@imported_at", DateTimeOffset.UtcNow);
            return (Guid)(await command.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Source representation was not readable after it was saved."));
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task LinkRepresentationPublicationAsync(
        Guid representationId,
        Guid canonicalPublicationId,
        Guid sourceWorkId,
        string localKey,
        CancellationToken cancellationToken)
    {
        await EnsureRepresentationLinkSchemaAsync(cancellationToken);
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
                INSERT INTO source_representation_publication (
                    source_representation_publication_id,
                    source_representation_id,
                    canonical_publication_id,
                    source_work_id,
                    local_key,
                    created_at)
                VALUES (@id, @representation_id, @publication_id, @work_id, @local_key, @created_at)
                ON CONFLICT (source_representation_id, source_work_id) DO UPDATE SET
                    canonical_publication_id = EXCLUDED.canonical_publication_id,
                    local_key = EXCLUDED.local_key;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@representation_id", representationId);
            AddParameter(command, "@publication_id", canonicalPublicationId);
            AddParameter(command, "@work_id", sourceWorkId);
            AddParameter(command, "@local_key", Require(localKey, nameof(localKey), 500));
            AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private Task EnsureRepresentationSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(RepresentationSchemaSql, cancellationToken);

    private Task EnsureRepresentationLinkSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(RepresentationLinkSchemaSql, cancellationToken);

    private static NormalizedSourceRecord NormalizeRecord(NormalizedSourceRecord record, string fallbackSourceCode)
    {
        var entityType = Require(record.EntityType, nameof(record.EntityType), 120);
        var name = Require(record.Name, nameof(record.Name), 300);
        var sourceCode = string.IsNullOrWhiteSpace(record.SourceCode)
            ? NormalizeKey(fallbackSourceCode, 120)
            : Require(record.SourceCode, nameof(record.SourceCode), 120);
        var naturalKey = Require(record.NaturalKey, nameof(record.NaturalKey), 800);
        if (string.IsNullOrWhiteSpace(record.RawJson))
        {
            throw new InvalidDataException("Normalized source record JSON can not be blank.");
        }
        using var _ = JsonDocument.Parse(record.RawJson);
        return record with
        {
            EntityType = entityType,
            Name = name,
            SourceCode = sourceCode,
            NaturalKey = naturalKey,
            LocatorKey = NormalizeOptional(record.LocatorKey, 500)
        };
    }

    private static string CanonicalJsonFingerprint(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, document.RootElement);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteCanonical(writer, child);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static void EnsurePackageMatches(SourcePackage package, ImportNormalizedSourceRequest request)
    {
        if (!string.Equals(package.DisplayName, request.PackageDisplayName.Trim(), StringComparison.Ordinal)
            || !string.Equals(package.Provider, request.Provider.Trim(), StringComparison.Ordinal)
            || !string.Equals(package.License, NormalizeOptional(request.License, 300), StringComparison.Ordinal)
            || package.IsPublic != request.IsPublic)
        {
            throw new InvalidOperationException(
                $"Source package '{package.Key}' is already registered with different immutable metadata.");
        }
    }

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value can not be blank.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static string NormalizeKey(string value, int maxLength)
    {
        var builder = new StringBuilder(value.Length);
        var separator = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (separator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(character);
                separator = false;
            }
            else
            {
                separator = true;
            }
        }
        var normalized = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(normalized))
        {
            normalized = CanonicalSourceIdentity.Fingerprint(value)[..24];
        }
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].TrimEnd('-');
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", nameof(value));
        }
        return normalized;
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

    private sealed record PersistedPublication(
        NormalizedSourcePublication Publication,
        Guid WorkId,
        Guid EditionId,
        IReadOnlyList<(ImportedSourceEntity Entity, NormalizedSourceRecord Record)> Entities);

    private const string RepresentationSchemaSql = """
        CREATE TABLE IF NOT EXISTS source_representation (
            source_representation_id uuid NOT NULL,
            source_package_id uuid NOT NULL,
            format_key varchar(80) NOT NULL,
            origin_identity varchar(2000) NOT NULL,
            file_name varchar(500) NOT NULL,
            source_uri varchar(2000) NULL,
            media_type varchar(200) NULL,
            content_sha256 varchar(64) NOT NULL,
            content_length bigint NOT NULL,
            content_bytes bytea NOT NULL,
            metadata_json jsonb NOT NULL,
            imported_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_representation PRIMARY KEY (source_representation_id),
            CONSTRAINT fk_source_representation_package FOREIGN KEY (source_package_id)
                REFERENCES source_package(source_package_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_representation_identity
            ON source_representation(source_package_id, origin_identity, content_sha256);
        CREATE INDEX IF NOT EXISTS ix_source_representation_package
            ON source_representation(source_package_id, imported_at DESC);
        """;

    private const string RepresentationLinkSchemaSql = """
        CREATE TABLE IF NOT EXISTS source_representation_publication (
            source_representation_publication_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            source_work_id uuid NOT NULL,
            local_key varchar(500) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_representation_publication PRIMARY KEY (source_representation_publication_id),
            CONSTRAINT fk_source_representation_publication_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_publication_canonical FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE RESTRICT,
            CONSTRAINT fk_source_representation_publication_work FOREIGN KEY (source_work_id)
                REFERENCES source_work(source_work_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_representation_publication_work
            ON source_representation_publication(source_representation_id, source_work_id);
        CREATE INDEX IF NOT EXISTS ix_source_representation_publication_canonical
            ON source_representation_publication(canonical_publication_id);
        """;
}
