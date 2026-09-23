using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Loads source-owned Character catalog documents, publication metadata, provider/source
/// provenance, and accessible competency profiles from persistence.
/// </summary>
internal sealed class CharacterMechanicsSourceMetadataReader(RulesCoreDbContext dbContext)
{
    internal static IReadOnlyList<CharacterMechanicSourceAttributionView> BuildRuleAttributions(
        ResolvedRuleCatalogItemView rule,
        string provider,
        string? sourceUri,
        IReadOnlyList<CharacterMechanicsPublicationAttribution> publications)
    {
        if (publications.Count == 0)
        {
            return
            [
                new CharacterMechanicSourceAttributionView(
                    rule.PackageKey,
                    rule.PackageDisplayName,
                    provider,
                    rule.SourceCode,
                    rule.SourceRevisionNumber,
                    WorkKey: null,
                    WorkDisplayName: null,
                    GameEdition: null,
                    ReleaseKind: null,
                    PublicationDate: null,
                    ReferenceKey: null,
                    ReferenceTitle: rule.SourceEntityName,
                    ReferenceUri: sourceUri,
                    PresentationRequired: false,
                    ReferenceLinkRequired: false)
            ];
        }
    
        return publications
            .OrderBy(value => value.WorkDisplayName, StringComparer.Ordinal)
            .ThenBy(value => value.WorkKey, StringComparer.Ordinal)
            .Select(value => new CharacterMechanicSourceAttributionView(
                rule.PackageKey,
                rule.PackageDisplayName,
                provider,
                rule.SourceCode,
                rule.SourceRevisionNumber,
                value.WorkKey,
                value.WorkDisplayName,
                value.GameEdition,
                value.ReleaseKind,
                value.PublicationDate,
                ReferenceKey: null,
                ReferenceTitle: rule.SourceEntityName,
                ReferenceUri: sourceUri,
                PresentationRequired: false,
                ReferenceLinkRequired: false))
            .ToArray();
    }
    
