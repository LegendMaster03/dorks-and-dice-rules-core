using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Verify actor access to the proposed new character and the exact current
/// revisions of all referenced biological parents (including an empty set for
/// native-only donors). Even an empty set requires verified creation access. Only an authoritative
/// application store may provide these records, never submitted profile JSON.
/// </summary>
public interface INativeOriginHostBiologicalAuthorityProvider
{
    Task<IReadOnlyList<NativeOriginVerifiedManifestedContributor>?> VerifyAsync(
        Guid actorId, Guid characterId, IReadOnlyList<NativeOriginSourceReference> sources,
        string rulesetId, string fingerprint, CancellationToken cancellationToken);

    // Optional character-specific feat eligibility for this exact creation
    // draft; catalog membership alone is never sufficient.
    Task<INativeOriginFeatChoiceAuthority?> VerifyChoicesAsync(Guid actorId,
        NativeOriginApiEndpoints.VerifiedBiologicalRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<INativeOriginFeatChoiceAuthority?>(null);
}

/// <summary>
/// Load the exact persisted underlying/current profile, and verify the actor's
/// rights, the revision and the applicable completion evidence. The authority
/// callbacks must independently bind decisions and native completion to this
/// character, event, target, source and catalog; a ticket is not that proof.
/// </summary>
public interface INativeOriginHostReconstructionAuthorityProvider
{
    Task<VerifiedPermanentOriginAuthorization?> VerifyAsync(
        Guid actorId, NativeOriginApiEndpoints.VerifiedReconstructionRequest request,
        CancellationToken cancellationToken);
}

public sealed record VerifiedPermanentOriginAuthorization(
    NativeOriginProfile ExistingProfile, long OriginRevision,
    NativeTableDecisionScope TrustedScope, INativeOriginAuthority? Authority = null,
    INativeOriginNativeCompletionAuthority? NativeCompletionAuthority = null);

public static partial class NativeOriginApiEndpoints
{
    public sealed record VerifiedBiologicalRequest(
        Guid CharacterId, string RulesetId, string Fingerprint, NativeOriginProposal Proposal);

    public sealed record VerifiedReconstructionRequest(
        Guid CharacterId, long OriginRevision, string RulesetId, string Fingerprint,
        string CompletionId, NativeOriginProposal TargetProposal);

