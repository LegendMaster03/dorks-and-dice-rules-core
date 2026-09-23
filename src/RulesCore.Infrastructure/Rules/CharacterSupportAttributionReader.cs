using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Loads source, publication, provider, and representation attribution for Character support.
/// </summary>
internal static class CharacterSupportAttributionReader
{
    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CharacterMechanicSourceAttributionView>>> ReadAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyList<ResolvedRuleCatalogItemView> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CharacterMechanicSourceAttributionView>>();
        }
    
        var packageKeys = rules.Select(value => value.PackageKey).Distinct().ToArray();
        var providerByPackageKey = await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => packageKeys.Contains(value.Key))
            .ToDictionaryAsync(
                value => value.Key,
                value => value.Provider,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
    
        var revisionIds = rules
            .Select(value => value.SourceEntityRevisionId)
            .Distinct()
            .ToArray();
        var sourceUriByRevision = await dbContext.SourceEntityRevisions
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
        var publications = await ReadPublicationAttributionsAsync(
            dbContext,
            revisionIds,
            cancellationToken);
    
        return rules.ToDictionary(
            rule => rule.RuleConceptId,
            rule => BuildAttributions(
                rule,
                providerByPackageKey.GetValueOrDefault(rule.PackageKey)
                    ?? rule.PackageDisplayName,
                sourceUriByRevision.GetValueOrDefault(
                    rule.SourceEntityRevisionId),
                publications.GetValueOrDefault(
                    rule.SourceEntityRevisionId,
                    [])));
    }
    
    private static IReadOnlyList<CharacterMechanicSourceAttributionView> BuildAttributions(
        ResolvedRuleCatalogItemView rule,
        string provider,
        string? sourceUri,
        IReadOnlyList<PublicationAttribution> publications)
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
                    ReferenceKey: rule.ConceptKey,
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
                ReferenceKey: rule.ConceptKey,
                ReferenceTitle: rule.SourceEntityName,
                ReferenceUri: sourceUri,
                PresentationRequired: false,
                ReferenceLinkRequired: false))
            .ToArray();
    }
    
    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PublicationAttribution>>> ReadPublicationAttributionsAsync(
        RulesCoreDbContext dbContext,
        IReadOnlyList<Guid> revisionIds,
        CancellationToken cancellationToken)
    {
        if (revisionIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<PublicationAttribution>>();
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
            AddParameter(command, "@revision_ids", revisionIds.ToArray());
    
            var values = new Dictionary<Guid, List<PublicationAttribution>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var revisionId = reader.GetGuid(0);
                if (!values.TryGetValue(revisionId, out var items))
                {
                    items = [];
                    values.Add(revisionId, items);
                }
    
                items.Add(new PublicationAttribution(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5)
                        ? null
                        : reader.GetFieldValue<DateOnly>(5)));
            }
    
            return values.ToDictionary(
                value => value.Key,
                value => (IReadOnlyList<PublicationAttribution>)value.Value
                    .Distinct()
                    .ToArray());
        }
        catch (DbException)
        {
            return new Dictionary<Guid, IReadOnlyList<PublicationAttribution>>();
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
    
    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
    
    
    private sealed record PublicationAttribution(
        string WorkKey,
        string WorkDisplayName,
        string? GameEdition,
        string? ReleaseKind,
        DateOnly? PublicationDate);
}