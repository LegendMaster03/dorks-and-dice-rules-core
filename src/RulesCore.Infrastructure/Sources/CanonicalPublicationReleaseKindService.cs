using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal sealed class CanonicalPublicationReleaseKindService(RulesCoreDbContext dbContext)
{
    public async Task MergeAsync(
        Guid canonicalPublicationId,
        string? releaseKind,
        CancellationToken cancellationToken = default)
    {
        if (canonicalPublicationId == Guid.Empty)
        {
            throw new ArgumentException("Canonical publication ID can not be empty.", nameof(canonicalPublicationId));
        }

        var normalized = SourceReleaseKinds.NormalizeImportLabel(releaseKind);
        if (normalized is null)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE canonical_publication ADD COLUMN IF NOT EXISTS release_kind varchar(40) NULL;",
            cancellationToken);

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            string? existing;
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = """
                    SELECT release_kind
                    FROM canonical_publication
                    WHERE canonical_publication_id = @id;
                    """;
                AddParameter(read, "@id", canonicalPublicationId);
                var value = await read.ExecuteScalarAsync(cancellationToken);
                if (value is null)
                {
                    throw new KeyNotFoundException(
                        $"Canonical publication '{canonicalPublicationId}' was not found.");
                }
                existing = value is DBNull ? null : (string)value;
            }

            if (existing is not null)
            {
                if (!string.Equals(existing, normalized, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Canonical publication '{canonicalPublicationId}' is already classified as release kind '{existing}', which conflicts with '{normalized}'.");
                }
                return;
            }

            await using var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE canonical_publication
                SET release_kind = @release_kind
                WHERE canonical_publication_id = @id
                    AND release_kind IS NULL;
                """;
            AddParameter(update, "@release_kind", normalized);
            AddParameter(update, "@id", canonicalPublicationId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
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
