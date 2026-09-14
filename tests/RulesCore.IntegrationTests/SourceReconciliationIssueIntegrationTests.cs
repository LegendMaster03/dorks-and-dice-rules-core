using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class SourceReconciliationIssueIntegrationTests
{
    [Fact]
    public async Task CanonicalConflictIsDurableUserScopedAndResolvedByCorrectedRetry()
    {
        var db = await OpenDatabaseAsync();
        if (db is null) return;
        await using (db)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var importer = new NormalizedSourceImportService(db);
            var packageKey = $"durable-conflict-{token}";
            var name = $"Durable Conflict Rule {token}";
            var originIdentity = $"test:{packageKey}";
            const string firstSemantic = "{\"effect\":\"Gain a +2 bonus.\"}";
            const string secondSemantic = "{\"effect\":\"Gain a +3 bonus.\"}";
            const string secondRaw = "{\"effect\":\"Gain a +3 bonus.\",\"page\":1}";

            var first = await importer.ImportAsync(Request(
                packageKey,
                name,
                "{\"effect\":\"Gain a +2 bonus.\",\"page\":1}",
                firstSemantic,
                originIdentity: originIdentity));
            var entity = Assert.Single(first.Entities);
            var firstCanonicalEntityId = await ReadLatestCanonicalEntityIdAsync(db, entity.EntityId);

            var scheme = $"durable-{token}";
            const string aliasValue = "stable-upstream-key";
            await new CanonicalEntityAliasStore(db).RegisterAsync(
                firstCanonicalEntityId,
                scheme,
                aliasValue,
                CanonicalSourceIdentity.SemanticFingerprint(secondSemantic),
                "deliberately-invalid-collapse-fixture",
                1.0);

            var conflicted = await importer.ImportAsync(Request(
                packageKey,
                name,
                secondRaw,
                secondSemantic,
                new Dictionary<string, string> { [scheme] = aliasValue },
                originIdentity));
            var returnedIssue = Assert.Single(conflicted.ReconciliationIssues);

            var owner = $"issue-owner-{token}";
            var otherUser = $"issue-other-{token}";
            var sourceRegistrationId = Guid.NewGuid();
            var sourceService = new CurrentUserSourceService(
                db,
                new SourceImportService(db),
                new SourceGrantService(db));
            _ = await sourceService.ListAsync(owner);
            await RegisterCurrentUserSourceAsync(
                db,
                sourceRegistrationId,
                owner,
                conflicted.PackageId,
                token);

            var issues = new SourceReconciliationIssueService(db);
            var sourceIssues = await issues.ListForCurrentUserSourceAsync(owner, sourceRegistrationId);
            var persistedIssue = Assert.Single(sourceIssues!);
            Assert.Equal(returnedIssue.Kind, persistedIssue.Kind);
            Assert.Equal(returnedIssue.PublicationLocalKey, persistedIssue.PublicationLocalKey);
            Assert.Equal(returnedIssue.PublicationDisplayName, persistedIssue.PublicationDisplayName);
            Assert.Equal(
                returnedIssue.SourceEntityIds.OrderBy(value => value).ToArray(),
                persistedIssue.SourceEntityIds.OrderBy(value => value).ToArray());
            Assert.Equal(returnedIssue.Message, persistedIssue.Message);
            Assert.Null(await issues.ListForCurrentUserSourceAsync(otherUser, sourceRegistrationId));

            var jobs = new CurrentUserSourceImportJobService(db);
            var job = await jobs.QueueWebAddAsync(
                owner,
                $"https://example.com/rules/{token}.json");
            await LinkCompletedJobAsync(db, job.Id, sourceRegistrationId);

            var jobIssues = await issues.ListForCurrentUserImportJobAsync(owner, job.Id);
            Assert.Single(jobIssues!);
            Assert.Null(await issues.ListForCurrentUserImportJobAsync(otherUser, job.Id));

            var retried = await importer.ImportAsync(Request(
                packageKey,
                name,
                secondRaw,
                secondSemantic,
                aliases: null,
                originIdentity));
            Assert.Empty(retried.ReconciliationIssues);
            Assert.Empty((await issues.ListForCurrentUserSourceAsync(owner, sourceRegistrationId))!);
            Assert.Empty((await issues.ListForCurrentUserImportJobAsync(owner, job.Id))!);
        }
    }

    private static ImportNormalizedSourceRequest Request(
        string packageKey,
        string name,
        string rawJson,
        string semanticJson,
        IReadOnlyDictionary<string, string>? aliases = null,
        string? originIdentity = null)
    {
        const string publicationKey = "durable-conflict-book";
        return new ImportNormalizedSourceRequest(
            packageKey,
            packageKey,
            "integration-test",
            License: null,
            IsPublic: false,
            new NormalizedSourceRepresentation(
                "durable-conflict-format",
                new SourceRepresentationArtifact(
                    $"{packageKey}.json",
                    Encoding.UTF8.GetBytes(rawJson),
                    originIdentity ?? $"test:{packageKey}:{Guid.NewGuid():N}"),
                [new NormalizedSourceRecord(
                    "rule",
                    name,
                    SourceCode: "DURABLE",
                    NativeKey: "stable-native-key",
                    RawJson: rawJson,
                    LocatorKey: "entry:1",
                    PublicationLocalKey: publicationKey,
                    SemanticJson: semanticJson,
                    CanonicalAliases: aliases)],
                [new NormalizedSourcePublication(
                    publicationKey,
                    $"Durable Conflict Fixture Book {packageKey}",
                    Publisher: "Example Press",
                    GameEdition: "3.5e",
                    PublicationDate: new DateOnly(2003, 7, 1))]));
    }

    private static async Task<Guid> ReadLatestCanonicalEntityIdAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT occurrence.canonical_entity_id
                FROM source_entity_occurrence_binding binding
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE binding.source_entity_id = @source_entity_id
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY binding.created_at DESC
                LIMIT 1;
                """;
            AddParameter(command, "@source_entity_id", sourceEntityId);
            return (Guid)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Source entity did not receive a canonical identity."));
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static Task RegisterCurrentUserSourceAsync(
        RulesCoreDbContext db,
        Guid sourceRegistrationId,
        string userId,
        Guid packageId,
        string token) =>
        db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO current_user_source (
                current_user_source_id,
                user_id,
                source_kind,
                display_name,
                source_url,
                source_package_id,
                origin_key,
                source_codes_json,
                entity_count,
                added_at,
                refreshed_at)
            VALUES (
                {{sourceRegistrationId}},
                {{userId}},
                'upload',
                {{"Durable conflict " + token}},
                NULL,
                {{packageId}},
                {{CanonicalSourceIdentity.Fingerprint("registration:" + token)}},
                '["DURABLE"]'::jsonb,
                1,
                {{DateTimeOffset.UtcNow}},
                {{DateTimeOffset.UtcNow}});
            """);

    private static Task LinkCompletedJobAsync(
        RulesCoreDbContext db,
        Guid jobId,
        Guid sourceRegistrationId) =>
        db.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE current_user_source_import_job
            SET status = 'completed',
                current_user_source_id = {{sourceRegistrationId}},
                progress_stage = 'completed',
                progress_detail = 'Import completed',
                progress_updated_at = {{DateTimeOffset.UtcNow}},
                completed_at = {{DateTimeOffset.UtcNow}}
            WHERE current_user_source_import_job_id = {{jobId}};
            """);

    private static async Task<RulesCoreDbContext?> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var db = new RulesCoreDbContext(
            new DbContextOptionsBuilder<RulesCoreDbContext>().UseNpgsql(connectionString).Options);
        await new RulesCoreSchemaInitializer(db).InitializeAsync();
        return db;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
