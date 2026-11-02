using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Host;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class PublicMechanicsSiteAuthenticationTests
{
    // Use the production HttpClient registration and a real HTTP callback. A
    // fake validator or preconfigured HttpClient would conceal the deployment bug.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicMechanicsNeedSiteUrlEvenWhenCatalogIsReady(bool configureSite)
    {
        var root = Path.Combine(Path.GetTempPath(), "core-site-auth-" + Guid.NewGuid().ToString("N"));
        var package = Path.Combine(root, "first-party-001");
        Directory.CreateDirectory(Path.Combine(package, "native"));
        await File.WriteAllTextAsync(Path.Combine(package, "manifest.json"),
            """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
        await File.WriteAllTextAsync(Path.Combine(package, "native", "class.json"),
            """{"id":"class.demo","kind":"class","name":"Demo","definition":{}}""");
        try
        {
            var callbacks = 0;
            var siteBuilder = WebApplication.CreateBuilder();
            siteBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var site = siteBuilder.Build();
            site.MapPost("/tool-host/registrations/rules-core/api/introspect", (HttpContext context) =>
            {
                Assert.Equal("Bearer test-target-ticket", context.Request.Headers.Authorization.ToString());
                Interlocked.Increment(ref callbacks);
                return Results.Json(new {
                    contractVersion = 1, toolKey = "rules-core", siteMode = "dorks-and-dice",
                    user = new { id = "test-user" }, delegatedFromToolKey = "character-sheet",
                    privateTunnelSourceToolKey = (string?)null
                });
            });
            await site.StartAsync();
            await using var core = RulesCoreApplication.Create([], builder => {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["RulesCore:ApiSurface"] = "PublicOnly",
                    ["Rulesets:ContentRoot"] = root,
                    ["ToolHost:BaseUrl"] = configureSite ? Address(site) : null
                });
            });
            await core.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(Address(core)) };
            using var ready = await client.GetAsync("/ready");
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            using var anonymous = await client.GetAsync("/api/mechanics/v1/ruleset");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/mechanics/v1/ruleset");
            request.Headers.Add("X-Dorks-Tool-Auth-Ticket", "test-target-ticket");
            request.Headers.Add("X-Dorks-Tool-Auth-Introspection-Path",
                "/tool-host/registrations/rules-core/api/introspect");
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(configureSite ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable,
                response.StatusCode);
            if (configureSite)
                Assert.Equal("first-party-001", body.GetProperty("rulesetId").GetString());
            else
                Assert.Equal("authentication_unavailable", body.GetProperty("error").GetString());
            Assert.Equal(configureSite ? 1 : 0, callbacks);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
}
