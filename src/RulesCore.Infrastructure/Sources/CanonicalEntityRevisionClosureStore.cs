using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal sealed class CanonicalEntityRevisionClosureStore(RulesCoreDbContext dbContext)
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> ReadAsync(
        IReadOnlyCollection<Guid> rootCanonicalEntityIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootCanonicalEntityIds);
        var roots = rootCanonicalEntityIds
            .Where(value => value != Guid.Empty)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (roots.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlySet<Guid>>();
        }

        await new CanonicalEntityRelationshipStore(dbContext).EnsureSchemaAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            var result = new Dictionary<Guid, IReadOnlySet<Guid>>(roots.Length);
            foreach (var root in roots)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    WITH RECURSIVE revision_closure(canonical_entity_id) AS (
                        SELECT CAST(@root_id AS uuid)
                        UNION
                        SELECT relationship.to_canonical_entity_id
                        FROM canonical_entity_relationship relationship
                        JOIN revision_closure parent
                            ON parent.canonical_entity_id = relationship.from_canonical_entity_id
                        WHERE relationship.relationship_kind = 'revision'
                    )
                    SELECT canonical_entity_id
                    FROM revision_closure
                    ORDER BY canonical_entity_id;
                    """;
                AddParameter(command, "@root_id", root);

                var closure = new HashSet<Guid>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) closure.Add(reader.GetGuid(0));
                result[root] = closure;
            }
            return result;
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
}
