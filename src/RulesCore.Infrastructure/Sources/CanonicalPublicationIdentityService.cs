using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Resolves shared bibliographic publication identity without treating contextual source aliases
/// as globally unique identifiers. This service stores recognition metadata only; it never grants
/// access to a package-owned representation or source entity.
/// </summary>
public sealed class CanonicalPublicationIdentityService(RulesCoreDbContext dbContext)
{
    private static readonly HashSet<string> StrongAliasSchemes = new(StringComparer.Ordinal)
    {
        "isbn",
        "isbn-10",
        "isbn10",
        "isbn-13",
        "isbn13"
    };

    private static readonly HashSet<string> SourceSpecificIdentitySchemes = new(StringComparer.Ordinal)
    {
        "5etools-corpus-id",
        "5etools-book-id",
        "5etools-adventure-id",
        TrustedCanonicalAliasPolicy.PublicationSourceCodeScheme
    };

    public async Task<CanonicalPublicationIdentityView> ResolveAsync(
        CanonicalPublicationEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(evidence.DisplayName))
        {
            throw new ArgumentException("Canonical publication display name can not be blank.", nameof(evidence));
        }

        await EnsureSchemaAsync(cancellationToken);
        var aliases = NormalizeAliases(evidence.Aliases);

        foreach (var alias in aliases.Where(value => StrongAliasSchemes.Contains(value.Key)))
        {
            var matches = await FindPublicationsByAliasAsync(alias.Key, alias.Value, cancellationToken);
            if (matches.Count == 1)
            {
                return matches[0] with { MatchKind = $"alias:{alias.Key}", Confidence = 1.0 };
            }
            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Strong publication identifier '{alias.Key}:{alias.Value}' is associated with multiple canonical publications.");
            }
        }

        // Reviewed source-specific identifiers must take precedence over bibliography. This is
        // what lets a newly reviewed cross-format identity repair an older duplicate publication
        // whose title/date fingerprint was already persisted before the identity was known.
        var sourceSpecificAliases = aliases
            .Where(value => SourceSpecificIdentitySchemes.Contains(value.Key))
            .ToArray();
        foreach (var alias in sourceSpecificAliases)
        {
            var matches = await FindSourceSpecificMatchesAsync(alias.Key, alias.Value, cancellationToken);
            var compatible = matches
                .Where(value => IsMetadataCompatible(value, evidence, aliases))
                .ToArray();
            if (compatible.Length == 1)
            {
                await AddAliasesAsync(compatible[0].Id, aliases, cancellationToken);
                return compatible[0] with
                {
                    MatchKind = $"alias:{alias.Key}",
                    Confidence = 0.995
                };
            }
        }

        var bibliographicFingerprint = CanonicalSourceIdentity.BibliographicFingerprint(evidence);
        var byBibliography = await FindPublicationByBibliographyAsync(
            bibliographicFingerprint,
            cancellationToken);
        if (byBibliography is not null)
        {
            await AddAliasesAsync(byBibliography.Id, aliases, cancellationToken);
            return byBibliography with { MatchKind = "bibliographic", Confidence = 0.98 };
        }

        var contextualAliases = aliases
            .Where(value => !StrongAliasSchemes.Contains(value.Key)
                && !SourceSpecificIdentitySchemes.Contains(value.Key))
            .ToArray();
        var aliasCandidates = await FindAliasCandidatesAsync(contextualAliases, cancellationToken);
        var compatibleCandidates = aliasCandidates
            .Where(value => IsMetadataCompatible(value.Publication, evidence, aliases))
            .Where(value => HasDisambiguatingContext(value, evidence, aliases))
            .Select(value => new RankedAliasCandidate(value, ScoreAliasCandidate(value, evidence, aliases)))
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Candidate.Publication.Id)
            .ToArray();

        if (compatibleCandidates.Length > 0
            && (compatibleCandidates.Length == 1
                || compatibleCandidates[0].Score > compatibleCandidates[1].Score))
        {
            var selected = compatibleCandidates[0].Candidate;
            await AddAliasesAsync(selected.Publication.Id, aliases, cancellationToken);
            return selected.Publication with
            {
                MatchKind = $"alias:{selected.MatchedSchemes[0]}",
                Confidence = 0.97
            };
        }

        var byContent = await FindPublicationByOccurrenceOverlapAsync(
            evidence.OccurrenceFingerprints,
            cancellationToken);
        if (byContent is not null
            && !HasMetadataConflict(byContent, evidence))
        {
            await AddAliasesAsync(byContent.Id, aliases, cancellationToken);
            return byContent;
        }

        var canonicalKey = $"publication-{CanonicalSourceIdentity.Fingerprint(bibliographicFingerprint)[..24]}";
        var created = await CreateOrReadPublicationAsync(
            canonicalKey,
            evidence,
            bibliographicFingerprint,
            cancellationToken);
        await AddAliasesAsync(created.Id, aliases, cancellationToken);
        return created with { MatchKind = "new", Confidence = 1.0 };
    }

    private async Task<IReadOnlyList<CanonicalPublicationIdentityView>> FindSourceSpecificMatchesAsync(
        string scheme,
        string value,
        CancellationToken cancellationToken)
    {
        var matches = await FindPublicationsByAliasAsync(scheme, value, cancellationToken);
        if (matches.Count > 0
            || !string.Equals(
                scheme,
                TrustedCanonicalAliasPolicy.PublicationSourceCodeScheme,
                StringComparison.Ordinal))
        {
            return matches;
        }

        // The reviewed legacy SRD snapshots predate the format-neutral publication alias and
        // therefore already exist under this compatibility scheme. Use it only as a migration
        // fallback for an explicitly reviewed Rules Core source code, then AddAliasesAsync above
        // backfills the format-neutral alias onto the selected canonical publication.
        return await FindPublicationsByAliasAsync("5etools-source-code", value, cancellationToken);
    }

    private async Task<IReadOnlyList<AliasCandidate>> FindAliasCandidatesAsync(
        IReadOnlyCollection<KeyValuePair<string, string>> aliases,
        CancellationToken cancellationToken)
    {
        if (aliases.Count == 0)
        {
            return [];
        }

        var candidates = new Dictionary<Guid, AliasCandidateBuilder>();
        foreach (var alias in aliases)
        {
            var matches = await FindPublicationsByAliasAsync(alias.Key, alias.Value, cancellationToken);
            foreach (var match in matches)
            {
                if (!candidates.TryGetValue(match.Id, out var builder))
                {
                    builder = new AliasCandidateBuilder(match);
                    candidates.Add(match.Id, builder);
                }
                builder.Add(alias.Key);
            }
        }

        return candidates.Values
            .Select(value => value.Build())
            .OrderBy(value => value.Publication.Id)
            .ToArray();
    }

    private async Task<IReadOnlyList<CanonicalPublicationIdentityView>> FindPublicationsByAliasAsync(
        string scheme,
        string value,
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
                SELECT
                    publication.canonical_publication_id,
                    publication.canonical_key,
                    publication.display_name,
                    publication.publisher,
                    publication.game_edition,
                    publication.publication_date
                FROM canonical_publication_alias alias
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = alias.canonical_publication_id
                WHERE alias.alias_scheme = @scheme
                    AND alias.alias_value = @value
                ORDER BY publication.created_at, publication.canonical_publication_id;
                """;
            AddParameter(command, "@scheme", scheme);
            AddParameter(command, "@value", value);
            var results = new List<CanonicalPublicationIdentityView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadPublication(reader, "alias", 1.0));
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

    private async Task<CanonicalPublicationIdentityView?> FindPublicationByBibliographyAsync(
        string fingerprint,
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
                SELECT canonical_publication_id, canonical_key, display_name, publisher,
                       game_edition, publication_date
                FROM canonical_publication
                WHERE bibliographic_fingerprint = @fingerprint
                ORDER BY created_at
                LIMIT 2;
                """;
            AddParameter(command, "@fingerprint", fingerprint);
            var results = new List<CanonicalPublicationIdentityView>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadPublication(reader, "bibliographic", 0.98));
            }
            return results.Count == 1 ? results[0] : null;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<CanonicalPublicationIdentityView?> FindPublicationByOccurrenceOverlapAsync(
        IReadOnlyCollection<string>? occurrenceFingerprints,
        CancellationToken cancellationToken)
    {
        var fingerprints = occurrenceFingerprints?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (fingerprints.Length < 3)
        {
            return null;
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
            var parameters = new List<string>(fingerprints.Length);
            for (var index = 0; index < fingerprints.Length; index++)
            {
                var name = $"@fp{index}";
                parameters.Add(name);
                AddParameter(command, name, fingerprints[index]);
            }
            command.CommandText = $"""
                SELECT
                    publication.canonical_publication_id,
                    publication.canonical_key,
                    publication.display_name,
                    publication.publisher,
                    publication.game_edition,
                    publication.publication_date,
                    COUNT(DISTINCT binding.semantic_fingerprint) AS match_count
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                JOIN canonical_publication publication
                    ON publication.canonical_publication_id = occurrence.canonical_publication_id
                WHERE binding.semantic_fingerprint IN ({string.Join(", ", parameters)})
                GROUP BY publication.canonical_publication_id, publication.canonical_key,
                         publication.display_name, publication.publisher,
                         publication.game_edition, publication.publication_date
                ORDER BY match_count DESC, publication.canonical_publication_id
                LIMIT 2;
                """;

            var matches = new List<(CanonicalPublicationIdentityView Publication, int Count)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add((ReadPublication(reader, "content-overlap", 0.92), checked((int)reader.GetInt64(6))));
            }
            if (matches.Count == 0 || matches[0].Count < 3)
            {
                return null;
            }
            if (matches.Count > 1 && matches[1].Count == matches[0].Count)
            {
                return null;
            }
            return matches[0].Publication;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<CanonicalPublicationIdentityView> CreateOrReadPublicationAsync(
        string canonicalKey,
        CanonicalPublicationEvidence evidence,
        string bibliographicFingerprint,
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
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO canonical_publication (
                    canonical_publication_id, canonical_key, display_name, publisher,
                    game_edition, publication_date, bibliographic_fingerprint, created_at)
                VALUES (@id, @canonical_key, @display_name, @publisher,
                        @game_edition, @publication_date, @fingerprint, @created_at)
                ON CONFLICT (canonical_key) DO NOTHING;
                """;
            AddParameter(insert, "@id", Guid.NewGuid());
            AddParameter(insert, "@canonical_key", canonicalKey);
            AddParameter(insert, "@display_name", evidence.DisplayName.Trim());
            AddNullableParameter(insert, "@publisher", NormalizeOptional(evidence.Publisher));
            AddNullableParameter(insert, "@game_edition", NormalizeOptional(evidence.GameEdition));
            AddNullableParameter(insert, "@publication_date", evidence.PublicationDate);
            AddParameter(insert, "@fingerprint", bibliographicFingerprint);
            AddParameter(insert, "@created_at", DateTimeOffset.UtcNow);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await using var read = connection.CreateCommand();
            read.CommandText = """
                SELECT canonical_publication_id, canonical_key, display_name, publisher,
                       game_edition, publication_date
                FROM canonical_publication
                WHERE canonical_key = @canonical_key;
                """;
            AddParameter(read, "@canonical_key", canonicalKey);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Canonical publication was not readable after creation.");
            }
            return ReadPublication(reader, "new", 1.0);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task AddAliasesAsync(
        Guid publicationId,
        IReadOnlyList<KeyValuePair<string, string>> aliases,
        CancellationToken cancellationToken)
    {
        if (aliases.Count == 0)
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
            foreach (var alias in aliases)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO canonical_publication_alias (
                        canonical_publication_alias_id, canonical_publication_id,
                        alias_scheme, alias_value, created_at)
                    VALUES (@id, @publication_id, @scheme, @value, @created_at)
                    ON CONFLICT DO NOTHING;
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@publication_id", publicationId);
                AddParameter(command, "@scheme", alias.Key);
                AddParameter(command, "@value", alias.Value);
                AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
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

    private Task EnsureSchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(SchemaSql, cancellationToken);

    private static bool IsMetadataCompatible(
        CanonicalPublicationIdentityView candidate,
        CanonicalPublicationEvidence evidence,
        IReadOnlyList<KeyValuePair<string, string>> aliases)
    {
        if (HasMetadataConflict(candidate, evidence))
        {
            return false;
        }

        var candidateTitle = CanonicalSourceIdentity.NormalizeIdentityPart(candidate.DisplayName);
        var observedTitle = CanonicalSourceIdentity.NormalizeIdentityPart(evidence.DisplayName);
        if (string.Equals(candidateTitle, observedTitle, StringComparison.Ordinal))
        {
            return true;
        }

        var candidateFallback = IsAliasFallback(candidate.DisplayName, aliases);
        var observedFallback = IsAliasFallback(evidence.DisplayName, aliases);
        return candidateFallback || observedFallback;
    }

    private static bool HasDisambiguatingContext(
        AliasCandidate candidate,
        CanonicalPublicationEvidence evidence,
        IReadOnlyList<KeyValuePair<string, string>> aliases)
    {
        if (candidate.MatchedSchemes.Any(SourceSpecificIdentitySchemes.Contains))
        {
            return true;
        }

        var candidateTitle = CanonicalSourceIdentity.NormalizeIdentityPart(candidate.Publication.DisplayName);
        var observedTitle = CanonicalSourceIdentity.NormalizeIdentityPart(evidence.DisplayName);
        if (string.Equals(candidateTitle, observedTitle, StringComparison.Ordinal))
        {
            return true;
        }

        return IsAliasFallback(candidate.Publication.DisplayName, aliases)
            && !IsAliasFallback(evidence.DisplayName, aliases);
    }

    private static int ScoreAliasCandidate(
        AliasCandidate candidate,
        CanonicalPublicationEvidence evidence,
        IReadOnlyList<KeyValuePair<string, string>> aliases)
    {
        var score = candidate.MatchedSchemes.Count * 2;
        var candidateTitle = CanonicalSourceIdentity.NormalizeIdentityPart(candidate.Publication.DisplayName);
        var observedTitle = CanonicalSourceIdentity.NormalizeIdentityPart(evidence.DisplayName);
        if (string.Equals(candidateTitle, observedTitle, StringComparison.Ordinal)) score += 20;
        if (candidate.MatchedSchemes.Any(SourceSpecificIdentitySchemes.Contains)) score += 20;
        if (Matches(candidate.Publication.Publisher, evidence.Publisher)) score += 8;
        if (Matches(candidate.Publication.GameEdition, evidence.GameEdition)) score += 6;
        if (candidate.Publication.PublicationDate.HasValue
            && evidence.PublicationDate.HasValue
            && candidate.Publication.PublicationDate.Value == evidence.PublicationDate.Value) score += 6;
        if (IsAliasFallback(candidate.Publication.DisplayName, aliases)
            && !IsAliasFallback(evidence.DisplayName, aliases)) score += 4;
        return score;
    }

    private static bool IsAliasFallback(
        string displayName,
        IReadOnlyList<KeyValuePair<string, string>> aliases)
    {
        var normalized = CanonicalSourceIdentity.NormalizeIdentityPart(displayName);
        return aliases.Any(value => string.Equals(
            normalized,
            CanonicalSourceIdentity.NormalizeIdentityPart(value.Value),
            StringComparison.Ordinal));
    }

    private static bool HasMetadataConflict(
        CanonicalPublicationIdentityView candidate,
        CanonicalPublicationEvidence evidence) =>
        Conflicts(candidate.Publisher, evidence.Publisher)
        || Conflicts(candidate.GameEdition, evidence.GameEdition)
        || (candidate.PublicationDate.HasValue
            && evidence.PublicationDate.HasValue
            && candidate.PublicationDate.Value != evidence.PublicationDate.Value);

    private static bool Conflicts(string? existing, string? observed)
    {
        if (string.IsNullOrWhiteSpace(existing) || string.IsNullOrWhiteSpace(observed)) return false;
        return !string.Equals(
            CanonicalSourceIdentity.NormalizeIdentityPart(existing),
            CanonicalSourceIdentity.NormalizeIdentityPart(observed),
            StringComparison.Ordinal);
    }

    private static bool Matches(string? existing, string? observed)
    {
        if (string.IsNullOrWhiteSpace(existing) || string.IsNullOrWhiteSpace(observed)) return false;
        return string.Equals(
            CanonicalSourceIdentity.NormalizeIdentityPart(existing),
            CanonicalSourceIdentity.NormalizeIdentityPart(observed),
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> NormalizeAliases(
        IReadOnlyDictionary<string, string>? aliases)
    {
        if (aliases is null) return [];
        return aliases
            .Where(value => !string.IsNullOrWhiteSpace(value.Key) && !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => new KeyValuePair<string, string>(
                CanonicalSourceIdentity.NormalizeIdentityPart(value.Key),
                value.Value.Trim().ToLowerInvariant()))
            .Where(value => !string.IsNullOrEmpty(value.Key))
            .DistinctBy(value => $"{value.Key}\n{value.Value}", StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static CanonicalPublicationIdentityView ReadPublication(
        DbDataReader reader,
        string matchKind,
        double confidence) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5),
            matchKind,
            confidence);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private sealed class AliasCandidateBuilder(CanonicalPublicationIdentityView publication)
    {
        private readonly HashSet<string> matchedSchemes = new(StringComparer.Ordinal);
        public void Add(string scheme) => matchedSchemes.Add(scheme);
        public AliasCandidate Build() => new(
            publication,
            matchedSchemes.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private sealed record AliasCandidate(
        CanonicalPublicationIdentityView Publication,
        IReadOnlyList<string> MatchedSchemes);

    private sealed record RankedAliasCandidate(AliasCandidate Candidate, int Score);

    private const string SchemaSql = """
        DROP INDEX IF EXISTS ux_canonical_publication_alias_identity;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_alias_publication_identity
            ON canonical_publication_alias(canonical_publication_id, alias_scheme, alias_value);
        CREATE INDEX IF NOT EXISTS ix_canonical_publication_alias_lookup
            ON canonical_publication_alias(alias_scheme, alias_value);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_publication_alias_strong_identity
            ON canonical_publication_alias(alias_scheme, alias_value)
            WHERE alias_scheme IN ('isbn', 'isbn-10', 'isbn10', 'isbn-13', 'isbn13');
        """;
}
