using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Reload an authorized installation and saved operating preferences together
/// with exact current DM/host-adjudicated source context. For Infernal Engines
/// the flags are the actual long-rest fuel and separately paid empowerment
/// state, not an instruction to spend or replenish fuel. For soul cores they
/// are usable-energy and authorized Overdrive decisions, not a soul quantity.
/// </summary>
public interface INativeConstructHostContextualPowerAuthorityProvider
{
    Task<VerifiedConstructContextualPowerAuthorization?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedConstructContextualPowerRequest request,
        CancellationToken cancellationToken);
}

public sealed record VerifiedConstructContextualPowerAuthorization(
    Guid CharacterId, long OriginRevision, long OperatingRevision, long ContextRevision,
    string RulesetId, string Fingerprint, string SourceKind,
    NativeConstructOriginConstructionResult Construction,
    NativeConstructOperatingPreset CurrentPreset,
    IReadOnlyDictionary<string, bool> VerifiedSupply,
    IReadOnlyDictionary<string, bool> VerifiedEnhancedOperation);

/// <summary>
/// Independently load the installation and custody records and authenticate an
/// unconsumed death or resolution event. This includes world-wide custody,
/// true proximity, soul eligibility, overlapping capture destinations and
/// authority for extraordinary release or final consumption. A character's
/// own local ledger cannot prove that another core has not captured a soul.
/// </summary>
public interface INativeConstructHostSoulCustodyAuthorityProvider
{
    Task<VerifiedConstructSoulCustodyAuthorization?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedConstructSoulCustodyRequest request,
        CancellationToken cancellationToken);
}

public sealed record VerifiedConstructSoulCustodyAuthorization(
    Guid CharacterId, long OriginRevision, long CustodyRevision,
    string RulesetId, string Fingerprint,
    NativeConstructOriginConstructionResult Construction,
    IReadOnlyList<NativeConstructSoulCustodyRecord> Records,
    NativeConstructSoulCustodyEvent Event);

public static partial class NativeOriginApiEndpoints
{
    public sealed record VerifiedConstructContextualPowerRequest(
        Guid CharacterId, long OriginRevision, long OperatingRevision, long ContextRevision,
        string RulesetId, string Fingerprint, string SourceKind);

    public sealed record VerifiedConstructSoulCustodyRequest(
        Guid CharacterId, long OriginRevision, long CustodyRevision,
        string RulesetId, string Fingerprint, string EventId);

    public static void MapVerifiedConstructContextualPower(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/construct/contextual-power/resolve", async (
            VerifiedConstructContextualPowerRequest request, NativeCatalogStore store,
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
                request.ContextRevision < 0 || request.SourceKind is not ("soul" or "infernal"))
                return Error(400, "invalid_construct_contextual_power_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var provider = services.GetService<INativeConstructHostContextualPowerAuthorityProvider>();
            if (provider is null) return Error(422, "construct_contextual_power_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision ||
                verified.OperatingRevision != request.OperatingRevision ||
                verified.ContextRevision != request.ContextRevision ||
                verified.RulesetId != catalog.RulesetId || verified.Fingerprint != catalog.Fingerprint ||
                verified.SourceKind != request.SourceKind ||
                verified.Construction?.IsMechanicallyAuthorized != true ||
                verified.Construction.OperatingSession is not { } session ||
                verified.CurrentPreset is null || verified.VerifiedSupply is null ||
                verified.VerifiedEnhancedOperation is null)
                return Error(422, "construct_contextual_power_authority_unverified");

            var bound = session.EvaluatePreset(verified.CurrentPreset);
            if (!bound.IsStructurallyValid)
                return Error(422, "construct_contextual_power_preset_unverified");
            var enabled = verified.CurrentPreset.Components.ToDictionary(part => part.InstanceId,
                part => part.Enabled, StringComparer.Ordinal);
            var modes = verified.CurrentPreset.Components.Where(part => part.ModeId is not null)
                .ToDictionary(part => part.InstanceId, part => part.ModeId!, StringComparer.Ordinal);
            // The native source evaluator, not this transport, determines
            // output, operational grants, and unresolved fuel consequences.
            NativeConstructSoulCoreOperationPreview? soul = null;
            NativeConstructInfernalOperationPreview? infernal = null;
            if (request.SourceKind == "soul")
                soul = session.PreviewSoulCoreOperation(verified.VerifiedSupply,
                    verified.VerifiedEnhancedOperation, enabled, modes);
            else
                infernal = session.PreviewInfernalOperation(verified.VerifiedSupply,
                    verified.VerifiedEnhancedOperation, enabled, modes);

            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                operatingRevision = request.OperatingRevision, contextRevision = request.ContextRevision,
                sourceKind = request.SourceKind, previewOnly = true, committed = false,
                soul, infernal
            }, statusCode: (soul?.CanOperate ?? infernal?.CanOperate ?? false) ? 200 : 422);
        });
    }

    public static void MapVerifiedConstructSoulCustodyPlan(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/construct/soul/custody/plan", async (
            VerifiedConstructSoulCustodyRequest request, NativeCatalogStore store,
            IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty ||
                request.OriginRevision < 1 || request.CustodyRevision < 0 ||
                request.CustodyRevision == long.MaxValue ||
                string.IsNullOrWhiteSpace(request.EventId) || request.EventId.Length > 200)
                return Error(400, "invalid_construct_soul_custody_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var provider = services.GetService<INativeConstructHostSoulCustodyAuthorityProvider>();
            if (provider is null) return Error(422, "construct_soul_custody_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision ||
                verified.CustodyRevision != request.CustodyRevision ||
                verified.RulesetId != catalog.RulesetId || verified.Fingerprint != catalog.Fingerprint ||
                verified.Construction?.IsMechanicallyAuthorized != true ||
                verified.Construction.OperatingSession is not { } session ||
                verified.Records is null || verified.Event?.EventId != request.EventId)
                return Error(422, "construct_soul_custody_authority_unverified");

            var plan = NativeConstructSoulCustodyPlanner.Plan(session, verified.Records, verified.Event);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                custodyRevision = request.CustodyRevision, eventId = request.EventId,
                previewOnly = true, committed = false, plan
            }, statusCode: plan.CanApply ? 200 : 422);
        });
    }
}
