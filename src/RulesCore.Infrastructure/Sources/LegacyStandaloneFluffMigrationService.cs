using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Converts legacy rule-bearing 5e.tools fluff revisions into companion content. Canonical
/// occurrence bindings are removed only for revisions whose companion payload was persisted
/// successfully; malformed or otherwise unconvertible legacy revisions remain visible for review.
/// </summary>
internal sealed class LegacyStandaloneFluffMigrationService(RulesCoreDbContext dbContext)
{
    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var store = new SourceCompanionContentStore(dbContext);
        await store.EnsureSchemaAsync(cancellationToken);

        var legacy = await dbContext.SourceEntities
            .AsNoTracking()
            .Where(value => value.EntityType == "monsterFluff"
                || value.EntityType == "raceFluff"
                || value.EntityType == "spellFluff")
            .Select(value => new LegacyEntity(
                value.Id,
                value.SourcePackageId,
                value.EntityType,
                value.Name,
                value.SourceCode,
                value.NativeKey))
            .ToArrayAsync(cancellationToken);
        if (legacy.Length == 0)
        {
            return 0;
        }

        var entityIds = legacy.Select(value => value.Id).ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => entityIds.Contains(value.SourceEntityId))
            .OrderBy(value => value.SourceEntityId)
            .ThenBy(value => value.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var byId = legacy.ToDictionary(value => value.Id);
        var migratedRevisionIds = new List<Guid>(revisions.Length);

        foreach (var revision in revisions)
        {
            var entity = byId[revision.SourceEntityId];
            if (!FiveEToolsCompanionSourceFormatAdapter.TryReadLegacyCompanion(
                    entity.EntityType,
                    revision.RawJson,
                    entity.Name,
                    entity.SourceCode,
                    entity.NativeKey,
                    out var companion))
            {
                continue;
            }

            await store.PersistAsync(
                entity.SourcePackageId,
                revision.SourceRepresentationId,
                [companion],
                cancellationToken);
            migratedRevisionIds.Add(revision.Id);
        }

        if (migratedRevisionIds.Count != 0)
        {
            var connection = dbContext.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere) await connection.OpenAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM source_entity_occurrence_binding
                    WHERE source_entity_revision_id = ANY(@revision_ids);
                    """;
                AddParameter(command, "@revision_ids", migratedRevisionIds.ToArray());
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }
        }

        await store.ResolvePendingAsync(null, cancellationToken);
        return migratedRevisionIds.Count;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record LegacyEntity(
        Guid Id,
        Guid SourcePackageId,
        string EntityType,
        string Name,
        string? SourceCode,
        string NativeKey);
}
