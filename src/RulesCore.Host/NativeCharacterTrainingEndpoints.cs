using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Reload the complete unaggregated primary training and all current earned
/// source occurrences independently. Verify Class entry/advancement, Origin
/// and acquired grant applicability, level allocations, the selected Life
/// Experiences and their context, duplicate choices and ruleset policy. Do not
/// supply previously aggregated training or reinclude Life Experience grants
/// in the other source lists. Optional equipment sources and replacement
/// selections must also be independently authenticated, including Class entry
/// mode and source ownership. Optional Origin construction evidence must be
/// the independently revalidated current-catalog underlying construction at
/// the requested Origin revision, not a historical or active-form snapshot.
/// Primary HitPointGains must contain ordinary class gains only, without any
/// inherent Origin maximum-HP bonus already included. If an advancement plan
/// is requested, independently load the prior verified character projection,
/// authoritative spent ledger and exact unconsumed advancement event, binding
/// all of them to the same character, Origin and target character revision.
/// Verify any capacity decisions and their scope independently. The request
/// contains no grant or resource assertions.
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
    NativeCompetencyDuplicateProficiencyPolicy DuplicatePolicy,
    NativeEquipmentQualificationRequest? OtherEquipmentSources = null,
    IReadOnlyList<NativeLifeExperienceEquipmentReplacement>? EquipmentAlternatives = null,
    NativeOriginConstructionResult? VerifiedOrigin = null,
    VerifiedOriginInnateAdvancement? InnateAdvancement = null);

public sealed record VerifiedOriginInnateAdvancement(
    string EventId, NativeCharacterProjection PreviousCharacter,
    NativeOriginInnateSpellLedgerState Ledger,
    IReadOnlyList<NativeTableRuleDecision>? CapacityDecisions = null,
    NativeTableDecisionScope? TrustedScope = null,
    INativeOriginAuthority? Authority = null);

public static class NativeCharacterTrainingEndpoints
{
    public sealed record Request(Guid CharacterId, long OriginRevision, long CharacterRevision,
        string RulesetId, string Fingerprint,
        string? AdvancementEventId = null, long? InnateLedgerRevision = null);

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
                request.CharacterRevision < 0 ||
                (request.AdvancementEventId is null) != (request.InnateLedgerRevision is null) ||
                request.InnateLedgerRevision is < 0 ||
                request.AdvancementEventId is { } eventId &&
                    (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 200))
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

            if (request.AdvancementEventId is not null &&
                (verified.VerifiedOrigin is null || verified.InnateAdvancement is not { } advance ||
                 advance.EventId != request.AdvancementEventId || advance.PreviousCharacter is null ||
                 advance.Ledger is null || advance.Ledger.Revision != request.InnateLedgerRevision))
                return Error(422, "origin_advancement_authority_unverified");

            var life = new NativeLifeExperienceResolver(catalog).Resolve(verified.LifeProposal, verified.LifeAuthorization);
            if (life.EarnedProjection is not { } earned)
                return Results.Json(new { error = "character_training_life_experiences_unresolved", life },
                    statusCode: 422);

            var result = NativeCharacterTrainingComposer.Compose(catalog, verified.Primary, earned,
                verified.OtherSkills, verified.OtherCompetencies, verified.SkillAlternatives,
                verified.CompetencyAlternatives, verified.DuplicatePolicy, context.RequestAborted);
            // Equipment aggregation is an independent supported subset.
            // An omitted source list is not evidence of zero equipment grants.
            var equipment = verified.OtherEquipmentSources is null ? null :
                NativeCharacterEquipmentComposer.Compose(catalog, earned.Equipment,
                    verified.OtherEquipmentSources, verified.EquipmentAlternatives ?? [], earned.EquipmentReplacements);
            // Derive all supported progression views from the same native
            // character projection. No client-supplied level, separate rank
            // spending, HP mutation or resource initialization is implied.
            var progression = verified.VerifiedOrigin is { } origin && result.Projection is { } character
                ? ProjectOriginProgression(catalog, origin, character) : null;

            // A requested advancement is a detached transaction plan, never
            // an implicit reset. Use the same resolved character progression
            // and the existing ledger's exact snapshot/revision validation.
            var advancement = request.AdvancementEventId is not null &&
                result.Status == "resolved" && result.Projection is { } advancedCharacter &&
                (equipment is null || equipment.Status == "resolved")
                ? NativeOriginInnateSpellResourceLedger.Advance(catalog, verified.VerifiedOrigin!,
                    verified.InnateAdvancement!.PreviousCharacter, advancedCharacter,
                    verified.InnateAdvancement.Ledger, request.InnateLedgerRevision!.Value,
                    verified.InnateAdvancement.CapacityDecisions, verified.InnateAdvancement.TrustedScope,
                    verified.InnateAdvancement.Authority)
                : null;

            return Results.Json(new
            {
                contractVersion = 1, rulesetId = catalog.RulesetId, fingerprint = catalog.Fingerprint,
                characterId = request.CharacterId, originRevision = request.OriginRevision,
                characterRevision = request.CharacterRevision, committed = false,
                supportedScope = "verified-primary-training", effectiveMechanicsReady = false,
                result, equipment, originProgression = progression,
                advancementEventId = request.AdvancementEventId, innateAdvancement = advancement,
                // Source-only entitlements/intents, not a second combined
                // training set and not committed inventory or languages.
                lifeExperiences = earned
            }, statusCode: result.Status == "resolved" &&
                (equipment is null || equipment.Status == "resolved") &&
                (request.AdvancementEventId is null || advancement?.Status == "resolved") ? 200 : 422);
        });
    }

    private static object ProjectOriginProgression(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult origin, NativeCharacterProjection character)
    {
        int? level = character.Values.TryGetValue("advancement.total-level", out var total) &&
            total.Status == "resolved" && total.Value is > 0 ? total.Value : null;
        // Keep these views distinct. Unsupported/suppressed characteristics
        // must not be interpreted as a zero or an active-form entitlement.
        var mixed = origin.EvaluatedProposal.Intent == NativeOriginIntent.Mixed;
        return new
        {
            status = "partial",
            characterLevel = level,
            hitPoints = NativeOriginHitPointProgressionProjector.Combine(catalog, origin, character),
            skillRanks = NativeOriginSkillRankEntitlementProjector.ForCharacter(catalog, origin, character),
            innateSpells = NativeOriginInnateSpellProgressionProjector.ForCharacter(catalog, origin, character),
            darkvision = mixed
                ? NativeOriginDarkvisionProjector.ProjectApprovedMixed(catalog, origin, level)
                : NativeOriginDarkvisionProjector.Project(catalog, origin, level),
            // These are primary-form views only. Do not convert a resolved
            // entitlement schedule into a spendable balance or live condition.
            effectiveMechanicsReady = false
        };
    }

    private static IResult Error(int status, string code) => Results.Json(new { error = code }, statusCode: status);
}
