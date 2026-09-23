namespace RulesCore.IntegrationTests;

public sealed class RuleBrowserAssetTests
{
    [Fact]
    public void BrowserConsumesToolRelativeRoutingContract()
    {
        var content = ReadWebAssets(
            "rules-browser.js",
            "rules-browser-index.js",
            "rules-browser-detail.js",
            "rules-browser-routing.js");

        Assert.Contains("toolRoute", content, StringComparison.Ordinal);
        Assert.Contains("toolBasePath", content, StringComparison.Ordinal);
        Assert.Contains("browserLink", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceLibraryConsumesStableEntityRoutingContract()
    {
        var content = ReadWebAsset("source-library.js");

        Assert.Contains("/sources/", content, StringComparison.Ordinal);
        Assert.Contains("parseSourceEntityRoute", content, StringComparison.Ordinal);
        Assert.Contains("libraryDeepLink", content, StringComparison.Ordinal);
        Assert.Contains("popstate", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RulesLawyerWorkspaceUsesAddressableHostedRoute()
    {
        var content = ReadWebAsset("workspace-routing.js");

        Assert.Contains("/adjudication/rules-lawyer", content, StringComparison.Ordinal);
        Assert.Contains("viewNavigation.global", content, StringComparison.Ordinal);
        Assert.Contains("toolBasePath", content, StringComparison.Ordinal);
        Assert.Contains("popstate", content, StringComparison.Ordinal);
        Assert.Contains("canEditGlobal", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishedCatalogEmptyStatesKeepSourceAndPublicationLayersDistinct()
    {
        var content = ReadWebAsset("rules-browser.js");

        Assert.Contains("resolvePublishedEmptyState", content, StringComparison.Ordinal);
        Assert.Contains("No published", content, StringComparison.Ordinal);
        Assert.Contains("source records exist in the Source Library", content, StringComparison.Ordinal);
        Assert.Contains("imported/source material is not published", content, StringComparison.Ordinal);
        Assert.Contains("Published catalogs are filtered by source access", content, StringComparison.Ordinal);
        Assert.Contains("Open Source Library", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRecordsExposeReadableAndRawRepresentations()
    {
        var content = ReadWebAsset("source-library.js");

        Assert.Contains("Readable content", content, StringComparison.Ordinal);
        Assert.Contains("Readable source-native content", content, StringComparison.Ordinal);
        Assert.Contains("renderReadableSourceValue", content, StringComparison.Ordinal);
        Assert.Contains("Raw source-native data (advanced)", content, StringComparison.Ordinal);
        Assert.Contains("Raw Rules Core mechanical data (advanced)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AdjudicationQueueSeparatesObservationFromDiscovery()
    {
        var content = ReadWebAsset("adjudication-queue.js");

        Assert.Contains("Run deterministic discovery", content, StringComparison.Ordinal);
        Assert.Contains("Loading existing adjudication work", content, StringComparison.Ordinal);
        Assert.Contains("No discovery was run", content, StringComparison.Ordinal);
        Assert.Contains("does not run discovery or create new work", content, StringComparison.Ordinal);
        Assert.Contains("may reconcile existing work", content, StringComparison.Ordinal);
        Assert.Contains("Discovery completed", content, StringComparison.Ordinal);
        Assert.Contains("Discovery failed", content, StringComparison.Ordinal);
        Assert.Contains("card.refresh = loadQueue", content, StringComparison.Ordinal);
        Assert.Contains("discover.addEventListener", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Running deterministic discovery and loading adjudication work", content, StringComparison.Ordinal);
    }

    [Fact]
    public void SourcesAndAdjudicationDoNotReintroduceLegacyWorkspaceShells()
    {
        var sourceLibrary = ReadWebAsset("source-library.js");
        var sourceSurfaces = ReadWebAssets(
            "source-library.js",
            "source-add.js",
            "source-removal.js",
            "source-admin.js",
            "hosted-source-authoring.js",
            "source-versioning.js",
            "source-normalization.js",
            "source-revision-review.js");
        var adjudication = ReadWebAssets(
            "adjudication-queue.js",
            "authoring.js",
            "source-normalization.js",
            "source-revision-review.js");
        var shell = ReadWebAsset("ux-shell.js");

        Assert.Contains("workspaceSection", sourceSurfaces, StringComparison.Ordinal);
        Assert.Contains("workspaceSection", adjudication, StringComparison.Ordinal);
        Assert.DoesNotContain("app.renderHeader = () => renderHeader(app)", sourceLibrary, StringComparison.Ordinal);
        Assert.DoesNotContain("app.renderNavigation = () => renderNavigation(app)", sourceLibrary, StringComparison.Ordinal);
        Assert.DoesNotContain("prependRulesLawyerWorkflow", sourceLibrary, StringComparison.Ordinal);
        Assert.DoesNotContain("firstCard.classList.add(\"rules-core-page-lead\")", shell, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "card card-body mb-3 rules-core-panel rules-core-add-source",
            sourceSurfaces,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "card card-body mb-3 rules-core-panel rules-core-source-removal",
            sourceSurfaces,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "className: \"card card-body mb-3\"",
            adjudication,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RulesBrowserInternalScrollChainingPreservesOrientation()
    {
        var content = ReadWebAsset("rules-core.css");

        Assert.Contains("scrollbar-gutter: stable", content, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior: auto", content, StringComparison.Ordinal);
        Assert.Contains("scroll-padding-top: 3rem", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MonsterRendererIsRegisteredAsSpecializedRuleRenderer()
    {
        var content = ReadWebAssets(
            "rule-renderers.js",
            "rule-renderer-support.js",
            "rule-renderers-specialized.js");

        Assert.Contains("[\"monster\", renderMonster]", content, StringComparison.Ordinal);
        Assert.Contains("Legendary Actions", content, StringComparison.Ordinal);
        Assert.Contains("Ability Scores", content, StringComparison.Ordinal);
        Assert.Contains("abilityDatum(\"Save\"", content, StringComparison.Ordinal);
        Assert.Contains("hasAbilitySaveModel", content, StringComparison.Ordinal);
    }

    private static string ReadWebAssets(params string[] filenames) =>
        string.Join(
            Environment.NewLine,
            filenames.Select(ReadWebAsset));

    private static string ReadWebAsset(string filename)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "dorks-and-dice-rules-core.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the Rules Core repository root.");
        }

        var path = Path.Combine(directory.FullName, "src", "RulesCore.Web", "wwwroot", filename);
        return File.ReadAllText(path);
    }
}