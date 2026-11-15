namespace RulesCore.Mechanics;

/// <summary>
/// Resolves one persistent alternate sheet against the latest supplied primary
/// mechanics and its single-parent inheritance chain. The host supplies verified
/// state and a real authority adapter; resolution never executes a state change,
/// awards grants, spends resources, or determines which form is manifested.
/// </summary>
public static class NativeCharacterSheetResolver
{
    public const string PrimarySheetId = "@primary";

    public static NativeCharacterSheetResolution Resolve(
        NativeCharacterState state, string targetSheetId,
        INativeCharacterSheetAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(authority);
        var issues = new List<NativeIssue>();
        void Issue(string code, string path, string detail) => issues.Add(new(code, path, detail));
        NativeCharacterSheetResolution Invalid() => new(
            "invalid", null, [], [], issues
                .OrderBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Detail, StringComparer.Ordinal).ToArray());

        if (string.IsNullOrWhiteSpace(state.CharacterId) ||
            state.PrimaryMechanics is null ||
            state.PrimaryMechanics.AbilityScores is null ||
            state.PrimaryMechanics.Advancements is null ||
            state.AlternateSheets is null ||
            string.IsNullOrWhiteSpace(targetSheetId))
        {
            Issue("invalid-character-state", "character", "A character, primary mechanics and target are required.");
            return Invalid();
        }

        var byId = new Dictionary<string, NativePersistentCharacterSheet>(StringComparer.Ordinal);
        foreach (var sheet in state.AlternateSheets)
        {
            if (sheet is null || string.IsNullOrWhiteSpace(sheet.Id) ||
                sheet.Id == PrimarySheetId)
            {
                Issue("invalid-sheet-id", "alternateSheets", "Sheet identity is missing or reserved.");
                continue;
            }
            if (!byId.TryAdd(sheet.Id, sheet))
                Issue("duplicate-sheet-id", "alternateSheets." + sheet.Id, "An alternate sheet ID must be unique.");
        }

