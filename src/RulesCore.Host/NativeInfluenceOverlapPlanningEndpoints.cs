using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Versioned source-verified overlap decision creation/replacement. This is a
/// backend-only plan; state revision and receipt commit belong to Character Sheet.
/// Neither the selected writer nor the source may be authorized by delegation.
/// </summary>
public sealed record VerifiedInfluenceOverlapPlanRequest(
    int ContractVersion, string CharacterId, string EventId, string IntentId,
    string RulesetId, string Fingerprint,
    long ExpectedSupernaturalRevision, long ExpectedLedgerRevision,
    string AuthoritativeInputsFingerprint,
    NativeCharacterState State, NativeManifestationLedger Ledger,
    NativeSupernaturalTransitionJournal ProgressionJournal,
    JsonElement VerifiedSource, string AuthorizationEvidenceId);

public sealed record VerifiedInfluenceOverlapHostAuthorization(
    NativeInfluenceOverlapSelection Decision, string? ReplacedDecisionId,
    INativeCharacterInfluenceAuthority Authority);

/// <summary>
/// Production implementations must reload the character/campaign authority,
/// pinned source rule, exact effect identities, prior decisions, decision event,
/// and revision-current ownership. No permissive registration is supplied.
/// </summary>
public interface INativeInfluenceOverlapHostAuthorityProvider
{
    Task<VerifiedInfluenceOverlapHostAuthorization?> VerifyAsync(
        Guid authenticatedActor, VerifiedInfluenceOverlapPlanRequest request,
        CancellationToken cancellationToken);
}

public static class NativeInfluenceOverlapPlanningEndpoints
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public static void MapVerifiedInfluenceOverlapPlans(this RouteGroupBuilder group)
    {
        group.MapPost("/characters/supernatural/overlaps/plan", async (
            VerifiedInfluenceOverlapPlanRequest request,
            NativeCatalogStore store, IServiceProvider services, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "supernatural_actor_unauthenticated");

            var catalog = store.Snapshot;
            if (catalog is null)
                return Error(503, "native_catalog_unavailable");

            if (request is null || request.ContractVersion != 1 ||
                !Guid.TryParse(request.CharacterId, out var characterId) ||
                characterId == Guid.Empty ||
                request.CharacterId != characterId.ToString("D") ||
                !Id(request.EventId, 160) || !Id(request.IntentId, 160) ||
                !Id(request.AuthorizationEvidenceId, 320) ||
                string.IsNullOrWhiteSpace(request.AuthoritativeInputsFingerprint) ||
                request.ExpectedSupernaturalRevision < 1 ||
                request.ExpectedSupernaturalRevision == long.MaxValue ||
                request.ExpectedLedgerRevision < 0 ||
                request.State is null || request.State.CharacterId != request.CharacterId ||
                request.State.PrimaryMechanics is null ||
                request.State.AlternateSheets is null ||
                request.State.PersistentInfluences is null ||
                request.Ledger is null || request.Ledger.CharacterId != request.CharacterId ||
                request.Ledger.Revision != request.ExpectedLedgerRevision ||
                request.Ledger.Instances is null || request.Ledger.Resources is null ||
                request.Ledger.ProcessedEventIds is null ||
                request.ProgressionJournal is null ||
                request.ProgressionJournal.CharacterId != request.CharacterId ||
                request.ProgressionJournal.Revision < 0 ||
                request.ProgressionJournal.ProcessedEventIds is null ||
                request.ProgressionJournal.ConsumedEligibilityEvidenceIds is null ||
                request.VerifiedSource.ValueKind != JsonValueKind.Object)
                return Error(400, "invalid_supernatural_overlap_request");

            if (request.RulesetId != catalog.RulesetId ||
                request.Fingerprint != catalog.Fingerprint ||
                request.State.PrimaryMechanics.RulesetId != catalog.RulesetId ||
                request.State.PrimaryMechanics.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            var provider = services.GetService<INativeInfluenceOverlapHostAuthorityProvider>();
            if (provider is null)
                return Error(422, "overlap_authority_unavailable");

            VerifiedInfluenceOverlapHostAuthorization? verified;
            try
            {
                verified = await provider.VerifyAsync(actor.UserId, request,
                    context.RequestAborted);
            }
            catch (Exception error) when (error is ArgumentException or
                InvalidOperationException or JsonException)
            {
                return Error(422, "overlap_authority_unverified");
            }
            if (verified?.Decision is null || verified.Authority is null ||
                !MatchesVerifiedSource(request, verified))
                return Error(422, "overlap_authority_unverified");

            NativeInfluenceOverlapDecisionPlan plan;
            try
            {
                plan = NativeInfluenceOverlapDecisionPlanner.Plan(
                    request.State, verified.Decision, verified.ReplacedDecisionId,
                    verified.Authority);
            }
            catch (Exception error) when (error is ArgumentException or
                InvalidOperationException or OverflowException)
            {
                return Error(422, "overlap_plan_not_committable");
            }
            if (plan.Status != "planned" || plan.ProposedState is null)
                return Results.Json(new
                {
                    error = "overlap_plan_not_committable",
                    status = plan.Status, issues = plan.Issues
                }, statusCode: StatusCodes.Status422UnprocessableEntity);

            return Results.Ok(new
            {
                contractVersion = 1,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                status = "planned",
                committed = false,
                effectiveMechanicsReady = false,
                plan = new
                {
                    status = plan.Status,
                    characterId = request.CharacterId,
                    eventId = request.EventId,
                    intentId = request.IntentId,
                    authorizationEvidenceId = request.AuthorizationEvidenceId,
                    expectedSupernaturalRevision = request.ExpectedSupernaturalRevision,
                    expectedManifestationRevision = request.ExpectedLedgerRevision,
                    expectedProgressionRevision = request.ProgressionJournal.Revision,
                    decisionId = verified.Decision.Id,
                    replacedDecisionId = verified.ReplacedDecisionId,
                    proposedState = plan.ProposedState,
                    issues = plan.Issues
                }
            });
        });
    }

    private static bool MatchesVerifiedSource(
        VerifiedInfluenceOverlapPlanRequest request,
        VerifiedInfluenceOverlapHostAuthorization verified)
    {
        try
        {
            var source = request.VerifiedSource;
            if (source.GetProperty("characterId").GetString() != request.CharacterId ||
                source.GetProperty("eventId").GetString() != request.EventId ||
                source.GetProperty("intentId").GetString() != request.IntentId ||
                source.GetProperty("authorizationEvidenceId").GetString() !=
                    request.AuthorizationEvidenceId ||
                source.GetProperty("rulesetId").GetString() != request.RulesetId ||
                source.GetProperty("fingerprint").GetString() != request.Fingerprint ||
                source.GetProperty("decisionId").GetString() != verified.Decision.Id ||
                source.GetProperty("replacedDecisionId").ValueKind is not
                    (JsonValueKind.Null or JsonValueKind.String))
                return false;
            var previous = source.GetProperty("replacedDecisionId").ValueKind == JsonValueKind.Null
                ? null : source.GetProperty("replacedDecisionId").GetString();
            var occurrence = source.GetProperty("sourceOccurrence")
                .Deserialize<NativeOriginSourceOccurrence>(Wire);
            return previous == verified.ReplacedDecisionId &&
                occurrence == verified.Decision.RuleOccurrence &&
                verified.Decision.AuthorizationEvidenceId ==
                    request.AuthorizationEvidenceId;
        }
        catch (Exception error) when (error is JsonException or
            InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private static bool Id(string? id, int length) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= length;

    private static IResult Error(int code, string error) =>
        Results.Json(new { error }, statusCode: code);
}
