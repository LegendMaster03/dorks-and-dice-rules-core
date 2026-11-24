using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// An independent host adapter must reload the exact committed Origin revision,
/// profile, event receipts, and applicable native/DM grants from authoritative
/// sources. A Site ticket authenticates the actor but never certifies the
/// client-supplied Origin profile.
/// </summary>
public interface INativeOriginHostUnderlyingAuthorityProvider
{
    Task<bool> VerifyAsync(
        Guid actorId, Guid characterId, long originRevision,
        NativeOriginProfile profile, CancellationToken cancellationToken);
}

/// <summary>
/// Read-only current-catalog review of the latest independently verified
/// Origin baseline. Underlying remains distinct from Current; this endpoint
/// never reconstructs a new Origin, activates a form, or issues a grant.
/// </summary>
public static class NativeOriginVerifiedUnderlyingEndpoints
{
    public sealed record VerifiedUnderlyingRequest(
        Guid CharacterId, long OriginRevision, string RulesetId,
        string Fingerprint, NativeOriginProfile Profile);

    public static void MapNativeOriginVerifiedUnderlying(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/underlying/verify", async (
            VerifiedUnderlyingRequest request,
            NativeCatalogStore store, IServiceProvider services,
            HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null)
                return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty ||
                request.OriginRevision < 1 ||
                request.Profile is null ||
                string.IsNullOrWhiteSpace(request.Profile.Id) ||
                request.Profile.Underlying is null ||
                request.Profile.Current is null ||
                request.Profile.IndependentGrants is null ||
                request.Profile.History is null ||
                request.Profile.Adjudication is null)
                return Error(400, "invalid_authorized_origin_request");

            if (request.RulesetId != catalog.RulesetId ||
                request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var verifier =
                services.GetService<INativeOriginHostUnderlyingAuthorityProvider>();
            if (verifier is null)
                return Error(422, "origin_authority_unavailable");
            if (!await verifier.VerifyAsync(actor.UserId, request.CharacterId,
                request.OriginRevision, request.Profile, context.RequestAborted))
                return Error(422, "origin_authority_unverified");

            NativeOriginContractReview review;
            try
            {
                review = new NativeOriginContractValidator(catalog)
                    .ReviewProfile(request.Profile);
            }
            catch (Exception exception) when (exception is ArgumentException or
                InvalidOperationException or OverflowException)
            {
                return Error(422, "origin_baseline_unresolved");
            }
            if (review.Status != NativeOriginReviewStatus.StructurallyConsistent)
                return Results.Json(new
                {
                    error = "origin_baseline_unresolved",
                    issues = review.Issues
                }, statusCode: StatusCodes.Status422UnprocessableEntity);

            // Structurally consistent is not a playable mechanics projection.
            // No inherited Origin grant or active-form applicability is issued.
            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId,
                originRevision = request.OriginRevision,
                supportedScope = "verified-origin-underlying",
                status = "partial",
                effectiveMechanicsReady = false,
                underlying = request.Profile.Underlying,
                current = request.Profile.Current,
                issues = review.Issues
            });
        });
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
