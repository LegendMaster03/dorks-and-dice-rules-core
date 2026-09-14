using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

internal sealed class PackageScopedPublicationEvidenceService(RulesCoreDbContext dbContext)
{
    public async Task<CanonicalPublicationEvidence> EnrichAsync(
        Guid sourcePackageId,
        CanonicalPublicationEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        if (sourcePackageId == Guid.Empty)
        {
            throw new ArgumentException("Source package ID can not be empty.", nameof(sourcePackageId));
        }
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Aliases is null || evidence.Aliases.Count == 0)
        {
            return evidence;
        }

        var aliases = evidence.Aliases
            .Where(value => !string.IsNullOrWhiteSpace(value.Key) && !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => new KeyValuePair<string, string>(
                CanonicalSourceIdentity.NormalizeIdentityPart(value.Key),
                value.Value.Trim().ToLowerInvariant()))
            .Where(value => string.Equals(value.Key, "5etools-source-code", StringComparison.Ordinal))
            .ToArray();
        if (aliases.Length == 0 || !IsAliasFallback(evidence.DisplayName, aliases))
        {
            return evidence;
        }

        var candidates = new Dictionary<Guid, StoredPublication>();
        foreach (var alias in aliases)
        {
            foreach (var candidate in await ReadPackageCandidatesAsync(
                         sourcePackageId,
                         alias.Key,
                         alias.Value,
                         cancellationToken))
            {
                candidates.TryAdd(candidate.Id, candidate);
            }
        }

        if (candidates.Count != 1)
        {
            return evidence;
        }

        var selected = candidates.Values.Single();
        return evidence with
        {
            DisplayName = selected.DisplayName,
            Publisher = evidence.Publisher ?? selected.Publisher,
            GameEdition = evidence.GameEdition ?? selected.GameEdition,
            PublicationDate = evidence.PublicationDate ?? selected.PublicationDate
        };
    }

    private async Task<IReadOnlyList<StoredPublication>> ReadPackageCandidatesAsync(
        Guid sourcePackageId,
        string aliasScheme,
        string aliasValue,
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
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT
                    publication.canonical_publication_id,
                    publication.display_name,
                    publication.publisher,
                    publication.game_edition,
                    publication.publication_date
                FROM source_representation representation
                JOIN source_representation_publication representation_publication
                    ON representation_publication.source_representation_id = representation.source_representation_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = representation_publication.canonical_publication_id
                JOIN canonical_publication_alias alias
                    ON alias.canonical_publication_id = publication.canonical_publication_id
                WHERE representation.source_package_id = @package_id
                    AND alias.alias_scheme = @scheme
                    AND alias.alias_value = @value
                ORDER BY publication.canonical_publication_id
                LIMIT 2;
                """;
            AddParameter(command, "@package_id", sourcePackageId);
            AddParameter(command, "@scheme", aliasScheme);
            AddParameter(command, "@value", aliasValue);

            var results = new List<StoredPublication>(2);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new StoredPublication(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4)));
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

    private static bool IsAliasFallback(
        string displayName,
        IReadOnlyCollection<KeyValuePair<string, string>> aliases)
    {
        var normalized = CanonicalSourceIdentity.NormalizeIdentityPart(displayName);
        return aliases.Any(value => string.Equals(
            normalized,
            CanonicalSourceIdentity.NormalizeIdentityPart(value.Value),
            StringComparison.Ordinal));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record StoredPublication(
        Guid Id,
        string DisplayName,
        string? Publisher,
        string? GameEdition,
        DateOnly? PublicationDate);
}
