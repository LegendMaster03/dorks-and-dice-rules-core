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
    Task<VerifiedLifeExperienceHostAuthorization?> VerifyAsync(Guid actorId,
        NativeLifeExperienceEndpoints.VerifiedRequest request, CancellationToken cancellationToken);
}

public sealed record VerifiedLifeExperienceHostAuthorization(Guid CharacterId,
    long OriginRevision, NativeLifeExperienceAuthorization Authorization);

/// <summary>
/// Verify the character's current source occurrences and actual check event.
/// The returned baseline must be the already resolved ordinary check total
/// and its proficiency contribution, not arbitrary client-supplied numbers.
/// </summary>
public interface INativeScopedSkillCheckHostAuthorityProvider
{
    Task<VerifiedScopedSkillCheckHostAuthorization?> VerifyAsync(Guid actorId,
        NativeLifeExperienceEndpoints.ScopedCheckRequest request, CancellationToken cancellationToken);
}
public sealed record VerifiedScopedSkillCheckHostAuthorization(Guid CharacterId,
    long OriginRevision, string CheckEventId, NativeScopedSkillCheckInput Input);

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
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision || verified.Authorization is null)
                return Error(422, "life_experience_authority_unverified");
            var result = new NativeLifeExperienceResolver(catalog).Resolve(request.Proposal, verified.Authorization);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                previewOnly = true, committed = false, result
            }, statusCode: result.EarnedProjection is not null ? 200 : 422);
        });
    }

    public sealed record ScopedCheckRequest(Guid CharacterId, long OriginRevision,
        string RulesetId, string Fingerprint, string CheckEventId);

    public static void MapVerifiedScopedSkillCheck(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/life-experiences/scoped-check/resolve", async (
            ScopedCheckRequest request, NativeCatalogStore store, IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty || request.OriginRevision < 1 ||
                string.IsNullOrWhiteSpace(request.CheckEventId) || request.CheckEventId.Length > 200)
                return Error(400, "invalid_scoped_check_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");
            var provider = services.GetService<INativeScopedSkillCheckHostAuthorityProvider>();
            if (provider is null) return Error(422, "scoped_check_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified?.Input is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision || verified.CheckEventId != request.CheckEventId)
                return Error(422, "scoped_check_authority_unverified");
            var result = new NativeScopedSkillCheckResolver(catalog).Resolve(verified.Input);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                checkEventId = request.CheckEventId, committed = false, result
            }, statusCode: result.Status == "resolved" ? 200 : 422);
        });
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
