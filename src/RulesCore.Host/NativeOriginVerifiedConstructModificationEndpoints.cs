using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Independently authenticate the completed installation event and exact target
/// assembly/proposal, acquisition and recipe evidence, competency, actual new
/// item identities and inventory reservations. Reload the current authorized
/// installation and profile. No client-supplied body or recipe quote is proof
/// of a completed modification; this interface does not persist the event.
/// </summary>
public interface INativeConstructHostModificationAuthorityProvider
{
    Task<VerifiedConstructModificationAuthorization?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedConstructModificationRequest request,
        CancellationToken cancellationToken);
}

public sealed record VerifiedConstructModificationAuthorization(
    Guid CharacterId, long OriginRevision, long OperatingRevision, long InventoryRevision,
    string RulesetId, string Fingerprint, string EventId,
    NativeConstructOriginConstructionResult ExistingConstruction, NativeOriginProfile ExistingProfile,
    NativeConstructAssemblyRequest TargetAssembly, NativeOriginProposal TargetProposal);

public static partial class NativeOriginApiEndpoints
{
    public sealed record VerifiedConstructModificationRequest(
        Guid CharacterId, long OriginRevision, long OperatingRevision, long InventoryRevision,
        string RulesetId, string Fingerprint, string EventId);

    public static void MapVerifiedConstructModificationPlan(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/construct/modification/plan", async (
            VerifiedConstructModificationRequest request, NativeCatalogStore store,
            IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty ||
                request.OriginRevision < 1 || request.OriginRevision == long.MaxValue ||
                request.OperatingRevision < 0 || request.InventoryRevision < 0 ||
                string.IsNullOrWhiteSpace(request.EventId) || request.EventId.Length > 200)
                return Error(400, "invalid_construct_modification_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");
            var provider = services.GetService<INativeConstructHostModificationAuthorityProvider>();
            if (provider is null) return Error(422, "construct_modification_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision ||
                verified.OperatingRevision != request.OperatingRevision ||
                verified.InventoryRevision != request.InventoryRevision ||
                verified.EventId != request.EventId ||
                verified.RulesetId != catalog.RulesetId || verified.Fingerprint != catalog.Fingerprint ||
                verified.ExistingConstruction is null || verified.ExistingProfile is null ||
                verified.TargetAssembly is null || verified.TargetProposal is null)
                return Error(422, "construct_modification_authority_unverified");

            var plan = new NativeConstructModificationPlanner(catalog).Plan(
                verified.ExistingConstruction, verified.ExistingProfile,
                verified.TargetAssembly, verified.TargetProposal, verified.EventId);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                operatingRevision = request.OperatingRevision, inventoryRevision = request.InventoryRevision,
                eventId = request.EventId, previewOnly = true, committed = false, plan
            }, statusCode: plan.CanApply ? 200 : 422);
        });
    }
}