    /// <summary>
    /// The universal engine resolves authenticated parent records, carries latent
    /// genes and applies the host-owned initial allocation policy. The response
    /// is a proposal only; the character-owning application must recheck revisions
    /// before committing a new Origin.
    /// </summary>
    public static void MapVerifiedBiologicalOriginPreview(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/biological/verified-preview", async (
            VerifiedBiologicalRequest request, NativeCatalogStore store,
            IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty ||
                !ValidProposal(request.Proposal) ||
                request.Proposal.Intent != NativeOriginIntent.Mixed ||
                request.Proposal.Method is not
                    { RuleId: "rule.creature-origination", MethodId: "biological" })
                return Error(400, "invalid_verified_biological_request");
            if (request.RulesetId != catalog.RulesetId ||
                request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var proposal = request.Proposal;
            if (HasUntrustedOriginState(proposal, allowVerifiedContributorReferences: true) ||
                proposal.Contributors.Any(c => c.Source.Kind is not
                    (NativeOriginSourceKind.Native or NativeOriginSourceKind.ManifestedProfile)))
                return Error(400, "trusted_origin_state_required");

            var sources = proposal.Contributors.Select(c => c.Source)
                .Where(source => source.Kind == NativeOriginSourceKind.ManifestedProfile)
                .Distinct().ToArray();
            if (sources.Any(source =>
                string.IsNullOrWhiteSpace(source.Version) ||
                source.Version.Length > 200 || source.Id.Length > 200))
                return Error(400, "verified_parent_revision_required");

            var provider = services.GetService<INativeOriginHostBiologicalAuthorityProvider>();
            if (provider is null) return Error(422, "biological_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request.CharacterId,
                sources, catalog.RulesetId, catalog.Fingerprint, context.RequestAborted);
            // Require one independently loaded exact current record per requested
            // parent. No fallback to a caller-supplied snapshot or stale revision.
            if (verified is null || verified.Count != sources.Length ||
                verified.Any(record => record?.Profile is null ||
                    string.IsNullOrWhiteSpace(record.Revision)) ||
                sources.Any(source => verified.Count(record =>
                    record.Profile.Id == source.Id && record.Revision == source.Version) != 1))
                return Error(422, "biological_parent_unverified");

            var scope = new NativeTableDecisionScope("character", request.CharacterId.ToString("D"));
            var parentAuthority = new VerifiedParentAuthority(scope, verified);
            var featAuthority = await provider.VerifyChoicesAsync(actor.UserId, request, context.RequestAborted);
            var result = new NativeOriginFirstPartyChoiceEvaluator(catalog).EvaluateVerifiedMixed(
                new NativeOriginConstructionRequest(proposal, scope,
                    AutoManifestDirectDefaults: false,
                    RequireCreationAllocation: true,
                    CreationAllocationPolicy: NativeOriginFirstPartyBiologicalPricing.Create()),
                parentAuthority, featAuthority);

            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId,
                previewOnly = true,
                committed = false,
                parentRevisions = sources.Select(source => new { profileId = source.Id, revision = source.Version }),
                result
            });
        });
    }

    /// <summary>
    /// The existing permanent reconstruction service validates the replacement
    /// baseline and authorized completion, preserving independently earned
    /// grants and history. Transport never writes the returned state.
    /// </summary>
    public static void MapVerifiedPermanentOriginReconstruction(this RouteGroupBuilder group)
    {
        group.MapPost("/origins/reconstruction/plan", async (
            VerifiedReconstructionRequest request, NativeCatalogStore store,
            IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty ||
                request.OriginRevision < 1 || string.IsNullOrWhiteSpace(request.CompletionId) ||
                request.CompletionId.Length > 200 || !ValidProposal(request.TargetProposal) ||
                request.TargetProposal.Intent != NativeOriginIntent.Direct ||
                HasClaimedOriginBaseline(request.TargetProposal) ||
                request.TargetProposal.Contributors.Any(c =>
                    c.Source.Kind == NativeOriginSourceKind.ManifestedProfile))
                return Error(400, "invalid_reconstruction_request");
            if (request.RulesetId != catalog.RulesetId ||
                request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var provider = services.GetService<INativeOriginHostReconstructionAuthorityProvider>();
            if (provider is null) return Error(422, "reconstruction_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified?.ExistingProfile is null || verified.OriginRevision != request.OriginRevision ||
                verified.TrustedScope is null ||
                string.IsNullOrWhiteSpace(verified.TrustedScope.Kind) ||
                string.IsNullOrWhiteSpace(verified.TrustedScope.Id) ||
                (verified.Authority is null && verified.NativeCompletionAuthority is null))
                return Error(422, "reconstruction_authority_unverified");

            var result = new NativeOriginPermanentReconstructionEngine(catalog).Execute(
                new(verified.ExistingProfile, request.TargetProposal, request.CompletionId,
                    verified.TrustedScope), verified.Authority, verified.NativeCompletionAuthority);
            return Results.Json(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId,
                originRevision = request.OriginRevision,
                completionId = request.CompletionId,
                previewOnly = true,
                committed = false,
                result
            }, statusCode: result.IsMechanicallyAuthorized ? 200 : 422);
        });
    }

    private static bool ValidProposal(NativeOriginProposal? proposal) =>
        proposal is not null && proposal.Method is not null &&
        !string.IsNullOrWhiteSpace(proposal.Id) &&
        proposal.Contributors is not null && proposal.Candidates is not null &&
        proposal.Manifestations is not null &&
        !HasMalformedOriginNestedInputs(proposal) && !ExceedsUntrustedOriginLimits(proposal);

    private sealed class VerifiedParentAuthority(
        NativeTableDecisionScope scope,
        IReadOnlyList<NativeOriginVerifiedManifestedContributor> parents)
        : INativeOriginManifestedContributorAuthority
    {
        public NativeOriginVerifiedManifestedContributor? ResolveCurrent(
            NativeOriginSourceReference source, NativeTableDecisionScope trustedScope) =>
            scope == trustedScope && source.Kind == NativeOriginSourceKind.ManifestedProfile
                ? parents.SingleOrDefault(parent => parent.Profile.Id == source.Id &&
                    parent.Revision == source.Version) : null;
    }
}
