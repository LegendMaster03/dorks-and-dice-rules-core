using System.Collections.Frozen;

namespace RulesCore.Mechanics;

/// <summary>
/// Read-only composition of separately authorized persistent influences with
/// Increment 1's latest inherited character view. No gameplay transition,
/// primary mutation, class advancement or form activation occurs here.
/// </summary>
public static class NativeCharacterInfluenceResolver
{
    public static NativeCharacterInfluenceResolution Resolve(
        NativeCharacterState state, string targetSheetId,
        INativeCharacterInfluenceAuthority authority)
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
        var effectIds = new HashSet<string>(StringComparer.Ordinal);
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
            if (string.IsNullOrWhiteSpace(influence.DefinitionId) ||
                !ValidSource(influence.DefinitionSource, requireVersion: true) ||
                string.IsNullOrWhiteSpace(influence.AcquisitionEvidenceId) ||
                string.IsNullOrWhiteSpace(influence.ExpressionEvidenceId) ||
                !ValidExpression(influence.Expression) || influence.Effects is null)
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
                if (!ValidEffect(effect, sheetIds))
                    Issue("invalid-influence-effect", path + ".effects." + effect.Id,
                        "Effect shape, typed operation, source, decision, expression, or sheet target is invalid.");
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
                    string.IsNullOrWhiteSpace(decision.AuthorizationEvidenceId))
                    Issue("invalid-influence-applicability", path + ".grantApplicability",
                        "A decision requires a locally owned grant, expression and verified source.");
            }
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
        if (issues.Count != 0) return Failed("invalid");

        var applied = influences
            .SelectMany(influence => influence.Effects
                .Where(effect => effect.TargetSheetIds.Contains(targetSheetId, StringComparer.Ordinal) &&
                    Matches(effect.RequiredExpression, influence.Expression))
                .Select(effect => new NativeInfluenceResolvedEffect(influence.Id, effect)))
            .OrderBy(x => EffectPath(x.Effect), StringComparer.Ordinal)
            .ThenBy(x => x.InfluenceId, StringComparer.Ordinal)
            .ThenBy(x => x.Effect.Id, StringComparer.Ordinal)
            .ToArray();

        // Do not infer additive stacking or precedence, even where both
        // definitions are individually authorized. One logical property has
        // at most one active influence writer in this increment.
        foreach (var group in applied.GroupBy(x => EffectPath(x.Effect), StringComparer.Ordinal))
            if (group.Count() > 1)
                Issue("influence-overlap-unresolved", group.Key,
                    "Multiple active effects target this property without a verified interaction rule.");
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
                    var matching = (influence.GrantApplicability ?? [])
                        .Where(x => x.GrantId == grant.Id &&
                            Matches(x.RequiredExpression, influence.Expression))
                        .ToArray();
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
                TargetSheetIds = Array.AsReadOnly(x.Effect.TargetSheetIds.ToArray())
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
        expression is not null && expression.Count <= 128 &&
        expression.All(x => !string.IsNullOrWhiteSpace(x.Key) &&
            !string.IsNullOrWhiteSpace(x.Value));

    private static bool Matches(IReadOnlyDictionary<string, string> required,
        IReadOnlyDictionary<string, string> actual) =>
        required.All(x => actual.TryGetValue(x.Key, out var value) &&
            string.Equals(value, x.Value, StringComparison.Ordinal));

    private static bool ValidEffect(NativeCharacterInfluenceEffect effect,
        HashSet<string> sheets)
    {
        if (!ValidOccurrence(effect.DefinitionOccurrence) ||
            !ValidDecision(effect.Basis) ||
            string.IsNullOrWhiteSpace(effect.AuthorizationEvidenceId) ||
            !ValidExpression(effect.RequiredExpression) ||
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

    private static NativeCharacterSheetOverrides BuildPatch(
        NativeCharacterRequest current, NativeCharacterInfluenceEffect effect)
    {
        var c = effect.Characteristic;
        var key = effect.Key!;
        int Number(int? old) => effect.Operation == NativeInfluenceOperation.Adjust
            ? checked((old ?? throw new InvalidOperationException()) + effect.NumericValue!.Value)
            : effect.NumericValue!.Value;

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
