using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// A deliberately stateless construction preview. This endpoint never trusts a browser
/// as the source of a previously authorized profile, grant, Rite, or DM decision.
/// Persisted updates require an independent character-scoped server operation.
/// </summary>
public static class NativeOriginApiEndpoints
{
    public sealed record PreviewRequest(
        string RulesetId, string? Fingerprint, NativeOriginProposal Proposal);

    public static void MapNativeOriginPreview(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/preview", (PreviewRequest request,
            NativeCatalogStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var catalog = store.Snapshot;
            if (catalog is null)
                return Error(503, "native_catalog_unavailable");

            if (request is null || request.Proposal is null ||
                request.Proposal.Contributors is null ||
                request.Proposal.Candidates is null ||
                request.Proposal.Manifestations is null)
                return Error(400, "invalid_origin_preview_request");

            if (!string.Equals(request.RulesetId, catalog.RulesetId,
                    StringComparison.Ordinal))
                return Error(409, "ruleset_identity_mismatch");

            // This is an initial-construction preview, not a way to replay a
            // claimed Authorized profile or execute a post-origination Rite.
            // The engine independently discovers and validates native offers.
            var proposal = request.Proposal;
            if (proposal.Intent == NativeOriginIntent.Transformation ||
                proposal.ExistingConfiguration is not null ||
                proposal.ExistingEffectiveConfiguration is not null ||
                proposal.ExistingEffectiveConfigurationBasis is not null ||
                proposal.ActiveTemporaryTransformation is not null ||
                (proposal.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
                proposal.TemporaryTransformationPrecedence is not null ||
                (proposal.History?.Count ?? 0) != 0 ||
                (proposal.TableDecisions?.Count ?? 0) != 0 ||
                proposal.Candidates.Any(candidate =>
                    candidate.SelectionBasis?.Eligibility == NativeOriginEligibility.DmAuthorized) ||
                proposal.Manifestations.Any(manifest =>
                    !string.IsNullOrWhiteSpace(manifest.TableDecisionId)))
                return Error(400, "trusted_origin_state_required");

            // Neither INativeOriginAuthority nor TrustedScope is supplied.
            // Candidate eligibility and decisions must be earned by the
            // native construction pipeline, not an assertion in JSON.
            var result = new NativeOriginConstructionEngine(catalog).Evaluate(
                new NativeOriginConstructionRequest(proposal));
            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                contentChangedSinceRequest = request.Fingerprint is not null &&
                    !string.Equals(request.Fingerprint, catalog.Fingerprint,
                        StringComparison.Ordinal),
                previewOnly = true,
                result
            });
        });
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
