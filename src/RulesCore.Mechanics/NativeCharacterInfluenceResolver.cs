using System.Collections.Frozen;
using System.Globalization;

namespace RulesCore.Mechanics;

/// <summary>
/// Read-only composition of separately authorized persistent influences with
/// Increment 1's latest inherited character view. No gameplay transition,
/// primary mutation, class advancement or form activation occurs here.
/// </summary>
public static class NativeCharacterInfluenceResolver
{
    // Transport/work limits, not supernatural mechanics: expression strings
    // are inspected before the trusted host authorizes an influence. Bound
    // their size before performing any decimal predicate parsing.
    private const int MaxExpressionDimensions = 128;
    private const int MaxExpressionNameLength = 160;
    private const int MaxExpressionValueLength = 1024;
    /// <summary>
    /// Verify persisted sheet and influence structure and every source-specific
    /// authority callback without requiring unrelated effective effects to compose.
    /// Mechanical collisions remain unresolved for the normal Resolve path.
    /// </summary>
    public static NativeCharacterInfluenceResolution ValidateState(
        NativeCharacterState state, string targetSheetId,
        INativeCharacterInfluenceAuthority authority) =>
        ResolveInternal(state, targetSheetId, authority, applyMechanics: false);

    public static NativeCharacterInfluenceResolution Resolve(
        NativeCharacterState state, string targetSheetId,
        INativeCharacterInfluenceAuthority authority) =>
        ResolveInternal(state, targetSheetId, authority, applyMechanics: true);

    private static NativeCharacterInfluenceResolution ResolveInternal(
        NativeCharacterState state, string targetSheetId,
        INativeCharacterInfluenceAuthority authority, bool applyMechanics)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(authority);

        // The base resolver verifies the authoritative primary and the entire
        // persistent sheet graph. Do not treat a derived effect as an authorized
        // replacement for primary data.
        var sheet = NativeCharacterSheetResolver.Resolve(state, targetSheetId, authority);
        if (sheet.Status != "resolved" || sheet.EffectiveMechanics is null)
            return new("invalid", null, [], [], [], sheet.Issues);

        var influences = state.PersistentInfluences ?? [];
        var issues = new List<NativeIssue>();
        void Issue(string code, string path, string detail) => issues.Add(new(code, path, detail));
        NativeIssue[] SortedIssues() => issues
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .ThenBy(x => x.Detail, StringComparer.Ordinal).ToArray();
        NativeCharacterInfluenceResolution Failed(string status) =>
            new(status, null, [], [], [], SortedIssues());

        if (influences.Count > 512)
        {
            Issue("influence-state-limits", "persistentInfluences",
                "The number of influences exceeds the resolver's processing limit.");
            return Failed("invalid");
        }

