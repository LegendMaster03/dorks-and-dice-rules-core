using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Hosting;
using RulesCore.Infrastructure.Persistence;
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
        sourceAccess = "ready"
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
    version = "0.3-dev",
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
}
else
{
    app.MapGet("/api/sources", () => Results.Problem(
        title: "Source Layer is unavailable",
        detail: "ConnectionStrings:RulesCore is not configured.",
        statusCode: StatusCodes.Status503ServiceUnavailable));

    app.MapGet("/api/sources/entities/{entityId:guid}", (Guid entityId) => Results.Problem(
        title: "Source Layer is unavailable",
        detail: "ConnectionStrings:RulesCore is not configured.",
        statusCode: StatusCodes.Status503ServiceUnavailable));
}

app.Run();

public partial class Program;
