using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Hosting;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;
using RulesCore.Web;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("RulesCore");
var hasDatabase = !string.IsNullOrWhiteSpace(connectionString);
if (hasDatabase)
{
    builder.Services.AddDbContext<RulesCoreDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IRulesCoreSchemaInitializer, RulesCoreSchemaInitializer>();
    builder.Services.AddScoped<ISourceImportService, SourceImportService>();
    builder.Services.AddScoped<ISourceCatalogService, SourceCatalogService>();
    builder.Services.AddScoped<ISourceGrantService, SourceGrantService>();
    builder.Services.AddScoped<IGlobalRulesService, GlobalRulesService>();
}

var toolHostBaseUrl = builder.Configuration["ToolHost:BaseUrl"];
Uri? toolHostBaseUri = null;
if (!string.IsNullOrWhiteSpace(toolHostBaseUrl))
{
    if (!Uri.TryCreate(toolHostBaseUrl, UriKind.Absolute, out toolHostBaseUri)
        || (toolHostBaseUri.Scheme != Uri.UriSchemeHttp
            && toolHostBaseUri.Scheme != Uri.UriSchemeHttps))
    {
        throw new InvalidOperationException("ToolHost:BaseUrl must be an absolute HTTP or HTTPS URL.");
    }
}

builder.Services
    .AddHttpClient<IToolHostAuthenticationClient, DorksAndDiceToolHostAuthenticationClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(3);
        client.BaseAddress = toolHostBaseUri;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    });

builder.Services.AddHealthChecks();

var app = builder.Build();

if (hasDatabase)
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<IRulesCoreSchemaInitializer>();
    await initializer.InitializeAsync();
}

app.UseMiddleware<HostedToolAuthenticationMiddleware>();
app.UseStaticFiles();

app.MapHealthChecks("/health");

