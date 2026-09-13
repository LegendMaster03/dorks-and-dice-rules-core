using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Sources;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class CurrentUserSourceCompatibilityIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Theory]
    [InlineData("notes.txt", "plain text")]
    [InlineData("notes.json", "{ \"data\": [{ \"name\": \"Introduction\" }] }")]
    public async Task IncompatibleUploadReturnsBadRequest(string fileName, string content)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"))) return;

        var authenticationClient = new FakeToolHostAuthenticationClient(
            new ToolHostAuthenticationContext(
                ContractVersion: 1,
                ToolSlug: "rules-core",
                SiteMode: "dorks-and-dice",
                User: new ToolHostUserContext("ordinary-user", "ordinary-user"),
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
        var payload = new AddCurrentUserSourceRequest(
            CurrentUserSourceKinds.Upload,
            FileName: fileName,
            Json: content);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sources/current-user")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, "user-ticket");
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid source", problem.Title);
        Assert.Contains("not compatible with Rules Core", problem.Detail, StringComparison.Ordinal);
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
