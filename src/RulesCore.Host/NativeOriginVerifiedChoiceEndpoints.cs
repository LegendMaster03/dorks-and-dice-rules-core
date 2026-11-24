using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Authorize the target creation draft/revision and its complete feat
/// eligibility context, including independently earned and proposed feats.
/// The provider cannot substitute working content for a native definition.
/// </summary>
public interface INativeOriginHostFeatChoiceAuthorityProvider
{
    Task<VerifiedOriginFeatChoiceAuthority?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedDirectChoiceRequest request, CancellationToken cancellationToken);
}

public sealed record VerifiedOriginFeatChoiceAuthority(Guid CharacterId, long DraftRevision,
    INativeOriginFeatChoiceAuthority Authority);

/// <summary>
/// Load the exact saved direct Origin and its current innate ledger. Verify
/// actor access and a completed, not-yet-applied Long Rest event, bound to
/// the character, revision and current native catalog. Short rests, stale or
/// already-consumed events must not return authorization.
/// </summary>
public interface INativeOriginHostCantripRestAuthorityProvider
{
    Task<VerifiedOriginCantripRestAuthority?> VerifyAsync(Guid actorId,
        NativeOriginApiEndpoints.CantripRestRequest request, CancellationToken cancellationToken);
}

public sealed record VerifiedOriginCantripRestAuthority(Guid CharacterId, long OriginRevision,
    string RestEventId, NativeOriginProposal SavedProposal, NativeOriginProfile SavedProfile,
    NativeCharacterProjection Character, NativeOriginInnateSpellLedgerState Ledger);

public static partial class NativeOriginApiEndpoints
{
    public sealed record VerifiedDirectChoiceRequest(Guid CharacterId, long DraftRevision,
        string RulesetId, string Fingerprint, NativeOriginProposal Proposal);

    public sealed record CantripRestRequest(Guid CharacterId, long OriginRevision, long LedgerRevision,
        string RulesetId, string Fingerprint, string RestEventId, string GrantId, string ReplacementSpellId);

    public static void MapVerifiedOriginDirectChoices(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/direct/verified-preview", async (VerifiedDirectChoiceRequest request,
            NativeCatalogStore store, IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)] is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty || request.DraftRevision < 0 ||
                !ValidProposal(request.Proposal) || request.Proposal.Intent != NativeOriginIntent.Direct ||
                request.Proposal.Method is not { RuleId: "rule.creature-origination", MethodId: "biological" })
                return Error(400, "invalid_verified_direct_choice_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");
            if (HasUntrustedOriginState(request.Proposal))
                return Error(400, "trusted_origin_state_required");
            var provider = services.GetService<INativeOriginHostFeatChoiceAuthorityProvider>();
            if (provider is null) return Error(422, "feat_choice_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.Authority is null || verified.CharacterId != request.CharacterId ||
                verified.DraftRevision != request.DraftRevision)
                return Error(422, "feat_choice_authority_unverified");
            var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(request.Proposal, verified.Authority);
            return Results.Ok(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, draftRevision = request.DraftRevision,
                previewOnly = true, committed = false, allocation = BiologicalAllocation(catalog, result), result
            });
        });
    }

    public static void MapVerifiedOriginCantripRest(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/innate-spells/cantrip-replacement/plan", async (
            CantripRestRequest request, NativeCatalogStore store, IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)] is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty || request.OriginRevision < 1 ||
                request.LedgerRevision < 0 || !ValidKey(request.RestEventId) ||
                !ValidKey(request.GrantId) || !ValidKey(request.ReplacementSpellId))
                return Error(400, "invalid_cantrip_replacement_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");
            var provider = services.GetService<INativeOriginHostCantripRestAuthorityProvider>();
            if (provider is null) return Error(422, "cantrip_rest_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision || verified.RestEventId != request.RestEventId ||
                verified.Ledger is null || verified.Ledger.Revision != request.LedgerRevision ||
                verified.Character is null || verified.SavedProfile is null ||
                !ValidProposal(verified.SavedProposal) || HasUntrustedOriginState(verified.SavedProposal))
                return Error(422, "cantrip_rest_authority_unverified");
            var plan = NativeOriginCantripReplacementPlanner.Plan(catalog, verified.SavedProposal,
                verified.SavedProfile, verified.Character, verified.Ledger, request.LedgerRevision,
                request.GrantId, request.ReplacementSpellId);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                ledgerRevision = request.LedgerRevision, restEventId = request.RestEventId,
                previewOnly = true, committed = false, plan
            }, statusCode: plan.Status == "resolved" ? 200 : 422);
        });

        static bool ValidKey(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200;
    }
}
