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

    /// <summary>
    /// This call is for Character Sheet's *server-side* DM approval operation.
    /// The group filter must first redeem a Site delegation ticket and attach
    /// VerifiedCharacterSheetToolIdentity. It does not mutate a character.
    /// </summary>
    public sealed record ApprovedMixedRequest(
        string RulesetId, string? Fingerprint, Guid CharacterId,
        Guid CampaignId, string ApprovalEvidenceId, NativeOriginProposal Proposal);

    public static void MapNativeOriginApprovedMixedEvaluation(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/evaluate-approved-mixed", (
            ApprovedMixedRequest request, NativeCatalogStore store, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.Proposal is null ||
                request.CharacterId == Guid.Empty || request.CampaignId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.ApprovalEvidenceId) ||
                request.ApprovalEvidenceId.Length > 200 ||
                !string.Equals(request.RulesetId, catalog.RulesetId, StringComparison.Ordinal))
                return Error(400, "invalid_approved_mixed_request");

            // Identity is Site-introspected once by the parent group filter.
            // A browser-supplied actor, campaign role or arbitrary Site context
            // must not be used to satisfy this authorization boundary.
            if (!context.Items.TryGetValue(typeof(VerifiedCharacterSheetToolIdentity),
                    out var verified) ||
                verified is not VerifiedCharacterSheetToolIdentity identity ||
                !identity.Campaigns.Any(campaign =>
                    campaign.Id == request.CampaignId &&
                    string.Equals(campaign.Role, "DM",
                        StringComparison.OrdinalIgnoreCase)))
                return Error(403, "campaign_dm_authority_required");

            var proposal = request.Proposal;
            var decisions = proposal.TableDecisions;
            var actorId = identity.UserId.ToString("D");
            var campaignId = request.CampaignId.ToString("D");
            if (proposal.Intent != NativeOriginIntent.Mixed ||
                proposal.Method is null || proposal.Contributors is null ||
                proposal.Contributors.Count < 2 || proposal.Contributors.Count > 32 ||
                proposal.Candidates is null || proposal.Manifestations is null ||
                proposal.Candidates.Count > 256 || proposal.Manifestations.Count > 256 ||
                decisions is null || decisions.Count == 0 || decisions.Count > 128 ||
                proposal.ExistingConfiguration is not null ||
                proposal.ExistingEffectiveConfiguration is not null ||
                proposal.ExistingEffectiveConfigurationBasis is not null ||
                proposal.ActiveTemporaryTransformation is not null ||
                (proposal.OtherActiveTemporaryTransformations?.Count ?? 0) != 0 ||
                proposal.TemporaryTransformationPrecedence is not null ||
                (proposal.History?.Count ?? 0) != 0 ||
                proposal.Contributors.Any(source => source is null ||
                    source.Source is null ||
                    source.Source.Kind != NativeOriginSourceKind.Native ||
                    source.ManifestedProfile is not null ||
                    (source.HistoricalSourceIds?.Count ?? 0) != 0) ||
                proposal.Manifestations.Any(manifest => manifest is null ||
                    (manifest.Choices?.Any(choice => choice is null ||
                        choice.CatalogEligibilityEvidenceId is not null) ?? false)) ||
                decisions.Any(decision => decision is null ||
                    decision.Authority is null || decision.Scope is null ||
                    decision.Target is null ||
                    decision.Authority.ActorId != actorId ||
                    decision.Authority.AuthorizationEvidenceId != request.ApprovalEvidenceId ||
                    decision.Scope.Kind != "campaign" ||
                    decision.Scope.Id != campaignId))
                return Error(400, "invalid_approved_mixed_proposal");

            // Character Sheet owns target-character authorization and persistence.
            // This delegated computation checks the DM actor and campaign role
            // independently; it never claims that the given character is owned,
            // currently linked to the campaign, or already committed.
            var scope = new NativeTableDecisionScope("campaign", campaignId);
            var result = new NativeOriginConstructionEngine(catalog).Evaluate(
                new NativeOriginConstructionRequest(proposal, scope,
                    AutoManifestDirectDefaults: false),
                new ScopedDmAuthority(actorId, campaignId,
                    request.ApprovalEvidenceId));
            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                contentChangedSinceRequest = request.Fingerprint is not null &&
                    !string.Equals(request.Fingerprint, catalog.Fingerprint,
                        StringComparison.Ordinal),
                previewOnly = true,
                dmActorId = actorId,
                campaignId,
                characterId = request.CharacterId,
                approvalEvidenceId = request.ApprovalEvidenceId,
                // The actual Rules Core evaluator, not Character Sheet, supplies
                // the source-attributed intrinsic and visual-sense projection.
                // Each projection remains previewOnly until an authoritative
                // Character Sheet commit validates the exact resulting grants.
                intrinsics = NativeOriginApprovedMixedIntrinsicProjector.Project(result),
                // A separate mixed interpreter verifies the exact approved
                // native vision occurrence; direct-only rules remain closed.
                vision = NativeOriginVisionProjector.ProjectApprovedMixed(catalog, result),
                result
            });
        });
    }

    private sealed class ScopedDmAuthority(
        string actorId, string campaignId, string approvalEvidenceId)
        : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope trustedScope) =>
            trustedScope.Kind == "campaign" && trustedScope.Id == campaignId &&
            decision.Scope.Kind == "campaign" && decision.Scope.Id == campaignId &&
            decision.Authority.ActorId == actorId &&
            decision.Authority.AuthorizationEvidenceId == approvalEvidenceId;

        // Mixed choice predicates without authored selected-native eligibility
        // are unresolved; DM authority alone does not manufacture a feat catalog.
        public bool AuthorizesCatalogChoice(
            string catalogId, string optionId, string evidenceId) => false;
    }

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
                // Interpretation of the *freshly evaluated* native intrinsic
                // grants only. This is not proof that any caller owns the
                // proposed Origin or that its traits are active on a Sheet.
                intrinsics = NativeOriginIntrinsicProjector.Project(result),
                // Authoritative authored low-light-vision rule, interpreted
                // only from an adjudicated and provenance-matched trait.
                // As with intrinsics, this remains preview-only until the
                // Character Sheet independently verifies committed state.
                vision = NativeOriginVisionProjector.Project(catalog, result),
                result
            });
        });
    }

    private static IResult Error(int status, string code) =>
        Results.Json(new { error = code }, statusCode: status);
}
