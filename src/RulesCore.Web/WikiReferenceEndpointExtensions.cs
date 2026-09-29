using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Web;

public static class WikiReferenceEndpointExtensions
{
    private const int InternalPageSize = 500;

    public static void MapWikiReferenceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/wiki/references", async (
            string? entityType,
            string? categoryMode,
            string? q,
            string? source,
            string? package,
            string? edition,
            int? limit,
            int? offset,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
                var catalog = await new WikiReferenceCatalogService(dbContext).GetGlobalCatalogAsync(
                    authenticationContext?.User.Id,
                    entityType,
                    categoryMode,
                    q,
                    source,
                    package,
                    edition,
                    limit ?? 200,
                    offset ?? 0,
                    cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(catalog);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/api/wiki/references/{referenceIdentity}", async (
            string referenceIdentity,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
                var detail = await new WikiReferenceCatalogService(dbContext).GetGlobalDetailAsync(
                    authenticationContext?.User.Id,
                    referenceIdentity,
                    cancellationToken);
                if (detail is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(detail);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/wiki/references", async (
            Guid campaignId,
            string? entityType,
            string? categoryMode,
            string? q,
            string? source,
            string? package,
            string? edition,
            bool? overridesOnly,
            int? limit,
            int? offset,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationFailure = RequireCampaignRead(httpContext, campaignId, out var authenticationContext);
            if (authenticationFailure is not null) return authenticationFailure;

            try
            {
                var service = new WikiReferenceCatalogService(dbContext);
                var catalog = overridesOnly == true
                    ? await GetCampaignOverridesOnlyAsync(
                        service,
                        campaignId,
                        authenticationContext!.User.Id,
                        entityType,
                        categoryMode,
                        q,
                        source,
                        package,
                        edition,
                        limit ?? 200,
                        offset ?? 0,
                        cancellationToken)
                    : await service.GetCampaignCatalogAsync(
                        campaignId,
                        authenticationContext!.User.Id,
                        entityType,
                        categoryMode,
                        q,
                        source,
                        package,
                        edition,
                        limit ?? 200,
                        offset ?? 0,
                        cancellationToken);
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(catalog);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapGet("/api/campaigns/{campaignId:guid}/wiki/references/{referenceIdentity}", async (
            Guid campaignId,
            string referenceIdentity,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authenticationFailure = RequireCampaignRead(httpContext, campaignId, out var authenticationContext);
            if (authenticationFailure is not null) return authenticationFailure;

            try
            {
                var detail = await new WikiReferenceCatalogService(dbContext).GetCampaignDetailAsync(
                    campaignId,
                    authenticationContext!.User.Id,
                    referenceIdentity,
                    cancellationToken);
                if (detail is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(detail);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });

        app.MapPost("/api/wiki/references/comparison", async (
            WikiReferenceComparisonRequest request,
            HttpContext httpContext,
            RulesCoreDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
                var userId = authenticationContext?.User.Id;
                var references = new WikiReferenceCatalogService(dbContext);
                if (!await references.ReferenceContainsRevisionsAsync(
                        userId,
                        request.ReferenceIdentity,
                        request.LeftSourceEntityRevisionId,
                        request.RightSourceEntityRevisionId,
                        cancellationToken))
                {
                    return Results.NotFound();
                }

                var comparison = await new RuleSemanticComparisonService(dbContext)
                    .CompareAccessibleSourcesAsync(
                        request.LeftSourceEntityRevisionId,
                        request.RightSourceEntityRevisionId,
                        userId,
                        cancellationToken);
                if (comparison is null) return Results.NotFound();
                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(comparison);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception);
            }
        });
    }

    private static async Task<WikiReferenceCatalogView> GetCampaignOverridesOnlyAsync(
        WikiReferenceCatalogService service,
        Guid campaignId,
        string userId,
        string? entityType,
        string? categoryMode,
        string? query,
        string? source,
        string? package,
        string? edition,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (limit < 1 || limit > InternalPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {InternalPageSize}.");
        }
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset can not be negative.");
        }

        var mode = WikiReferenceCategoryModes.Normalize(categoryMode);
        var all = new List<WikiReferenceItemView>();
        WikiReferenceCatalogView? firstPage = null;
        var sourceOffset = 0;
        while (true)
        {
            var page = await service.GetCampaignCatalogAsync(
                campaignId,
                userId,
                entityType: null,
                mode,
                query,
                source,
                package,
                edition,
                InternalPageSize,
                sourceOffset,
                cancellationToken);
            firstPage ??= page;
            all.AddRange(page.References);
            sourceOffset += page.References.Count;
            if (page.References.Count == 0 || sourceOffset >= page.TotalCount) break;
        }

        var overrides = all
            .Where(value => value.HasCampaignOverride)
            .ToArray();
        var entityFacets = BuildEntityFacets(overrides, mode);
        var historicalFacets = await BuildVariationFacetsAsync(
            service,
            campaignId,
            userId,
            overrides,
            cancellationToken);

        var requestedType = NormalizeReferenceCategory(entityType);
        IEnumerable<WikiReferenceItemView> filtered = overrides;
        if (requestedType is not null)
        {
            filtered = filtered.Where(value => mode == WikiReferenceCategoryModes.Effective
                ? string.Equals(value.EffectiveCategory, requestedType, StringComparison.OrdinalIgnoreCase)
                : value.CategoryHistory.Any(history =>
                    string.Equals(history.Category, requestedType, StringComparison.OrdinalIgnoreCase)));
        }

        var matches = filtered
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.ReferenceIdentity, StringComparer.Ordinal)
            .ToArray();
        var pageItems = matches.Skip(offset).Take(limit).ToArray();
        var basis = firstPage ?? new WikiReferenceCatalogView(
            "campaign",
            campaignId,
            null,
            null,
            0,
            mode,
            [],
            [],
            [],
            [],
            []);
        return basis with
        {
            TotalCount = matches.Length,
            CategoryMode = mode,
            EntityTypeFacets = entityFacets,
            SourceFacets = historicalFacets.Source,
            PackageFacets = historicalFacets.Package,
            EditionFacets = historicalFacets.Edition,
            References = pageItems
        };
    }

