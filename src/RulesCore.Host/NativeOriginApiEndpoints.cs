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
                request.Proposal.Method is null ||
                request.Proposal.Contributors is null ||
                request.Proposal.Candidates is null ||
                request.Proposal.Manifestations is null ||
                request.Proposal.Contributors.Any(c => c is null || c.Source is null) ||
                request.Proposal.Candidates.Any(c => c is null ||
                    c.Identity is null || c.SelectionBasis is null) ||
                request.Proposal.Manifestations.Any(m => m is null))
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
                // These are trusted inputs only after the actual contributor's
                // saved manifested state and acquisition have been verified.
                proposal.Contributors.Any(contributor =>
                    contributor.Source.Kind == NativeOriginSourceKind.ManifestedProfile ||
                    contributor.ManifestedProfile is not null ||
                    (contributor.HistoricalSourceIds?.Count ?? 0) != 0) ||
                // A scoped table decision cannot be smuggled in through a
                // candidate, resolution, requirement or manifestation.
                proposal.Candidates.Any(candidate =>
                    candidate.SelectionBasis.Eligibility == NativeOriginEligibility.DmAuthorized ||
                    !string.IsNullOrWhiteSpace(candidate.SelectionBasis.TableDecisionId)) ||
                (proposal.Resolutions?.Any(resolution => resolution?.Basis is null ||
                    resolution.Basis.Adjudication == NativeOriginAdjudication.DmAuthorized ||
                    !string.IsNullOrWhiteSpace(resolution.Basis.TableDecisionId)) ?? false) ||
                (proposal.Constraints?.Any(constraint =>
                    constraint is null ||
                    !string.IsNullOrWhiteSpace(constraint.TableDecisionId)) ?? false) ||
                (proposal.InputRequirements?.Any(requirement =>
                    requirement is null ||
                    !string.IsNullOrWhiteSpace(requirement.TableDecisionId)) ?? false) ||
                proposal.Manifestations.Any(manifest =>
                    !string.IsNullOrWhiteSpace(manifest.TableDecisionId) ||
                    (manifest.Choices?.Any(choice => choice is null ||
                        choice.CatalogEligibilityEvidenceId is not null) ?? false)))
                return Error(400, "trusted_origin_state_required");

            // Only native catalog membership proof is supplied internally.
            // Table authorization always fails; the stateless API will never
            // trust a client-provided choice proof or a DM ruling.
            var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(proposal);
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
