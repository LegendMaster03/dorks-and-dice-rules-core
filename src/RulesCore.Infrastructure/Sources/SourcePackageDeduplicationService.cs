using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed record SourcePackageDeduplicationResult(
    int CandidatePackageCount,
    int ConsolidatedPackageCount,
    int RetainedReferencedDuplicateCount,
    int RenamedSharedPackageCount);

public sealed class SourcePackageDeduplicationService(RulesCoreDbContext dbContext)
{
    public async Task<SourcePackageDeduplicationResult> ConsolidateAsync(
        CancellationToken cancellationToken = default)
    {
        SourceImportExecutionPolicy.Apply(dbContext);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);

        try
        {
            if (!await RelationExistsAsync(connection, "source_representation", cancellationToken))
            {
                return new SourcePackageDeduplicationResult(0, 0, 0, 0);
            }

            var packages = await ReadCandidatesAsync(connection, cancellationToken);
            var groups = packages
                .Where(value => value.Representations.Count > 0)
                .GroupBy(value => BundleFingerprint(value.Representations), StringComparer.Ordinal)
                .ToArray();

            var consolidated = 0;
            var retainedReferenced = 0;
            var renamed = 0;

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fingerprint = group.Key;
                var desiredKey = CurrentUserSourceService.SharedPackageKey(fingerprint);
                var members = group
                    .OrderBy(value => value.CreatedAt)
                    .ThenBy(value => value.Id)
                    .ToArray();
                var keeper = members.FirstOrDefault(value =>
                        string.Equals(value.Key, desiredKey, StringComparison.Ordinal))
                    ?? members[0];

                foreach (var duplicate in members.Where(value => value.Id != keeper.Id))
                {
                    if (await TryConsolidatePackageAsync(
                            connection,
                            keeper.Id,
                            duplicate.Id,
                            cancellationToken))
                    {
                        consolidated++;
                    }
                    else
                    {
                        retainedReferenced++;
                    }
                }

                if (!string.Equals(keeper.Key, desiredKey, StringComparison.Ordinal)
                    && !await PackageKeyExistsAsync(
                        connection,
                        desiredKey,
                        keeper.Id,
                        cancellationToken))
                {
                    await using var rename = connection.CreateCommand();
                    rename.CommandText = """
                        UPDATE source_package
                        SET package_key = @package_key,
                            display_name = @display_name,
                            provider = 'user-source',
                            license = NULL
                        WHERE source_package_id = @package_id;
                        """;
                    AddParameter(rename, "@package_key", desiredKey);
                    AddParameter(rename, "@display_name", $"Shared user source {fingerprint[..16]}");
                    AddParameter(rename, "@package_id", keeper.Id);
                    renamed += await rename.ExecuteNonQueryAsync(cancellationToken);
                }
                else if (string.Equals(keeper.Key, desiredKey, StringComparison.Ordinal))
                {
                    await using var normalize = connection.CreateCommand();
                    normalize.CommandText = """
                        UPDATE source_package
                        SET display_name = @display_name,
                            provider = 'user-source',
                            license = NULL
                        WHERE source_package_id = @package_id;
                        """;
                    AddParameter(normalize, "@display_name", $"Shared user source {fingerprint[..16]}");
                    AddParameter(normalize, "@package_id", keeper.Id);
                    await normalize.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            if (await RelationExistsAsync(connection, "source_content_blob", cancellationToken))
            {
                await using var orphanCleanup = connection.CreateCommand();
                orphanCleanup.CommandText = """
                    DELETE FROM source_content_blob blob
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM source_representation representation
                        WHERE representation.content_sha256 = blob.content_sha256);
                    """;
                await orphanCleanup.ExecuteNonQueryAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            return new SourcePackageDeduplicationResult(
                packages.Count,
                consolidated,
                retainedReferenced,
                renamed);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task<List<CandidatePackage>> ReadCandidatesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var byId = new Dictionary<Guid, CandidatePackage>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                package.source_package_id,
                package.package_key,
                package.created_at,
                representation.format_key,
                representation.file_name,
                representation.content_sha256
            FROM source_package package
            LEFT JOIN source_representation representation
                ON representation.source_package_id = package.source_package_id
            WHERE package.is_public = FALSE
                AND (
                    package.package_key LIKE 'user-source-%'
                    OR package.package_key LIKE 'user-content-%')
            ORDER BY package.created_at, package.source_package_id,
                     representation.format_key, representation.file_name,
                     representation.content_sha256;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var packageId = reader.GetGuid(0);
            if (!byId.TryGetValue(packageId, out var package))
            {
                package = new CandidatePackage(
                    packageId,
                    reader.GetString(1),
                    reader.GetFieldValue<DateTimeOffset>(2),
                    []);
                byId.Add(packageId, package);
            }

            if (!reader.IsDBNull(3))
            {
                package.Representations.Add(new StoredRepresentation(
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)));
            }
        }

        return byId.Values.ToList();
    }

