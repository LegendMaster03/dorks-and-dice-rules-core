using Microsoft.AspNetCore.Routing;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Independent source-of-truth verification for read-only persisted character
/// manifestations. A delegated Site ticket authenticates the actor but is not
/// proof of acquisition, a resource policy, or a campaign/DM ruling.
/// </summary>
public interface INativeManifestationHostReadAuthorityProvider
{
    Task<INativeCharacterManifestationAuthority?> VerifyAsync(
        Guid authenticatedActor, NativeCharacterState state,
        NativeManifestationLedger ledger, CancellationToken cancellationToken);
}

/// <summary>
/// Read-only use of the existing native manifestation projector. No provider
/// is registered by the Rules Core host: a production verifier must reload
/// authoritative character, source, revision, Origin and campaign evidence.
/// </summary>
public static class NativeCharacterStateVerifiedEndpoints
{
    public sealed record VerifiedManifestationReadRequest(
        NativeCharacterState State, NativeManifestationLedger Ledger);

    public static void MapVerifiedManifestationReads(this RouteGroupBuilder group)
    {
        group.MapPost("/characters/effective-state", async (
            VerifiedManifestationReadRequest request,
            NativeCatalogStore store, IServiceProvider services,
            HttpContext context) =>
        {
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Results.Unauthorized();

            var catalog = store.Snapshot;
            if (catalog is null)
                return Results.Json(new { error = "native_catalog_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            if (request?.State?.PrimaryMechanics is null ||
                request.State.AlternateSheets is null ||
                request.State.PersistentInfluences is null ||
                request.Ledger is null ||
                request.Ledger.Instances is null ||
                request.Ledger.Resources is null ||
                request.Ledger.ProcessedEventIds is null ||
                !Guid.TryParse(request.State.CharacterId, out var characterId) ||
                characterId == Guid.Empty ||
                request.State.CharacterId != characterId.ToString("D") ||
                request.Ledger.CharacterId != request.State.CharacterId ||
                request.Ledger.Revision < 0)
                return Results.Json(new { error = "invalid_character_scope" },
                    statusCode: StatusCodes.Status400BadRequest);

            if (request.State.PrimaryMechanics.RulesetId != catalog.RulesetId ||
                request.State.PrimaryMechanics.Fingerprint != catalog.Fingerprint)
                return Results.Json(new { error = "native_content_changed" },
                    statusCode: StatusCodes.Status409Conflict);

            var provider = services.GetService<INativeManifestationHostReadAuthorityProvider>();
            if (provider is null)
                return Results.Json(new { error = "supernatural_authority_unavailable" },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            var authority = await provider.VerifyAsync(actor.UserId, request.State,
                request.Ledger, context.RequestAborted);
            if (authority is null)
                return Results.Json(new { error = "supernatural_authority_unverified" },
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            NativeManifestationProjectedCharacter projected;
            try
            {
                projected = NativeCharacterManifestationProjector.Project(
                    request.State, request.Ledger, authority,
                    new NativeCharacterProjector(catalog), context.RequestAborted);
            }
            catch (Exception error) when (error is ArgumentException or
                InvalidOperationException or OverflowException)
            {
                return Results.Json(new { error = "manifestation_read_unresolved" },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            // The native projector can verify inheritance and authored source
            // constraints, not independent Character Sheet Origin/Class/
            // equipment/HP policies. Even a resolved graph is partial until
            // those host-level entitlements are revalidated.
            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                supportedScope = "verified-manifestations",
                status = projected.Status is "invalid" or "unresolved"
                    ? projected.Status : "partial",
                effectiveMechanicsReady = false,
                projection = projected
            });
        });
    }
}