        var sheetIds = new HashSet<string>(state.AlternateSheets.Select(x => x.Id),
            StringComparer.Ordinal) { NativeCharacterSheetResolver.PrimarySheetId };
        var influenceIds = new HashSet<string>(StringComparer.Ordinal);
        // Caller-owned IReadOnlyDictionary implementations can use a
        // case-insensitive comparer. Preserve the stored key spellings while
        // evaluating source-authored expression dimensions ordinally.
        var expressionsById = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            StringComparer.Ordinal);
        var effectIds = new HashSet<string>(StringComparer.Ordinal);
        var effectDefinitions = new Dictionary<string, NativeCharacterInfluenceEffect>(
            StringComparer.Ordinal);
        var overlapSelections = state.InfluenceOverlapSelections ?? [];
        if (overlapSelections.Count > 1024)
        {
            Issue("influence-overlap-limits", "influenceOverlapSelections",
                "The number of source-authored overlap decisions exceeds processing limits.");
            return Failed("invalid");
        }
        var progressionIds = new HashSet<string>(StringComparer.Ordinal);
        var grantIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alternate in state.AlternateSheets)
            foreach (var progression in alternate.Progression ?? [])
            {
                progressionIds.Add(progression.Id);
                foreach (var grant in progression.Grants)
                    grantIds.Add(grant.Id);
            }

        long effectCount = 0, progressionCount = 0, grantCount = 0, decisionCount = 0;
        // Structural validation runs over every influence, including dormant
        // and unrelated ones, before any influence authorization callbacks.
        foreach (var influence in influences)
        {
            if (influence is null || string.IsNullOrWhiteSpace(influence.Id) ||
                !influenceIds.Add(influence.Id) || sheetIds.Contains(influence.Id))
            {
                Issue("invalid-influence-id", "persistentInfluences",
                    "Influence identities must be present, unique and distinct from sheet identities.");
                continue;
            }
            var path = "persistentInfluences." + influence.Id;
            var validExpression = ValidExpression(influence.Expression);
            if (validExpression)
                expressionsById.Add(influence.Id,
                    influence.Expression.ToFrozenDictionary(StringComparer.Ordinal));
            if (string.IsNullOrWhiteSpace(influence.DefinitionId) ||
                !ValidSource(influence.DefinitionSource, requireVersion: true) ||
                string.IsNullOrWhiteSpace(influence.AcquisitionEvidenceId) ||
                string.IsNullOrWhiteSpace(influence.ExpressionEvidenceId) ||
                !validExpression || influence.Effects is null)
                Issue("invalid-influence", path,
                    "Definition, version, acquisition, verified expression and effect list are required.");

            effectCount += influence.Effects?.Count ?? 0;
            progressionCount += influence.Progression?.Count ?? 0;
            decisionCount += influence.GrantApplicability?.Count ?? 0;
            if (effectCount > 16384 || progressionCount > 4096 ||
                decisionCount > 16384)
            {
                Issue("influence-state-limits", path, "Influence record count exceeds processing limits.");
                return Failed("invalid");
            }

            foreach (var effect in influence.Effects ?? [])
            {
                if (effect is null || string.IsNullOrWhiteSpace(effect.Id) ||
                    !effectIds.Add(effect.Id))
                {
                    Issue("invalid-influence-effect-id", path + ".effects",
                        "Effect IDs must be stable and unique across this character.");
                    continue;
                }
                if (!ValidEffect(effect, sheetIds) ||
                    (validExpression &&
                     !NumericExpressionValid(expressionsById[influence.Id], effect.ExpressionConditions)))
                    Issue("invalid-influence-effect", path + ".effects." + effect.Id,
                        "Effect shape, typed operation, expression predicates, prerequisites, source, or sheet target is invalid.");
                else
                    effectDefinitions.Add(effect.Id, effect);
            }

            var localGrants = new HashSet<string>(StringComparer.Ordinal);
            foreach (var progression in influence.Progression ?? [])
            {
                if (progression is null || string.IsNullOrWhiteSpace(progression.Id) ||
                    !progressionIds.Add(progression.Id))
                {
                    Issue("invalid-influence-progression", path + ".progression",
                        "Progression IDs must be present and unique across sheets and influences.");
                    continue;
                }
                var progressionPath = path + ".progression." + progression.Id;
                if (string.IsNullOrWhiteSpace(progression.DefinitionId) ||
                    !ValidOccurrence(progression.SourceOccurrence) ||
                    string.IsNullOrWhiteSpace(progression.AcquisitionEvidenceId) ||
                    progression.Grants is null ||
                    progression.Choices?.Count > 1024 ||
                    progression.Choices?.Any(p => string.IsNullOrWhiteSpace(p.Key) ||
                        string.IsNullOrWhiteSpace(p.Value)) == true)
                {
                    Issue("invalid-influence-progression", progressionPath,
                        "Advancement requires source, acquisition, grants, and valid choices.");
                    continue;
                }
                grantCount += progression.Grants.Count;
                if (grantCount > 16384)
                {
                    Issue("influence-state-limits", progressionPath, "Too many grants.");
                    return Failed("invalid");
                }
                foreach (var grant in progression.Grants)
                {
                    if (grant is null || string.IsNullOrWhiteSpace(grant.Id) ||
                        !grantIds.Add(grant.Id))
                    {
                        Issue("invalid-influence-grant", progressionPath + ".grants",
                            "Grant IDs must be unique across sheets and influences.");
                        continue;
                    }
                    localGrants.Add(grant.Id);
                    if (string.IsNullOrWhiteSpace(grant.DefinitionId) ||
                        !ValidOccurrence(grant.SourceOccurrence) ||
                        string.IsNullOrWhiteSpace(grant.AcquisitionEvidenceId) ||
                        !ValidLevelReference(grant.LevelReference))
                        Issue("invalid-influence-grant", progressionPath + ".grants." + grant.Id,
                            "Grant definition, source, level reference or acquisition is invalid.");
                }
            }
            foreach (var decision in influence.GrantApplicability ?? [])
            {
                if (decision is null || !localGrants.Contains(decision.GrantId) ||
                    !Enum.IsDefined(decision.State) || !ValidDecision(decision.Basis) ||
                    !ValidExpression(decision.RequiredExpression) ||
                    !ValidConditions(decision.ExpressionConditions) ||
                    !ValidEarned(decision.RequiredEarned) ||
                    (validExpression &&
                     !NumericExpressionValid(expressionsById[influence.Id], decision.ExpressionConditions)) ||
                    string.IsNullOrWhiteSpace(decision.AuthorizationEvidenceId))
                    Issue("invalid-influence-applicability", path + ".grantApplicability",
                        "A decision requires a locally owned grant, expression and verified source.");
            }
        }
        // Decisions are checked even while dormant. A claimed rule cannot
        // silently reference a foreign or nonexistent effect, target a
        // different logical property, or choose a writer outside its set.
        var selectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var selection in overlapSelections)
        {
            if (selection is null || string.IsNullOrWhiteSpace(selection.Id) ||
                !selectionIds.Add(selection.Id) ||
                selection.EffectIds is not { Count: >= 2 and <= 64 } ||
                selection.EffectIds.Any(string.IsNullOrWhiteSpace) ||
                selection.EffectIds.Distinct(StringComparer.Ordinal).Count() !=
                    selection.EffectIds.Count ||
                !selection.EffectIds.Contains(selection.SelectedEffectId, StringComparer.Ordinal) ||
                !ValidOccurrence(selection.RuleOccurrence) ||
                !ValidDecision(selection.Basis) ||
                string.IsNullOrWhiteSpace(selection.AuthorizationEvidenceId) ||
                selection.EffectIds.Any(id => !effectDefinitions.ContainsKey(id)) ||
                selection.EffectIds.Select(id => effectDefinitions.TryGetValue(id, out var definition)
                        ? EffectPath(definition) : null)
                    .Distinct(StringComparer.Ordinal).Count() != 1)
                Issue("invalid-influence-overlap-selection", "influenceOverlapSelections",
                    "An overlap decision requires a unique ID, exact existing writer IDs for one property, a selected member, source occurrence and evidence.");
        }
        if (issues.Count != 0) return Failed("invalid");

        // All records are now structurally valid. These checks must consult
        // authenticated character/campaign state; naming a receipt is not proof.
        foreach (var influence in influences.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var path = "persistentInfluences." + influence.Id;
            if (!authority.AuthorizesInfluence(state.CharacterId, influence))
                Issue("unauthorized-influence", path, "Influence acquisition was not verified.");
            if (!authority.AuthorizesExpression(state.CharacterId, influence))
                Issue("unauthorized-expression", path + ".expression",
                    "The current expression change was not independently authorized.");
            foreach (var effect in influence.Effects)
                if (!authority.AuthorizesEffect(state.CharacterId, influence.Id, effect))
                    Issue("unauthorized-influence-effect", path + ".effects." + effect.Id,
                        "The exact authored effect, target and source occurrence were not verified.");
            foreach (var progression in influence.Progression ?? [])
            {
                if (!authority.AuthorizesInfluenceProgression(state.CharacterId, influence.Id, progression))
                    Issue("unauthorized-influence-progression", path + ".progression." + progression.Id,
                        "The advancement acquisition was not verified.");
                foreach (var grant in progression.Grants)
                    if (!authority.AuthorizesInfluenceGrant(state.CharacterId, influence.Id,
                            progression, grant))
                        Issue("unauthorized-influence-grant", path + ".grants." + grant.Id,
                            "The source-qualified earned grant was not verified.");
            }
            foreach (var decision in influence.GrantApplicability ?? [])
                if (!authority.AuthorizesInfluenceApplicability(state.CharacterId, influence.Id, decision))
                    Issue("unauthorized-influence-applicability", path + ".applicability." + decision.GrantId,
                        "The source-qualified current-use decision was not verified.");
        }
        if (overlapSelections.Count > 0)
        {
            if (authority is not INativeCharacterInfluenceOverlapAuthority overlapAuthority)
                Issue("influence-overlap-authority-required", "influenceOverlapSelections",
                    "Source-authored overlap decisions require an independent host verifier.");
            else
                foreach (var selection in overlapSelections.OrderBy(x => x.Id, StringComparer.Ordinal))
                    if (!overlapAuthority.AuthorizesOverlapSelection(state.CharacterId, selection))
                        Issue("unauthorized-influence-overlap-selection",
                            "influenceOverlapSelections." + selection.Id,
                            "The host did not verify the exact writer set, selected effect, rule and scope.");
        }
        if (issues.Count != 0) return Failed("invalid");
        if (!applyMechanics)
            return new("resolved", null, [], [], [], []);

        // Build one verified, source-qualified earned index per influence.
        // Repeated effect and applicability checks must not scan potentially
        // thousands of progression and grant records for every prerequisite.
        var earnedById = influences.ToDictionary(
            x => x.Id, IndexEarned, StringComparer.Ordinal);

        var candidates = influences
            .SelectMany(influence => influence.Effects
                .Where(effect => effect.TargetSheetIds.Contains(targetSheetId, StringComparer.Ordinal) &&
                    Active(effect.RequiredExpression, effect.ExpressionConditions,
                        effect.RequiredEarned, expressionsById[influence.Id],
                        earnedById[influence.Id]))
                .Select(effect => new NativeInfluenceResolvedEffect(influence.Id, effect)))
            .OrderBy(x => EffectPath(x.Effect), StringComparer.Ordinal)
            .ThenBy(x => x.InfluenceId, StringComparer.Ordinal)
            .ThenBy(x => x.Effect.Id, StringComparer.Ordinal)
            .ToArray();

        // With an exact source-verified decision, one active writer can replace
        // the other writers in that collision. No decision, a partial writer
        // set, or multiple matching decisions is unresolved, regardless of
        // caller order. Dormant decisions have no mechanical side effects.
        var displaced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in candidates.GroupBy(x => EffectPath(x.Effect), StringComparer.Ordinal))
        {
            var writers = group.ToArray();
            if (writers.Length < 2) continue;
            var ids = writers.Select(x => x.Effect.Id).OrderBy(x => x,
                StringComparer.Ordinal).ToArray();
            var matches = overlapSelections.Where(x =>
                x.EffectIds.Count == ids.Length &&
                x.EffectIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(ids,
                    StringComparer.Ordinal)).Take(2).ToArray();
            if (matches.Length != 1)
            {
                Issue("influence-overlap-unresolved", group.Key,
                    "Multiple active effects require one verified decision covering the exact collision.");
                continue;
            }
            foreach (var writer in writers)
                if (writer.Effect.Id != matches[0].SelectedEffectId)
                    displaced.Add(writer.Effect.Id);
        }
        if (issues.Count != 0) return Failed("unresolved");
        var applied = candidates.Where(x => !displaced.Contains(x.Effect.Id)).ToArray();

        // An explicitly targeted alternate sheet may have its own local
        // overrides. A supernatural writer overlapping one of those local
        // overrides needs a separately authored interaction decision: never
        // silently let either one win based on evaluation order.
        if (targetSheetId != NativeCharacterSheetResolver.PrimarySheetId)
        {
            var byId = state.AlternateSheets.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var cursor = targetSheetId;
            while (cursor != NativeCharacterSheetResolver.PrimarySheetId)
            {
                var currentSheet = byId[cursor];
                if (currentSheet.Overrides is not null)
                    foreach (var item in applied)
                        if (Conflicts(currentSheet.Overrides, item.Effect))
                            Issue("influence-sheet-overlap-unresolved", EffectPath(item.Effect),
                                "An inherited sheet override and an active influence both write this property.");
                cursor = currentSheet.ParentId;
            }
        }
        if (issues.Count != 0) return Failed("unresolved");

        var effective = NativeCharacterSheetResolver.CopyMechanics(sheet.EffectiveMechanics);
        foreach (var item in applied)
        {
            try
            {
                // Reuse the nested property/collection patch semantics from
                // Increment 1; neither current resources nor saved state changes.
                effective = NativeCharacterSheetResolver.Apply(effective,
                    BuildPatch(effective, item.Effect));
            }
            catch (OverflowException)
            {
                Issue("influence-value-unresolved", EffectPath(item.Effect),
                    "The authorized numerical change exceeds supported integer limits.");
            }
            catch (ArgumentOutOfRangeException)
            {
                Issue("influence-value-unresolved", EffectPath(item.Effect),
                    "The resulting value is outside the native character input bounds.");
            }
            catch (InvalidOperationException)
            {
                Issue("influence-value-unresolved", EffectPath(item.Effect),
                    "A relative change needs an existing inherited numeric property.");
            }
        }
        if (issues.Count != 0) return Failed("unresolved");

        var progressionView = new List<NativeInfluenceResolvedProgression>();
        var grantView = new List<NativeInfluenceResolvedGrant>();
        foreach (var influence in influences.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            // Index only decisions belonging to this verified influence. The
            // naive per-grant scan would be quadratic at the allowed 16K-grant
            // and 16K-decision limits, even for unrelated grant identities.
            var decisions = (influence.GrantApplicability ?? [])
                .ToLookup(x => x.GrantId, StringComparer.Ordinal);
            foreach (var record in influence.Progression ?? [])
            {
                var detached = record with
                {
                    Grants = Array.AsReadOnly(record.Grants.ToArray()),
                    Choices = record.Choices?.ToFrozenDictionary(StringComparer.Ordinal)
                };
                progressionView.Add(new(influence.Id, detached));
                foreach (var grant in detached.Grants)
                {
                    // Only zero, one, or multiple matches matter. Do not
                    // materialize arbitrarily many overlapping decisions.
                    var matching = decisions[grant.Id]
                        .Where(x => Active(x.RequiredExpression, x.ExpressionConditions,
                            x.RequiredEarned, expressionsById[influence.Id],
                            earnedById[influence.Id]))
                        .Take(2).ToArray();
                    if (matching.Length > 1)
                        Issue("influence-grant-applicability-unresolved",
                            "persistentInfluences." + influence.Id + ".grants." + grant.Id,
                            "Overlapping current-use decisions require an explicit interaction rule.");
                    var decision = matching.Length == 1 ? matching[0] : null;
                    grantView.Add(new(influence.Id, detached.Id, grant,
                        decision?.State ?? NativeOriginGrantApplicabilityState.Unresolved,
                        decision?.Basis));
                }
            }
        }

        // Return detached effect metadata; its gates and target lists can not
        // be modified through the resolved view to alter a prior calculation.
        var detachedEffects = applied.Select(x => x with
        {
            Effect = x.Effect with
            {
                RequiredExpression = x.Effect.RequiredExpression.ToFrozenDictionary(StringComparer.Ordinal),
                TargetSheetIds = Array.AsReadOnly(x.Effect.TargetSheetIds.ToArray()),
                ExpressionConditions = Detach(x.Effect.ExpressionConditions),
                RequiredEarned = Detach(x.Effect.RequiredEarned)
            }
        }).ToArray();

        return new(issues.Count == 0 ? "resolved" : "partial", effective,
            Array.AsReadOnly(detachedEffects),
            Array.AsReadOnly(progressionView.ToArray()),
            Array.AsReadOnly(grantView.ToArray()),
            SortedIssues());
    }

    private static bool ValidSource(NativeOriginSourceReference? source, bool requireVersion = false) =>
        source is not null && Enum.IsDefined(source.Kind) &&
        !string.IsNullOrWhiteSpace(source.Id) &&
        (!requireVersion || !string.IsNullOrWhiteSpace(source.Version));

    private static bool ValidOccurrence(NativeOriginSourceOccurrence? occurrence) =>
        occurrence is not null && ValidSource(occurrence.Source) &&
        !string.IsNullOrWhiteSpace(occurrence.OccurrencePath);

    private static bool ValidLevelReference(NativeSheetLevelReference? reference) =>
        reference is null || (reference.Source switch
        {
            NativeSheetLevelSource.TotalClassLevels => reference.ClassId is null,
            NativeSheetLevelSource.SpecificClassLevel => !string.IsNullOrWhiteSpace(reference.ClassId),
            _ => false
        });

    private static bool ValidDecision(NativeOriginDecisionBasis? decision) =>
        decision is not null &&
        decision.Adjudication is NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized &&
        (!string.IsNullOrWhiteSpace(decision.NativeRuleReference) ||
            !string.IsNullOrWhiteSpace(decision.TableDecisionId));

    private static bool ValidExpression(IReadOnlyDictionary<string, string>? expression) =>
        expression is not null && expression.Count <= MaxExpressionDimensions &&
        expression.All(x => !string.IsNullOrWhiteSpace(x.Key) &&
            x.Key.Length <= MaxExpressionNameLength &&
            !string.IsNullOrWhiteSpace(x.Value) &&
            x.Value.Length <= MaxExpressionValueLength);

    private static bool Matches(IReadOnlyDictionary<string, string> required,
        IReadOnlyDictionary<string, string> actual) =>
        required.All(x => actual.TryGetValue(x.Key, out var value) &&
            string.Equals(value, x.Value, StringComparison.Ordinal));

    private static bool Numeric(NativeInfluenceExpressionComparison comparison) =>
        comparison is NativeInfluenceExpressionComparison.GreaterThan or
            NativeInfluenceExpressionComparison.GreaterThanOrEqual or
            NativeInfluenceExpressionComparison.LessThan or
            NativeInfluenceExpressionComparison.LessThanOrEqual;

    private static bool Decimal(string value, out decimal number) =>
        decimal.TryParse(value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out number);

    private static bool ValidConditions(
        IReadOnlyList<NativeInfluenceExpressionCondition>? conditions)
    {
        if (conditions is null) return true;
        if (conditions.Count > MaxExpressionDimensions) return false;
        var seen = new HashSet<NativeInfluenceExpressionCondition>();
        foreach (var condition in conditions)
            if (condition is null || string.IsNullOrWhiteSpace(condition.Dimension) ||
                condition.Dimension.Length > MaxExpressionNameLength ||
                string.IsNullOrWhiteSpace(condition.Value) ||
                condition.Value.Length > MaxExpressionValueLength ||
                !Enum.IsDefined(condition.Comparison) || !seen.Add(condition) ||
                (Numeric(condition.Comparison) && !Decimal(condition.Value, out _)))
                return false;
        return true;
    }

    private static bool NumericExpressionValid(
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyList<NativeInfluenceExpressionCondition>? conditions) =>
        conditions is null || conditions.All(condition =>
            !Numeric(condition.Comparison) ||
            !actual.TryGetValue(condition.Dimension, out var current) ||
            Decimal(current, out _));

    private static bool ValidEarned(
        IReadOnlyList<NativeInfluenceEarnedRequirement>? required)
    {
        if (required is null) return true;
        if (required.Count > 128) return false;
        var seen = new HashSet<NativeInfluenceEarnedRequirement>();
        return required.All(x => x is not null &&
            Enum.IsDefined(x.Kind) &&
            !string.IsNullOrWhiteSpace(x.DefinitionId) &&
            ValidOccurrence(x.Occurrence) && seen.Add(x));
    }

    private static HashSet<NativeInfluenceEarnedRequirement> IndexEarned(
        NativeCharacterPersistentInfluence influence)
    {
        var found = new HashSet<NativeInfluenceEarnedRequirement>();
        foreach (var progression in influence.Progression ?? [])
        {
            found.Add(new(NativeInfluenceEarnedKind.Progression,
                progression.DefinitionId, progression.SourceOccurrence));
            foreach (var grant in progression.Grants)
                found.Add(new(NativeInfluenceEarnedKind.Grant,
                    grant.DefinitionId, grant.SourceOccurrence));
        }
        return found;
    }

    private static bool HasEarned(
        IReadOnlySet<NativeInfluenceEarnedRequirement> earned,
        IReadOnlyList<NativeInfluenceEarnedRequirement>? required) =>
        required is null || required.All(earned.Contains);

    private static bool ConditionsMatch(
        IReadOnlyList<NativeInfluenceExpressionCondition>? conditions,
        IReadOnlyDictionary<string, string> expression)
    {
        if (conditions is null) return true;
        foreach (var condition in conditions)
        {
            if (!expression.TryGetValue(condition.Dimension, out var actual))
                return false;
            if (condition.Comparison == NativeInfluenceExpressionComparison.Equals)
            {
                if (!string.Equals(actual, condition.Value, StringComparison.Ordinal)) return false;
                continue;
            }
            if (condition.Comparison == NativeInfluenceExpressionComparison.NotEquals)
            {
                if (string.Equals(actual, condition.Value, StringComparison.Ordinal)) return false;
                continue;
            }
            if (!Decimal(actual, out var current) ||
                !Decimal(condition.Value, out var threshold))
                return false;
            var matched = condition.Comparison switch
            {
                NativeInfluenceExpressionComparison.GreaterThan => current > threshold,
                NativeInfluenceExpressionComparison.GreaterThanOrEqual => current >= threshold,
                NativeInfluenceExpressionComparison.LessThan => current < threshold,
                NativeInfluenceExpressionComparison.LessThanOrEqual => current <= threshold,
                _ => false
            };
            if (!matched) return false;
        }
        return true;
    }

    private static bool Active(
        IReadOnlyDictionary<string, string> exact,
        IReadOnlyList<NativeInfluenceExpressionCondition>? conditions,
        IReadOnlyList<NativeInfluenceEarnedRequirement>? required,
        IReadOnlyDictionary<string, string> expression,
        IReadOnlySet<NativeInfluenceEarnedRequirement> earned) =>
        Matches(exact, expression) &&
        ConditionsMatch(conditions, expression) &&
        HasEarned(earned, required);

    private static IReadOnlyList<T>? Detach<T>(IReadOnlyList<T>? values) =>
        values is null ? null : Array.AsReadOnly(values.ToArray());

    /// <summary>
    /// Shared effect-shape checks for persisted influences and source-authored
    /// progression proposals; neither caller invents a second validation policy.
    /// </summary>
    internal static bool ValidEffect(NativeCharacterInfluenceEffect effect,
        HashSet<string> sheets)
    {
        if (!ValidOccurrence(effect.DefinitionOccurrence) ||
            !ValidDecision(effect.Basis) ||
            string.IsNullOrWhiteSpace(effect.AuthorizationEvidenceId) ||
            !ValidExpression(effect.RequiredExpression) ||
            !ValidConditions(effect.ExpressionConditions) ||
            !ValidEarned(effect.RequiredEarned) ||
            effect.TargetSheetIds is null || effect.TargetSheetIds.Count == 0 ||
            effect.TargetSheetIds.Count > 513 ||
            effect.TargetSheetIds.Distinct(StringComparer.Ordinal).Count() != effect.TargetSheetIds.Count ||
            effect.TargetSheetIds.Any(x => string.IsNullOrWhiteSpace(x) || !sheets.Contains(x)) ||
            !Enum.IsDefined(effect.Characteristic) || !Enum.IsDefined(effect.Operation))
            return false;

        var isSpeed = effect.Characteristic == NativeInfluenceCharacteristic.BaseWalkSpeed;
        var isCondition = effect.Characteristic == NativeInfluenceCharacteristic.ConditionId;
        if (isSpeed ? effect.Key is not null : string.IsNullOrWhiteSpace(effect.Key))
            return false;
        if (isCondition)
            return (effect.Operation is NativeInfluenceOperation.Add or NativeInfluenceOperation.Suppress) &&
                effect.NumericValue is null && effect.BooleanValue is null && effect.TextValue is null;

        var isBool = effect.Characteristic is NativeInfluenceCharacteristic.SkillProficient
            or NativeInfluenceCharacteristic.CompetencyProficient;
        if (isBool)
            return effect.Operation == NativeInfluenceOperation.Replace &&
                effect.BooleanValue is not null && effect.NumericValue is null && effect.TextValue is null;

        var isText = effect.Characteristic is NativeInfluenceCharacteristic.SkillAbility
            or NativeInfluenceCharacteristic.CompetencyAbility;
        if (isText)
            return (effect.Operation is NativeInfluenceOperation.Replace or NativeInfluenceOperation.Suppress) &&
                (effect.Operation == NativeInfluenceOperation.Suppress
                    ? effect.TextValue is null : !string.IsNullOrWhiteSpace(effect.TextValue)) &&
                effect.NumericValue is null && effect.BooleanValue is null;

        if (isSpeed && effect.Operation == NativeInfluenceOperation.Suppress)
            return effect.NumericValue is null && effect.BooleanValue is null &&
                effect.TextValue is null;

        return (effect.Operation is NativeInfluenceOperation.Replace or NativeInfluenceOperation.Adjust) &&
            effect.NumericValue is not null && effect.BooleanValue is null && effect.TextValue is null;
    }

    internal static bool ValidAuthoredEffect(
        NativeCharacterInfluenceEffect effect,
        IReadOnlyDictionary<string, string> expression,
        HashSet<string> sheets) =>
        ValidEffect(effect with { AuthorizationEvidenceId = "planner.structural-validation" },
            sheets) &&
        NumericExpressionValid(expression, effect.ExpressionConditions);

    private static string EffectPath(NativeCharacterInfluenceEffect effect)
    {
        var key = effect.Key;
        return effect.Characteristic switch
        {
            NativeInfluenceCharacteristic.AbilityScore => "abilityScores." + key,
            NativeInfluenceCharacteristic.SkillRanks => "skills." + key + ".ranks",
            NativeInfluenceCharacteristic.SkillProficient => "skills." + key + ".proficient",
            NativeInfluenceCharacteristic.SkillMiscModifier => "skills." + key + ".miscModifier",
            NativeInfluenceCharacteristic.SkillAbility => "skills." + key + ".ability",
            NativeInfluenceCharacteristic.CompetencyRanks => "competencies." + key + ".ranks",
            NativeInfluenceCharacteristic.CompetencyProficient => "competencies." + key + ".proficient",
            NativeInfluenceCharacteristic.CompetencyMiscModifier => "competencies." + key + ".miscModifier",
            NativeInfluenceCharacteristic.CompetencyAbility => "competencies." + key + ".ability",
            NativeInfluenceCharacteristic.DefenseContribution => "defenseContributions." + key,
            NativeInfluenceCharacteristic.HitPointGain => "hitPointGains." + key,
            NativeInfluenceCharacteristic.BaseWalkSpeed => "baseWalkSpeed",
            NativeInfluenceCharacteristic.ConditionId => "conditionIds." + key,
            _ => throw new InvalidOperationException("Invalid effect target.")
        };
    }

    private static bool Conflicts(NativeCharacterSheetOverrides patch,
        NativeCharacterInfluenceEffect effect)
    {
        var key = effect.Key!;
        static bool Map<T>(NativeSheetMapPatch<T>? map, string name) =>
            map is not null && (map.Replace || map.Entries.ContainsKey(name));
        static bool Training(NativeSheetMapPatch<NativeSheetTrainingPatch>? map,
            string name, NativeInfluenceCharacteristic target)
        {
            if (map is null) return false;
            if (map.Replace) return true;
            if (!map.Entries.TryGetValue(name, out var change)) return false;
            if (change.Replacement is not null) return true;
            return target switch
            {
                NativeInfluenceCharacteristic.SkillRanks or
                    NativeInfluenceCharacteristic.CompetencyRanks => change.Ranks is not null,
                NativeInfluenceCharacteristic.SkillProficient or
                    NativeInfluenceCharacteristic.CompetencyProficient => change.Proficient is not null,
                NativeInfluenceCharacteristic.SkillMiscModifier or
                    NativeInfluenceCharacteristic.CompetencyMiscModifier => change.MiscModifier is not null,
                NativeInfluenceCharacteristic.SkillAbility or
                    NativeInfluenceCharacteristic.CompetencyAbility => change.Ability is not null,
                _ => false
            };
        }
        return effect.Characteristic switch
        {
            NativeInfluenceCharacteristic.AbilityScore => Map(patch.AbilityScores, key),
            NativeInfluenceCharacteristic.SkillRanks or
            NativeInfluenceCharacteristic.SkillProficient or
            NativeInfluenceCharacteristic.SkillMiscModifier or
            NativeInfluenceCharacteristic.SkillAbility =>
                Training(patch.Skills, key, effect.Characteristic),
            NativeInfluenceCharacteristic.CompetencyRanks or
            NativeInfluenceCharacteristic.CompetencyProficient or
            NativeInfluenceCharacteristic.CompetencyMiscModifier or
            NativeInfluenceCharacteristic.CompetencyAbility =>
                Training(patch.Competencies, key, effect.Characteristic),
            NativeInfluenceCharacteristic.DefenseContribution => Map(patch.DefenseContributions, key),
            NativeInfluenceCharacteristic.HitPointGain => Map(patch.HitPointGains, key),
            NativeInfluenceCharacteristic.BaseWalkSpeed => patch.BaseWalkSpeed is not null,
            NativeInfluenceCharacteristic.ConditionId => patch.ConditionIds is not null &&
                (patch.ConditionIds.Replace ||
                 patch.ConditionIds.Added.Contains(key, StringComparer.Ordinal) ||
                 (patch.ConditionIds.Removed?.Contains(key, StringComparer.Ordinal) ?? false)),
            _ => false
        };
    }

    private static int? Lookup(IReadOnlyDictionary<string, int>? values, string key) =>
        values is not null && values.TryGetValue(key, out var value) ? value : null;

    // These are the *native character projector's input bounds*, not new
    // supernatural balance rules. A syntactically authorized effect must not
    // produce a "resolved" input that the next projector immediately rejects.
    // Validate the final value, not just the delta, because a lawful delta can
    // push an inherited value out of range.
    private static int WithinNativeInputBounds(NativeInfluenceCharacteristic characteristic,
        int value)
    {
        const int maxMagnitude = 1_000_000;
        var valid = characteristic switch
        {
            NativeInfluenceCharacteristic.AbilityScore => value is >= 0 and <= 1000,
            NativeInfluenceCharacteristic.SkillRanks or
                NativeInfluenceCharacteristic.CompetencyRanks or
                NativeInfluenceCharacteristic.HitPointGain or
                NativeInfluenceCharacteristic.BaseWalkSpeed => value is >= 0 and <= maxMagnitude,
            NativeInfluenceCharacteristic.SkillMiscModifier or
                NativeInfluenceCharacteristic.CompetencyMiscModifier or
                NativeInfluenceCharacteristic.DefenseContribution =>
                value is >= -maxMagnitude and <= maxMagnitude,
            _ => true
        };
        if (!valid)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Derived input violates the native character projector's numeric limits.");
        return value;
    }

    private static NativeCharacterSheetOverrides BuildPatch(
        NativeCharacterRequest current, NativeCharacterInfluenceEffect effect)
    {
        var c = effect.Characteristic;
        var key = effect.Key!;
        int Number(int? old)
        {
            var result = effect.Operation == NativeInfluenceOperation.Adjust
                ? checked((old ?? throw new InvalidOperationException()) + effect.NumericValue!.Value)
                : effect.NumericValue!.Value;
            return WithinNativeInputBounds(c, result);
        }

        if (c == NativeInfluenceCharacteristic.AbilityScore)
            return new(AbilityScores: new(false,
                new Dictionary<string, int> { [key] = Number(Lookup(current.AbilityScores, key)) }));
        if (c == NativeInfluenceCharacteristic.DefenseContribution)
            return new(DefenseContributions: new(false,
                new Dictionary<string, int> { [key] = Number(Lookup(current.DefenseContributions, key)) }));
        if (c == NativeInfluenceCharacteristic.HitPointGain)
            return new(HitPointGains: new(false,
                new Dictionary<string, int> { [key] = Number(Lookup(current.HitPointGains, key)) }));
        if (c == NativeInfluenceCharacteristic.BaseWalkSpeed)
            return new(BaseWalkSpeed: new(
                effect.Operation == NativeInfluenceOperation.Suppress
                    ? (int?)null : Number(current.BaseWalkSpeed)));
        if (c == NativeInfluenceCharacteristic.ConditionId)
            return new(ConditionIds: new(false,
                effect.Operation == NativeInfluenceOperation.Add ? [key] : [],
                effect.Operation == NativeInfluenceOperation.Suppress ? [key] : []));

        var isSkill = c is NativeInfluenceCharacteristic.SkillRanks
            or NativeInfluenceCharacteristic.SkillProficient
            or NativeInfluenceCharacteristic.SkillMiscModifier
            or NativeInfluenceCharacteristic.SkillAbility;
        var existingMap = isSkill ? current.Skills : current.Competencies;
        NativeSkillTraining? existing = null;
        if (existingMap is not null)
            existingMap.TryGetValue(key, out existing);
        NativeSheetTrainingPatch training = c switch
        {
            NativeInfluenceCharacteristic.SkillRanks or NativeInfluenceCharacteristic.CompetencyRanks =>
                new(Ranks: new(Number(existing?.Ranks))),
            NativeInfluenceCharacteristic.SkillProficient or NativeInfluenceCharacteristic.CompetencyProficient =>
                new(Proficient: new(effect.BooleanValue!.Value)),
            NativeInfluenceCharacteristic.SkillMiscModifier or NativeInfluenceCharacteristic.CompetencyMiscModifier =>
                new(MiscModifier: new(Number(existing?.MiscModifier))),
            NativeInfluenceCharacteristic.SkillAbility or NativeInfluenceCharacteristic.CompetencyAbility =>
                new(Ability: new(effect.Operation == NativeInfluenceOperation.Suppress
                    ? null : effect.TextValue)),
            _ => throw new InvalidOperationException("Unsupported nested characteristic.")
        };
        var change = new NativeSheetMapPatch<NativeSheetTrainingPatch>(false,
            new Dictionary<string, NativeSheetTrainingPatch> { [key] = training });
        return isSkill ? new(Skills: change) : new(Competencies: change);
    }
}
