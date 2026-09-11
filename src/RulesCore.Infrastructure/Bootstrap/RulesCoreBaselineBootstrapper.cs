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
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        foreach (var package in RulesCoreBaselineCatalog.SourcePackages)
        {
            await EnsurePackageAsync(package, cancellationToken);
        }

        var authorityReferenceCount = await EnsureAuthorityReferencesAsync(cancellationToken);
        var hostedSourceDefinitionCount = await BuiltInSrdHostedSources.EnsureAsync(
            dbContext,
            importer,
            cancellationToken);
        var houseRuleImport = await importer.Import5eToolsDocumentAsync(
            RulesCoreBaselineCatalog.CreateHouseRuleImportRequest(),
            cancellationToken);

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
        var now = DateTimeOffset.UtcNow;
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
                CreatedAt = now
            };
            dbContext.SourcePackages.Add(package);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            EnsurePackageMatches(package, seed);
        }

        foreach (var workSeed in seed.Works)
        {
            var work = await dbContext.SourceWorks
                .SingleOrDefaultAsync(
                    value => value.SourcePackageId == package.Id
                        && value.Key == workSeed.Key,
                    cancellationToken);
            if (work is null)
            {
                work = new SourceWork
                {
                    Id = Guid.NewGuid(),
                    SourcePackageId = package.Id,
                    Key = workSeed.Key,
                    DisplayName = workSeed.DisplayName,
                    CreatedAt = now
                };
                dbContext.SourceWorks.Add(work);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (!string.Equals(
                         work.DisplayName,
                         workSeed.DisplayName,
                         StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Built-in source work '{seed.Key}/{workSeed.Key}' already exists with different immutable metadata.");
            }

            var edition = await dbContext.SourceEditions
                .SingleOrDefaultAsync(
                    value => value.SourceWorkId == work.Id
                        && value.Key == workSeed.EditionKey,
                    cancellationToken);
            if (edition is null)
            {
                edition = new SourceEdition
                {
                    Id = Guid.NewGuid(),
                    SourceWorkId = work.Id,
                    Key = workSeed.EditionKey,
                    DisplayName = workSeed.EditionDisplayName,
                    CreatedAt = now
                };
                dbContext.SourceEditions.Add(edition);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (!string.Equals(
                         edition.DisplayName,
                         workSeed.EditionDisplayName,
                         StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Built-in source release '{seed.Key}/{workSeed.Key}/{workSeed.EditionKey}' already exists with different immutable metadata.");
            }

            await SourceFrameworkStore.MergeEditionMetadataAsync(
                dbContext,
                edition.Id,
                workSeed.GameEdition,
                workSeed.ReleaseKind,
                workSeed.PublicationDate,
                cancellationToken);
        }
    }

    private async Task<int> EnsureAuthorityReferencesAsync(CancellationToken cancellationToken)
    {
        foreach (var seed in RulesCoreBaselineCatalog.SrdAuthorities)
        {
            var editionId = await (
                from edition in dbContext.SourceEditions.AsNoTracking()
                join work in dbContext.SourceWorks.AsNoTracking()
                    on edition.SourceWorkId equals work.Id
                join package in dbContext.SourcePackages.AsNoTracking()
                    on work.SourcePackageId equals package.Id
                where package.Key == seed.PackageKey
                    && work.Key == seed.WorkKey
                    && edition.Key == seed.EditionKey
                select edition.Id)
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
                    INSERT INTO source_edition_authority_reference (
                        source_edition_authority_reference_id,
                        source_edition_id,
                        authority_kind,
                        uri,
                        media_type,
                        note,
                        created_at)
                    VALUES (
                        @id,
                        @edition_id,
                        @authority_kind,
                        @uri,
                        @media_type,
                        @note,
                        @created_at)
                    ON CONFLICT (source_edition_id, authority_kind, uri) DO NOTHING;
                    """;
                AddParameter(command, "@id", Guid.NewGuid());
                AddParameter(command, "@edition_id", editionId);
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
