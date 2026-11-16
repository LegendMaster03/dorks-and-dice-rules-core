using Microsoft.AspNetCore.Routing;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// A host-side source-of-truth verifier, not a caller-provided permissions
/// bundle. An implementation must reload authoritative character ownership,
/// source occurrences, acquired grants, rule/DM decision evidence and current
/// revisions independently before it returns a scoped authority adapter.
/// </summary>
public interface INativeManifestationHostAuthorityProvider
{
    Task<INativeCharacterManifestationAuthority?> VerifyAsync(
        Guid authenticatedActor,
        NativeCharacterState currentCharacter,
        NativeManifestationLedger currentLedger,
        NativeManifestationTransitionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Delegated-only pure planning. There is intentionally NO built-in authority
/// provider. Core must not infer acquisition or DM permission from Site
/// delegation alone, from a client-supplied source ID, or from saved JSON.
/// </summary>
public static class NativeCharacterManifestationPlanningEndpoints
{
    public sealed record VerifiedManifestationPlanRequest(
        NativeCharacterState State,
        NativeManifestationLedger Ledger,
        NativeManifestationTransitionRequest Transition);

    public static void MapVerifiedManifestationPlans(this RouteGroupBuilder group)
    {
        group.MapPost("/characters/manifestations/plan", async (
            VerifiedManifestationPlanRequest request,
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

            // Reject incomplete/ambiguous request graphs before consulting an
            // external source provider. A delegated tool identity does not
            // make deserialized client input structurally trustworthy.
            if (request?.State?.PrimaryMechanics is null ||
                request.State.AlternateSheets is null ||
                request.State.PersistentInfluences is null ||
                request.Ledger is null || request.Transition is null ||
                request.Ledger.Instances is null ||
                request.Ledger.Resources is null ||
                request.Ledger.ProcessedEventIds is null ||
                request.Transition.Steps is null ||
                request.Transition.Steps.Count is < 1 or > 512 ||
                string.IsNullOrWhiteSpace(request.Transition.EventId) ||
                string.IsNullOrWhiteSpace(request.State.CharacterId) ||
                !Guid.TryParse(request.State.CharacterId, out var characterId) ||
                characterId == Guid.Empty ||
                request.State.CharacterId != characterId.ToString("D") ||
                !string.Equals(request.State.CharacterId, request.Ledger.CharacterId,
                    StringComparison.Ordinal) ||
                !string.Equals(request.State.CharacterId, request.Transition.CharacterId,
                    StringComparison.Ordinal) ||
                request.Ledger.Revision < 0)
                return Results.Json(new { error = "invalid_character_scope" },
                    statusCode: StatusCodes.Status400BadRequest);

            if (request.State.PrimaryMechanics.RulesetId != catalog.RulesetId ||
                request.State.PrimaryMechanics.Fingerprint != catalog.Fingerprint)
                return Results.Json(new { error = "native_content_changed" },
                    statusCode: StatusCodes.Status409Conflict);

            if (request.Transition.ExpectedRevision != request.Ledger.Revision)
                return Results.Json(new { error = "manifestation_revision_conflict" },
                    statusCode: StatusCodes.Status409Conflict);

            // Absence of an independently verifying adapter is normal until
            // first-party transformation acquisition and evidence sources are
            // implemented. It MUST NOT fall back to a permissive test authority.
            var provider = services.GetService<INativeManifestationHostAuthorityProvider>();
            if (provider is null)
                return Results.Json(new { error = "supernatural_authority_unavailable" },
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            var authority = await provider.VerifyAsync(actor.UserId, request.State,
                request.Ledger, request.Transition, context.RequestAborted);
            if (authority is null)
                return Results.Json(new { error = "supernatural_authority_unverified" },
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            var plan = NativeCharacterManifestationTransitionPlanner.Plan(
                request.State, request.Ledger, request.Transition, authority);
            if (plan.Status != "planned" || plan.Proposed is null ||
                plan.ProposedEffective is null)
                return Results.Json(new
                {
                    contractVersion = 1,
                    rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint,
                    status = plan.Status, plan,
                    error = "manifestation_plan_not_committable"
                }, statusCode: StatusCodes.Status422UnprocessableEntity);

            // Success is ONLY a candidate. Character Sheet must revalidate
            // its authoritative input fingerprint and perform the database
            // revision/event compare-and-swap. No Core endpoint commits.
            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                status = "planned",
                committed = false,
                effectiveMechanicsReady = false,
                plan
            });
        });
    }
}
