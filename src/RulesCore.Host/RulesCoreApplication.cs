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

        // Fail startup for invalid configuration. An absent catalog is not an
        // invalid configuration: this release intentionally starts unready.
        var settings = RulesCoreSettings.Read(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(15));
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<INativeCatalogReadiness, UnavailableNativeCatalogReadiness>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "alive" }));

        app.MapGet("/ready", (INativeCatalogReadiness catalog) =>
        {
            var snapshot = catalog.Current();
            return snapshot.IsReady
                ? Results.Ok(new { status = "ready" })
                : Results.Json(
                    new { status = "not_ready", reason = snapshot.Reason },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        // No consumer or private APIs exist at Phase 1B. In particular,
        // this host does not map /api/rules or any privileged Wiki routes.
        app.Lifetime.ApplicationStarted.Register(() =>
            app.Logger.LogInformation(
                "Rules Core started with surface {ApiSurface}, ruleset {RulesetId}, native path {NativeDirectory}",
                settings.Surface,
                settings.Ruleset.RulesetId,
                settings.Ruleset.NativeDirectory));
        app.Lifetime.ApplicationStopping.Register(() =>
            app.Logger.LogInformation("Rules Core stopping"));

        return app;
    }
}
