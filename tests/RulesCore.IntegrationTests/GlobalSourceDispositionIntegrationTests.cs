using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class GlobalSourceDispositionIntegrationTests
{
    [Fact]
    public async Task IgnoringPackageSuppressesGlobalNormalizationWithoutRevokingSourceAccess()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        await using var factory = new WebApplicationFactory<Program>();
        await using var scope = factory.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
        var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
        var catalog = scope.ServiceProvider.GetRequiredService<ISourceCatalogService>();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
        var normalization = new SourceNormalizationService(db);
        var disposition = new GlobalSourceDispositionService(db);

        var token = Guid.NewGuid().ToString("N")[..10];
        var packageKey = $"ignored-homebrew-{token}";
        var userId = $"ignore-user-{token}";
        Guid? packageId = null;

        try
        {
            var imported = await importer.Import5eToolsDocumentAsync(new Import5eToolsDocumentRequest(
                packageKey,
                "Large homebrew collection",
                "integration-test",
                License: null,
                IsPublic: false,
                WorkKey: "homebrew-work",
                WorkDisplayName: "Homebrew work",
                EditionKey: "current",
                EditionDisplayName: "Current",
                Json: $$"""
                    {
                      "spell": [
                        {
                          "name": "Irrelevant Homebrew {{token}}",
                          "source": "HB{{token}}",
                          "level": 1,
                          "entries": ["Integration test content."]
                        }
                      ]
                    }
                    """,
                GameEdition: "5e",
                ReleaseKind: SourceReleaseKinds.Other,
                Publisher: "Homebrew Publisher"));
            packageId = imported.PackageId;
            await grants.GrantAsync(userId, imported.PackageId);

            var before = await normalization.GetCandidatesPageAsync(
                userId,
                query: token,
                limit: 20);
            var candidate = Assert.Single(before);
            Assert.Equal(imported.PackageId, candidate.SourcePackageId);

            var ignored = await disposition.SetIgnoredAsync(
                imported.PackageId,
                new SetGlobalSourceIgnoredRequest(true, "Not relevant to the global ruleset."),
                "rules-lawyer");
            Assert.NotNull(ignored);
            Assert.Equal("Homebrew Publisher", imported.Publisher);

            var ignoredPackages = await disposition.GetIgnoredAsync();
            var ignoredView = Assert.Single(
                ignoredPackages,
                value => value.SourcePackageId == imported.PackageId);
            Assert.Equal("Not relevant to the global ruleset.", ignoredView.Reason);

            var whileIgnored = await normalization.GetCandidatesPageAsync(
                userId,
                query: token,
                limit: 20);
            Assert.Empty(whileIgnored);

            var accessiblePackages = await catalog.GetAccessiblePackagesAsync(userId);
            Assert.Contains(accessiblePackages, value => value.Id == imported.PackageId);
            Assert.True(await grants.HasGrantAsync(userId, imported.PackageId));

            await disposition.SetIgnoredAsync(
                imported.PackageId,
                new SetGlobalSourceIgnoredRequest(false),
                "rules-lawyer");

            var afterRestore = await normalization.GetCandidatesPageAsync(
                userId,
                query: token,
                limit: 20);
            Assert.Single(afterRestore);
            Assert.DoesNotContain(
                await disposition.GetIgnoredAsync(),
                value => value.SourcePackageId == imported.PackageId);
        }
        finally
        {
            if (packageId is Guid id)
            {
                var package = await db.SourcePackages.SingleOrDefaultAsync(value => value.Id == id);
                if (package is not null)
                {
                    db.SourcePackages.Remove(package);
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}