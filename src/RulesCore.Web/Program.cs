using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("RulesCore");
var hasDatabase = !string.IsNullOrWhiteSpace(connectionString);
if (hasDatabase)
{
    builder.Services.AddDbContext<RulesCoreDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IRulesCoreSchemaInitializer, RulesCoreSchemaInitializer>();
    builder.Services.AddScoped<ISourceImportService, SourceImportService>();
    builder.Services.AddScoped<ISourceCatalogService, SourceCatalogService>();
}

builder.Services.AddHealthChecks();

var app = builder.Build();

if (hasDatabase)
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<IRulesCoreSchemaInitializer>();
    await initializer.InitializeAsync();
}

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

    return Results.Ok(new { status = "ready", database = "postgresql", sourceLayer = "ready" });
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
    version = "0.2-dev",
    endpointFamilies = new[]
    {
        "/api/rules",
        "/api/sources",
        "/api/global/rules",
        "/api/campaigns/{campaignId}/rules",
        "/api/integration"
    }
}));

if (hasDatabase)
{
    app.MapGet("/api/sources", async (
        ISourceCatalogService catalog,
        CancellationToken cancellationToken) =>
        Results.Ok(await catalog.GetPublicPackagesAsync(cancellationToken)));

    app.MapGet("/api/sources/entities/{entityId:guid}", async (
        Guid entityId,
        ISourceCatalogService catalog,
        CancellationToken cancellationToken) =>
    {
        var entity = await catalog.GetLatestPublicEntityAsync(entityId, cancellationToken);
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
