using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Reload the character's selected/earned Life Experiences and exact current
/// revision. Verify the proposed selections, source occurrences, contextual
/// eligibility and ordinary inventory definitions independently. A Site
/// delegation ticket or a browser-originated selection is not that proof.
/// </summary>
public interface INativeLifeExperienceHostAuthorityProvider
{
    Task<NativeLifeExperienceAuthorization?> VerifyAsync(Guid actorId,
        NativeLifeExperienceEndpoints.VerifiedRequest request, CancellationToken cancellationToken);
}

public static class NativeLifeExperienceEndpoints
{
    public sealed record PreviewRequest(string RulesetId, string Fingerprint,
        NativeLifeExperienceProposal Proposal);
    public sealed record VerifiedRequest(Guid CharacterId, long OriginRevision,
        string RulesetId, string Fingerprint, NativeLifeExperienceProposal Proposal);

    public static void MapNativeLifeExperiencePreview(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/life-experiences/preview", (PreviewRequest request,
            NativeCatalogStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || !NativeLifeExperienceResolver.ValidProposal(request.Proposal))
                return Error(400, "invalid_life_experience_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");
            var result = new NativeLifeExperienceResolver(catalog).Resolve(request.Proposal);
            return Results.Ok(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                previewOnly = true, committed = false, result
            });
        });
    }

    public static void MapVerifiedLifeExperiencePlan(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/life-experiences/verified-plan", async (VerifiedRequest request,
            NativeCatalogStore store, IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty || request.OriginRevision < 1 ||
                !NativeLifeExperienceResolver.ValidProposal(request.Proposal))
                return Error(400, "invalid_life_experience_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");
            var provider = services.GetService<INativeLifeExperienceHostAuthorityProvider>();
            if (provider is null) return Error(422, "life_experience_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null) return Error(422, "life_experience_authority_unverified");
            var result = new NativeLifeExperienceResolver(catalog).Resolve(request.Proposal, verified);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                previewOnly = true, committed = false, result
            }, statusCode: result.EarnedProjection is not null ? 200 : 422);
        });
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
