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
        if (request.Representation.Records.Count == 0)
        {
            throw new InvalidDataException("The source did not contain any compatible source records.");
        }

        await EnsureSupplementalSchemaAsync(cancellationToken);

        var packageKey = NormalizeKey(request.PackageKey, 200);
        var now = DateTimeOffset.UtcNow;
        var allImportedEntities = new List<ImportedSourceEntity>();
        var persisted = new List<(SourceEntity Entity, SourceEntityRevision Revision, NormalizedSourceRecord Record)>();
        var importedPublications = new List<ImportedNormalizedPublication>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var package = await dbContext.SourcePackages
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is null)
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
            EnsurePackageMatches(package, request);
        }

        var representation = await StoreRepresentationAsync(
            package.Id,
            request.Representation,
            cancellationToken);

        foreach (var record in request.Representation.Records)
        {
            var normalized = NormalizeRecord(record);
            var entity = await dbContext.SourceEntities.SingleOrDefaultAsync(
                value => value.SourcePackageId == package.Id
                    && value.FormatKey == request.Representation.FormatKey
                    && value.NativeKey == normalized.NativeKey,
                cancellationToken);

            if (entity is null)
            {
                entity = new SourceEntity
                {
                    Id = Guid.NewGuid(),
                    SourcePackageId = package.Id,
                    FormatKey = Require(request.Representation.FormatKey, nameof(request.Representation.FormatKey), 80),
                    EntityType = normalized.EntityType,
                    Name = normalized.Name,
                    SourceCode = normalized.SourceCode,
                    NativeKey = normalized.NativeKey,
                    NativeIdentityJson = normalized.NativeIdentityJson ?? "{}",
                    CreatedAt = now
                };
                dbContext.SourceEntities.Add(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (!string.Equals(entity.EntityType, normalized.EntityType, StringComparison.Ordinal)
                     || !string.Equals(entity.Name, normalized.Name, StringComparison.Ordinal)
                     || !string.Equals(entity.SourceCode, normalized.SourceCode, StringComparison.Ordinal)
                     || !JsonEquivalent(entity.NativeIdentityJson, normalized.NativeIdentityJson ?? "{}"))
            {
                throw new InvalidOperationException(
                    $"Native source record '{normalized.NativeKey}' conflicts with an existing immutable source identity.");
            }

            var fingerprint = CanonicalJsonFingerprint(normalized.RawJson);
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
                    SourceRepresentationId = representation.Id,
                    RevisionNumber = (latest?.RevisionNumber ?? 0) + 1,
                    Fingerprint = fingerprint,
                    RawJson = normalized.RawJson,
                    LocatorKey = normalized.LocatorKey,
                    ImportedAt = DateTimeOffset.UtcNow
                };
                dbContext.SourceEntityRevisions.Add(latest);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await LinkRepresentationEntityAsync(
                representation.Id,
                entity.Id,
                latest!.Id,
                normalized.LocatorKey,
                cancellationToken);

            var imported = new ImportedSourceEntity(
                entity.Id,
                entity.EntityType,
                entity.Name,
                entity.SourceCode ?? string.Empty,
                latest.RevisionNumber,
                latest.Fingerprint,
                createdRevision);
            allImportedEntities.Add(imported);
            persisted.Add((entity, latest, normalized));
        }

        var publicationEvidence = (request.Representation.Publications ?? [])
            .GroupBy(value => Require(value.LocalKey, nameof(value.LocalKey), 500), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Single())
            .ToDictionary(value => value.LocalKey, StringComparer.OrdinalIgnoreCase);

        foreach (var publication in publicationEvidence.Values)
        {
            var records = persisted
                .Where(value => string.Equals(
                    value.Record.PublicationLocalKey,
                    publication.LocalKey,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var fingerprints = records
                .Select(value => CanonicalSourceIdentity.SemanticFingerprint(value.Record.RawJson))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var evidence = new CanonicalPublicationEvidence(
                publication.DisplayName,
                publication.Publisher,
                publication.GameEdition,
                publication.PublicationDate,
                publication.ExternalIdentifiers,
                fingerprints);

            Guid? canonicalPublicationId = null;
            foreach (var value in records)
            {
                var association = await new CanonicalSourceRepresentationService(dbContext)
                    .AssociateSourceEntityAsync(
                        value.Entity.Id,
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
                        "One publication-evidence group unexpectedly resolved to multiple canonical publications.");
                }
            }

            if (canonicalPublicationId is null)
            {
                canonicalPublicationId = (await new CanonicalSourceIdentityService(dbContext)
                    .ResolvePublicationAsync(evidence, cancellationToken)).Id;
            }

            await new CanonicalPublicationEvidenceReconciliationService(dbContext).ReconcileAsync(
                canonicalPublicationId.Value,
                representation.Id,
                evidence,
                cancellationToken);
            await LinkRepresentationPublicationAsync(
                representation.Id,
                canonicalPublicationId.Value,
                publication.LocalKey,
                cancellationToken);

            importedPublications.Add(new ImportedNormalizedPublication(
                canonicalPublicationId.Value,
                publication.LocalKey,
                publication.DisplayName,
                records.Length));
        }

        await transaction.CommitAsync(cancellationToken);

        return new NormalizedSourceImportResult(
            package.Id,
            importedPublications,
            allImportedEntities,
            request.Representation.Records
                .Select(value => value.SourceCode)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private async Task<SourceRepresentation> StoreRepresentationAsync(
        Guid packageId,
        NormalizedSourceRepresentation representation,
        CancellationToken cancellationToken)
    {
        var artifact = representation.Artifact;
        var hash = Convert.ToHexString(SHA256.HashData(artifact.Content)).ToLowerInvariant();
        var originIdentity = Require(artifact.OriginIdentity, nameof(artifact.OriginIdentity), 2000);
        var existing = await dbContext.SourceRepresentations.SingleOrDefaultAsync(
            value => value.SourcePackageId == packageId
                && value.OriginIdentity == originIdentity
                && value.ContentSha256 == hash,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var previous = await dbContext.SourceRepresentations
            .Where(value => value.SourcePackageId == packageId && value.OriginIdentity == originIdentity)
            .OrderByDescending(value => value.ImportedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var stored = new SourceRepresentation
        {
            Id = Guid.NewGuid(),
            SourcePackageId = packageId,
            PreviousSourceRepresentationId = previous?.Id,
            FormatKey = Require(representation.FormatKey, nameof(representation.FormatKey), 80),
            OriginIdentity = originIdentity,
            FileName = Require(artifact.FileName, nameof(artifact.FileName), 500),
            SourceUri = NormalizeOptional(artifact.SourceUri, 2000),
            MediaType = NormalizeOptional(artifact.MediaType, 200),
            ContentSha256 = hash,
            ContentLength = artifact.Content.LongLength,
            ContentBytes = artifact.Content,
            MetadataJson = NormalizeJson(representation.MetadataJson ?? "{}", nameof(representation.MetadataJson)),
            ImportedAt = DateTimeOffset.UtcNow
        };
        dbContext.SourceRepresentations.Add(stored);
        await dbContext.SaveChangesAsync(cancellationToken);
        return stored;
    }

    private async Task LinkRepresentationEntityAsync(
        Guid representationId,
        Guid entityId,
        Guid revisionId,
        string? locatorKey,
        CancellationToken cancellationToken)
    {
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
                INSERT INTO source_representation_entity (
                    source_representation_entity_id,
                    source_representation_id,
                    source_entity_id,
                    source_entity_revision_id,
                    locator_key,
                    created_at)
                VALUES (@id, @representation_id, @entity_id, @revision_id, @locator_key, @created_at)
                ON CONFLICT (source_representation_id, source_entity_id) DO UPDATE SET
                    source_entity_revision_id = EXCLUDED.source_entity_revision_id,
                    locator_key = EXCLUDED.locator_key;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@representation_id", representationId);
            AddParameter(command, "@entity_id", entityId);
            AddParameter(command, "@revision_id", revisionId);
            AddNullableParameter(command, "@locator_key", NormalizeOptional(locatorKey, 500));
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

    private async Task LinkRepresentationPublicationAsync(
        Guid representationId,
        Guid canonicalPublicationId,
        string localKey,
        CancellationToken cancellationToken)
    {
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
                    local_key,
                    created_at)
                VALUES (@id, @representation_id, @publication_id, @local_key, @created_at)
                ON CONFLICT (source_representation_id, local_key) DO UPDATE SET
                    canonical_publication_id = EXCLUDED.canonical_publication_id;
                """;
            AddParameter(command, "@id", Guid.NewGuid());
            AddParameter(command, "@representation_id", representationId);
            AddParameter(command, "@publication_id", canonicalPublicationId);
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

    private Task EnsureSupplementalSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(SupplementalSchemaSql, cancellationToken);

    private static NormalizedSourceRecord NormalizeRecord(NormalizedSourceRecord record)
    {
        var entityType = Require(record.EntityType, nameof(record.EntityType), 120);
        var name = Require(record.Name, nameof(record.Name), 300);
        var sourceCode = NormalizeOptional(record.SourceCode, 120);
        var nativeKey = Require(record.NativeKey, nameof(record.NativeKey), 1000);
        var rawJson = NormalizeJson(record.RawJson, nameof(record.RawJson));
        var nativeIdentityJson = NormalizeJson(record.NativeIdentityJson ?? "{}", nameof(record.NativeIdentityJson));
        return record with
        {
            EntityType = entityType,
            Name = name,
            SourceCode = sourceCode,
            NativeKey = nativeKey,
            RawJson = rawJson,
            LocatorKey = NormalizeOptional(record.LocatorKey, 500),
            PublicationLocalKey = NormalizeOptional(record.PublicationLocalKey, 500),
            NativeIdentityJson = nativeIdentityJson
        };
    }

    private static string NormalizeJson(string json, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{parameterName} JSON can not be blank.");
        }
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetRawText();
    }

    private static bool JsonEquivalent(string left, string right) =>
        string.Equals(CanonicalJsonFingerprint(left), CanonicalJsonFingerprint(right), StringComparison.Ordinal);

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

    private const string SupplementalSchemaSql = """
        CREATE TABLE IF NOT EXISTS source_representation_entity (
            source_representation_entity_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            source_entity_id uuid NOT NULL,
            source_entity_revision_id uuid NOT NULL,
            locator_key varchar(500) NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_representation_entity PRIMARY KEY (source_representation_entity_id),
            CONSTRAINT fk_source_representation_entity_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_entity_entity FOREIGN KEY (source_entity_id)
                REFERENCES source_entity(source_entity_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_entity_revision FOREIGN KEY (source_entity_revision_id)
                REFERENCES source_entity_revision(source_entity_revision_id) ON DELETE CASCADE);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_representation_entity_identity
            ON source_representation_entity(source_representation_id, source_entity_id);
        CREATE INDEX IF NOT EXISTS ix_source_representation_entity_revision
            ON source_representation_entity(source_entity_revision_id);

        CREATE TABLE IF NOT EXISTS source_representation_publication (
            source_representation_publication_id uuid NOT NULL,
            source_representation_id uuid NOT NULL,
            canonical_publication_id uuid NOT NULL,
            local_key varchar(500) NOT NULL,
            created_at timestamp with time zone NOT NULL,
            CONSTRAINT pk_source_representation_publication PRIMARY KEY (source_representation_publication_id),
            CONSTRAINT fk_source_representation_publication_representation FOREIGN KEY (source_representation_id)
                REFERENCES source_representation(source_representation_id) ON DELETE CASCADE,
            CONSTRAINT fk_source_representation_publication_canonical FOREIGN KEY (canonical_publication_id)
                REFERENCES canonical_publication(canonical_publication_id) ON DELETE RESTRICT);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_source_representation_publication_identity
            ON source_representation_publication(source_representation_id, local_key);
        CREATE INDEX IF NOT EXISTS ix_source_representation_publication_canonical
            ON source_representation_publication(canonical_publication_id);
        """;
}
