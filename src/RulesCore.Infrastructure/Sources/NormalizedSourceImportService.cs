using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class NormalizedSourceImportService(RulesCoreDbContext dbContext) : INormalizedSourceImportService
{
    public async Task<NormalizedSourceImportResult> ImportAsync(
        ImportNormalizedSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Representation);
        var packageKey = NormalizeKey(request.PackageKey);
        var packageDisplayName = Require(request.PackageDisplayName, nameof(request.PackageDisplayName), 300);
        var provider = Require(request.Provider, nameof(request.Provider), 200);
        var license = NormalizeOptional(request.License, 300);
        var formatKey = Require(request.Representation.FormatKey, nameof(request.Representation.FormatKey), 80);
        if (request.Representation.Records.Count == 0)
            throw new InvalidDataException("The normalized source representation did not contain any source records.");

        var translatedRecords = new List<NormalizedSourceRecord>(request.Representation.Records.Count);
        var translatedCount = 0;
        await ReportAsync(request, new CurrentUserSourceImportProgress(
            "translating",
            0,
            request.Representation.Records.Count,
            "Translating source-native records into Rules Core mechanical content",
            AdapterFormat: formatKey,
            RecordsDiscovered: request.Representation.Records.Count,
            RecordsTranslated: 0), cancellationToken);

        foreach (var record in request.Representation.Records)
        {
            var translated = RulesCoreContentTranslation.TranslateRecord(request.Representation, record);
            var normalized = TrustedCanonicalAliasPolicy.Apply(
                request.Representation,
                NormalizeRecord(translated));
            translatedRecords.Add(normalized);
            translatedCount++;
            if (ShouldReport(translatedCount, request.Representation.Records.Count))
            {
                await ReportAsync(request, new CurrentUserSourceImportProgress(
                    "translating",
                    translatedCount,
                    request.Representation.Records.Count,
                    CurrentItem: normalized.Name,
                    CurrentItemType: normalized.EntityType,
                    AdapterFormat: formatKey,
                    RecordsDiscovered: request.Representation.Records.Count,
                    RecordsTranslated: translatedCount), cancellationToken);
            }
        }

        await ReportAsync(request, new CurrentUserSourceImportProgress(
            "persisting",
            0,
            translatedRecords.Count,
            "Preparing to persist normalized source records",
            AdapterFormat: formatKey,
            RecordsDiscovered: translatedRecords.Count,
            RecordsTranslated: translatedRecords.Count,
            EntitiesPersisted: 0), cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var package = await dbContext.SourcePackages
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is null)
        {
            package = new SourcePackage
            {
                Id = Guid.NewGuid(),
                Key = packageKey,
                DisplayName = packageDisplayName,
                Provider = provider,
                License = license,
                IsPublic = request.IsPublic,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.SourcePackages.Add(package);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            EnsurePackageMatches(package, packageDisplayName, provider, license, request.IsPublic);
        }

        var representationResult = await StoreRepresentationAsync(
            package.Id,
            request.Representation,
            formatKey,
            cancellationToken);
        var representation = representationResult.Representation;
        var representationsStored = representationResult.Reused ? 0 : 1;
        var representationsReused = representationResult.Reused ? 1 : 0;
        var reconciliationIssueService = new SourceReconciliationIssueService(dbContext);
        await reconciliationIssueService.EnsureSchemaAsync(cancellationToken);
        var importedEntities = new List<ImportedSourceEntity>();
        var importedPublications = new List<ImportedNormalizedPublication>();
        var reconciliationIssues = new List<NormalizedSourceReconciliationIssue>();
        var persisted = new List<(SourceEntity Entity, SourceEntityRevision Revision, NormalizedSourceRecord Record)>();
        var newEntities = 0;
        var unchangedEntities = 0;
        var newRevisions = 0;
        var translationOnlyUpdates = 0;
        var persistedCount = 0;

        await ReportAsync(request, new CurrentUserSourceImportProgress(
            "persisting",
            0,
            translatedRecords.Count,
            "Persisting normalized source records",
            AdapterFormat: formatKey,
            RecordsDiscovered: translatedRecords.Count,
            RecordsTranslated: translatedRecords.Count,
            EntitiesPersisted: 0,
            RepresentationsStored: representationsStored,
            RepresentationsReused: representationsReused), cancellationToken);

        foreach (var normalized in translatedRecords)
        {
            var entity = await dbContext.SourceEntities.SingleOrDefaultAsync(
                value => value.SourcePackageId == package.Id
                    && value.FormatKey == formatKey
                    && value.NativeKey == normalized.NativeKey,
                cancellationToken);
            var createdEntity = entity is null;
            if (createdEntity)
            {
                entity = new SourceEntity
                {
                    Id = Guid.NewGuid(),
                    SourcePackageId = package.Id,
                    FormatKey = formatKey,
                    EntityType = normalized.EntityType,
                    Name = normalized.Name,
                    SourceCode = normalized.SourceCode,
                    NativeKey = normalized.NativeKey,
                    NativeIdentityJson = normalized.NativeIdentityJson,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                dbContext.SourceEntities.Add(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
                newEntities++;
            }
            else
            {
                var migratedIdentity = EnsureEntityIdentityMatches(entity!, normalized);
                if (migratedIdentity)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            var fingerprint = CanonicalJsonFingerprint(normalized.RawJson);
            var latest = await dbContext.SourceEntityRevisions
                .Where(value => value.SourceEntityId == entity!.Id)
                .OrderByDescending(value => value.RevisionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            var createdRevision = latest is null
                || !string.Equals(latest.Fingerprint, fingerprint, StringComparison.Ordinal);
            if (createdRevision)
            {
                latest = new SourceEntityRevision
                {
                    Id = Guid.NewGuid(),
                    SourceEntityId = entity!.Id,
                    SourceRepresentationId = representation.Id,
                    RevisionNumber = (latest?.RevisionNumber ?? 0) + 1,
                    Fingerprint = fingerprint,
                    RawJson = normalized.RawJson,
                    ContentJson = normalized.ContentJson,
                    LocatorKey = normalized.LocatorKey,
                    ImportedAt = DateTimeOffset.UtcNow
                };
                dbContext.SourceEntityRevisions.Add(latest);
                await dbContext.SaveChangesAsync(cancellationToken);
                newRevisions++;
            }
            else if (!JsonEquivalentOptional(latest!.ContentJson, normalized.ContentJson))
            {
                // Translator improvements are Rules Core interpretation changes, not native
                // source revisions. Update only ContentJson on the existing native revision.
                latest.ContentJson = normalized.ContentJson;
                await dbContext.SaveChangesAsync(cancellationToken);
                translationOnlyUpdates++;
            }
            else
            {
                unchangedEntities++;
            }

            await LinkRepresentationEntityAsync(
                representation.Id,
                entity!.Id,
                latest!.Id,
                normalized.LocatorKey,
                cancellationToken);
            importedEntities.Add(new ImportedSourceEntity(
                entity.Id,
                entity.EntityType,
                entity.Name,
                entity.SourceCode ?? string.Empty,
                latest.RevisionNumber,
                latest.Fingerprint,
                createdRevision));
            persisted.Add((entity, latest, normalized));
            persistedCount++;

            if (ShouldReport(persistedCount, translatedRecords.Count))
            {
                await ReportAsync(request, new CurrentUserSourceImportProgress(
                    "persisting",
                    persistedCount,
                    translatedRecords.Count,
                    CurrentItem: normalized.Name,
                    CurrentItemType: normalized.EntityType,
                    AdapterFormat: formatKey,
                    RecordsDiscovered: translatedRecords.Count,
                    RecordsTranslated: translatedRecords.Count,
                    EntitiesPersisted: persistedCount,
                    NewEntities: newEntities,
                    UnchangedEntities: unchangedEntities,
                    NewRevisions: newRevisions,
                    TranslationOnlyUpdates: translationOnlyUpdates,
                    RepresentationsStored: representationsStored,
                    RepresentationsReused: representationsReused), cancellationToken);
            }
        }

        var publicationEvidence = (request.Representation.Publications ?? [])
            .Select(value => TrustedCanonicalAliasPolicy.ApplyPublication(request.Representation, value))
            .GroupBy(
                value => Require(value.LocalKey, nameof(value.LocalKey), 500),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Single())
            .ToDictionary(value => value.LocalKey, StringComparer.OrdinalIgnoreCase);
        var publicationIndex = 0;
        var publicationProcessed = 0;
        await ReportAsync(request, new CurrentUserSourceImportProgress(
            "reconciling",
            0,
            publicationEvidence.Count,
            "Reconciling canonical publication and entity identity",
            AdapterFormat: formatKey,
            RecordsDiscovered: translatedRecords.Count,
            RecordsTranslated: translatedRecords.Count,
            EntitiesPersisted: persistedCount,
            NewEntities: newEntities,
            UnchangedEntities: unchangedEntities,
            NewRevisions: newRevisions,
            TranslationOnlyUpdates: translationOnlyUpdates,
            PublicationsProcessed: 0,
            PublicationTotal: publicationEvidence.Count,
            ReconciliationIssueCount: 0,
            RepresentationsStored: representationsStored,
            RepresentationsReused: representationsReused), cancellationToken);

        foreach (var publication in publicationEvidence.Values)
        {
            var records = persisted
                .Where(value => string.Equals(
                    value.Record.PublicationLocalKey,
                    publication.LocalKey,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var fingerprints = records
                .Select(value => SemanticFingerprint(value.Record))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var evidence = new CanonicalPublicationEvidence(
                publication.DisplayName,
                publication.Publisher,
                publication.GameEdition,
                publication.PublicationDate,
                publication.ExternalIdentifiers,
                fingerprints);
            var savepointName = $"canonical_reconciliation_{publicationIndex++}";
            await transaction.CreateSavepointAsync(savepointName, cancellationToken);
            try
            {
                Guid? canonicalPublicationId = null;
                foreach (var value in records)
                {
                    var semanticFingerprint = SemanticFingerprint(value.Record);
                    var association = await new CanonicalSourceRepresentationService(dbContext)
                        .AssociateSourceEntityAsync(
                            value.Entity.Id,
                            value.Revision.Id,
                            evidence,
                            new CanonicalSourceOccurrenceEvidence(
                                value.Record.EntityType,
                                value.Record.Name,
                                value.Record.LocatorKey,
                                semanticFingerprint),
                            request.Representation.FormatKey,
                            cancellationToken,
                            value.Record.CanonicalAliases);
                    canonicalPublicationId ??= association.Publication.Id;
                    if (canonicalPublicationId != association.Publication.Id)
                    {
                        throw new CanonicalReconciliationConflictException(
                            "One publication-evidence group unexpectedly resolved to multiple canonical publications.");
                    }
                }

                if (canonicalPublicationId is null)
                {
                    canonicalPublicationId = (await new CanonicalPublicationIdentityService(dbContext)
                        .ResolveAsync(evidence, cancellationToken)).Id;
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
                await reconciliationIssueService.ResolveAsync(
                    representation.Id,
                    publication.LocalKey,
                    cancellationToken);
                importedPublications.Add(new ImportedNormalizedPublication(
                    canonicalPublicationId.Value,
                    publication.LocalKey,
                    publication.DisplayName,
                    records.Length));
                await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
            }
            catch (CanonicalReconciliationConflictException exception)
            {
                await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
                await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
                var issue = new NormalizedSourceReconciliationIssue(
                    NormalizedSourceReconciliationIssueKinds.CanonicalIdentityConflict,
                    publication.LocalKey,
                    publication.DisplayName,
                    records.Select(value => value.Entity.Id).Distinct().ToArray(),
                    exception.Message);
                reconciliationIssues.Add(issue);
                await reconciliationIssueService.RecordAsync(
                    representation.Id,
                    issue,
                    cancellationToken);
            }

            publicationProcessed++;
            await ReportAsync(request, new CurrentUserSourceImportProgress(
                "reconciling",
                publicationProcessed,
                publicationEvidence.Count,
                CurrentItem: publication.DisplayName,
                CurrentItemType: "publication",
                AdapterFormat: formatKey,
                RecordsDiscovered: translatedRecords.Count,
                RecordsTranslated: translatedRecords.Count,
                EntitiesPersisted: persistedCount,
                NewEntities: newEntities,
                UnchangedEntities: unchangedEntities,
                NewRevisions: newRevisions,
                TranslationOnlyUpdates: translationOnlyUpdates,
                PublicationsProcessed: publicationProcessed,
                PublicationTotal: publicationEvidence.Count,
                ReconciliationIssueCount: reconciliationIssues.Count,
                RepresentationsStored: representationsStored,
                RepresentationsReused: representationsReused), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var sourceCodes = request.Representation.Records
            .Select(value => value.SourceCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await ReportAsync(request, new CurrentUserSourceImportProgress(
            "finalizing",
            persistedCount,
            persistedCount,
            $"{sourceCodes.Length} publication code(s) · {persistedCount} source record(s) · {importedPublications.Count} publication identity group(s)",
            AdapterFormat: formatKey,
            RecordsDiscovered: translatedRecords.Count,
            RecordsTranslated: translatedRecords.Count,
            EntitiesPersisted: persistedCount,
            NewEntities: newEntities,
            UnchangedEntities: unchangedEntities,
            NewRevisions: newRevisions,
            TranslationOnlyUpdates: translationOnlyUpdates,
            PublicationsProcessed: publicationProcessed,
            PublicationTotal: publicationEvidence.Count,
            ReconciliationIssueCount: reconciliationIssues.Count,
            RepresentationsStored: representationsStored,
            RepresentationsReused: representationsReused), cancellationToken);

        return new NormalizedSourceImportResult(
            package.Id,
            importedPublications,
            importedEntities,
            sourceCodes)
        {
            ReconciliationIssues = reconciliationIssues
        };
    }

    private async Task<(SourceRepresentation Representation, bool Reused)> StoreRepresentationAsync(
        Guid packageId,
        NormalizedSourceRepresentation normalized,
        string formatKey,
        CancellationToken cancellationToken)
    {
        var artifact = normalized.Artifact
            ?? throw new ArgumentException(
                "Normalized source representation artifact can not be null.",
                nameof(normalized));
        var fileName = Require(artifact.FileName, nameof(artifact.FileName), 500);
        var originIdentity = Require(artifact.OriginIdentity, nameof(artifact.OriginIdentity), 2000);
        ArgumentNullException.ThrowIfNull(artifact.Content);
        if (artifact.Content.Length == 0)
            throw new InvalidDataException("Source representation content can not be empty.");
        var contentHash = Convert.ToHexString(SHA256.HashData(artifact.Content)).ToLowerInvariant();
        var existing = await dbContext.SourceRepresentations.SingleOrDefaultAsync(
            value => value.SourcePackageId == packageId
                && value.OriginIdentity == originIdentity
                && value.ContentSha256 == contentHash,
            cancellationToken);
        if (existing is not null) return (existing, true);

        var previous = await dbContext.SourceRepresentations
            .Where(value => value.SourcePackageId == packageId && value.OriginIdentity == originIdentity)
            .OrderByDescending(value => value.ImportedAt)
            .ThenByDescending(value => value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var representation = new SourceRepresentation
        {
            Id = Guid.NewGuid(),
            SourcePackageId = packageId,
            PreviousSourceRepresentationId = previous?.Id,
            FormatKey = formatKey,
            OriginIdentity = originIdentity,
            FileName = fileName,
            SourceUri = NormalizeOptional(artifact.SourceUri, 2000),
            MediaType = NormalizeOptional(artifact.MediaType, 200),
            ContentSha256 = contentHash,
            ContentLength = artifact.Content.LongLength,
            ContentBytes = artifact.Content.ToArray(),
            MetadataJson = NormalizeMetadataJson(normalized.MetadataJson),
            ImportedAt = DateTimeOffset.UtcNow
        };
        dbContext.SourceRepresentations.Add(representation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (representation, false);
    }

    private async Task LinkRepresentationEntityAsync(
        Guid representationId,
        Guid entityId,
        Guid revisionId,
        string? locatorKey,
        CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO source_representation_entity (
                source_representation_entity_id, source_representation_id, source_entity_id,
                source_entity_revision_id, locator_key, created_at)
            VALUES ({{Guid.NewGuid()}}, {{representationId}}, {{entityId}}, {{revisionId}}, {{locatorKey}}, {{DateTimeOffset.UtcNow}})
            ON CONFLICT (source_representation_id, source_entity_id) DO NOTHING;
            """, cancellationToken);

    private async Task LinkRepresentationPublicationAsync(
        Guid representationId,
        Guid publicationId,
        string localKey,
        CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO source_representation_publication (
                source_representation_publication_id, source_representation_id, canonical_publication_id, local_key, created_at)
            VALUES ({{Guid.NewGuid()}}, {{representationId}}, {{publicationId}}, {{localKey}}, {{DateTimeOffset.UtcNow}})
            ON CONFLICT (source_representation_id, local_key) DO UPDATE
            SET canonical_publication_id = EXCLUDED.canonical_publication_id;
            """, cancellationToken);

    private static NormalizedSourceRecord NormalizeRecord(NormalizedSourceRecord record)
    {
        var contentJson = string.IsNullOrWhiteSpace(record.ContentJson)
            ? null
            : RequireJsonObject(record.ContentJson, nameof(record.ContentJson));
        var semanticJson = string.IsNullOrWhiteSpace(record.SemanticJson)
            ? null
            : RequireJsonObject(record.SemanticJson, nameof(record.SemanticJson));
        return record with
        {
            EntityType = Require(record.EntityType, nameof(record.EntityType), 120),
            Name = Require(record.Name, nameof(record.Name), 300),
            SourceCode = NormalizeOptional(record.SourceCode, 120),
            NativeKey = Require(record.NativeKey, nameof(record.NativeKey), 1000),
            RawJson = RequireJsonObject(record.RawJson, nameof(record.RawJson)),
            ContentJson = contentJson,
            LocatorKey = NormalizeOptional(record.LocatorKey, 500),
            PublicationLocalKey = NormalizeOptional(record.PublicationLocalKey, 500),
            NativeIdentityJson = NormalizeIdentityJson(record.NativeIdentityJson),
            SemanticJson = semanticJson,
            CanonicalAliases = NormalizeCanonicalAliases(record.CanonicalAliases),
            CanonicalIdentityKey = NormalizeOptional(record.CanonicalIdentityKey, 1000)
        };
    }

    private static IReadOnlyDictionary<string, string>? NormalizeCanonicalAliases(
        IReadOnlyDictionary<string, string>? aliases)
    {
        if (aliases is null || aliases.Count == 0) return null;
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            var scheme = Require(alias.Key, "Canonical alias scheme", 100);
            var value = Require(alias.Value, "Canonical alias value", 1000);
            if (!normalized.TryAdd(scheme, value))
                throw new InvalidDataException($"Duplicate canonical alias scheme '{scheme}'.");
        }
        return normalized;
    }

    private static string SemanticFingerprint(NormalizedSourceRecord record) =>
        string.IsNullOrWhiteSpace(record.CanonicalIdentityKey)
            ? CanonicalSourceIdentity.SemanticFingerprint(SemanticDocument(record))
            : ExactCompetencyTranslationPolicy.CanonicalFingerprint(record.CanonicalIdentityKey);

    private static string SemanticDocument(NormalizedSourceRecord record) =>
        string.IsNullOrWhiteSpace(record.ContentJson) ? record.RawJson : record.ContentJson;

    private static bool EnsureEntityIdentityMatches(SourceEntity entity, NormalizedSourceRecord record)
    {
        var sourceCodeMatches = string.Equals(
            entity.SourceCode,
            record.SourceCode,
            StringComparison.Ordinal);
        var entityTypeMatches = string.Equals(
            entity.EntityType,
            record.EntityType,
            StringComparison.Ordinal);
        var nameMatches = string.Equals(
            entity.Name,
            record.Name,
            StringComparison.Ordinal);
        if ((!entityTypeMatches || !nameMatches) && sourceCodeMatches
            && string.Equals(
                entity.FormatKey,
                LegacySrdSourceFormatAdapter.Format,
                StringComparison.Ordinal)
            && ExactCompetencyTranslationPolicy.IsReviewedLegacyCompetencyMigration(
                entity.EntityType,
                entity.Name,
                record))
        {
            // The native key, RawJson, and NativeIdentityJson remain unchanged. This is a
            // correction to Rules Core's derived normalized competency identity, using the same
            // reviewed direct-conversion policy applied to new imports. Existing source revision
            // IDs and any canonical/Rules Layer references therefore remain stable.
            entity.EntityType = record.EntityType;
            entity.Name = record.Name;
            entityTypeMatches = true;
            nameMatches = true;
        }

        if (!entityTypeMatches || !nameMatches || !sourceCodeMatches)
        {
            throw new InvalidOperationException(
                $"Source entity native identity '{entity.NativeKey}' changed immutable identity metadata across representations.");
        }

        if (JsonEquivalent(entity.NativeIdentityJson, record.NativeIdentityJson))
        {
            return false;
        }

        if (string.Equals(entity.FormatKey, FiveEToolsSourceFormatAdapter.Format, StringComparison.Ordinal))
        {
            // Before 5e.tools was established as the native Rules Core mechanical schema,
            // the adapter incorrectly stored edition/parentSource and other non-identity
            // metadata in NativeIdentityJson. NativeKey is the authoritative 5e.tools UID
            // for an existing match, so correcting that historical metadata is a schema
            // migration rather than a native source revision.
            entity.NativeIdentityJson = record.NativeIdentityJson;
            return true;
        }

        throw new InvalidOperationException(
            $"Source entity native identity '{entity.NativeKey}' changed immutable identity metadata across representations.");
    }

    private static void EnsurePackageMatches(
        SourcePackage package,
        string displayName,
        string provider,
        string? license,
        bool isPublic)
    {
        if (!string.Equals(package.DisplayName, displayName, StringComparison.Ordinal)
            || !string.Equals(package.Provider, provider, StringComparison.Ordinal)
            || !string.Equals(package.License, license, StringComparison.Ordinal)
            || package.IsPublic != isPublic)
        {
            throw new InvalidOperationException(
                $"Source package '{package.Key}' already exists with different immutable metadata.");
        }
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
                foreach (var child in element.EnumerateArray()) WriteCanonical(writer, child);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string RequireJsonObject(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("JSON value can not be blank.", parameterName);
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("A source entity document must be a JSON object.");
        return document.RootElement.GetRawText();
    }

    private static string NormalizeIdentityJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "{}";
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Native source identity metadata must be a JSON object.");
        return document.RootElement.GetRawText();
    }

    private static string NormalizeMetadataJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "{}";
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Source representation metadata must be a JSON object.");
        return document.RootElement.GetRawText();
    }

    private static bool JsonEquivalent(string left, string right)
    {
        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return JsonElement.DeepEquals(leftDocument.RootElement, rightDocument.RootElement);
    }

    private static bool JsonEquivalentOptional(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right);
        return JsonEquivalent(left, right);
    }

    private static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0) builder.Append('-');
                builder.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }
        var normalized = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("A source key must contain at least one letter or number.", nameof(value));
        return normalized.Length <= 200 ? normalized : normalized[..200].TrimEnd('-');
    }

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value can not be blank.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"Value can not exceed {maxLength} characters.");
        return normalized;
    }

    private static bool ShouldReport(int current, int total) =>
        current == 1 || current == total || current % 100 == 0;

    private static async Task ReportAsync(
        ImportNormalizedSourceRequest request,
        CurrentUserSourceImportProgress progress,
        CancellationToken cancellationToken)
    {
        if (request.ProgressReporter is null) return;
        try
        {
            await request.ProgressReporter(progress, cancellationToken);
        }
        catch
        {
            // Progress is advisory. Persistence, reconciliation, and native revision history
            // must not depend on the availability of the progress sink.
        }
    }
}
