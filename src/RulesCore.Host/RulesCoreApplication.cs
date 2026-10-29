using Microsoft.Extensions.Hosting;
using RulesCore.Content;

namespace RulesCore.Host;

public static class RulesCoreApplication
{
    public static WebApplication Create(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configureBuilder?.Invoke(builder);
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

        if (settings.Surface == ApiSurfaceMode.PrivateOnly)
        {
            var baseUrl = builder.Configuration["ToolHost:BaseUrl"];
            // No credential bypass: missing Site address yields HTTP 503 for every private request.
            if (!string.IsNullOrEmpty(baseUrl) &&
                (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ||
                 (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)))
                throw new ArgumentException("ToolHost:BaseUrl must be an HTTP(S) URL.");
            builder.Services.AddHttpClient<PrivateReferenceTicketValidator>(client =>
            {
                if (!string.IsNullOrWhiteSpace(baseUrl)) client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(3);
            }).ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        }

        // Integration tests may replace HTTP transport after host service registration.
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "alive" }));
        app.MapGet("/ready", (INativeCatalogReadiness catalog) =>
        {
            var state = catalog.Current();
            return state.IsReady
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "not_ready", reason = state.Reason }, statusCode: 503);
        });

        if (settings.Surface == ApiSurfaceMode.PrivateOnly)
            app.MapNativePrivateReferences();
        // Unrecognized API paths never resolve to the UI or a legacy endpoint.
        app.Map("/api/{**unmatched}", () => Results.NotFound(new { error = "api_not_found" }));

        app.Lifetime.ApplicationStarted.Register(() =>
            app.Logger.LogInformation(
                "Rules Core started with surface {ApiSurface}, ruleset {RulesetId}",
                settings.Surface, settings.Ruleset.RulesetId));
        app.Lifetime.ApplicationStopping.Register(() =>
            app.Logger.LogInformation("Rules Core stopping"));
        return app;
    }
}
