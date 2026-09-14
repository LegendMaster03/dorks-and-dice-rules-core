using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class PcGenCurrentUserSourceIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task SignedInUserCanUploadStandalonePcGenListThroughNormalAddSourceEndpoint()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var userId = $"pcgen-upload-{Guid.NewGuid():N}";
        var authenticationClient = new FakeToolHostAuthenticationClient(
            new ToolHostAuthenticationContext(
                ContractVersion: 1,
                ToolSlug: "rules-core",
                SiteMode: "dorks-and-dice",
                User: new ToolHostUserContext(userId, userId),
                GlobalRoles: [],
                Campaigns: []));

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton<IToolHostAuthenticationClient>(authenticationClient);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var pcGen = """
            SOURCELONG:Representative 3.5 Source\tSOURCESHORT:R35
            Arc Spark\tTYPE:Arcane\tSCHOOL:Evocation\tDESC:Representative source text.
            """;
        var payload = new AddCurrentUserSourceRequest(
            CurrentUserSourceKinds.Upload,
            FileName: "representative_spells.lst",
            Json: pcGen);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sources/current-user")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, "user-ticket");
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var source = await response.Content.ReadFromJsonAsync<CurrentUserSourceView>();
        Assert.NotNull(source);
        Assert.Equal(CurrentUserSourceKinds.Upload, source.Kind);
        Assert.Equal("representative_spells.lst", source.DisplayName);
        Assert.Equal(1, source.EntityCount);
        Assert.Contains("R35", source.SourceCodes, StringComparer.Ordinal);

        using var searchRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/sources/entities?entityType=spell&q=Arc%20Spark");
        searchRequest.Headers.Add(ToolHostAuthenticationHeaders.Ticket, "user-ticket");
        searchRequest.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        using var searchResponse = await client.SendAsync(searchRequest);
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var entities = await searchResponse.Content.ReadFromJsonAsync<List<SourceEntitySummary>>();
        Assert.NotNull(entities);
        var spell = Assert.Single(entities, value => value.Name == "Arc Spark");
        Assert.Equal("spell", spell.EntityType);
        Assert.Equal("R35", spell.SourceCode);
        Assert.Equal("pcgen-data", spell.EditionKey);
    }

    private sealed class FakeToolHostAuthenticationClient(ToolHostAuthenticationContext context)
        : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolHostAuthenticationContext?>(
                string.Equals(ticket, "user-ticket", StringComparison.Ordinal)
                && string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal)
                    ? context
                    : null);
    }
}
