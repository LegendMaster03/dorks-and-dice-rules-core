using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RulesCore.Host;

/// <summary>
/// Separate Site Tool-to-Tool authorization for Character Sheet mechanics.
/// A Wiki ticket must never authorize a mechanics request. Fail closed.
/// </summary>
public sealed class CharacterSheetMechanicsTicketValidator(HttpClient siteClient)
{
    private const string TicketHeader = "X-Dorks-Tool-Auth-Ticket";
    private const string PathHeader = "X-Dorks-Tool-Auth-Introspection-Path";
    private const string IntrospectionPath = "/tool-host/registrations/rules-core/api/introspect";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SiteUser(string? Id);
    private sealed record SiteContext(
        int ContractVersion,
        string? ToolKey,
        string? SiteMode,
        SiteUser? User,
        string? PrivateTunnelSourceToolKey,
        string? DelegatedFromToolKey);

    public async Task<IResult?> ValidateAsync(HttpContext context, bool requirePrivateTunnel = true)
    {
        context.Response.Headers.CacheControl = "no-store";
        var tickets = context.Request.Headers[TicketHeader];
        var paths = context.Request.Headers[PathHeader];
        if (tickets.Count != 1 || paths.Count != 1 || string.IsNullOrWhiteSpace(tickets[0]) ||
            !string.Equals(paths[0], IntrospectionPath, StringComparison.Ordinal))
            return Fail(401, "invalid_ticket");
        if (siteClient.BaseAddress is null) return Fail(503, "authentication_unavailable");
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, IntrospectionPath);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tickets[0]!.Trim());
            using var response = await siteClient.SendAsync(message,
                HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Fail(401, "invalid_ticket");
            if (!response.IsSuccessStatusCode) return Fail(502, "authentication_unavailable");
            var session = await response.Content.ReadFromJsonAsync<SiteContext>(
                JsonOptions, context.RequestAborted);
            if (session?.ContractVersion != 1 || session.ToolKey != "rules-core" ||
                string.IsNullOrWhiteSpace(session.User?.Id))
                return Fail(401, "invalid_ticket");
            // Ordinary Character Sheet delegation is the established Site contract
            // for general Tool APIs. It is NOT equivalent to Wiki private-tunnel access.
            // Each ingress requires its own Site-issued source provenance.
            if (session.SiteMode != "dorks-and-dice")
                return Fail(403, "mechanics_access_denied");
            if (requirePrivateTunnel)
            {
                if (session.PrivateTunnelSourceToolKey != "character-sheet" ||
                    session.DelegatedFromToolKey is not null)
                    return Fail(403, "mechanics_access_denied");
            }
            else if (session.DelegatedFromToolKey != "character-sheet" ||
                     session.PrivateTunnelSourceToolKey is not null)
                return Fail(403, "mechanics_access_denied");
            return null;
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            return Fail(504, "authentication_timeout");
        }
        catch (HttpRequestException) { return Fail(502, "authentication_unavailable"); }
        catch (JsonException) { return Fail(502, "invalid_authentication_response"); }
        catch (NotSupportedException) { return Fail(502, "invalid_authentication_response"); }
    }

    private static IResult Fail(int code, string error) =>
        Results.Json(new { error }, statusCode: code);
}
