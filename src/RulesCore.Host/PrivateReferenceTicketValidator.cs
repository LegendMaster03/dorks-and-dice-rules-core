using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RulesCore.Host;

/// <summary>
/// Redeems one-time, target-scoped Site tickets. The ticket and header path are
/// never accepted as identity; only the Site's returned context is authoritative.
/// </summary>
public sealed class PrivateReferenceTicketValidator(HttpClient siteClient)
{
    public const string TicketHeader = "X-Dorks-Tool-Auth-Ticket";
    public const string PathHeader = "X-Dorks-Tool-Auth-Introspection-Path";
    public const string IntrospectionPath = "/tool-host/registrations/rules-core/api/introspect";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class SiteContext
    {
        public int ContractVersion { get; init; }
        public string? ToolKey { get; init; }
        public string? SiteMode { get; init; }
        public SiteUser? User { get; init; }
        public string? PrivateTunnelSourceToolKey { get; init; }
        public string? DelegatedFromToolKey { get; init; }
    }

    private sealed class SiteUser
    {
        public string? Id { get; init; }
    }

    public async Task<IResult?> ValidateAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        var tickets = context.Request.Headers[TicketHeader];
        var paths = context.Request.Headers[PathHeader];
        if (tickets.Count != 1 || paths.Count != 1 || string.IsNullOrWhiteSpace(tickets[0]) ||
            !string.Equals(paths[0], IntrospectionPath, StringComparison.Ordinal))
            return Error(StatusCodes.Status401Unauthorized, "invalid_ticket");

        // Read-only consumer traffic has no need for credentials other than the ticket.
        if (siteClient.BaseAddress is null)
            return Error(StatusCodes.Status503ServiceUnavailable, "authentication_unavailable");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, IntrospectionPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tickets[0]!.Trim());
            using var response = await siteClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Error(StatusCodes.Status401Unauthorized, "invalid_ticket");
            if (!response.IsSuccessStatusCode)
                return Error(StatusCodes.Status502BadGateway, "authentication_unavailable");

            var siteContext = await response.Content.ReadFromJsonAsync<SiteContext>(
                JsonOptions, context.RequestAborted);
            if (siteContext is null || siteContext.ContractVersion != 1 ||
                !string.Equals(siteContext.ToolKey, "rules-core", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(siteContext.User?.Id) ||
                string.IsNullOrWhiteSpace(siteContext.SiteMode))
                return Error(StatusCodes.Status401Unauthorized, "invalid_ticket");

            // This is a *source-specific* authorization decision, not merely
            // proof of being on a Docker network or holding a delegated ticket.
            if (!string.Equals(siteContext.PrivateTunnelSourceToolKey, "rules-wiki", StringComparison.Ordinal) ||
                !string.Equals(siteContext.SiteMode, "dorks-and-dice", StringComparison.Ordinal))
                return Error(StatusCodes.Status403Forbidden, "reference_read_denied");

            return null;
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            return Error(StatusCodes.Status504GatewayTimeout, "authentication_timeout");
        }
        catch (HttpRequestException)
        {
            return Error(StatusCodes.Status502BadGateway, "authentication_unavailable");
        }
        catch (JsonException)
        {
            return Error(StatusCodes.Status502BadGateway, "invalid_authentication_response");
        }
        catch (NotSupportedException)
        {
            return Error(StatusCodes.Status502BadGateway, "invalid_authentication_response");
        }
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
