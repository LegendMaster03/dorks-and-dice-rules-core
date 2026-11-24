using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Reload the complete unaggregated primary training and all current earned
/// source occurrences independently. Verify Class entry/advancement, Origin
/// and acquired grant applicability, level allocations, the selected Life
/// Experiences and their context, duplicate choices and ruleset policy. Do not
/// supply previously aggregated training or reinclude Life Experience grants
/// in the other source lists. The request contains no grant assertions.
/// </summary>
public interface INativeCharacterHostTrainingAuthorityProvider
{
    Task<VerifiedCharacterTrainingAuthorization?> VerifyAsync(Guid actorId,
        NativeCharacterTrainingEndpoints.Request request, CancellationToken cancellationToken);
}

public sealed record VerifiedCharacterTrainingAuthorization(
    Guid CharacterId, long OriginRevision, long CharacterRevision,
    NativeCharacterRequest Primary, NativeLifeExperienceProposal LifeProposal,
    NativeLifeExperienceAuthorization LifeAuthorization,
    IReadOnlyList<NativeCharacterSkillProficiencyGrant> OtherSkills,
    IReadOnlyList<NativeCompetencyTrainingGrant> OtherCompetencies,
    IReadOnlyDictionary<string, string> SkillAlternatives,
    IReadOnlyDictionary<string, string> CompetencyAlternatives,
    NativeCompetencyDuplicateProficiencyPolicy DuplicatePolicy);

public static class NativeCharacterTrainingEndpoints
{
    public sealed record Request(Guid CharacterId, long OriginRevision, long CharacterRevision,
        string RulesetId, string Fingerprint);

    public static void MapVerifiedCharacterTraining(this RouteGroupBuilder group)
    {
        group.MapPost("/characters/training/resolve", async (
            Request request, NativeCatalogStore store, IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "origin_actor_unauthenticated");
            var catalog = store.Snapshot;
            if (catalog is null) return Error(503, "native_catalog_unavailable");
            if (request is null || request.CharacterId == Guid.Empty || request.OriginRevision < 1 ||
                request.CharacterRevision < 0)
                return Error(400, "invalid_character_training_request");
            if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");
            var provider = services.GetService<INativeCharacterHostTrainingAuthorityProvider>();
            if (provider is null) return Error(422, "character_training_authority_unavailable");
            var verified = await provider.VerifyAsync(actor.UserId, request, context.RequestAborted);
            if (verified is null || verified.CharacterId != request.CharacterId ||
                verified.OriginRevision != request.OriginRevision || verified.CharacterRevision != request.CharacterRevision ||
                verified.Primary?.RulesetId != catalog.RulesetId || verified.Primary.Fingerprint != catalog.Fingerprint ||
                verified.LifeAuthorization is null || !NativeLifeExperienceResolver.ValidProposal(verified.LifeProposal))
                return Error(422, "character_training_authority_unverified");

            var life = new NativeLifeExperienceResolver(catalog).Resolve(verified.LifeProposal, verified.LifeAuthorization);
            if (life.EarnedProjection is not { } earned)
                return Results.Json(new { error = "character_training_life_experiences_unresolved", life },
                    statusCode: 422);

            var result = NativeCharacterTrainingComposer.Compose(catalog, verified.Primary, earned,
                verified.OtherSkills, verified.OtherCompetencies, verified.SkillAlternatives,
                verified.CompetencyAlternatives, verified.DuplicatePolicy, context.RequestAborted);
            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                characterRevision = request.CharacterRevision, committed = false,
                supportedScope = "verified-primary-training", effectiveMechanicsReady = false,
                result
            }, statusCode: result.Status == "resolved" ? 200 : 422);
        });
    }

    private static IResult Error(int status, string code) => Results.Json(new { error = code }, statusCode: status);
}
