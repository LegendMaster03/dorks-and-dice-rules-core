using Microsoft.Extensions.Hosting;
using RulesCore.Content;

namespace RulesCore.Host;

public static class RulesCoreApplication
{
    /// <summary>
    /// Constructs the headless host. The optional builder callback is used by
    /// integration tests; it must run before configuration is validated.
    /// </summary>
    public static WebApplication Create(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configureBuilder?.Invoke(builder);

        // An invalid configuration fails startup. An unavailable or invalid
        // catalog does not: liveness stays independent of content readiness.
        var settings = RulesCoreSettings.Read(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(15));
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<NativeCatalogStore>();
        builder.Services.AddSingleton<INativeCatalogReadiness>(services =>
            services.GetRequiredService<NativeCatalogStore>());
        builder.Services.AddHostedService<NativeCatalogInitializer>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "alive" }));

        app.MapGet("/ready", (INativeCatalogReadiness catalog) =>
        {
            var state = catalog.Current();
            return state.IsReady
                ? Results.Ok(new { status = "ready" })
                : Results.Json(
                    new { status = "not_ready", reason = state.Reason },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        // Phase 2 exposes no consumer or Rules Wiki endpoints.
        app.Lifetime.ApplicationStarted.Register(() =>
            app.Logger.LogInformation(
                "Rules Core started with surface {ApiSurface}, ruleset {RulesetId}",
                settings.Surface, settings.Ruleset.RulesetId));
        app.Lifetime.ApplicationStopping.Register(() =>
            app.Logger.LogInformation("Rules Core stopping"));

        return app;
    }
}