    private static async Task<bool> TryConsolidatePackageAsync(
        DbConnection connection,
        Guid keeperPackageId,
        Guid duplicatePackageId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            if (await RelationExistsAsync(connection, "user_source_grant", cancellationToken, transaction))
            {
                var duplicateGrants = new List<(string UserId, DateTimeOffset GrantedAt)>();
                await using (var readGrants = connection.CreateCommand())
                {
                    readGrants.Transaction = transaction;
                    readGrants.CommandText = """
                        SELECT user_id, granted_at
                        FROM user_source_grant
                        WHERE source_package_id = @duplicate_package_id;
                        """;
                    AddParameter(readGrants, "@duplicate_package_id", duplicatePackageId);
                    await using var reader = await readGrants.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        duplicateGrants.Add((
                            reader.GetString(0),
                            reader.GetFieldValue<DateTimeOffset>(1)));
                    }
                }

                foreach (var (userId, grantedAt) in duplicateGrants)
                {
                    await using var grant = connection.CreateCommand();
                    grant.Transaction = transaction;
                    grant.CommandText = """
                        INSERT INTO user_source_grant (
                            user_source_grant_id, source_package_id, user_id, granted_at)
                        VALUES (@id, @keeper_package_id, @user_id, @granted_at)
                        ON CONFLICT (user_id, source_package_id) DO NOTHING;
                        """;
                    AddParameter(grant, "@id", Guid.NewGuid());
                    AddParameter(grant, "@keeper_package_id", keeperPackageId);
                    AddParameter(grant, "@user_id", userId);
                    AddParameter(grant, "@granted_at", grantedAt);
                    await grant.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            if (await RelationExistsAsync(connection, "current_user_source", cancellationToken, transaction))
            {
                await using var registrations = connection.CreateCommand();
                registrations.Transaction = transaction;
                registrations.CommandText = """
                    UPDATE current_user_source
                    SET source_package_id = @keeper_package_id
                    WHERE source_package_id = @duplicate_package_id;
                    """;
                AddParameter(registrations, "@keeper_package_id", keeperPackageId);
                AddParameter(registrations, "@duplicate_package_id", duplicatePackageId);
                await registrations.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = """
                DELETE FROM source_package
                WHERE source_package_id = @duplicate_package_id;
                """;
            AddParameter(delete, "@duplicate_package_id", duplicatePackageId);
            await delete.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<bool> PackageKeyExistsAsync(
        DbConnection connection,
        string packageKey,
        Guid exceptPackageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM source_package
                WHERE package_key = @package_key
                    AND source_package_id <> @except_package_id);
            """;
        AddParameter(command, "@package_key", packageKey);
        AddParameter(command, "@except_package_id", exceptPackageId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static string BundleFingerprint(IReadOnlyCollection<StoredRepresentation> representations)
    {
        var identities = representations
            .Select(value => $"{value.FormatKey}\n{value.FileName}\n{value.ContentSha256}")
            .OrderBy(value => value, StringComparer.Ordinal);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", identities))))
            .ToLowerInvariant();
    }

    private static async Task<bool> RelationExistsAsync(
        DbConnection connection,
        string relationName,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT to_regclass(@relation_name) IS NOT NULL;";
        AddParameter(command, "@relation_name", relationName);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record CandidatePackage(
        Guid Id,
        string Key,
        DateTimeOffset CreatedAt,
        List<StoredRepresentation> Representations);

    private sealed record StoredRepresentation(
        string FormatKey,
        string FileName,
        string ContentSha256);
}