    internal async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CharacterMechanicsPublicationAttribution>>> ReadCharacterMechanicsPublicationAttributionsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CharacterMechanicsPublicationAttribution>>();
        }
    
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
                SELECT DISTINCT
                    binding.source_entity_revision_id,
                    publication.canonical_key,
                    publication.display_name,
                    publication.game_edition,
                    publication.release_kind,
                    publication.publication_date
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE binding.source_entity_revision_id = ANY(@revision_ids);
                """;
            AddParameter(command, "@revision_ids", revisionIds);
    
            var values = new Dictionary<Guid, List<CharacterMechanicsPublicationAttribution>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var revisionId = reader.GetGuid(0);
                if (!values.TryGetValue(revisionId, out var publications))
                {
                    publications = [];
                    values.Add(revisionId, publications);
                }
    
                publications.Add(new CharacterMechanicsPublicationAttribution(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5)));
            }
    
            return values.ToDictionary(
                value => value.Key,
                value => (IReadOnlyList<CharacterMechanicsPublicationAttribution>)value.Value
                    .Distinct()
                    .ToArray());
        }
        catch (DbException)
        {
            return new Dictionary<Guid, IReadOnlyList<CharacterMechanicsPublicationAttribution>>();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
    
    internal async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CharacterCompetencyProfileView>>> ReadCompetencyProfilesAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        string? userId,
        CancellationToken cancellationToken)
    {
        var conceptIds = rules.Select(value => value.RuleConceptId).Distinct().ToArray();
        if (conceptIds.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CharacterCompetencyProfileView>>();
        }
    
        await CanonicalRuleBindingStore.EnsureSchemaAsync(dbContext, cancellationToken);
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
                WITH RECURSIVE concept_entities(rule_concept_id, canonical_entity_id) AS (
                    SELECT rule_concept_id, canonical_entity_id
                    FROM rule_concept_source_binding
                    WHERE rule_concept_id = ANY(@concept_ids)
                    UNION
                    SELECT parent.rule_concept_id, relationship.to_canonical_entity_id
                    FROM canonical_entity_relationship relationship
                    JOIN concept_entities parent
                        ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                    WHERE relationship.relationship_kind = 'revision'
                ),
                latest AS (
                    SELECT DISTINCT ON (source_entity_id)
                        source_entity_id,
                        source_entity_revision_id
                    FROM source_entity_revision
                    ORDER BY source_entity_id, revision_number DESC
                )
                SELECT DISTINCT
                    concept_entity.rule_concept_id,
                    revision.source_entity_revision_id,
                    revision.content_json,
                    revision.raw_json,
                    source.entity_type,
                    publication.game_edition,
                    package.package_key,
                    package.display_name,
                    package.provider,
                    source.source_code,
                    revision.revision_number,
                    publication.canonical_key,
                    publication.display_name,
                    publication.release_kind,
                    publication.publication_date,
                    representation.source_uri
                FROM concept_entities concept_entity
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_entity_id = concept_entity.canonical_entity_id
                JOIN source_entity_occurrence_binding occurrence_binding
                    ON occurrence_binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                JOIN latest
                    ON latest.source_entity_revision_id = occurrence_binding.source_entity_revision_id
                JOIN source_entity_revision revision
                    ON revision.source_entity_revision_id = latest.source_entity_revision_id
                JOIN source_entity source
                    ON source.source_entity_id = revision.source_entity_id
                JOIN source_representation representation
                    ON representation.source_representation_id = revision.source_representation_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                JOIN source_package package
                    ON package.source_package_id = source.source_package_id
                WHERE package.is_public
                    OR (@user_id IS NOT NULL AND EXISTS (
                        SELECT 1
                        FROM user_source_grant grant_row
                        WHERE grant_row.source_package_id = package.source_package_id
                            AND grant_row.user_id = @user_id))
                ORDER BY concept_entity.rule_concept_id, revision.source_entity_revision_id;
                """;
            AddParameter(command, "@concept_ids", conceptIds);
            AddNullableStringParameter(command, "@user_id", NormalizeOptionalUserId(userId));
    
            var profiles = new Dictionary<Guid, List<CharacterCompetencyProfileView>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var conceptId = reader.GetGuid(0);
                var revisionId = reader.GetGuid(1);
                var contentJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                var rawJson = reader.GetString(3);
                var entityType = reader.GetString(4);
                var gameEdition = reader.IsDBNull(5) ? null : reader.GetString(5);
                var attribution = new CharacterMechanicSourceAttributionView(
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.GetInt32(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    gameEdition,
                    reader.IsDBNull(13) ? null : reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetFieldValue<DateOnly>(14),
                    ReferenceKey: null,
                    ReferenceTitle: null,
                    ReferenceUri: reader.IsDBNull(15) ? null : reader.GetString(15),
                    PresentationRequired: false,
                    ReferenceLinkRequired: false);
                var mechanicalJson =
                    string.IsNullOrWhiteSpace(contentJson) ? rawJson : contentJson;
                var profile = CharacterCompetencyProfileFactory.BuildProfile(
                    revisionId,
                    entityType,
                    CharacterCompetencyProfileFactory.ReadSourceNativeName(mechanicalJson),
                    mechanicalJson,
                    gameEdition,
                    [attribution]);
                if (profile is null)
                {
                    continue;
                }
    
                if (!profiles.TryGetValue(conceptId, out var conceptProfiles))
                {
                    conceptProfiles = [];
                    profiles.Add(conceptId, conceptProfiles);
                }
                conceptProfiles.Add(profile);
            }
    
            return profiles.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<CharacterCompetencyProfileView>)pair.Value
                    .GroupBy(value => new { value.SourceEntityRevisionId, value.ProfileKey })
                    .Select(group =>
                    {
                        var first = group.First();
                        var attributions = group
                            .SelectMany(value => value.SourceAttributions ?? [])
                            .Distinct()
                            .OrderBy(value => value.WorkDisplayName, StringComparer.Ordinal)
                            .ThenBy(value => value.PackageKey, StringComparer.Ordinal)
                            .ToArray();
                        return first with { SourceAttributions = attributions };
                    })
                    .OrderBy(value => value.ProfileKey, StringComparer.Ordinal)
                    .ThenBy(value => value.GameEdition, StringComparer.Ordinal)
                    .ThenBy(value => value.SourceEntityRevisionId)
                    .ToArray());
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
    
    internal async Task<IReadOnlyDictionary<Guid, string>> ReadMechanicalDocumentsAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }
    
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        return revisions.ToDictionary(
            value => value.Id,
            value => string.IsNullOrWhiteSpace(value.ContentJson)
                ? value.RawJson
                : value.ContentJson!);
    }
    
    internal async Task<IReadOnlyDictionary<string, string>> ReadProvidersAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var packageKeys = rules.Select(value => value.PackageKey).Distinct().ToArray();
        if (packageKeys.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    
        return await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => packageKeys.Contains(value.Key))
            .ToDictionaryAsync(
                value => value.Key,
                value => value.Provider,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
    }
    
    internal async Task<IReadOnlyDictionary<Guid, string?>> ReadSourceUrisAsync(
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        var revisionIds = rules.Select(value => value.SourceEntityRevisionId).Distinct().ToArray();
        if (revisionIds.Length == 0)
        {
            return new Dictionary<Guid, string?>();
        }
    
        return await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .Select(value => new
            {
                value.Id,
                value.SourceRepresentation.SourceUri
            })
            .ToDictionaryAsync(
                value => value.Id,
                value => value.SourceUri,
                cancellationToken);
    }
    
    
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
    
    private static void AddNullableStringParameter(DbCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = (object?)value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
    
    private static string? NormalizeOptionalUserId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    
}
