using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Bootstrap;

public interface IRulesCoreBaselineBootstrapper
{
    Task<RulesCoreBaselineBootstrapResult> EnsureAsync(
        CancellationToken cancellationToken = default);
}

public sealed record RulesCoreBaselineBootstrapResult(
    int SourcePackageCount,
    int SourceAuthorityReferenceCount,
    int HostedSourceDefinitionCount,
    int HouseRuleSourceEntityCount,
    bool RulesBaselineApplied,
    PublishedRulesetRevisionView? PublishedRuleset);

public sealed class RulesCoreBaselineBootstrapper(
    RulesCoreDbContext dbContext,
    ISourceImportService importer,
    IGlobalRulesService globalRules)
    : IRulesCoreBaselineBootstrapper
{
    private const string AuthorityKind = "membership-authority";

    public async Task<RulesCoreBaselineBootstrapResult> EnsureAsync(
        CancellationToken cancellationToken = default)
    {
        foreach (var package in RulesCoreBaselineCatalog.SourcePackages)
        {
            await EnsurePackageAsync(package, cancellationToken);
        }

        var authorityReferenceCount = await EnsureAuthorityReferencesAsync(cancellationToken);
        _ = await BundledSrdSnapshots.EnsureAsync(
            dbContext,
            importer,
            cancellationToken);
        var hostedSourceDefinitionCount = await BuiltInSrdHostedSources.EnsureAsync(
            dbContext,
            importer,
            cancellationToken);
        var houseRuleImport = await ImportHouseRulesAsync(cancellationToken);

        var publishedRuleset = await EnsureRulesBaselineAsync(
            houseRuleImport,
            cancellationToken);

        return new RulesCoreBaselineBootstrapResult(
            RulesCoreBaselineCatalog.SourcePackages.Count,
            authorityReferenceCount,
            hostedSourceDefinitionCount,
            houseRuleImport.Entities.Count,
            publishedRuleset is not null,
            publishedRuleset);
    }

    private async Task EnsurePackageAsync(
        SourcePackageSeed seed,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.SourcePackages
            .SingleOrDefaultAsync(value => value.Key == seed.Key, cancellationToken);
        if (package is null)
        {
            package = new SourcePackage
            {
                Id = Guid.NewGuid(),
                Key = seed.Key,
                DisplayName = seed.DisplayName,
                Provider = seed.Provider,
                License = seed.License,
                IsPublic = seed.IsPublic,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.SourcePackages.Add(package);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        EnsurePackageMatches(package, seed);
    }

    private async Task<int> EnsureAuthorityReferencesAsync(CancellationToken cancellationToken)
    {
        await EnsureAuthoritySchemaAsync(cancellationToken);
        foreach (var seed in RulesCoreBaselineCatalog.SrdAuthorities)
        {
            var packageId = await dbContext.SourcePackages
                .AsNoTracking()
                .Where(value => value.Key == seed.PackageKey)
                .Select(value => value.Id)
                .SingleAsync(cancellationToken);

            var connection = dbContext.Database.GetDbConnection();
            var openedHere = connection.State != System.Data.ConnectionState.Open;
            if (openedHere)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO source_package_authority_reference (
                        source_package_authority_reference_id,
                        source_package_id,
                        publication_key,
                        authority_kind,
                        uri,
                        media_type,
                        note,
                        created_at)
                    VALUES (
                        @id,
                        @package_id,
                        @publication_key,
                        @authority_kind,
                        @uri,
                        @media_type,
                        @note,
                        @created_at)
                    ON CONFLICT (source_package_id, publication_key, authority_kind, uri) DO NOTHING;
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@package_id", packageId);
                AddParameter(command, "@publication_key", seed.WorkKey);
                AddParameter(command, "@authority_kind", AuthorityKind);
                AddParameter(command, "@uri", seed.Uri);
                AddParameter(command, "@media_type", seed.MediaType);
                AddParameter(command, "@note", seed.Note);
                AddParameter(command, "@created_at", DateTimeOffset.UtcNow);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }
        }

        return RulesCoreBaselineCatalog.SrdAuthorities.Count;
    }

    private async Task<SourceImportResult> ImportHouseRulesAsync(CancellationToken cancellationToken)
    {
        var request = RulesCoreBaselineCatalog.CreateHouseRuleImportRequest();
        var representation = RulesCoreBaselineSourceAdapter.Read(request);
        var imported = await new NormalizedSourceImportService(dbContext).ImportAsync(
            new ImportNormalizedSourceRequest(
                request.PackageKey,
                request.PackageDisplayName,
                request.Provider,
                request.License,
                request.IsPublic,
                representation),
            cancellationToken);

        foreach (var publication in imported.Publications)
        {
            await new CanonicalPublicationReleaseKindService(dbContext).MergeAsync(
                publication.CanonicalPublicationId,
                request.ReleaseKind,
                cancellationToken);
        }

        return new SourceImportResult(
            imported.PackageId,
            imported.Entities,
            request.GameEdition,
            SourceReleaseKinds.NormalizeImportLabel(request.ReleaseKind),
            request.PublicationDate,
            request.Publisher ?? request.Provider);
    }

    private Task EnsureAuthoritySchemaAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS source_package_authority_reference (
                source_package_authority_reference_id uuid NOT NULL,
                source_package_id uuid NOT NULL,
                publication_key varchar(200) NOT NULL,
                authority_kind varchar(80) NOT NULL,
                uri varchar(2000) NOT NULL,
                media_type varchar(200) NOT NULL,
                note varchar(2000) NULL,
                created_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_source_package_authority_reference PRIMARY KEY (source_package_authority_reference_id),
                CONSTRAINT fk_source_package_authority_reference_package FOREIGN KEY (source_package_id)
                    REFERENCES source_package(source_package_id) ON DELETE CASCADE);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_source_package_authority_reference_identity
                ON source_package_authority_reference(source_package_id, publication_key, authority_kind, uri);
            """,
            cancellationToken);

    private async Task<PublishedRulesetRevisionView?> EnsureRulesBaselineAsync(
        SourceImportResult houseRuleImport,
        CancellationToken cancellationToken)
    {
        var baselineConceptKeys = RulesCoreBaselineCatalog.Rules
            .Select(value => value.ConceptKey)
            .ToHashSet(StringComparer.Ordinal);

        var existingConceptKeys = await dbContext.RuleConcepts
            .AsNoTracking()
            .Select(value => value.Key)
            .ToArrayAsync(cancellationToken);
        var hasNonBaselineConcept = existingConceptKeys.Any(
            value => !baselineConceptKeys.Contains(value));
        var hasPublishedRuleset = await dbContext.RulesetRevisions
            .AsNoTracking()
            .AnyAsync(cancellationToken);
        var hasNonBootstrapDecision = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .AnyAsync(
                value => value.CreatedByUserId != RulesCoreBaselineCatalog.BootstrapActor,
                cancellationToken);

        if (hasNonBaselineConcept || hasPublishedRuleset || hasNonBootstrapDecision)
        {
            return null;
        }

        foreach (var seed in RulesCoreBaselineCatalog.Rules)
        {
            var importedEntity = houseRuleImport.Entities.SingleOrDefault(value =>
                string.Equals(value.EntityType, "houseRule", StringComparison.Ordinal)
                && string.Equals(value.Name, seed.SourceEntityName, StringComparison.Ordinal));
            if (importedEntity is null)
            {
                throw new InvalidOperationException(
                    $"Built-in house-rule source entity '{seed.SourceEntityName}' was not imported.");
            }

            var concept = (await globalRules.CreateConceptAsync(
                new CreateRuleConceptRequest(
                    seed.ConceptKey,
                    importedEntity.EntityType,
                    seed.DisplayName),
                RulesCoreBaselineCatalog.BootstrapActor,
                cancellationToken)).Value;

            await globalRules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(importedEntity.EntityId),
                RulesCoreBaselineCatalog.BootstrapActor,
                cancellationToken);

            var hasDecision = await dbContext.GlobalRuleDecisions
                .AsNoTracking()
                .AnyAsync(
                    value => value.RuleConceptId == concept.Id,
                    cancellationToken);
            if (hasDecision)
            {
                continue;
            }

            var sourceRevisionId = await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .Where(value => value.SourceEntityId == importedEntity.EntityId)
                .OrderByDescending(value => value.RevisionNumber)
                .Select(value => value.Id)
                .FirstAsync(cancellationToken);

            await globalRules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(
                    sourceRevisionId,
                    "Built-in Dorks & Dice baseline rule."),
                RulesCoreBaselineCatalog.BootstrapActor,
                cancellationToken);
        }

        return await globalRules.PublishAsync(
            RulesCoreBaselineCatalog.BootstrapActor,
            cancellationToken);
    }

    private static void EnsurePackageMatches(
        SourcePackage package,
        SourcePackageSeed seed)
    {
        if (!string.Equals(package.DisplayName, seed.DisplayName, StringComparison.Ordinal)
            || !string.Equals(package.Provider, seed.Provider, StringComparison.Ordinal)
            || !string.Equals(package.License, seed.License, StringComparison.Ordinal)
            || package.IsPublic != seed.IsPublic)
        {
            throw new InvalidOperationException(
                $"Built-in source package '{seed.Key}' already exists with different immutable metadata.");
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
