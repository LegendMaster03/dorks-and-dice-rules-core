using System.Collections.Frozen;

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
            string.IsNullOrWhiteSpace(targetSheetId) ||
            state.AlternateSheets.Count > 512)
        {
            Issue("invalid-character-state", "character", "A character, primary mechanics and target are required.");
            return Invalid();
        }

        // Enforce transport/work limits before invoking user-supplied host
        // authority callbacks or traversing an arbitrarily deep/large graph.
        // These are safety bounds, not rules for supernatural progression.
        if (!WithinLimits(state))
        {
            Issue("sheet-state-limits", "alternateSheets",
                "Sheet, progression, grant, or override count exceeds evaluator limits.");
            return Invalid();
        }

        if (!authority.AuthorizesPrimary(state.CharacterId, state.PrimaryMechanics))
            Issue("unauthorized-primary", "character.primaryMechanics",
                "The host did not verify the current authoritative primary mechanics.");

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

        foreach (var sheet in byId.Values)
        {
            if (string.IsNullOrWhiteSpace(sheet.ParentId) ||
                (sheet.ParentId != PrimarySheetId && !byId.ContainsKey(sheet.ParentId)))
                Issue("invalid-sheet-parent", "alternateSheets." + sheet.Id + ".parentId",
                    "The parent must be the primary sheet or an existing alternate sheet.");
        }

        // Validate every sheet, including currently unmanifested and unrelated
        // sheets. The three DFS colors (unseen/visiting/complete) detect cycles
        // without assuming parent order. A corrupt graph is never partially trusted.
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

        // Nothing about malformed parent graphs is passed to a trusted host
        // callback. In particular, a host is not asked to recurse into a
        // cycle while verifying sheet provenance.
        if (issues.Count != 0) return Invalid();

        var seenProgression = new HashSet<string>(StringComparer.Ordinal);
        var seenGrants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheet in byId.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var path = "alternateSheets." + sheet.Id;
            // A malformed record must not reach an authorization adapter.
            // The adapter can assume a structurally valid sheet, but only it
            // can prove the acquisition and exact rule or DM authorization.
            if (string.IsNullOrWhiteSpace(sheet.DefinitionId) ||
                sheet.DefinitionSource is null ||
                string.IsNullOrWhiteSpace(sheet.DefinitionSource.Id) ||
                !Enum.IsDefined(sheet.DefinitionSource.Kind) ||
                string.IsNullOrWhiteSpace(sheet.AcquisitionEvidenceId))
            {
                Issue("invalid-sheet-source", path, "Definition and acquisition evidence are required.");
                continue;
            }
            if (!ValidateOverrides(path + ".overrides", sheet.Overrides, Issue))
                continue;
            if (!authority.AuthorizesSheet(state.CharacterId, sheet))
                Issue("unauthorized-sheet", path, "The host did not verify this sheet and its overrides.");
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
                    {
                        Issue("invalid-sheet-progression", progressPath, "Definition, source, choices and evidence must be valid.");
                        continue;
                    }
                    if (!authority.AuthorizesProgression(state.CharacterId, sheet.Id, progression))
                        Issue("unauthorized-progression", progressPath, "The host did not verify this earned advancement.");
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
                            !ValidOccurrence(grant.SourceOccurrence) ||
                            !ValidLevelReference(grant.LevelReference))
                        {
                            Issue("invalid-sheet-grant", grantPath, "Grant definition, source and evidence are required.");
                            continue;
                        }
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
                    if (!ValidDecision(decision.Basis) ||
                        !Enum.IsDefined(decision.State))
                    {
                        Issue("invalid-grant-applicability", path + ".grantApplicability." + decision.GrantId,
                            "Applicability needs a valid state and supported rule/DM reference.");
                        continue;
                    }
                    if (!authority.AuthorizesApplicability(state.CharacterId, sheet.Id, decision))
                        Issue("unauthorized-applicability", path + ".grantApplicability." + decision.GrantId,
                            "The host did not verify this exact applicability decision.");
                }
            }
        }

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

        // Stack root-to-leaf rather than copying complete sheets into each
        // descendant. The latest primary input is read afresh on every call.
        var chain = new Stack<NativePersistentCharacterSheet>();
        var currentId = targetSheetId;
        while (currentId != PrimarySheetId)
        {
            var sheet = byId[currentId];
            chain.Push(sheet);
            currentId = sheet.ParentId;
        }

        var mechanics = CopyMechanics(state.PrimaryMechanics);
        var progressionView = new List<NativeSheetResolvedProgression>();
        var grantView = new List<NativeSheetResolvedGrant>();
        foreach (var sheet in chain)
        {
            mechanics = Apply(mechanics, sheet.Overrides);
            foreach (var progression in sheet.Progression ?? [])
            {
                var detached = CopyProgression(progression);
                progressionView.Add(new(sheet.Id, detached));
                foreach (var grant in detached.Grants)
                    grantView.Add(new(sheet.Id, detached.Id, grant,
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
        return new("resolved", mechanics,
            Array.AsReadOnly(progressionView.ToArray()),
            Array.AsReadOnly(grantView.ToArray()), []);
    }

    /// <summary>
    /// A character can have many forms but no one request may demand unbounded
    /// host callbacks, grant validation, or whole-graph ancestry traversal.
    /// These bounds do not restrict native authored advancement levels.
    /// </summary>
    internal static bool WithinLimits(NativeCharacterState state)
    {
        if (state.AlternateSheets is null || state.AlternateSheets.Count > 512)
            return false;
        long progressionCount = 0, grantCount = 0, decisionCount = 0;
        static bool MapSize<T>(NativeSheetMapPatch<T>? patch) =>
            patch is null || patch.Entries?.Count <= 1024;
        static bool CollectionSize(NativeSheetCollectionPatch<string>? patch) =>
            patch is null || patch.Added?.Count <= 1024 &&
                (patch.Removed?.Count ?? 0) <= 1024;

        foreach (var sheet in state.AlternateSheets)
        {
            if (sheet is null) continue; // Report malformed sheet IDs separately.
            progressionCount += sheet.Progression?.Count ?? 0;
            decisionCount += sheet.GrantApplicability?.Count ?? 0;
            if (progressionCount > 4096 || decisionCount > 16384)
                return false;
            var patch = sheet.Overrides;
            if (patch is not null &&
                (!MapSize(patch.AbilityScores) || !MapSize(patch.Skills) ||
                 !MapSize(patch.Competencies) || !MapSize(patch.DefenseContributions) ||
                 !MapSize(patch.CurrentResources) || !MapSize(patch.HitPointGains) ||
                 !CollectionSize(patch.SelectedOptions) || !CollectionSize(patch.ConditionIds)))
                return false;
            foreach (var progression in sheet.Progression ?? [])
            {
                if (progression is null) continue; // Structural validation reports it.
                grantCount += progression.Grants?.Count ?? 0;
                if (grantCount > 16384 || progression.Choices?.Count > 1024)
                    return false;
            }
        }
        return true;
    }

    private static bool ValidOccurrence(NativeOriginSourceOccurrence? occurrence) =>
        occurrence is not null && occurrence.Source is not null &&
        !string.IsNullOrWhiteSpace(occurrence.Source.Id) &&
        Enum.IsDefined(occurrence.Source.Kind) &&
        !string.IsNullOrWhiteSpace(occurrence.OccurrencePath);

    private static bool ValidLevelReference(NativeSheetLevelReference? reference) =>
        reference is null ||
        (reference.Source switch
        {
            NativeSheetLevelSource.TotalClassLevels => reference.ClassId is null,
            NativeSheetLevelSource.SpecificClassLevel =>
                !string.IsNullOrWhiteSpace(reference.ClassId),
            _ => false
        });

    // Preserve durable ownership/definition identity while detaching nested
    // collections so callers can not mutate saved receipts through a view.
    private static NativeSheetEarnedProgression CopyProgression(NativeSheetEarnedProgression item) =>
        item with
        {
            Grants = Array.AsReadOnly(item.Grants.ToArray()),
            Choices = item.Choices?.ToFrozenDictionary(StringComparer.Ordinal)
        };

    private static bool ValidDecision(NativeOriginDecisionBasis? basis) =>
        basis is not null &&
        basis.Adjudication is NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized &&
        (!string.IsNullOrWhiteSpace(basis.NativeRuleReference) ||
            !string.IsNullOrWhiteSpace(basis.TableDecisionId));

    private static bool ValidateOverrides(string path, NativeCharacterSheetOverrides? patch,
        Action<string, string, string> issue)
    {
        if (patch is null) return true;
        var valid = true;
        void Invalid(string code, string at, string detail)
        {
            valid = false;
            issue(code, at, detail);
        }
        void Map<T>(string name, NativeSheetMapPatch<T>? map)
        {
            if (map is null) return;
            if (map.Entries is null ||
                map.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null))
                Invalid("invalid-sheet-override", path + "." + name, "Map keys and values must be present.");
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
                Invalid("invalid-sheet-override", path + "." + name, "Collection members must be present.");
        }
        Collection("selectedOptions", patch.SelectedOptions);
        Collection("conditionIds", patch.ConditionIds);
        foreach (var map in new[] { patch.Skills, patch.Competencies })
        {
            if (map?.Entries is null) continue;
            foreach (var (key, item) in map.Entries)
                if (item is not null && item.Replacement is { Value: null })
                    Invalid("invalid-sheet-override", path + ".training." + key,
                        "A whole training object replacement may not be null.");
        }
        return valid;
    }

    // Read-only views must not expose mutable host-owned collections. This
    // helper is also used by the narrow Origin adapter when preparing a new
    // derived input; it never persists or reauthorizes character state.
    internal static NativeCharacterRequest CopyMechanics(NativeCharacterRequest source) => source with
    {
        AbilityScores = source.AbilityScores.ToFrozenDictionary(StringComparer.Ordinal),
        Advancements = Array.AsReadOnly(source.Advancements.ToArray()),
        Skills = source.Skills?.ToFrozenDictionary(StringComparer.Ordinal),
        Competencies = source.Competencies?.ToFrozenDictionary(StringComparer.Ordinal),
        DefenseContributions = source.DefenseContributions?.ToFrozenDictionary(StringComparer.Ordinal),
        CurrentResources = source.CurrentResources?.ToFrozenDictionary(StringComparer.Ordinal),
        HitPointGains = source.HitPointGains?.ToFrozenDictionary(StringComparer.Ordinal),
        SelectedOptions = source.SelectedOptions is null
            ? null : Array.AsReadOnly(source.SelectedOptions.ToArray()),
        ConditionIds = source.ConditionIds is null
            ? null : Array.AsReadOnly(source.ConditionIds.ToArray())
    };

    /// <summary>
    /// Creates a detached read-only snapshot of a host-verified character.
    /// The returned partial sheets share immutable record values and source
    /// identities, but never mutable advancement/choice/override collections.
    /// This is a snapshot for a single calculation, not a persistence command.
    /// </summary>
    internal static NativeCharacterState SnapshotState(NativeCharacterState state)
    {
        static NativeSheetMapPatch<T>? SnapshotMap<T>(NativeSheetMapPatch<T>? patch) =>
            patch is null ? null : patch with
            {
                Entries = patch.Entries.ToFrozenDictionary(StringComparer.Ordinal)
            };

        static NativeSheetCollectionPatch<string>? SnapshotCollection(
            NativeSheetCollectionPatch<string>? patch) =>
            patch is null ? null : patch with
            {
                Added = Array.AsReadOnly(patch.Added.ToArray()),
                Removed = patch.Removed is null ? null : Array.AsReadOnly(patch.Removed.ToArray())
            };

        static NativeCharacterSheetOverrides? SnapshotOverrides(NativeCharacterSheetOverrides? patch) =>
            patch is null ? null : patch with
            {
                AbilityScores = SnapshotMap(patch.AbilityScores),
                Skills = SnapshotMap(patch.Skills),
                Competencies = SnapshotMap(patch.Competencies),
                DefenseContributions = SnapshotMap(patch.DefenseContributions),
                CurrentResources = SnapshotMap(patch.CurrentResources),
                HitPointGains = SnapshotMap(patch.HitPointGains),
                SelectedOptions = SnapshotCollection(patch.SelectedOptions),
                ConditionIds = SnapshotCollection(patch.ConditionIds)
            };

        static NativePersistentCharacterSheet SnapshotSheet(NativePersistentCharacterSheet sheet) =>
            sheet with
            {
                Overrides = SnapshotOverrides(sheet.Overrides),
                Progression = sheet.Progression is null ? null :
                    Array.AsReadOnly(sheet.Progression.Select(CopyProgression).ToArray()),
                GrantApplicability = sheet.GrantApplicability is null ? null :
                    Array.AsReadOnly(sheet.GrantApplicability.ToArray())
            };

        return state with
        {
            PrimaryMechanics = CopyMechanics(state.PrimaryMechanics),
            AlternateSheets = Array.AsReadOnly(state.AlternateSheets.Select(SnapshotSheet).ToArray())
        };
    }

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
        return result.ToFrozenDictionary(StringComparer.Ordinal);
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
            // A replacement resets all siblings first. Without it, only the
            // explicitly supplied properties change; an explicit null Ability
            // is distinct from an omitted Ability wrapper.
            var initial = change.Replacement?.Value ?? previous ?? new NativeSkillTraining();
            result[key] = initial with
            {
                Ranks = change.Ranks?.Value ?? initial.Ranks,
                Proficient = change.Proficient?.Value ?? initial.Proficient,
                MiscModifier = change.MiscModifier?.Value ?? initial.MiscModifier,
                Ability = change.Ability is null ? initial.Ability : change.Ability.Value
            };
        }
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string>? Collection(
        IReadOnlyList<string>? parent, NativeSheetCollectionPatch<string>? patch)
    {
        if (patch is null) return parent;
        // Collection removal affects this derived sheet only. It is not a
        // revocation of a separately earned/source-owned entitlement.
        var result = patch.Replace || parent is null
            ? new List<string>() : new List<string>(parent);
        var removed = new HashSet<string>(patch.Removed ?? [], StringComparer.Ordinal);
        result.RemoveAll(removed.Contains);
        foreach (var entry in patch.Added)
            if (!result.Contains(entry, StringComparer.Ordinal))
                result.Add(entry);
        return Array.AsReadOnly(result.ToArray());
    }
}
