using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Http;
using RulesCore.Host;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class CharacterSheetMechanicsTicketValidatorTests
{
    private const string Ticket = "X-Dorks-Tool-Auth-Ticket";
    private const string Path = "X-Dorks-Tool-Auth-Introspection-Path";
    private const string Introspection = "/tool-host/registrations/rules-core/api/introspect";

    [Theory]
    [InlineData("rules-wiki", true)]
    [InlineData("character-sheet", false)]
    [InlineData("block-initiative", true)]
    public async Task OnlyCharacterSheetSiteTicketsAreAllowed(string source, bool denied)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[Ticket] = "opaque-token";
        context.Request.Headers[Path] = Introspection;
        var client = new HttpClient(new TestHandler(source)) {
            BaseAddress = new Uri("https://site.invalid") };
        var validator = new CharacterSheetMechanicsTicketValidator(client);
        var result = await validator.ValidateAsync(context);
        Assert.Equal(denied, result is not null);
    }

    [Theory]
    [InlineData("character-sheet", false)]
    [InlineData("rules-wiki", true)]
    [InlineData("block-initiative", true)]
    public async Task OnlyCharacterSheetOrdinaryDelegationCanUsePublicMechanics(string source, bool denied)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[Ticket] = "ordinary-target-ticket";
        context.Request.Headers[Path] = Introspection;
        using var client = new HttpClient(new TestHandler(source, ordinary: true)) {
            BaseAddress = new Uri("https://site.invalid") };
        var result = await new CharacterSheetMechanicsTicketValidator(client)
            .ValidateAsync(context, requirePrivateTunnel: false);
        Assert.Equal(denied, result is not null);
    }

    [Fact]
    public async Task OrdinaryDelegationDoesNotAuthorizePrivateMechanics()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[Ticket] = "ordinary-target-ticket";
        context.Request.Headers[Path] = Introspection;
        using var client = new HttpClient(new TestHandler("character-sheet", ordinary: true)) {
            BaseAddress = new Uri("https://site.invalid") };
        Assert.NotNull(await new CharacterSheetMechanicsTicketValidator(client)
            .ValidateAsync(context, requirePrivateTunnel: true));
    }

    [Fact]
    public async Task PrivateTunnelDoesNotAuthorizeOrdinaryMechanics()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[Ticket] = "private-target-ticket";
        context.Request.Headers[Path] = Introspection;
        using var client = new HttpClient(new TestHandler("character-sheet")) {
            BaseAddress = new Uri("https://site.invalid") };
        Assert.NotNull(await new CharacterSheetMechanicsTicketValidator(client)
            .ValidateAsync(context, requirePrivateTunnel: false));
    }

    [Fact]
    public async Task MissingTicketIsRejectedWithoutRedeemingAnything()
    {
        var validator = new CharacterSheetMechanicsTicketValidator(new HttpClient(new TestHandler("character-sheet")));
        Assert.NotNull(await validator.ValidateAsync(new DefaultHttpContext()));
    }

    private sealed class TestHandler(string source, bool ordinary = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(Introspection, request.RequestUri?.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new {
                    contractVersion = 1, toolKey = "rules-core", siteMode = "dorks-and-dice",
                    user = new { id = "test-user" },
                    privateTunnelSourceToolKey = ordinary ? null : source,
                    delegatedFromToolKey = ordinary ? source : null
                }), Encoding.UTF8, "application/json") });
        }
    }
}
