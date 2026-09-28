using System.Net;
using System.Text;
using RulesCore.Application.Hosting;
using RulesCore.Infrastructure.Hosting;

namespace RulesCore.IntegrationTests;

public sealed class ToolHostAuthenticationClientTests
{
    [Fact]
    public async Task ValidHeadlessServiceIntrospectionContextIsAccepted()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "contractVersion": 1,
                  "toolKey": "rules-core",
                  "siteMode": "dorks-and-dice",
                  "user": { "id": "user-123", "displayName": "Rules Lawyer" },
                  "globalRoles": ["Rules Lawyer"],
                  "campaigns": [
                    {
                      "id": "11111111-1111-1111-1111-111111111111",
                      "name": "Test Campaign",
                      "role": "DM"
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://dorks-and-dice-site:8080")
        };
        var client = new DorksAndDiceToolHostAuthenticationClient(httpClient);

        var context = await client.RedeemAsync(
            "ticket-123",
            DorksAndDiceToolHostAuthenticationClient.ExpectedIntrospectionPath);

        Assert.NotNull(context);
        Assert.Equal("rules-core", context.ToolKey);
        Assert.Null(context.ToolSlug);
        Assert.Equal("user-123", context.User.Id);
        Assert.True(context.HasGlobalRole("Rules Lawyer"));
        Assert.True(context.HasCampaignRole(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "DM"));
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            "Bearer ticket-123",
            handler.LastRequest.Headers.Authorization?.ToString());
        Assert.Equal(
            DorksAndDiceToolHostAuthenticationClient.ExpectedIntrospectionPath,
            handler.LastRequest.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UnauthorizedTicketReturnsNoContext()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://dorks-and-dice-site:8080")
        };
        var client = new DorksAndDiceToolHostAuthenticationClient(httpClient);

        var context = await client.RedeemAsync(
            "invalid-ticket",
            DorksAndDiceToolHostAuthenticationClient.ExpectedIntrospectionPath);

        Assert.Null(context);
    }

    [Fact]
    public async Task UnexpectedIntrospectionPathIsRejectedBeforeRequest()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://dorks-and-dice-site:8080")
        };
        var client = new DorksAndDiceToolHostAuthenticationClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.RedeemAsync(
            "ticket-123",
            "/tool-host/registrations/other/api/introspect"));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ContextForAnotherRegistrationKeyIsRejected()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "contractVersion": 1,
                  "toolKey": "another-tool",
                  "siteMode": "dorks-and-dice",
                  "user": { "id": "user-123", "displayName": "User" },
                  "globalRoles": [],
                  "campaigns": []
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://dorks-and-dice-site:8080")
        };
        var client = new DorksAndDiceToolHostAuthenticationClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.RedeemAsync(
            "ticket-123",
            DorksAndDiceToolHostAuthenticationClient.ExpectedIntrospectionPath));
    }

    [Fact]
    public async Task ContextWithoutStableRegistrationKeyIsRejected()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "contractVersion": 1,
                  "toolSlug": "rules-core",
                  "siteMode": "dorks-and-dice",
                  "user": { "id": "user-123", "displayName": "User" },
                  "globalRoles": [],
                  "campaigns": []
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://dorks-and-dice-site:8080")
        };
        var client = new DorksAndDiceToolHostAuthenticationClient(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.RedeemAsync(
            "ticket-123",
            DorksAndDiceToolHostAuthenticationClient.ExpectedIntrospectionPath));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