        var seenProgression = new HashSet<string>(StringComparer.Ordinal);
        var seenGrants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheet in byId.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var path = "alternateSheets." + sheet.Id;
            if (string.IsNullOrWhiteSpace(sheet.ParentId) ||
                (sheet.ParentId != PrimarySheetId && !byId.ContainsKey(sheet.ParentId)))
                Issue("invalid-sheet-parent", path + ".parentId", "The parent must be the primary sheet or an existing alternate sheet.");
            if (string.IsNullOrWhiteSpace(sheet.DefinitionId) ||
                sheet.DefinitionSource is null ||
                string.IsNullOrWhiteSpace(sheet.DefinitionSource.Id) ||
                string.IsNullOrWhiteSpace(sheet.AcquisitionEvidenceId))
                Issue("invalid-sheet-source", path, "Definition and acquisition evidence are required.");
            if (!authority.AuthorizesSheet(state.CharacterId, sheet))
                Issue("unauthorized-sheet", path, "The host did not verify this sheet and its overrides.");
            ValidateOverrides(path + ".overrides", sheet.Overrides, Issue);
            if (sheet.Progression is not null)
            {
                foreach (var progression in sheet.Progression)
                {
                    if (progression is null || string.IsNullOrWhiteSpace(progression.Id))
                    {
                        Issue("invalid-sheet-progression", path + ".progression", "Progression identity is required.");
                        continue;
                    }
                    var progressPath = path + ".progression." + progression.Id;
                    if (!seenProgression.Add(progression.Id))
                        Issue("duplicate-progression-id", progressPath, "An advancement must have one authoritative owner.");
                    if (string.IsNullOrWhiteSpace(progression.DefinitionId) ||
                        string.IsNullOrWhiteSpace(progression.AcquisitionEvidenceId) ||
                        !ValidOccurrence(progression.SourceOccurrence) ||
                        progression.Grants is null ||
                        progression.Choices?.Any(choice => string.IsNullOrWhiteSpace(choice.Key) ||
                            string.IsNullOrWhiteSpace(choice.Value)) == true)
                        Issue("invalid-sheet-progression", progressPath, "Definition, source, choices and evidence must be valid.");
                    if (!authority.AuthorizesProgression(state.CharacterId, sheet.Id, progression))
                        Issue("unauthorized-progression", progressPath, "The host did not verify this earned advancement.");
                    if (progression.Grants is null) continue;
                    foreach (var grant in progression.Grants)
                    {
                        if (grant is null || string.IsNullOrWhiteSpace(grant.Id))
                        {
                            Issue("invalid-sheet-grant", progressPath + ".grants", "An earned grant ID is required.");
                            continue;
                        }
                        var grantPath = progressPath + ".grants." + grant.Id;
                        if (!seenGrants.Add(grant.Id))
                            Issue("duplicate-grant-id", grantPath, "A durable grant identity must have one authoritative owner.");
                        if (string.IsNullOrWhiteSpace(grant.DefinitionId) ||
                            string.IsNullOrWhiteSpace(grant.AcquisitionEvidenceId) ||
                            !ValidOccurrence(grant.SourceOccurrence))
                            Issue("invalid-sheet-grant", grantPath, "Grant definition, source and evidence are required.");
                        if (!authority.AuthorizesGrant(state.CharacterId, sheet.Id, progression, grant))
                            Issue("unauthorized-grant", grantPath, "The host did not verify the source-qualified entitlement.");
                    }
                }
            }
            if (sheet.GrantApplicability is not null)
            {
                var local = new HashSet<string>(StringComparer.Ordinal);
                foreach (var decision in sheet.GrantApplicability)
                {
                    if (decision is null || string.IsNullOrWhiteSpace(decision.GrantId))
                    {
                        Issue("invalid-grant-applicability", path + ".grantApplicability", "A grant ID is required.");
                        continue;
                    }
                    if (!local.Add(decision.GrantId))
                        Issue("duplicate-grant-applicability", path + ".grantApplicability." + decision.GrantId,
                            "The same sheet can not decide one entitlement twice.");
                    if (!ValidDecision(decision.Basis))
                        Issue("invalid-grant-applicability", path + ".grantApplicability." + decision.GrantId,
                            "Applicability needs verified rule or DM decision evidence.");
                }
            }
        }

        // Validate every sheet, including currently unmanifested and unrelated
        // sheets. A corrupt graph is not treated as partially authoritative.
        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (colors.TryGetValue(id, out var color))
            {
                if (color == 1) Issue("sheet-inheritance-cycle", "alternateSheets." + id,
                    "An alternate sheet may not be its own ancestor.");
                return;
            }
            colors[id] = 1;
            var parent = byId[id].ParentId;
            if (parent != PrimarySheetId && byId.ContainsKey(parent))
                Visit(parent);
            colors[id] = 2;
        }
        foreach (var id in byId.Keys.OrderBy(id => id, StringComparer.Ordinal)) Visit(id);

        if (targetSheetId != PrimarySheetId && !byId.ContainsKey(targetSheetId))
            Issue("missing-target-sheet", "targetSheetId", "The requested alternate sheet does not exist.");

        // Applicability decisions must address a grant owned by this sheet or
        // one of its ancestors, never a sibling or an arbitrary client ID.
        if (issues.Count == 0)
        {
            foreach (var sheet in byId.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                var available = new HashSet<string>(StringComparer.Ordinal);
                var cursor = sheet;
                while (true)
                {
                    foreach (var progress in cursor.Progression ?? [])
                        foreach (var grant in progress.Grants)
                            available.Add(grant.Id);
                    if (cursor.ParentId == PrimarySheetId) break;
                    cursor = byId[cursor.ParentId];
                }
                foreach (var decision in sheet.GrantApplicability ?? [])
                    if (!available.Contains(decision.GrantId))
                        Issue("unknown-applicability-grant", "alternateSheets." + sheet.Id +
                            ".grantApplicability." + decision.GrantId,
                            "A sheet can only determine applicability of an inherited entitlement.");
            }
        }
        if (issues.Count != 0) return Invalid();

        var chain = new Stack<NativePersistentCharacterSheet>();
        var currentId = targetSheetId;
        while (currentId != PrimarySheetId)
        {
            var sheet = byId[currentId];
            chain.Push(sheet);
            currentId = sheet.ParentId;
        }

        var mechanics = Copy(state.PrimaryMechanics);
        var progressionView = new List<NativeSheetResolvedProgression>();
        var grantView = new List<NativeSheetResolvedGrant>();
        foreach (var sheet in chain)
        {
            mechanics = Apply(mechanics, sheet.Overrides);
            foreach (var progression in sheet.Progression ?? [])
            {
                progressionView.Add(new(sheet.Id, progression));
                foreach (var grant in progression.Grants)
                    grantView.Add(new(sheet.Id, progression.Id, grant,
                        NativeOriginGrantApplicabilityState.Unresolved, null));
            }
            foreach (var decision in sheet.GrantApplicability ?? [])
            {
                var index = grantView.FindIndex(item => item.Entitlement.Id == decision.GrantId);
                // The graph validation above guarantees index >= 0.
                grantView[index] = grantView[index] with
                {
                    Applicability = decision.State,
                    ApplicabilityBasis = decision.Basis
                };
            }
        }
        return new("resolved", mechanics, progressionView.ToArray(), grantView.ToArray(), []);
    }

    private static bool ValidOccurrence(NativeOriginSourceOccurrence? occurrence) =>
        occurrence is not null && occurrence.Source is not null &&
        !string.IsNullOrWhiteSpace(occurrence.Source.Id) &&
        !string.IsNullOrWhiteSpace(occurrence.OccurrencePath);

    private static bool ValidDecision(NativeOriginDecisionBasis? basis) =>
        basis is not null &&
        basis.Adjudication is NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized &&
        (!string.IsNullOrWhiteSpace(basis.NativeRuleReference) ||
            !string.IsNullOrWhiteSpace(basis.TableDecisionId));

    private static void ValidateOverrides(string path, NativeCharacterSheetOverrides? patch,
        Action<string, string, string> issue)
    {
        if (patch is null) return;
        void Map<T>(string name, NativeSheetMapPatch<T>? map)
        {
            if (map is null) return;
            if (map.Entries is null ||
                map.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null))
                issue("invalid-sheet-override", path + "." + name, "Map keys and values must be present.");
        }
        Map("abilityScores", patch.AbilityScores);
        Map("skills", patch.Skills);
        Map("competencies", patch.Competencies);
        Map("defenseContributions", patch.DefenseContributions);
        Map("currentResources", patch.CurrentResources);
        Map("hitPointGains", patch.HitPointGains);
        void Collection(string name, NativeSheetCollectionPatch<string>? collection)
        {
            if (collection is null) return;
            if (collection.Added is null || collection.Added.Any(string.IsNullOrWhiteSpace) ||
                collection.Removed?.Any(string.IsNullOrWhiteSpace) == true)
                issue("invalid-sheet-override", path + "." + name, "Collection members must be present.");
        }
        Collection("selectedOptions", patch.SelectedOptions);
        Collection("conditionIds", patch.ConditionIds);
        foreach (var map in new[] { patch.Skills, patch.Competencies })
        {
            if (map?.Entries is null) continue;
            foreach (var (key, item) in map.Entries)
                if (item is not null && item.Replacement is { Value: null })
                    issue("invalid-sheet-override", path + ".training." + key,
                        "A whole training object replacement may not be null.");
        }
    }

    private static NativeCharacterRequest Copy(NativeCharacterRequest source) => source with
    {
        AbilityScores = source.AbilityScores.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Advancements = source.Advancements.ToArray(),
        Skills = source.Skills is null ? null :
            source.Skills.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Competencies = source.Competencies is null ? null :
            source.Competencies.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        DefenseContributions = source.DefenseContributions is null ? null :
            source.DefenseContributions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        CurrentResources = source.CurrentResources is null ? null :
            source.CurrentResources.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        HitPointGains = source.HitPointGains is null ? null :
            source.HitPointGains.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        SelectedOptions = source.SelectedOptions?.ToArray(),
        ConditionIds = source.ConditionIds?.ToArray()
    };

    private static NativeCharacterRequest Apply(NativeCharacterRequest parent,
        NativeCharacterSheetOverrides? patch)
    {
        if (patch is null) return parent;
        return parent with
        {
            AbilityScores = Map(parent.AbilityScores, patch.AbilityScores)!,
            Skills = Training(parent.Skills, patch.Skills),
            Competencies = Training(parent.Competencies, patch.Competencies),
            DefenseContributions = Map(parent.DefenseContributions, patch.DefenseContributions),
            CurrentResources = Map(parent.CurrentResources, patch.CurrentResources),
            HitPointGains = Map(parent.HitPointGains, patch.HitPointGains),
            SelectedOptions = Collection(parent.SelectedOptions, patch.SelectedOptions),
            ConditionIds = Collection(parent.ConditionIds, patch.ConditionIds),
            BaseWalkSpeed = patch.BaseWalkSpeed is null
                ? parent.BaseWalkSpeed : patch.BaseWalkSpeed.Value
        };
    }

    private static IReadOnlyDictionary<string, T>? Map<T>(
        IReadOnlyDictionary<string, T>? parent, NativeSheetMapPatch<T>? patch)
    {
        if (patch is null) return parent;
        var result = patch.Replace || parent is null
            ? new Dictionary<string, T>(StringComparer.Ordinal)
            : parent.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var pair in patch.Entries)
            result[pair.Key] = pair.Value;
        return result;
    }

    private static IReadOnlyDictionary<string, NativeSkillTraining>? Training(
        IReadOnlyDictionary<string, NativeSkillTraining>? parent,
        NativeSheetMapPatch<NativeSheetTrainingPatch>? patch)
    {
        if (patch is null) return parent;
        var result = patch.Replace || parent is null
            ? new Dictionary<string, NativeSkillTraining>(StringComparer.Ordinal)
            : parent.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var (key, change) in patch.Entries)
        {
            result.TryGetValue(key, out var previous);
            var initial = change.Replacement?.Value ?? previous ?? new NativeSkillTraining();
            result[key] = initial with
            {
                Ranks = change.Ranks?.Value ?? initial.Ranks,
                Proficient = change.Proficient?.Value ?? initial.Proficient,
                MiscModifier = change.MiscModifier?.Value ?? initial.MiscModifier,
                Ability = change.Ability is null ? initial.Ability : change.Ability.Value
            };
        }
        return result;
    }

    private static IReadOnlyList<string>? Collection(
        IReadOnlyList<string>? parent, NativeSheetCollectionPatch<string>? patch)
    {
        if (patch is null) return parent;
        var result = patch.Replace || parent is null
            ? new List<string>() : new List<string>(parent);
        var removed = new HashSet<string>(patch.Removed ?? [], StringComparer.Ordinal);
        result.RemoveAll(removed.Contains);
        foreach (var entry in patch.Added)
            if (!result.Contains(entry, StringComparer.Ordinal))
                result.Add(entry);
        return result.ToArray();
    }
}
