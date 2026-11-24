using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Independently authenticate one unprocessed day or completed long rest,
/// current effective character and installed source state, and exact distinct
/// fuel inventory reservations. A zero reservation records missed sustenance.
/// Recheck the inventory, event, Origin and sustenance revisions when applying
/// the returned plan; the Rules Core endpoint does not consume any inventory.
/// </summary>
public interface INativeConstructHostSustenanceAuthorityProvider
{
    Task<VerifiedConstructSustenanceAuthorization?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedConstructSustenanceRequest request,
        CancellationToken cancellationToken);
}

public sealed record VerifiedConstructSustenanceAuthorization(
    Guid CharacterId, long OriginRevision, long SustenanceRevision, long InventoryRevision,
    string RulesetId, string Fingerprint, string EventId, string EventKind,
    NativeConstructOriginConstructionResult Construction,
    IReadOnlyDictionary<string, int> ReservedFuelUnits,
    int? ConstitutionModifier = null, int? PreviousDaysWithoutFullRation = null);

public static partial class NativeOriginApiEndpoints
{
    public sealed record VerifiedConstructSustenanceRequest(
        Guid CharacterId, long OriginRevision, long SustenanceRevision, long InventoryRevision,
        string RulesetId, string Fingerprint, string EventId, string EventKind);

    public static void MapVerifiedConstructSustenancePlan(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/construct/sustenance/plan", async (
            VerifiedConstructSustenanceRequest request, NativeCatalogStore store,
            IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty ||
                request.OriginRevision < 1 || request.SustenanceRevision < 0 ||
                request.SustenanceRevision == long.MaxValue || request.InventoryRevision < 0 ||
                request.InventoryRevision == long.MaxValue ||
                string.IsNullOrWhiteSpace(request.EventId) || request.EventId.Length > 200 ||
                request.EventKind is not ("mechanical-day" or "infernal-long-rest"))
                return Error(400, "invalid_construct_sustenance_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var provider = services.GetService<INativeConstructHostSustenanceAuthorityProvider>();
            if (provider is null) return Error(422, "construct_sustenance_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision ||
                verified.SustenanceRevision != request.SustenanceRevision ||
                verified.InventoryRevision != request.InventoryRevision ||
                verified.EventId != request.EventId || verified.EventKind != request.EventKind ||
                verified.RulesetId != catalog.RulesetId || verified.Fingerprint != catalog.Fingerprint ||
                verified.Construction?.IsMechanicallyAuthorized != true ||
                verified.Construction.OperatingSession is not { } session ||
                verified.ReservedFuelUnits is null)
                return Error(422, "construct_sustenance_authority_unverified");

            var plan = session.PlanSustenance(verified.EventKind, verified.ReservedFuelUnits,
                verified.ConstitutionModifier, verified.PreviousDaysWithoutFullRation);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                sustenanceRevision = request.SustenanceRevision, inventoryRevision = request.InventoryRevision,
                eventId = request.EventId, eventKind = request.EventKind,
                previewOnly = true, committed = false, plan
            }, statusCode: plan.CanApply ? 200 : 422);
        });
    }
}
