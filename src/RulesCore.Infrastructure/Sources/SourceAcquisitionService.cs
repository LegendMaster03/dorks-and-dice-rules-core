using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceAcquisitionService(RulesCoreDbContext dbContext)
    : ISourceAcquisitionService
{
    public async Task<IReadOnlyList<SourceAcquisitionView>> GetCurrentUserAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
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
                SELECT
                    a.source_acquisition_id,
                    a.source_package_id,
                    p.package_key,
                    p.display_name,
                    p.is_public,
                    a.acquisition_kind,
                    a.reference_text,
                    a.acquired_at,
                    a.recorded_by_user_id,
                    a.recorded_at,
                    r.source_acquisition_revocation_id,
                    r.reason,
                    r.revoked_by_user_id,
                    r.revoked_at
                FROM source_acquisition a
                JOIN source_package p ON p.source_package_id = a.source_package_id
                LEFT JOIN source_acquisition_revocation r
                    ON r.source_acquisition_id = a.source_acquisition_id
                WHERE a.user_id = @user_id
                ORDER BY a.recorded_at DESC, a.source_acquisition_id;
                """;
            AddParameter(command, "@user_id", userId);

            var results = new List<SourceAcquisitionView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadView(reader));
            }
            return results;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<SourceAcquisitionMutationView?> RecordCurrentUserAsync(
        string currentUserId,
        Guid sourcePackageId,
        RecordCurrentUserSourceAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = RequireUserId(currentUserId);
        RequireGuid(sourcePackageId, nameof(sourcePackageId));
        var kind = RequireKind(request.AcquisitionKind);
        var reference = NormalizeOptional(request.Reference, 500, nameof(request.Reference));

        var package = await dbContext.SourcePackages
            .AsNoTracking()
            .Where(value => value.Id == sourcePackageId)
            .Select(value => new PackageReference(
                value.Id,
                value.Key,
                value.DisplayName,
                value.IsPublic))
            .SingleOrDefaultAsync(cancellationToken);
        if (package is null)
        {
            return null;
        }

        var id = Guid.NewGuid();
        var recordedAt = DateTimeOffset.UtcNow;
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
                INSERT INTO source_acquisition (
                    source_acquisition_id,
                    source_package_id,
                    user_id,
                    acquisition_kind,
                    reference_text,
                    acquired_at,
                    recorded_by_user_id,
                    recorded_at)
                VALUES (
                    @id,
                    @package_id,
                    @user_id,
                    @kind,
                    @reference,
                    @acquired_at,
                    @recorded_by,
                    @recorded_at);
                """;
            AddParameter(command, "@id", id);
            AddParameter(command, "@package_id", sourcePackageId);
            AddParameter(command, "@user_id", userId);
            AddParameter(command, "@kind", kind);
            AddNullableParameter(command, "@reference", reference);
            AddNullableParameter(command, "@acquired_at", request.AcquiredAt);
            AddParameter(command, "@recorded_by", userId);
            AddParameter(command, "@recorded_at", recordedAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }

        return new SourceAcquisitionMutationView(
            new SourceAcquisitionView(
                id,
                package.Id,
                package.Key,
                package.DisplayName,
                package.IsPublic,
                kind,
                reference,
                request.AcquiredAt,
                userId,
                recordedAt,
                IsVoided: false,
                VoidReason: null,
                VoidedByUserId: null,
                VoidedAt: null),
            Changed: true);
    }

    public async Task<SourceAcquisitionMutationView?> VoidCurrentUserAsync(
        string currentUserId,
        Guid sourceAcquisitionId,
        VoidCurrentUserSourceAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = RequireUserId(currentUserId);
        RequireGuid(sourceAcquisitionId, nameof(sourceAcquisitionId));
        var reason = NormalizeOptional(request.Reason, 1000, nameof(request.Reason));

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            SourceAcquisitionView? existing;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    SELECT
                        a.source_acquisition_id,
                        a.source_package_id,
                        p.package_key,
                        p.display_name,
                        p.is_public,
                        a.acquisition_kind,
                        a.reference_text,
                        a.acquired_at,
                        a.recorded_by_user_id,
                        a.recorded_at,
                        r.source_acquisition_revocation_id,
                        r.reason,
                        r.revoked_by_user_id,
                        r.revoked_at
                    FROM source_acquisition a
                    JOIN source_package p ON p.source_package_id = a.source_package_id
                    LEFT JOIN source_acquisition_revocation r
                        ON r.source_acquisition_id = a.source_acquisition_id
                    WHERE a.source_acquisition_id = @id
                        AND a.user_id = @user_id
                    FOR UPDATE OF a;
                    """;
                AddParameter(command, "@id", sourceAcquisitionId);
                AddParameter(command, "@user_id", userId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                existing = await reader.ReadAsync(cancellationToken) ? ReadView(reader) : null;
            }

            if (existing is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            if (existing.IsVoided)
            {
                await transaction.CommitAsync(cancellationToken);
                return new SourceAcquisitionMutationView(existing, Changed: false);
            }

            var revokedAt = DateTimeOffset.UtcNow;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO source_acquisition_revocation (
                        source_acquisition_revocation_id,
                        source_acquisition_id,
                        reason,
                        revoked_by_user_id,
                        revoked_at)
                    VALUES (@id, @acquisition_id, @reason, @revoked_by, @revoked_at);
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@acquisition_id", sourceAcquisitionId);
                AddNullableParameter(command, "@reason", reason);
                AddParameter(command, "@revoked_by", userId);
                AddParameter(command, "@revoked_at", revokedAt);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new SourceAcquisitionMutationView(
                existing with
                {
                    IsVoided = true,
                    VoidReason = reason,
                    VoidedByUserId = userId,
                    VoidedAt = revokedAt
                },
                Changed: true);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static SourceAcquisitionView ReadView(DbDataReader reader)
    {
        var revocationIdOrdinal = reader.GetOrdinal("source_acquisition_revocation_id");
        var hasRevocation = !reader.IsDBNull(revocationIdOrdinal);
        return new SourceAcquisitionView(
            reader.GetGuid(reader.GetOrdinal("source_acquisition_id")),
            reader.GetGuid(reader.GetOrdinal("source_package_id")),
            reader.GetString(reader.GetOrdinal("package_key")),
            reader.GetString(reader.GetOrdinal("display_name")),
            reader.GetBoolean(reader.GetOrdinal("is_public")),
            reader.GetString(reader.GetOrdinal("acquisition_kind")),
            GetNullableString(reader, "reference_text"),
            GetNullableDateTimeOffset(reader, "acquired_at"),
            reader.GetString(reader.GetOrdinal("recorded_by_user_id")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("recorded_at")),
            hasRevocation,
            hasRevocation ? GetNullableString(reader, "reason") : null,
            hasRevocation ? GetNullableString(reader, "revoked_by_user_id") : null,
            hasRevocation ? GetNullableDateTimeOffset(reader, "revoked_at") : null);
    }

    private static string? GetNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddNullableParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string RequireKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Acquisition kind can not be blank.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (!SourceAcquisitionKinds.All.Contains(normalized))
        {
            throw new ArgumentException(
                $"Acquisition kind must be one of: {string.Join(", ", SourceAcquisitionKinds.All.Order())}.",
                nameof(value));
        }
        return normalized;
    }

    private static string RequireUserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("User ID can not exceed 200 characters.", nameof(value));
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be empty.", parameterName);
        }
    }

    private sealed record PackageReference(
        Guid Id,
        string Key,
        string DisplayName,
        bool IsPublic);
}
