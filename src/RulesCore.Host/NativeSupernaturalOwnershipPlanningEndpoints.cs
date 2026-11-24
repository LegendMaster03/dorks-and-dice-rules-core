using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Version 2 of the delegated, backend-only acquisition/progression contract.
/// Character Sheet owns the expected state and manifestation revisions; native
/// progression has its own independent event journal and revision.
/// </summary>
public sealed record VerifiedSupernaturalOwnershipPlanRequest(
    int ContractVersion,
    string Operation,
    string CharacterId,
    string EventId,
    string IntentId,
    string RulesetId,
    string Fingerprint,
    long ExpectedSupernaturalRevision,
    long ExpectedLedgerRevision,
    string AuthoritativeInputsFingerprint,
    NativeCharacterState State,
    NativeManifestationLedger Ledger,
    NativeSupernaturalTransitionJournal ProgressionJournal,
    JsonElement VerifiedSource,
    string AuthorizationEvidenceId);

/// <summary>
/// An independently loaded source definition, exact requested event, and
/// character/campaign-bound authority. Never infer any of these from the
/// authenticated delegation token or the caller's VerifiedSource object.
/// </summary>
public sealed record VerifiedSupernaturalHostAuthorization(
    NativeSupernaturalDefinition Definition,
    NativeSupernaturalTransitionRequest Transition,
    INativeSupernaturalProgressionAuthority Authority);

/// <summary>
/// This intentionally has no production default registration. Its provider
/// must reload and verify the exact source occurrence, earned progression,
/// eligibility evidence, character ownership, and authorization revisions.
/// </summary>
public interface INativeSupernaturalHostAuthorityProvider
{
    Task<VerifiedSupernaturalHostAuthorization?> VerifyAsync(
        Guid authenticatedActor,
        VerifiedSupernaturalOwnershipPlanRequest request,
        CancellationToken cancellationToken);
}

