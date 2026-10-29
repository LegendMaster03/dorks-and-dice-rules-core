using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RulesCore.Content;
using RulesCore.Host;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class CatalogHostReadinessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadinessFollowsCatalogValidationWithoutExposingRoutes(bool invalid)
    {
        var root = Path.Combine(Path.GetTempPath(), "rules-host-" + Guid.NewGuid().ToString("N"));
        var package = Path.Combine(root, "first-party-001");
        var native = Path.Combine(package, "native");
        Directory.CreateDirectory(native);
        try
        {
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","workingRoot":"working/","runtimeContentRoot":"native/"}""");
            File.WriteAllText(Path.Combine(native, "rule.json"),
                invalid ? "{ invalid" : """{"id":"rule.valid","name":"Valid","kind":"rule"}""");

            await using var app = RulesCoreApplication.Create([], builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rulesets:ContentRoot"] = root,
                    ["Rulesets:DefaultRuleset"] = "first-party-001",
                    ["ConnectionStrings:RulesCore"] = "Host=invalid.invalid;Port=1"
                });
            });
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            var ready = await client.GetAsync("/ready");
            Assert.Equal(invalid ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
                ready.StatusCode);
            if (invalid)
            {
                var text = await ready.Content.ReadAsStringAsync();
                Assert.Contains("invalid_native_json", text);
                Assert.DoesNotContain(root, text);
            }
            var store = app.Services.GetRequiredService<NativeCatalogStore>();
            if (!invalid)
            {
                Assert.Equal(1, store.Snapshot!.Count);
                store.MarkUnavailable("native_content_invalid");
                Assert.Equal(HttpStatusCode.ServiceUnavailable,
                    (await client.GetAsync("/ready")).StatusCode);
                store.Publish(NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001")));
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ready")).StatusCode);
            }
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync("/api/rules")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync("/api/wiki/references")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync("/health")).StatusCode);
            await app.StopAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
