using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Reconciles logical reference histories without destructively merging canonical entities.
/// Same logical names and compatible categories are connected by revision history by default;
/// ordinary class names with a trailing parenthetical qualifier (X (Y)) share X's logical-name
/// key when an ordinary class X exists. Persisted same-name-different-entity decisions and explicit
/// variant/reprint relationships prevent automatic bridges across identities known to remain distinct.
/// </summary>
internal sealed class CanonicalReferenceHistoryReconciliationService(RulesCoreDbContext dbContext)
{
    internal const string AutomaticEvidenceKind = "normalized-name-compatible-category";
    private const string ClassEntityType = "class";

    public Task ReconcileAllAsync(CancellationToken cancellationToken = default) =>
        ReconcileHistoryKeysAsync(null, cancellationToken);

    public async Task ReconcileSourceEntitiesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityIds.Count == 0)
        {
            return;
        }

        var historyKeys = await ReadHistoryKeysForSourceEntitiesAsync(sourceEntityIds, cancellationToken);
        if (historyKeys.Count != 0)
        {
            await ReconcileHistoryKeysAsync(historyKeys, cancellationToken);
        }
    }

    private async Task ReconcileHistoryKeysAsync(
        IReadOnlyCollection<string>? historyKeys,
        CancellationToken cancellationToken)
    {
        var entities = await ReadEntitiesAsync(historyKeys, cancellationToken);
        var requested = historyKeys is { Count: > 0 }
            ? historyKeys.ToHashSet(StringComparer.Ordinal)
            : null;

        foreach (var nameGroup in entities
                     .Where(value => requested is null || requested.Contains(HistoryNameKey(value)))
                     .GroupBy(HistoryNameKey, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1 && HasRequiredClassBase(group.Key, group)))
        {
            await ReconcileNameGroupAsync(nameGroup.ToArray(), cancellationToken);
        }
    }

    private async Task ReconcileNameGroupAsync(
        IReadOnlyList<EntityRow> entities,
        CancellationToken cancellationToken)
    {
        var ids = entities.Select(value => value.Id).ToArray();
        var relationships = await ReadRelationshipsAsync(ids, cancellationToken);
        var separations = await ReadSeparationsAsync(ids, cancellationToken);
        var union = new UnionFind(ids);

        foreach (var relationship in relationships.Where(value =>
                     !string.Equals(value.EvidenceKind, AutomaticEvidenceKind, StringComparison.Ordinal)))
        {
            union.Union(relationship.FromId, relationship.ToId);
        }

        var desired = new HashSet<EdgeKey>();
        var ordered = entities
            .OrderBy(value => value.EarliestPublicationDate.HasValue ? 0 : 1)
            .ThenBy(value => value.EarliestPublicationDate)
            .ThenBy(value => value.CreatedAt)
            .ThenBy(value => value.Id)
            .ToArray();

        for (var leftIndex = 0; leftIndex < ordered.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < ordered.Length; rightIndex++)
            {
                var left = ordered[leftIndex];
                var right = ordered[rightIndex];
                if (!CanonicalReferenceHistoryPolicy.AreCategoriesCompatible(left.EntityType, right.EntityType)
                    || union.Find(left.Id) == union.Find(right.Id)
                    || WouldViolateSeparation(union, left.Id, right.Id, separations))
                {
                    continue;
                }

                desired.Add(new EdgeKey(left.Id, right.Id));
                union.Union(left.Id, right.Id);
            }
        }

        var existingAutomatic = relationships
            .Where(value => string.Equals(value.EvidenceKind, AutomaticEvidenceKind, StringComparison.Ordinal))
            .ToArray();
        var obsolete = existingAutomatic
            .Where(value => !desired.Contains(new EdgeKey(value.FromId, value.ToId)))
            .Select(value => value.Id)
            .ToArray();
        if (obsolete.Length != 0)
        {
            await DeleteRelationshipsAsync(obsolete, cancellationToken);
        }

        var existingKeys = existingAutomatic
            .Where(value => !obsolete.Contains(value.Id))
            .Select(value => new EdgeKey(value.FromId, value.ToId))
            .ToHashSet();
        var store = new CanonicalEntityRelationshipStore(dbContext);
        foreach (var edge in desired.Where(value => !existingKeys.Contains(value)))
        {
            await store.RelateRevisionAsync(
                edge.FromId,
                edge.ToId,
                AutomaticEvidenceKind,
                1.0,
                cancellationToken);
        }
    }

    private static bool WouldViolateSeparation(
        UnionFind union,
        Guid left,
        Guid right,
        IReadOnlyList<SeparationRow> separations)
    {
        var leftRoot = union.Find(left);
        var rightRoot = union.Find(right);
        foreach (var separation in separations)
        {
            var separationLeftRoot = union.Find(separation.LeftId);
            var separationRightRoot = union.Find(separation.RightId);
            if (separationLeftRoot == separationRightRoot)
            {
                // An authoritative same-history relationship already connects this pair. Do not
                // make the conflict worse, but do not rewrite authoritative history here either.
                continue;
            }

            if ((separationLeftRoot == leftRoot && separationRightRoot == rightRoot)
                || (separationLeftRoot == rightRoot && separationRightRoot == leftRoot))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<IReadOnlyList<string>> ReadHistoryKeysForSourceEntitiesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT
                    canonical.entity_type,
                    canonical.normalized_name,
                    canonical.display_name
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_entity canonical
                    ON canonical.canonical_entity_id = occurrence.canonical_entity_id
                WHERE binding.source_entity_id = ANY(@source_entity_ids)
                  AND occurrence.canonical_entity_id IS NOT NULL;
                """;
            AddParameter(command, "@source_entity_ids", sourceEntityIds.ToArray());
            var keys = new HashSet<string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                keys.Add(HistoryNameKey(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2)));
            }
            return keys.ToArray();
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<EntityRow>> ReadEntitiesAsync(
        IReadOnlyCollection<string>? historyKeys,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = historyKeys is { Count: > 0 }
                ? """
                    SELECT
                        canonical.canonical_entity_id,
                        canonical.entity_type,
                        canonical.normalized_name,
                        canonical.display_name,
                        canonical.created_at,
                        MIN(publication.publication_date)
                    FROM canonical_entity canonical
                    LEFT JOIN canonical_source_occurrence occurrence
                        ON occurrence.canonical_entity_id = canonical.canonical_entity_id
                    LEFT JOIN canonical_publication publication
                        ON publication.canonical_publication_id = occurrence.canonical_publication_id
                    WHERE canonical.normalized_name = ANY(@history_keys)
                       OR (
                            canonical.entity_type = @class_entity_type
                            AND canonical.normalized_name LIKE ANY(@class_history_prefixes)
                            AND STRPOS(canonical.display_name, '(') > 1
                            AND RIGHT(BTRIM(canonical.display_name), 1) = ')')
                    GROUP BY canonical.canonical_entity_id, canonical.entity_type,
                             canonical.normalized_name, canonical.display_name, canonical.created_at;
                    """
                : """
                    SELECT
                        canonical.canonical_entity_id,
                        canonical.entity_type,
                        canonical.normalized_name,
                        canonical.display_name,
                        canonical.created_at,
                        MIN(publication.publication_date)
                    FROM canonical_entity canonical
                    LEFT JOIN canonical_source_occurrence occurrence
                        ON occurrence.canonical_entity_id = canonical.canonical_entity_id
                    LEFT JOIN canonical_publication publication
                        ON publication.canonical_publication_id = occurrence.canonical_publication_id
                    GROUP BY canonical.canonical_entity_id, canonical.entity_type,
                             canonical.normalized_name, canonical.display_name, canonical.created_at;
                    """;
            if (historyKeys is { Count: > 0 })
            {
                var distinctKeys = historyKeys.Distinct(StringComparer.Ordinal).ToArray();
                AddParameter(command, "@history_keys", distinctKeys);
                AddParameter(command, "@class_entity_type", ClassEntityType);
                AddParameter(
                    command,
                    "@class_history_prefixes",
                    distinctKeys.Select(value => $"{value}-%").ToArray());
            }

            var rows = new List<EntityRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new EntityRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetFieldValue<DateTimeOffset>(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static string HistoryNameKey(EntityRow entity) =>
        HistoryNameKey(entity.EntityType, entity.NormalizedName, entity.DisplayName);

    private static string HistoryNameKey(
        string entityType,
        string normalizedName,
        string displayName)
    {
        if (!IsOrdinaryClass(entityType))
        {
            return normalizedName;
        }

        var trimmed = displayName.Trim();
        if (trimmed.Length < 4 || trimmed[^1] != ')')
        {
            return normalizedName;
        }

        var openParenthesis = trimmed.LastIndexOf('(');
        if (openParenthesis <= 0 || openParenthesis >= trimmed.Length - 2)
        {
            return normalizedName;
        }

        var baseName = trimmed[..openParenthesis].TrimEnd();
        var qualifier = trimmed[(openParenthesis + 1)..^1].Trim();
        if (baseName.Length == 0 || qualifier.Length == 0)
        {
            return normalizedName;
        }

        var normalizedBaseName = CanonicalSourceIdentity.NormalizeIdentityPart(baseName);
        return normalizedBaseName.Length == 0 ? normalizedName : normalizedBaseName;
    }

    private static bool HasRequiredClassBase(
        string historyKey,
        IEnumerable<EntityRow> entities)
    {
        var rows = entities as IReadOnlyCollection<EntityRow> ?? entities.ToArray();
        var hasParentheticalClass = rows.Any(value =>
            IsOrdinaryClass(value.EntityType)
            && !string.Equals(value.NormalizedName, historyKey, StringComparison.Ordinal));
        return !hasParentheticalClass
            || rows.Any(value =>
                IsOrdinaryClass(value.EntityType)
                && string.Equals(value.NormalizedName, historyKey, StringComparison.Ordinal));
    }

    private static bool IsOrdinaryClass(string entityType) =>
        string.Equals(
            CanonicalReferenceHistoryPolicy.NormalizeCategory(entityType),
            ClassEntityType,
            StringComparison.Ordinal);

    private async Task<IReadOnlyList<RelationshipRow>> ReadRelationshipsAsync(
        IReadOnlyCollection<Guid> canonicalEntityIds,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT canonical_entity_relationship_id,
                       from_canonical_entity_id,
                       to_canonical_entity_id,
                       evidence_kind
                FROM canonical_entity_relationship
                WHERE relationship_kind IN ('revision', 'rename')
                  AND from_canonical_entity_id = ANY(@ids)
                  AND to_canonical_entity_id = ANY(@ids);
                """;
            AddParameter(command, "@ids", canonicalEntityIds.ToArray());
            var rows = new List<RelationshipRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new RelationshipRow(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<SeparationRow>> ReadSeparationsAsync(
        IReadOnlyCollection<Guid> canonicalEntityIds,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT canonical_entity_id, related_canonical_entity_id
                FROM canonical_bootstrap_reconciliation
                WHERE classification = 'same-name-different-entity'
                  AND confidence = 1.0
                  AND canonical_entity_id IS NOT NULL
                  AND related_canonical_entity_id IS NOT NULL
                  AND canonical_entity_id = ANY(@ids)
                  AND related_canonical_entity_id = ANY(@ids)
                UNION
                SELECT from_canonical_entity_id, to_canonical_entity_id
                FROM canonical_entity_relationship
                WHERE relationship_kind IN ('variant', 'reprint')
                  AND from_canonical_entity_id = ANY(@ids)
                  AND to_canonical_entity_id = ANY(@ids);
                """;
            AddParameter(command, "@ids", canonicalEntityIds.ToArray());
            var rows = new List<SeparationRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new SeparationRow(reader.GetGuid(0), reader.GetGuid(1)));
            }
            return rows;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task DeleteRelationshipsAsync(
        IReadOnlyCollection<Guid> relationshipIds,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM canonical_entity_relationship
                WHERE canonical_entity_relationship_id = ANY(@ids)
                  AND evidence_kind = @evidence_kind;
                """;
            AddParameter(command, "@ids", relationshipIds.ToArray());
            AddParameter(command, "@evidence_kind", AutomaticEvidenceKind);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record EntityRow(
        Guid Id,
        string EntityType,
        string NormalizedName,
        string DisplayName,
        DateTimeOffset CreatedAt,
        DateOnly? EarliestPublicationDate);
    private sealed record RelationshipRow(Guid Id, Guid FromId, Guid ToId, string EvidenceKind);
    private sealed record SeparationRow(Guid LeftId, Guid RightId);
    private sealed record EdgeKey(Guid FromId, Guid ToId);

    private sealed class UnionFind(IEnumerable<Guid> values)
    {
        private readonly Dictionary<Guid, Guid> parent = values.Distinct().ToDictionary(value => value);

        public Guid Find(Guid value)
        {
            var current = value;
            while (parent[current] != current)
            {
                current = parent[current];
            }
            var root = current;
            current = value;
            while (parent[current] != current)
            {
                var next = parent[current];
                parent[current] = root;
                current = next;
            }
            return root;
        }

        public void Union(Guid left, Guid right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot == rightRoot) return;
            parent[rightRoot] = leftRoot;
        }
    }
}
