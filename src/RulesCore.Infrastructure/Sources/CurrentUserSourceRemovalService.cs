using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Removes only the current user's source registration and access grant. Shared Source Layer
/// packages, canonical identity, aliases, revisions, and Rules Layer history are retained so
/// a later reimport can reuse established identity safely.
/// </summary>
public sealed class CurrentUserSourceRemovalService(RulesCoreDbContext dbContext)
{
    public async Task<bool> RemoveAsync(
        string currentUserId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        if (currentUserSourceId == Guid.Empty)
            throw new ArgumentException("Source ID can not be empty.", nameof(currentUserSourceId));

        var jobs = new CurrentUserSourceImportJobService(dbContext);
        if (await jobs.HasActiveImportAsync(userId, currentUserSourceId, cancellationToken))
        {
            throw new InvalidOperationException(
                "This source has an active import. Remove it after the import reaches a completed or failed state.");
        }

        var packageId = await ReadPackageIdAsync(userId, currentUserSourceId, cancellationToken);
        if (packageId is null) return false;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                DELETE FROM current_user_source
                WHERE current_user_source_id = {{currentUserSourceId}}
                    AND user_id = {{userId}};
                """, cancellationToken);
            if (affected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            // Keep registration removal and grant revocation in one database transaction.
            // If an unusual installation has another registration for the same package, retain
            // the grant until the user's final registration for that package is removed.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                DELETE FROM user_source_grant
                WHERE user_id = {{userId}}
                    AND source_package_id = {{packageId.Value}}
                    AND NOT EXISTS (
                        SELECT 1
                        FROM current_user_source
                        WHERE user_id = {{userId}}
                            AND source_package_id = {{packageId.Value}});
                """, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Guid?> ReadPackageIdAsync(
        string userId,
        Guid currentUserSourceId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT source_package_id
                FROM current_user_source
                WHERE current_user_source_id = @id
                    AND user_id = @user_id;
                """;
            AddParameter(command, "@id", currentUserSourceId);
            AddParameter(command, "@user_id", userId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is Guid id ? id : null;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("User ID can not be blank.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 200)
            throw new ArgumentException("User ID can not exceed 200 characters.", nameof(value));
        return normalized;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
