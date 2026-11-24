using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Independently reload the exact character installation, current operating
/// preferences and revisions. Return a server-reconstructed authorized session
/// only when the existing source power/fuel state is verified and sufficient for
/// evaluating a preference-only transition. Rated generator output, a delegated
/// ticket, a client profile, or a requested mode is not proof of live fuel.
/// Context-dependent generators and resource-changing operations require their
/// own verified planning contracts; no store is provided by Rules Core.
/// </summary>
public interface INativeConstructHostOperatingAuthorityProvider
{
    Task<VerifiedConstructOperatingAuthorization?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedConstructOperatingRequest request,
        CancellationToken cancellationToken);
}

public sealed record VerifiedConstructOperatingAuthorization(
    Guid CharacterId, long OriginRevision, long OperatingRevision,
    string RulesetId, string Fingerprint,
    NativeConstructOriginConstructionResult Construction,
    NativeConstructOperatingPreset CurrentPreset);

public static partial class NativeOriginApiEndpoints
{
    public sealed record VerifiedConstructOperatingRequest(
        Guid CharacterId, long OriginRevision, long OperatingRevision,
        string RulesetId, string Fingerprint, NativeConstructOperatingPreset Target);

    /// <summary>
    /// Evaluate a preference change against an independently verified, currently
    /// operating installation. The existing session owns instance/mode checks
    /// and operational grant applicability; transport cannot issue or remove
    /// permanent grants, install components, spend resources or commit state.
    /// </summary>
    public static void MapVerifiedConstructOperatingPlan(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/construct/operating/plan", async (
            VerifiedConstructOperatingRequest request, NativeCatalogStore store,
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
                request.Target?.Components is null ||
                request.Target.Components.Count is < 1 or > 256 ||
                string.IsNullOrWhiteSpace(request.Target.Name) ||
                request.Target.Name.Length > 96 ||
                request.Target.Components.Any(part => part is null ||
                    !ValidOperatingKey(part.InstanceId) || !ValidOperatingKey(part.DefinitionId) ||
                    (part.ModeId is not null && !ValidOperatingKey(part.ModeId))))
                return Error(400, "invalid_construct_operating_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var provider = services.GetService<INativeConstructHostOperatingAuthorityProvider>();
            if (provider is null) return Error(422, "construct_operating_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision ||
                verified.OperatingRevision != request.OperatingRevision ||
                verified.RulesetId != catalog.RulesetId || verified.Fingerprint != catalog.Fingerprint ||
                verified.Construction?.IsMechanicallyAuthorized != true ||
                verified.Construction.OperatingSession is not { } session ||
                verified.CurrentPreset is null)
                return Error(422, "construct_operating_authority_unverified");

            // Keep this first transport increment restricted to verified
            // healthy operating baselines. The native session fails closed
            // for context-dependent sources without their contextual inputs.
            var transition = session.PreviewPresetTransition(verified.CurrentPreset, request.Target);
            if (!transition.Before.IsOperatingConfigurationValid || !transition.IsTargetValid)
                return Results.Json(new
                {
                    error = "construct_operating_transition_unresolved",
                    previewOnly = true,
                    committed = false,
                    transition
                }, statusCode: 422);

            // Both presets have now been bound to the same complete installation.
            // Reuse the canonical movement projector; never infer speeds from
            // an installed component that has ceased operating.
            NativeOriginMovementModeProjector.View Movement(NativeConstructOperatingPreset preset) =>
                NativeOriginMovementModeProjector.ProjectConstruct(catalog, verified.Construction,
                    preset.Components.ToDictionary(part => part.InstanceId, part => part.Enabled,
                        StringComparer.Ordinal),
                    preset.Components.Where(part => part.ModeId is not null).ToDictionary(
                        part => part.InstanceId, part => part.ModeId!, StringComparer.Ordinal));

            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId,
                originRevision = request.OriginRevision,
                operatingRevision = request.OperatingRevision,
                previewOnly = true,
                committed = false,
                transition,
                beforeMovement = Movement(verified.CurrentPreset),
                afterMovement = Movement(request.Target)
            });
        });

        static bool ValidOperatingKey(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= 200;
    }
}