    private static IReadOnlyList<WikiReferenceFacetView> BuildEntityFacets(
        IReadOnlyCollection<WikiReferenceItemView> references,
        string categoryMode)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references)
        {
            var categories = categoryMode == WikiReferenceCategoryModes.Effective
                ? [reference.EffectiveCategory]
                : reference.CategoryHistory.Select(value => value.Category).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var category in categories)
            {
                counts[category] = counts.GetValueOrDefault(category) + 1;
            }
        }

        return counts
            .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .Select(value => new WikiReferenceFacetView(value.Key, value.Key, value.Value))
            .ToArray();
    }

    private static async Task<(IReadOnlyList<WikiReferenceFacetView> Source,
        IReadOnlyList<WikiReferenceFacetView> Package,
        IReadOnlyList<WikiReferenceFacetView> Edition)> BuildVariationFacetsAsync(
        WikiReferenceCatalogService service,
        Guid campaignId,
        string userId,
        IReadOnlyCollection<WikiReferenceItemView> references,
        CancellationToken cancellationToken)
    {
        var sourceGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var packageGroups = new Dictionary<string, (string Name, HashSet<string> References)>(StringComparer.OrdinalIgnoreCase);
        var editionGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in references)
        {
            var detail = await service.GetCampaignDetailAsync(
                campaignId,
                userId,
                reference.ReferenceIdentity,
                cancellationToken);
            if (detail is null) continue;
            foreach (var variation in detail.Variations)
            {
                AddFacet(sourceGroups, variation.SourceCode, reference.ReferenceIdentity);
                if (!string.IsNullOrWhiteSpace(variation.PackageKey))
                {
                    if (!packageGroups.TryGetValue(variation.PackageKey, out var package))
                    {
                        package = (variation.PackageDisplayName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    }
                    package.References.Add(reference.ReferenceIdentity);
                    packageGroups[variation.PackageKey] = package;
                }
                AddFacet(editionGroups, variation.EditionKey, reference.ReferenceIdentity);
            }
        }

        var sourceFacets = sourceGroups
            .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .Select(value => new WikiReferenceFacetView(value.Key, value.Key, value.Value.Count))
            .ToArray();
        var packageFacets = packageGroups
            .OrderBy(value => value.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(value => new WikiReferenceFacetView(value.Key, value.Value.Name, value.Value.References.Count))
            .ToArray();
        var editionFacets = editionGroups
            .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
            .Select(value => new WikiReferenceFacetView(value.Key, value.Key, value.Value.Count))
            .ToArray();
        return (sourceFacets, packageFacets, editionFacets);
    }

    private static void AddFacet(
        IDictionary<string, HashSet<string>> values,
        string key,
        string referenceIdentity)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (!values.TryGetValue(key, out var references))
        {
            references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            values[key] = references;
        }
        references.Add(referenceIdentity);
    }

    private static string? NormalizeReferenceCategory(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : RuleConceptEntityTypes.Normalize(value);

    private static IResult? RequireCampaignRead(
        HttpContext httpContext,
        Guid campaignId,
        out ToolHostAuthenticationContext? authenticationContext)
    {
        authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
        if (authenticationContext is null) return Results.Unauthorized();
        return RulesAuthority.CanAccessCampaignRules(authenticationContext, campaignId)
            ? null
            : Results.NotFound();
    }

    private static IResult BadRequest(ArgumentException exception) =>
        Results.Problem(
            title: "Invalid Rules Wiki reference request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
}
