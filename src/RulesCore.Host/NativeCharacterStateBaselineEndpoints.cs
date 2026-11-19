using Microsoft.AspNetCore.Routing;
using RulesCore.Content;
using RulesCore.Mechanics;

namespace RulesCore.Host;

/// <summary>
/// Narrow first transport increment for an already Site-authorized Character
/// Sheet primary character. Native supernatural acquisition and transition
/// verification have no production provider yet, so this route MUST refuse
/// every alternate sheet, influence, manifestation and stored event.
/// </summary>
public static class NativeCharacterStateBaselineEndpoints
{
    public sealed record NativeVerifiedPrimaryStateRequest(
        NativeCharacterState State, NativeManifestationLedger Ledger);

    public static void MapVerifiedPrimaryState(this RouteGroupBuilder group)
    {
        group.MapPost("/characters/effective-state-baseline",
            (NativeVerifiedPrimaryStateRequest request, HttpContext context,
                NativeCatalogStore store) =>
            {
                // The enclosing authenticated mechanics group already
                // validated delegated Character Sheet tool provenance. Reject
                // legacy anonymous/non-GUID service identities on this new
                // character-owned read path.
                if (context.Items[typeof(VerifiedCharacterSheetToolIdentity)]
                    is not VerifiedCharacterSheetToolIdentity)
                    return Results.Unauthorized();

                var catalog = store.Snapshot;
                if (catalog is null)
                    return Results.Json(new { error = "native_catalog_unavailable" },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                if (request?.State?.PrimaryMechanics is null ||
                    request.Ledger is null ||
                    string.IsNullOrWhiteSpace(request.State.CharacterId) ||
                    request.State.CharacterId != request.Ledger.CharacterId ||
                    !Guid.TryParse(request.State.CharacterId, out var id) ||
                    id == Guid.Empty)
                    return Results.BadRequest(new { error = "invalid_character_state" });

                var state = request.State;
                var ledger = request.Ledger;
                if (state.AlternateSheets is null || state.PersistentInfluences is null ||
                    ledger.Instances is null || ledger.Resources is null ||
                    ledger.ProcessedEventIds is null ||
                    state.AlternateSheets.Count != 0 ||
                    state.PersistentInfluences.Count != 0 ||
                    ledger.Instances.Count != 0 || ledger.Resources.Count != 0 ||
                    ledger.ProcessedEventIds.Count != 0 ||
                    (ledger.Interactions?.Count ?? 0) != 0 || ledger.Revision != 0)
                    return Results.Json(new { error = "supernatural_authority_unavailable",
                        detail = "A delegated tool ticket does not prove supernatural acquisition, grants, or transition receipts." },
                        statusCode: StatusCodes.Status422UnprocessableEntity);

                if (state.PrimaryMechanics.RulesetId != catalog.RulesetId ||
                    state.PrimaryMechanics.Fingerprint != catalog.Fingerprint)
                    return Results.Json(new { error = "native_content_changed" },
                        statusCode: StatusCodes.Status409Conflict);

                var authority = new NativePrimaryOnlyAuthority(state, ledger, catalog);
                var projected = NativeCharacterManifestationProjector.Project(
                    state, ledger, authority, new NativeCharacterProjector(catalog),
                    context.RequestAborted);

                // Projection cannot certify that Character Sheet sourced Class,
                // Origin, and mutable inputs correctly. The verified server
                // must report its own provenance and unsupported cross-pillar
                // mechanics. This transport certifies NO acquired form.
                return Results.Ok(new
                {
                    contractVersion = 1,
                    rulesetId = catalog.RulesetId,
                    fingerprint = catalog.Fingerprint,
                    status = projected.Status == "invalid" ? "invalid" : "partial",
                    effectiveMechanicsReady = false,
                    supportedScope = "primary-only",
                    projection = projected
                });
            });
    }

    /// <summary>
    /// Fail-closed native adapter. Only the exact single primary input and
    /// empty journal from the same authenticated request are accepted. This
    /// adapter never verifies form acquisition, expression, grants, rule
    /// occurrences, or campaign/DM decisions, and returns false for all of them.
    /// An actual production entitlement authority must replace this narrow
    /// adapter before supernatural state is accepted.
    /// </summary>
    private sealed class NativePrimaryOnlyAuthority(
        NativeCharacterState verifiedInput,
        NativeManifestationLedger verifiedEmptyLedger,
        NativeCatalogSnapshot catalog) : INativeCharacterManifestationAuthority
    {
        private bool SameCharacter(string characterId) =>
            characterId == verifiedInput.CharacterId &&
            characterId == verifiedEmptyLedger.CharacterId;

        public bool AuthorizesPrimary(string characterId, NativeCharacterRequest primary) =>
            SameCharacter(characterId) &&
            ReferenceEquals(primary, verifiedInput.PrimaryMechanics) &&
            primary.RulesetId == catalog.RulesetId &&
            primary.Fingerprint == catalog.Fingerprint &&
            new NativeCharacterProjector(catalog).Project(primary).Status != "invalid";

        public bool AuthorizesLedger(string characterId, NativeManifestationLedger ledger) =>
            SameCharacter(characterId) &&
            ReferenceEquals(ledger, verifiedEmptyLedger) &&
            ledger.Revision == 0 &&
            ledger.Instances.Count == 0 &&
            ledger.Resources.Count == 0 &&
            ledger.ProcessedEventIds.Count == 0 &&
            (ledger.Interactions?.Count ?? 0) == 0;

        public bool AuthorizesSheet(string characterId, NativePersistentCharacterSheet sheet) => false;
        public bool AuthorizesProgression(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression) => false;
        public bool AuthorizesGrant(string characterId, string ownerSheetId,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) => false;
        public bool AuthorizesApplicability(string characterId, string sheetId,
            NativeSheetGrantApplicability decision) => false;
        public bool AuthorizesInfluence(string characterId, NativeCharacterPersistentInfluence influence) => false;
        public bool AuthorizesExpression(string characterId, NativeCharacterPersistentInfluence influence) => false;
        public bool AuthorizesEffect(string characterId, string influenceId,
            NativeCharacterInfluenceEffect effect) => false;
        public bool AuthorizesInfluenceProgression(string characterId, string influenceId,
            NativeSheetEarnedProgression progression) => false;
        public bool AuthorizesInfluenceGrant(string characterId, string influenceId,
            NativeSheetEarnedProgression progression, NativeSheetEarnedGrant grant) => false;
        public bool AuthorizesInfluenceApplicability(string characterId, string influenceId,
            NativeInfluenceGrantApplicability decision) => false;
        public bool AuthorizesInstance(string characterId, NativeManifestationInstance instance,
            NativePersistentCharacterSheet sheet) => false;
        public bool AuthorizesResource(string characterId, NativeManifestationResourceState resource) => false;
        public bool AuthorizesResourcePolicy(string characterId, NativeManifestationInstance instance,
            NativeManifestationResourcePolicy policy) => false;
        public bool AuthorizesInteraction(string characterId, NativeManifestationInteractionDecision decision) => false;
        public bool AuthorizesTransition(string characterId, NativeManifestationLedger current,
            NativeManifestationTransitionRequest request,
            NativeManifestationTransitionStep step) => false;
        public bool AuthorizesUnlistedEnding(string characterId, NativeManifestationLedger current,
            NativeManifestationInstance instance,
            NativeManifestationTransitionStep step) => false;
    }
}
