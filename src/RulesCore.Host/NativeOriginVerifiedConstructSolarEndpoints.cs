using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Reload the authoritative installed Construct, saved preferences, all solar
/// counters and an unconsumed game-time event with independently confirmed
/// exposure and duration. Never accept balances or elapsed time from the
/// request body. The provider must authenticate actor access and source state.
/// </summary>
public interface INativeConstructHostSolarIntervalAuthorityProvider
{
    Task<VerifiedConstructSolarIntervalAuthorization?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedConstructSolarIntervalRequest request,
        CancellationToken cancellationToken);
}

public sealed record VerifiedConstructSolarIntervalAuthorization(
    Guid CharacterId, long OriginRevision, long OperatingRevision, long SolarRevision,
    string RulesetId, string Fingerprint, string IntervalEventId,
    NativeConstructOriginConstructionResult Construction,
    NativeConstructOperatingPreset CurrentPreset, NativeConstructSolarState State,
    IReadOnlyDictionary<string, NativeConstructSolarExposure> Exposures, int ElapsedMinutes);

public static partial class NativeOriginApiEndpoints
{
    public sealed record VerifiedConstructSolarIntervalRequest(
        Guid CharacterId, long OriginRevision, long OperatingRevision, long SolarRevision,
        string RulesetId, string Fingerprint, string IntervalEventId);

    public static void MapVerifiedConstructSolarIntervalPlan(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/construct/solar/interval/plan", async (
            VerifiedConstructSolarIntervalRequest request, NativeCatalogStore store,
            IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty ||
                request.OriginRevision < 1 || request.OperatingRevision < 0 ||
                request.SolarRevision < 0 || request.SolarRevision == long.MaxValue ||
                string.IsNullOrWhiteSpace(request.IntervalEventId) ||
                request.IntervalEventId.Length > 200)
                return Error(400, "invalid_construct_solar_interval_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var provider = services.GetService<INativeConstructHostSolarIntervalAuthorityProvider>();
            if (provider is null) return Error(422, "construct_solar_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision ||
                verified.OperatingRevision != request.OperatingRevision ||
                verified.SolarRevision != request.SolarRevision ||
                verified.IntervalEventId != request.IntervalEventId ||
                verified.RulesetId != catalog.RulesetId || verified.Fingerprint != catalog.Fingerprint ||
                verified.Construction?.IsMechanicallyAuthorized != true ||
                verified.Construction.OperatingSession is not { } session ||
                verified.CurrentPreset is null || verified.State is null ||
                verified.Exposures is null || verified.ElapsedMinutes <= 0)
                return Error(422, "construct_solar_authority_unverified");

            var plan = NativeConstructSolarIntervalPlanner.Plan(session, verified.CurrentPreset,
                verified.State, verified.Exposures, verified.ElapsedMinutes);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                operatingRevision = request.OperatingRevision, solarRevision = request.SolarRevision,
                intervalEventId = request.IntervalEventId, previewOnly = true, committed = false,
                plan
            }, statusCode: plan.CanAdvance ? 200 : 422);
        });
    }
}
