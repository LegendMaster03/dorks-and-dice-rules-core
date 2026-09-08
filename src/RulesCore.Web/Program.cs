using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("RulesCore");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<RulesCoreDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddHealthChecks();

var app = builder.Build();

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

    return Results.Ok(new { status = "ready", database = "postgresql" });
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
    version = "0.1-dev",
    endpointFamilies = new[]
    {
        "/api/rules",
        "/api/sources",
        "/api/global/rules",
        "/api/campaigns/{campaignId}/rules",
        "/api/integration"
    }
}));

app.Run();

public partial class Program;