public static class NativeSupernaturalOwnershipPlanningEndpoints
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public static void MapVerifiedSupernaturalOwnershipPlans(this RouteGroupBuilder group)
    {
        group.MapPost("/characters/supernatural/ownership/plan", async (
            VerifiedSupernaturalOwnershipPlanRequest request,
            NativeCatalogStore store,
            IServiceProvider services,
            HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                is not VerifiedCharacterSheetToolIdentity actor)
                return Error(401, "supernatural_actor_unauthenticated");

            var catalog = store.Snapshot;
            if (catalog is null)
                return Error(503, "native_catalog_unavailable");

            if (request is null || request.ContractVersion != 2 ||
                request.Operation is not ("Acquire" or "Advance") ||
                !Guid.TryParse(request.CharacterId, out var characterId) ||
                characterId == Guid.Empty ||
                request.CharacterId != characterId.ToString("D") ||
                string.IsNullOrWhiteSpace(request.EventId) ||
                request.EventId.Length > 160 ||
                string.IsNullOrWhiteSpace(request.IntentId) ||
                request.IntentId.Length > 160 ||
                string.IsNullOrWhiteSpace(request.AuthorizationEvidenceId) ||
                request.AuthorizationEvidenceId.Length > 320 ||
                string.IsNullOrWhiteSpace(request.AuthoritativeInputsFingerprint) ||
                request.ExpectedSupernaturalRevision < 0 ||
                request.ExpectedLedgerRevision < 0 ||
                request.State is null ||
                request.State.PrimaryMechanics is null ||
                request.State.AlternateSheets is null ||
                request.State.PersistentInfluences is null ||
                request.State.CharacterId != request.CharacterId ||
                request.Ledger is null ||
                request.Ledger.CharacterId != request.CharacterId ||
                request.Ledger.Instances is null ||
                request.Ledger.Resources is null ||
                request.Ledger.ProcessedEventIds is null ||
                request.Ledger.Revision != request.ExpectedLedgerRevision ||
                request.ProgressionJournal is null ||
                request.ProgressionJournal.CharacterId != request.CharacterId ||
                request.ProgressionJournal.Revision < 0 ||
                request.ProgressionJournal.ProcessedEventIds is null ||
                request.ProgressionJournal.ConsumedEligibilityEvidenceIds is null ||
                request.VerifiedSource.ValueKind != JsonValueKind.Object)
                return Error(400, "invalid_supernatural_ownership_request");

            if (request.RulesetId != catalog.RulesetId ||
                request.Fingerprint != catalog.Fingerprint ||
                request.State.PrimaryMechanics.RulesetId != catalog.RulesetId ||
                request.State.PrimaryMechanics.Fingerprint != catalog.Fingerprint)
                return Error(409, "native_content_changed");

            // A trusted host extension must independently verify the source;
            // a supplied source payload or earlier Character Sheet check does
            // not authorize an acquisition or an earned progression.
            var provider = services.GetService<INativeSupernaturalHostAuthorityProvider>();
            if (provider is null)
                return Error(422, "supernatural_authority_unavailable");

            VerifiedSupernaturalHostAuthorization? verified;
            try
            {
                verified = await provider.VerifyAsync(actor.UserId, request,
                    context.RequestAborted);
            }
            catch (Exception error) when (error is ArgumentException or
                InvalidOperationException or JsonException)
            {
                return Error(422, "supernatural_authority_unverified");
            }
            if (verified?.Definition is null ||
                verified.Transition is null ||
                verified.Authority is null ||
                !MatchesVerifiedSource(request, verified))
                return Error(422, "supernatural_authority_unverified");

            NativeSupernaturalTransitionPlan plan;
            try
            {
                plan = NativeSupernaturalProgressionPlanner.Plan(
                    request.State, request.ProgressionJournal,
                    verified.Definition, verified.Transition, verified.Authority);
            }
            catch (Exception error) when (error is ArgumentException or
                InvalidOperationException or OverflowException)
            {
                return Error(422, "supernatural_plan_not_committable");
            }
            if (plan.Status != "planned" || plan.ProposedState is null ||
                plan.ProposedJournal is null)
                return Results.Json(new
                {
                    error = "supernatural_plan_not_committable",
                    status = plan.Status, issues = plan.Issues
                }, statusCode: StatusCodes.Status422UnprocessableEntity);

            return Results.Ok(new
            {
                contractVersion = 2,
                rulesetId = catalog.RulesetId,
                fingerprint = catalog.Fingerprint,
                status = "planned",
                committed = false,
                effectiveMechanicsReady = false,
                plan = new
                {
                    status = plan.Status,
                    characterId = request.CharacterId,
                    operation = request.Operation,
                    eventId = request.EventId,
                    intentId = request.IntentId,
                    authorizationEvidenceId = request.AuthorizationEvidenceId,
                    expectedSupernaturalRevision = request.ExpectedSupernaturalRevision,
                    expectedManifestationRevision = request.ExpectedLedgerRevision,
                    expectedProgressionRevision = request.ProgressionJournal.Revision,
                    proposedState = plan.ProposedState,
                    proposedJournal = plan.ProposedJournal,
                    newProgression = plan.NewProgression,
                    newGrants = plan.NewGrants,
                    issues = plan.Issues
                }
            });
        });
    }

    private static bool MatchesVerifiedSource(
        VerifiedSupernaturalOwnershipPlanRequest request,
        VerifiedSupernaturalHostAuthorization verified)
    {
        try
        {
            var source = request.VerifiedSource;
            if (source.GetProperty("characterId").GetString() != request.CharacterId ||
                source.GetProperty("eventId").GetString() != request.EventId ||
                source.GetProperty("operation").GetString() != request.Operation ||
                source.GetProperty("authorizationEvidenceId").GetString() !=
                    request.AuthorizationEvidenceId ||
                source.GetProperty("rulesetId").GetString() != request.RulesetId ||
                source.GetProperty("fingerprint").GetString() != request.Fingerprint)
                return false;

            var occurrence = source.GetProperty("sourceOccurrence")
                .Deserialize<NativeOriginSourceOccurrence>(Wire);
            var expectedKind = request.Operation == "Acquire"
                ? NativeSupernaturalTransitionKind.Acquire
                : NativeSupernaturalTransitionKind.Advance;
            return occurrence is not null &&
                occurrence.Source == verified.Definition.Source &&
                verified.Transition.Kind == expectedKind &&
                verified.Transition.CharacterId == request.CharacterId &&
                verified.Transition.EventId == request.EventId &&
                verified.Transition.ExpectedRevision ==
                    request.ProgressionJournal.Revision &&
                verified.Transition.EvidenceId == request.AuthorizationEvidenceId &&
                verified.Transition.InfluenceId is not null &&
                verified.Transition.InfluenceId.Length is > 0 and <= 160;
        }
        catch (Exception error) when (error is JsonException or
            InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private static IResult Error(int code, string error) =>
        Results.Json(new { error }, statusCode: code);
}
