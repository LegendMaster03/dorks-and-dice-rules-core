namespace RulesCore.Mechanics;

/// <summary>
/// A source-authorized change to one exact-effect writer decision. Ownership,
/// expression, progression, manifestation and resource history are unaffected.
/// This pure planner never treats a caller-supplied selection as its own proof.
/// </summary>
public sealed record NativeInfluenceOverlapDecisionPlan(
    string Status, NativeCharacterState? ProposedState,
    IReadOnlyList<NativeIssue> Issues);

public static class NativeInfluenceOverlapDecisionPlanner
{
    public static NativeInfluenceOverlapDecisionPlan Plan(
        NativeCharacterState state, NativeInfluenceOverlapSelection selection,
        string? replacedDecisionId, INativeCharacterInfluenceAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(authority);

        NativeInfluenceOverlapDecisionPlan Fail(string code, string detail) =>
            new("invalid", null, [new NativeIssue(code, "influenceOverlapSelections", detail)]);

        if (authority is not INativeCharacterInfluenceOverlapAuthority overlapAuthority)
            return Fail("influence-overlap-authority-required",
                "A separate host overlap authority is mandatory.");

        var prior = state.InfluenceOverlapSelections ?? [];
        if (prior.Count >= 1024 ||
            string.IsNullOrWhiteSpace(selection.Id) ||
            prior.Any(x => x.Id == selection.Id) ||
            (replacedDecisionId is not null &&
                (string.IsNullOrWhiteSpace(replacedDecisionId) ||
                 replacedDecisionId == selection.Id)))
            return Fail("invalid-overlap-decision-identity",
                "A decision must have a new unique ID and an explicit existing replacement, if any.");

        var replaced = replacedDecisionId is null ? null :
            prior.SingleOrDefault(x => x.Id == replacedDecisionId);
        if (replacedDecisionId is not null && replaced is null)
            return Fail("unknown-overlap-decision",
                "The replaced decision must belong to this character.");

        // Replacing a winner must concern the same source-qualified collision
        // and sheet. A new collision or form needs a distinct decision.
        if (replaced is not null &&
            (replaced.TargetSheetId != selection.TargetSheetId ||
             replaced.EffectIds.Count != selection.EffectIds.Count ||
             !replaced.EffectIds.OrderBy(x => x, StringComparer.Ordinal)
                 .SequenceEqual(selection.EffectIds.OrderBy(x => x,
                     StringComparer.Ordinal), StringComparer.Ordinal)))
            return Fail("overlap-replacement-scope-changed",
                "A replacement may change the selection, not its exact collision scope.");

        if (prior.Any(x => x.Id != replacedDecisionId &&
            x.TargetSheetId == selection.TargetSheetId &&
            x.EffectIds.Count == selection.EffectIds.Count &&
            x.EffectIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                selection.EffectIds.OrderBy(id => id, StringComparer.Ordinal),
                StringComparer.Ordinal)))
            return Fail("duplicate-overlap-decision-scope",
                "Two active decisions may not select the same exact collision.");

        var validated = NativeCharacterInfluenceResolver.ValidateState(
            state, NativeCharacterSheetResolver.PrimarySheetId, authority);
        if (validated.Status != "resolved")
            return new(validated.Status, null, validated.Issues);

        if (!overlapAuthority.AuthorizesOverlapSelection(state.CharacterId, selection))
            return Fail("unauthorized-overlap-decision",
                "The host did not independently authorize this exact new decision.");

        var proposed = state with
        {
            InfluenceOverlapSelections = prior
                .Where(x => x.Id != replacedDecisionId)
                .Append(selection)
                .ToArray()
        };
        validated = NativeCharacterInfluenceResolver.ValidateState(
            proposed, NativeCharacterSheetResolver.PrimarySheetId, authority);
        return validated.Status == "resolved"
            ? new("planned", NativeCharacterSheetResolver.SnapshotState(proposed), [])
            : new(validated.Status, null, validated.Issues);
    }
}