app.MapGet("/ready", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetService<RulesCoreDbContext>();

    if (db is null)
    {
        return Results.Problem(
            title: "Rules Core is not ready",
            detail: "ConnectionStrings:RulesCore is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!await db.Database.CanConnectAsync(cancellationToken))
    {
        return Results.Problem(
            title: "Rules Core is not ready",
            detail: "The configured PostgreSQL database is unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "ready",
        database = "postgresql",
        sourceLayer = "ready",
        sourceAccess = "ready",
        globalRulesLayer = "ready"
    });
});

app.MapGet("/", () => Results.Ok(new
{
    service = "Dorks & Dice Rules Core",
    status = "running",
    module = "/app.js",
    health = "/health",
    readiness = "/ready",
    api = "/api"
}));

app.MapGet("/api", () => Results.Ok(new
{
    service = "Rules Core API",
    version = "0.4-dev",
    endpointFamilies = new[]
    {
        "/api/rules",
        "/api/sources",
        "/api/global/rules",
        "/api/campaigns/{campaignId}/rules",
        "/api/integration"
    }
}));

app.MapGet("/api/integration/session", (HttpContext httpContext) =>
{
    var authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
    if (authenticationContext is null)
    {
        return Results.Unauthorized();
    }

    httpContext.Response.Headers.CacheControl = "no-store";
    return Results.Ok(authenticationContext);
});

if (hasDatabase)
{
    app.MapGet("/api/sources", async (
        HttpContext httpContext,
        ISourceCatalogService catalog,
        CancellationToken cancellationToken) =>
    {
        var userId = HostedToolAuthenticationMiddleware
            .GetAuthenticationContext(httpContext)?
            .User.Id;
        return Results.Ok(await catalog.GetAccessiblePackagesAsync(userId, cancellationToken));
    });

    app.MapGet("/api/sources/entities/{entityId:guid}", async (
        Guid entityId,
        HttpContext httpContext,
        ISourceCatalogService catalog,
        CancellationToken cancellationToken) =>
    {
        var userId = HostedToolAuthenticationMiddleware
            .GetAuthenticationContext(httpContext)?
            .User.Id;
        var entity = await catalog.GetLatestAccessibleEntityAsync(
            entityId,
            userId,
            cancellationToken);
        return entity is null ? Results.NotFound() : Results.Ok(entity);
    });

    app.MapGet("/api/rules/{conceptKey}", async (
        string conceptKey,
        HttpContext httpContext,
        IGlobalRulesService rules,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var userId = HostedToolAuthenticationMiddleware
                .GetAuthenticationContext(httpContext)?
                .User.Id;
            var resolved = await rules.ResolveLatestAsync(conceptKey, userId, cancellationToken);
            return resolved is null ? Results.NotFound() : Results.Ok(resolved);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Invalid rule concept key",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    });

    app.MapPost("/api/global/rules/concepts", async (
        CreateRuleConceptRequest request,
        HttpContext httpContext,
        IGlobalRulesService rules,
        CancellationToken cancellationToken) =>
    {
        var authorizationFailure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        try
        {
            var result = await rules.CreateConceptAsync(
                request,
                authenticationContext!.User.Id,
                cancellationToken);
            return result.Created
                ? Results.Created($"/api/global/rules/concepts/{result.Value.Id}", result.Value)
                : Results.Ok(result.Value);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Invalid rule concept",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Rule concept conflict",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    });

    app.MapPost("/api/global/rules/concepts/{conceptId:guid}/bindings", async (
        Guid conceptId,
        BindRuleConceptSourceRequest request,
        HttpContext httpContext,
        IGlobalRulesService rules,
        CancellationToken cancellationToken) =>
    {
        var authorizationFailure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        try
        {
            var result = await rules.BindSourceEntityAsync(
                conceptId,
                request,
                authenticationContext!.User.Id,
                cancellationToken);
            return result.Created
                ? Results.Created(
                    $"/api/global/rules/concepts/{conceptId}/bindings/{result.Value.Id}",
                    result.Value)
                : Results.Ok(result.Value);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Source binding conflict",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    });

    app.MapPut("/api/global/rules/concepts/{conceptId:guid}/decision", async (
        Guid conceptId,
        SetGlobalRuleDecisionRequest request,
        HttpContext httpContext,
        IGlobalRulesService rules,
        CancellationToken cancellationToken) =>
    {
        var authorizationFailure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        try
        {
            var result = await rules.SetDecisionAsync(
                conceptId,
                request,
                authenticationContext!.User.Id,
                cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                title: "Invalid global rule decision",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Global rule decision conflict",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    });

    app.MapPost("/api/global/rules/publish", async (
        HttpContext httpContext,
        IGlobalRulesService rules,
        CancellationToken cancellationToken) =>
    {
        var authorizationFailure = RequireGlobalRulesAuthority(httpContext, out var authenticationContext);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        try
        {
            return Results.Ok(await rules.PublishAsync(
                authenticationContext!.User.Id,
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Ruleset publication conflict",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    });
}
else
{
    app.MapGet("/api/sources", () => DatabaseUnavailable("Source Layer"));
    app.MapGet("/api/sources/entities/{entityId:guid}", (Guid entityId) => DatabaseUnavailable("Source Layer"));
    app.MapGet("/api/rules/{conceptKey}", (string conceptKey) => DatabaseUnavailable("Rules Layer"));
    app.MapPost("/api/global/rules/concepts", () => DatabaseUnavailable("Rules Layer"));
    app.MapPost("/api/global/rules/concepts/{conceptId:guid}/bindings", (Guid conceptId) => DatabaseUnavailable("Rules Layer"));
    app.MapPut("/api/global/rules/concepts/{conceptId:guid}/decision", (Guid conceptId) => DatabaseUnavailable("Rules Layer"));
    app.MapPost("/api/global/rules/publish", () => DatabaseUnavailable("Rules Layer"));
}

app.Run();

static IResult? RequireGlobalRulesAuthority(
    HttpContext httpContext,
    out ToolHostAuthenticationContext? authenticationContext)
{
    authenticationContext = HostedToolAuthenticationMiddleware.GetAuthenticationContext(httpContext);
    if (authenticationContext is null)
    {
        return Results.Unauthorized();
    }

    return RulesAuthority.CanEditGlobalRules(authenticationContext)
        ? null
        : Results.StatusCode(StatusCodes.Status403Forbidden);
}

static IResult DatabaseUnavailable(string component) =>
    Results.Problem(
        title: $"{component} is unavailable",
        detail: "ConnectionStrings:RulesCore is not configured.",
        statusCode: StatusCodes.Status503ServiceUnavailable);

public partial class Program;
