using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class ServerTimingIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/registrations/rules-core/api/introspect";

    [Fact]
    public async Task ReferenceRequestReportsRulesCoreRequestAuthDatabaseAndReferenceMetrics()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/wiki/references?limit=1");
        request.Headers.TryAddWithoutValidation(ToolHostAuthenticationHeaders.Ticket, "timing-ticket");
        request.Headers.TryAddWithoutValidation(
            ToolHostAuthenticationHeaders.IntrospectionPath,
            IntrospectionPath);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metrics = ReadMetrics(response);

        AssertMetric(metrics, "rules-core");
        AssertMetric(metrics, "rules-core-auth");
        AssertMetric(metrics, "rules-core-db");
        AssertMetric(metrics, "rules-core-reference-query");
        AssertMetric(metrics, "rules-core-reference-materialize");
        AssertMetric(metrics, "rules-core-reference-total");

        Assert.DoesNotContain("rules-auth", metrics.Keys);
        Assert.DoesNotContain("rules-wiki-query", metrics.Keys);
        Assert.DoesNotContain("rules-wiki-docs", metrics.Keys);
        Assert.DoesNotContain("rules-wiki-total", metrics.Keys);
        Assert.DoesNotContain(metrics.Keys, name => name.StartsWith("platform-", StringComparison.Ordinal));
        Assert.DoesNotContain(metrics.Keys, name => name.StartsWith("dnd-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HealthRequestWithoutTicketReportsWholeRequestTimingWithoutAuthenticationTiming()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metrics = ReadMetrics(response);
        AssertMetric(metrics, "rules-core");
        Assert.DoesNotContain("rules-core-auth", metrics.Keys);
        Assert.DoesNotContain(metrics.Keys, name => name.StartsWith("platform-", StringComparison.Ordinal));
        Assert.DoesNotContain(metrics.Keys, name => name.StartsWith("dnd-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandledAuthenticationErrorStillReportsOneWholeRequestMetric()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/integration/session");
        request.Headers.TryAddWithoutValidation(ToolHostAuthenticationHeaders.Ticket, "incomplete-ticket");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var metrics = ReadMetricEntries(response);
        var requestMetrics = metrics
            .Where(metric => metric.Name.Equals("rules-core", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(requestMetrics);
        Assert.True(requestMetrics[0].DurationMilliseconds >= 0);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(new FakeAuthenticationClient());
            });
        });

    private static IReadOnlyDictionary<string, double> ReadMetrics(HttpResponseMessage response)
    {
        var entries = ReadMetricEntries(response);
        var metrics = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            Assert.True(
                metrics.TryAdd(entry.Name, entry.DurationMilliseconds),
                $"Duplicate Server-Timing metric '{entry.Name}'.");
        }

        return metrics;
    }

    private static IReadOnlyList<TimingMetric> ReadMetricEntries(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Server-Timing", out var values));

        var metrics = new List<TimingMetric>();
        foreach (var rawMetric in values.SelectMany(value =>
                     value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            var segments = rawMetric.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.NotEmpty(segments);

            var durationSegment = segments
                .Skip(1)
                .SingleOrDefault(segment => segment.StartsWith("dur=", StringComparison.Ordinal));
            Assert.NotNull(durationSegment);
            Assert.True(double.TryParse(
                durationSegment[4..],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var durationMilliseconds));
            Assert.True(durationMilliseconds >= 0);

            metrics.Add(new TimingMetric(segments[0], durationMilliseconds));
        }

        return metrics;
    }

    private static void AssertMetric(
        IReadOnlyDictionary<string, double> metrics,
        string name)
    {
        Assert.True(metrics.TryGetValue(name, out var duration), $"Missing Server-Timing metric '{name}'.");
        Assert.True(duration >= 0);
    }

    private sealed class FakeAuthenticationClient : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            if (!ticket.Equals("timing-ticket", StringComparison.Ordinal))
            {
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            }

            return Task.FromResult<ToolHostAuthenticationContext?>(new ToolHostAuthenticationContext(
                ContractVersion: 1,
                ToolSlug: "rules-core",
                SiteMode: "dorks-and-dice",
                User: new ToolHostUserContext("timing-user", "Timing User"),
                GlobalRoles: ["Rules Lawyer"],
                Campaigns: [])
            {
                ToolKey = "rules-core",
                ScopedRoles = ["Rules Lawyer"],
                DelegatedFromToolKey = "rules-wiki",
                DelegatedFromToolSlug = "rules-wiki"
            });
        }
    }

    private sealed record TimingMetric(string Name, double DurationMilliseconds);
}
