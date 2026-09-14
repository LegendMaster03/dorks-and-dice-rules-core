using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class IncompleteCurrentUserSourceImportCleanupService(RulesCoreDbContext dbContext)
{
    public async Task<bool> CleanupWebAddAsync(
        string currentUserId,
        string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(currentUserId));
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Web source URL must be an absolute HTTPS URL.", nameof(url));
        }

        var userId = currentUserId.Trim();
        var normalizedOrigin = NormalizeWebOrigin(uri);
        var originIdentity = $"web:{normalizedOrigin}";
        var packageKey = $"user-source-{Fingerprint(Encoding.UTF8.GetBytes($"{userId}\n{originIdentity}"))[..24]}";

        var package = await dbContext.SourcePackages
            .Include(value => value.UserGrants)
            .SingleOrDefaultAsync(value => value.Key == packageKey, cancellationToken);
        if (package is null || package.UserGrants.Count != 0)
        {
            return false;
        }

        var hasRulesBindings = await dbContext.RuleConceptSourceBindings.AnyAsync(
            value => value.SourceEntityId.HasValue
                && value.SourceEntity != null
                && value.SourceEntity.SourcePackageId == package.Id,
            cancellationToken);
        if (hasRulesBindings)
        {
            return false;
        }

        var canonicalCandidates = await ReadCanonicalCandidatesAsync(package.Id, cancellationToken);
        var packageCreatedAt = package.CreatedAt;

        dbContext.SourcePackages.Remove(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        await DeleteOrphanedCanonicalCandidatesAsync(
            canonicalCandidates,
            packageCreatedAt,
            cancellationToken);
        return true;
    }

    private async Task<IReadOnlyList<Guid>> ReadCanonicalCandidatesAsync(
        Guid packageId,
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
            if (!await RelationExistsAsync(connection, "source_representation_publication", cancellationToken))
            {
                return [];
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT link.canonical_publication_id
                FROM source_representation_publication link
                JOIN source_representation representation
                    ON representation.source_representation_id = link.source_representation_id
                WHERE representation.source_package_id = @package_id;
                """;
            AddParameter(command, "@package_id", packageId);
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
            return ids;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task DeleteOrphanedCanonicalCandidatesAsync(
        IReadOnlyCollection<Guid> publicationIds,
        DateTimeOffset packageCreatedAt,
        CancellationToken cancellationToken)
    {
        if (publicationIds.Count == 0)
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await RelationExistsAsync(connection, "canonical_publication", cancellationToken)
                || !await RelationExistsAsync(connection, "source_representation_publication", cancellationToken)
                || !await RelationExistsAsync(connection, "canonical_source_occurrence", cancellationToken)
                || !await RelationExistsAsync(connection, "source_entity_occurrence_binding", cancellationToken))
            {
                return;
            }

            foreach (var publicationId in publicationIds)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM canonical_publication publication
                    WHERE publication.canonical_publication_id = @publication_id
                        AND publication.created_at >= @package_created_at
                        AND NOT EXISTS (
                            SELECT 1
                            FROM source_representation_publication link
                            WHERE link.canonical_publication_id = publication.canonical_publication_id)
                        AND NOT EXISTS (
                            SELECT 1
                            FROM canonical_source_occurrence occurrence
                            JOIN source_entity_occurrence_binding binding
                                ON binding.canonical_source_occurrence_id = occurrence.canonical_source_occurrence_id
                            WHERE occurrence.canonical_publication_id = publication.canonical_publication_id);
                    """;
                AddParameter(command, "@publication_id", publicationId);
                AddParameter(command, "@package_created_at", packageCreatedAt);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> RelationExistsAsync(
        DbConnection connection,
        string relationName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass(@relation_name) IS NOT NULL;";
        AddParameter(command, "@relation_name", relationName);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
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

    private static string Fingerprint(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
