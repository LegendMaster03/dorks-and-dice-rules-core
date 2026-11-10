using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A two-bit equipment training state. Mastery alone is an authored entitlement,
/// not a usable ability: effective mastery requires both bits (value 3).
/// </summary>
[Flags]
public enum NativeEquipmentTrainingFlags : byte
{
    None = 0,
    Proficiency = 1,
    Mastery = 2
}

/// <summary>
/// An earned mastery source and its verified parent-Class level when relevant.
/// The caller must authenticate source ownership, feature level and choices.
/// </summary>
public sealed record NativeWeaponMasterySource(
    string SourceId, string OccurrenceId, int? ClassLevel = null);

/// <summary>
/// The proficiency request is resolved from native sources, never from a
/// caller-supplied projection. Mastery choice keys are occurrenceId/choiceId.
/// </summary>
public sealed record NativeEquipmentTrainingRequest(
    NativeEquipmentQualificationRequest Proficiencies,
    IReadOnlyList<NativeWeaponMasterySource> MasterySources,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? SelectedMasteries = null);

public sealed record NativeWeaponMasteryProvenance(
    string SourceId, string SourceOccurrenceId, string GrantId);

public sealed record NativeWeaponMasteryGrant(
    string TargetId, IReadOnlyList<NativeWeaponMasteryProvenance> Provenance);

public sealed record NativeWeaponMasteryChoice(
    string ChoiceKey, int Count, IReadOnlyList<string> EligibleWeaponTypeIds);

/// <summary>
/// Proficiency and mastery are separate authored grants over one shared
/// equipment catalog. Only resolved projections may be used for coverage.
/// </summary>
public sealed record NativeEquipmentTrainingProjection(
    int ContractVersion, string RulesetId, string Fingerprint, string Status,
    NativeEquipmentQualificationProjection Proficiencies,
    IReadOnlyList<NativeWeaponMasteryGrant> Masteries,
    IReadOnlyList<NativeWeaponMasteryChoice> RequiredMasteryChoices,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// Flags is 0, 1, 2, or 3. A value of 2 represents a mastery entitlement
/// lacking proficiency and MUST NOT enable its combat effect. Null denotes
/// unresolved or unavailable coverage, never absence of training.
/// </summary>
public sealed record NativeEquipmentTrainingCoverage(
    string Status, int? Flags,
    IReadOnlyList<NativeEquipmentQualificationProvenance> ProficiencyProvenance,
    IReadOnlyList<NativeWeaponMasteryProvenance> MasteryProvenance,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// Shared equipment qualification evaluator extended with independent weapon
/// mastery entitlement resolution. It does not execute attack or mastery effects.
/// </summary>
public sealed partial class NativeEquipmentQualifications
{
    public static bool HasUsableMastery(int flags) =>
        (flags & (int)(NativeEquipmentTrainingFlags.Proficiency |
            NativeEquipmentTrainingFlags.Mastery)) == 3;

    public NativeEquipmentTrainingProjection ResolveTraining(NativeEquipmentTrainingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<NativeIssue>();
        var choices = new List<NativeWeaponMasteryChoice>();
        var awarded = new SortedDictionary<string, List<NativeWeaponMasteryProvenance>>(
            StringComparer.Ordinal);
        var seenOccurrences = new HashSet<string>(StringComparer.Ordinal);
        var seenChoices = new HashSet<string>(StringComparer.Ordinal);
        var needsChoices = false;
        var fixedCount = 0;
        var choiceCount = 0;

        var proficiencies = request.Proficiencies is null
            ? new NativeEquipmentQualificationProjection(1, masteryCatalog.RulesetId,
                masteryCatalog.Fingerprint, "invalid", [], [],
                [new NativeIssue("invalid-equipment-qualification-input", "proficiencies",
                    "A source-based proficiency request is required.")])
            : Resolve(request.Proficiencies);

        NativeEquipmentTrainingProjection Finish(string status)
        {
            NativeWeaponMasteryGrant[] masteries = status == "resolved"
                ? awarded.Select(pair => new NativeWeaponMasteryGrant(pair.Key,
                    pair.Value.ToArray())).ToArray() : [];
            return new(1, masteryCatalog.RulesetId, masteryCatalog.Fingerprint,
                status, proficiencies, masteries, choices.ToArray(), issues.ToArray());
        }

        // Never make a training result available when its prerequisite
        // proficiency selection is stale, incomplete or malformed.
        if (proficiencies.Status != "resolved")
        {
            issues.Add(new("proficiency-projection-unavailable", "proficiencies",
                "A complete, snapshot-pinned proficiency projection is required."));
            return Finish(proficiencies.Status);
        }
        if (request.MasterySources is null || request.MasterySources.Count > 100 ||
            request.SelectedMasteries?.Count > 200 ||
            request.SelectedMasteries?.Any(pair =>
                pair.Key.Length > 321 || pair.Value is null ||
                pair.Value.Count > 32 || pair.Value.Any(id => !ValidId(id))) == true)
        {
            issues.Add(new("invalid-weapon-mastery-input", "sources",
                "Sources and mastery choices exceed the request work limits."));
            return Finish("invalid");
        }

        foreach (var source in request.MasterySources)
        {
            if (source is null || !ValidId(source.SourceId) ||
                !ValidChoiceKeyComponent(source.OccurrenceId) ||
                source.ClassLevel is < 1 ||
                !seenOccurrences.Add(source.OccurrenceId))
            {
                issues.Add(new("invalid-weapon-mastery-source", "sources",
                    "Each source occurrence needs a unique valid identity and level."));
                continue;
            }
            if (!masteryCatalog.TryGetById(source.SourceId, out var entity) || entity is null)
            {
                issues.Add(new("unknown-weapon-mastery-source", source.SourceId,
                    "The source must exist in the active native catalog."));
                continue;
            }
            var definition = entity.Definition;
            if (entity.Category == "classFeature")
            {
                var unlocked = Number(definition, "classLevel");
                if (unlocked is null || source.ClassLevel is null ||
                    source.ClassLevel < unlocked)
                {
                    issues.Add(new("unearned-weapon-mastery-feature", source.SourceId,
                        "The verified parent-Class level must meet this feature's level."));
                    continue;
                }
            }
            // Both source layouts exist during migration; an ambiguous double
            // declaration is never interpreted as two independent grants.
            var hasRoot = definition.TryGetProperty("weaponMasteryGrants", out var root);
            JsonElement nested = default;
            var hasNested = TryObject(definition, "definition", out var contents) &&
                contents.TryGetProperty("weaponMasteryGrants", out nested);
            if (hasRoot == hasNested)
            {
                issues.Add(new("weapon-mastery-grants-not-reconciled", source.SourceId,
                    "A mastery source must define exactly one authored grant block."));
                continue;
            }
            var grants = hasRoot ? root : nested;
            if (grants.ValueKind != JsonValueKind.Object ||
                grants.EnumerateObject().Any(p => p.Name is not ("fixed" or "choices")))
            {
                issues.Add(new("unsupported-weapon-mastery-grants", source.SourceId,
                    "Mastery grant blocks accept only fixed or choice entitlements."));
                continue;
            }
            var seenGrantIds = new HashSet<string>(StringComparer.Ordinal);
            if (!grants.TryGetProperty("fixed", out _) &&
                !grants.TryGetProperty("choices", out _))
            {
                issues.Add(new("invalid-weapon-mastery-grants", source.SourceId,
                    "A mastery block must explicitly declare a grant kind."));
                continue;
            }
            if (grants.TryGetProperty("fixed", out var fixedGrants))
            {
                if (fixedGrants.ValueKind != JsonValueKind.Array ||
                    fixedGrants.GetArrayLength() > 100 ||
                    fixedCount > 1000 - fixedGrants.GetArrayLength())
                    issues.Add(new("invalid-weapon-mastery-grants", source.SourceId,
                        "Fixed mastery grants exceed the work budget."));
                else
                {
                    fixedCount += fixedGrants.GetArrayLength();
                    foreach (var grant in fixedGrants.EnumerateArray())
                    {
                        if (grant.ValueKind != JsonValueKind.Object ||
                            grant.EnumerateObject().Any(p => p.Name is not ("id" or "targetId")))
                        {
                            issues.Add(new("unsupported-weapon-mastery-grant", source.SourceId,
                                "Conditional or unrecognized mastery grants are unsupported."));
                            continue;
                        }
                        var id = Text(grant, "id");
                        var target = Text(grant, "targetId");
                        if (!ValidId(id) || !ValidId(target) || !seenGrantIds.Add(id!) ||
                            CategoryOf(target!) != "weapon")
                        {
                            issues.Add(new("invalid-weapon-mastery-grant", source.SourceId,
                                "Fixed mastery must target a native weapon type or group."));
                            continue;
                        }
                        Award(target!, new(source.SourceId, source.OccurrenceId, id!));
                    }
                }
            }
            if (!grants.TryGetProperty("choices", out var authoredChoices))
                continue;
            if (authoredChoices.ValueKind != JsonValueKind.Array ||
                authoredChoices.GetArrayLength() > 100 ||
                choiceCount > 200 - authoredChoices.GetArrayLength())
            {
                issues.Add(new("invalid-weapon-mastery-choices", source.SourceId,
                    "Mastery choices exceed the work budget."));
                continue;
            }
            choiceCount += authoredChoices.GetArrayLength();
            foreach (var choice in authoredChoices.EnumerateArray())
            {
                if (choice.ValueKind != JsonValueKind.Object ||
                    choice.EnumerateObject().Any(p => p.Name is not
                        ("id" or "count" or "countByClassLevel" or "fromGroupIds" or
                         "fromTypeIds" or "requireProficiency")))
                {
                    issues.Add(new("unsupported-weapon-mastery-choice", source.SourceId,
                        "Only authored weapon-type selections are supported."));
                    continue;
                }
                var id = Text(choice, "id");
                var hasCount = choice.TryGetProperty("count", out _);
                var hasProgression = choice.TryGetProperty("countByClassLevel", out var progression);
                var count = hasCount ? Number(choice, "count") :
                    hasProgression ? CountForClassLevel(progression, source.ClassLevel) : null;
                if (!ValidChoiceKeyComponent(id) || !seenGrantIds.Add(id!) ||
                    hasCount == hasProgression || count is null or < 1 or > 32 ||
                    (choice.TryGetProperty("requireProficiency", out var prerequisite) &&
                     prerequisite.ValueKind != JsonValueKind.True))
                {
                    issues.Add(new("invalid-weapon-mastery-choice", source.SourceId,
                        "A mastery choice needs an ID, valid level-adjusted count and proficiency prerequisite."));
                    continue;
                }

                var groups = new HashSet<string>(StringComparer.Ordinal);
                if (choice.TryGetProperty("fromGroupIds", out var restrictions))
                {
                    if (restrictions.ValueKind != JsonValueKind.Array ||
                        restrictions.GetArrayLength() is < 1 or > 32)
                    {
                        issues.Add(new("invalid-weapon-mastery-choice", source.SourceId,
                            "Group filters must be bounded nonempty arrays."));
                        continue;
                    }
                    foreach (var group in restrictions.EnumerateArray())
                    {
                        var target = group.ValueKind == JsonValueKind.String
                            ? group.GetString() : null;
                        if (!ValidId(target) || !groups.Add(target!) ||
                            CategoryOf(target!) != "weapon" ||
                            !masteryCatalog.TryGetById(target!, out var entry) ||
                            entry?.Category != "equipment-group")
                            issues.Add(new("unknown-weapon-mastery-group", source.SourceId,
                                "Mastery filters must reference distinct authored weapon groups."));
                    }
                }
                HashSet<string>? types = null;
                if (choice.TryGetProperty("fromTypeIds", out var typeRestrictions))
                {
                    if (typeRestrictions.ValueKind != JsonValueKind.Array ||
                        typeRestrictions.GetArrayLength() is < 1 or > 100)
                    {
                        issues.Add(new("invalid-weapon-mastery-choice", source.SourceId,
                            "Type filters must be bounded nonempty arrays."));
                        continue;
                    }
                    types = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var option in typeRestrictions.EnumerateArray())
                    {
                        var target = option.ValueKind == JsonValueKind.String
                            ? option.GetString() : null;
                        if (!ValidId(target) || !types.Add(target!) ||
                            CategoryOf(target!) != "weapon" ||
                            !masteryCatalog.TryGetById(target!, out var entry) ||
                            entry?.Category != "equipment-type")
                            issues.Add(new("unknown-weapon-mastery-type", source.SourceId,
                                "Mastery type filters must identify distinct native weapon types."));
                    }
                }
                var key = source.OccurrenceId + "/" + id;
                seenChoices.Add(key);
                var eligible = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var weapon in EquipmentOfKind("equipment-type"))
                {
                    if (CategoryOf(weapon.Id) != "weapon" ||
                        (types is not null && !types.Contains(weapon.Id)))
                        continue;
                    // Source-specific restrictions are additional to the
                    // universal prerequisite of proficiency in the base type.
                    if (groups.Count != 0)
                    {
                        var matches = false;
                        foreach (var group in groups)
                        {
                            var included = GroupCovers(group, weapon.Id);
                            if (included is null)
                                issues.Add(new("unresolved-weapon-mastery-group", group,
                                    "A mastery restriction has invalid membership."));
                            if (included == true) matches = true;
                        }
                        if (!matches) continue;
                    }
                    if (Covers(proficiencies, weapon.Id).Proficient == true)
                        eligible.Add(weapon.Id);
                }
                if (eligible.Count < count)
                    issues.Add(new("weapon-mastery-options-unavailable", key,
                        "The current catalog has too few proficient eligible weapons."));
                choices.Add(new(key, count.Value, eligible.ToArray()));
                if (request.SelectedMasteries?.TryGetValue(key, out var selected) != true)
                {
                    needsChoices = true;
                    continue;
                }
                if (selected is null || selected.Count != count.Value ||
                    selected.Distinct(StringComparer.Ordinal).Count() != count.Value ||
                    selected.Any(v => !eligible.Contains(v)))
                {
                    issues.Add(new("invalid-weapon-mastery-selection", key,
                        "Mastery selections must name distinct eligible proficient weapon types."));
                    continue;
                }
                foreach (var target in selected)
                    Award(target, new(source.SourceId, source.OccurrenceId, id!));
            }
        }
        foreach (var selected in request.SelectedMasteries?.Keys ?? [])
            if (!seenChoices.Contains(selected))
                issues.Add(new("unexpected-weapon-mastery-selection", selected,
                    "A mastery selection does not match an active source choice."));

        return Finish(issues.Count > 0 ? "unavailable" :
            needsChoices ? "needs-choices" : "resolved");

        void Award(string target, NativeWeaponMasteryProvenance provenance)
        {
            if (!awarded.TryGetValue(target, out var list))
                awarded[target] = list = [];
            list.Add(provenance);
        }
    }

    /// <summary>
    /// Resolve a base weapon or explicitly linked native item. The two-bit
    /// result is a single state, not two separately persisted booleans.
    /// </summary>
    public NativeEquipmentTrainingCoverage CoversTraining(
        NativeEquipmentTrainingProjection projection, string equipmentId)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.Status != "resolved" ||
            projection.RulesetId != masteryCatalog.RulesetId ||
            projection.Fingerprint != masteryCatalog.Fingerprint)
            return new("unavailable", null, [], [], [
                new("equipment-training-unavailable", "training",
                    "A complete native equipment training projection is required.")
            ]);

        var proficiency = Covers(projection.Proficiencies, equipmentId);
        if (proficiency.Status != "resolved")
            return new(proficiency.Status, null, [], [], proficiency.Issues);
        // The legacy proficiency coverage API already validated the type link.
        masteryCatalog.TryGetById(equipmentId, out var equipment);
        var typeId = equipment!.Category == "item"
            ? Text(equipment.Definition, "equipmentTypeId")! : equipmentId;
        var masteryProvenance = new List<NativeWeaponMasteryProvenance>();
        foreach (var mastery in projection.Masteries)
        {
            if (CategoryOf(typeId) != "weapon") continue;
            bool? covered = mastery.TargetId == typeId ? true :
                masteryCatalog.TryGetById(mastery.TargetId, out var group) &&
                group?.Category == "equipment-group"
                    ? GroupCovers(mastery.TargetId, typeId) : false;
            if (covered is null)
                return new("unresolved", null, [], [], [
                    new("unresolved-weapon-mastery-group", mastery.TargetId,
                        "Mastery group membership could not be resolved.")
                ]);
            if (covered == true) masteryProvenance.AddRange(mastery.Provenance);
        }
        var flags = (proficiency.Proficient == true
            ? (int)NativeEquipmentTrainingFlags.Proficiency : 0) |
            (masteryProvenance.Count > 0 ? (int)NativeEquipmentTrainingFlags.Mastery : 0);
        // The (rare) value 2 preserves an authored mastery entitlement
        // without proficiency; consumers must require value 3 to use mastery.
        return new("resolved", flags, proficiency.Provenance,
            masteryProvenance.ToArray(), []);
    }

    private static int? CountForClassLevel(JsonElement progression, int? classLevel)
    {
        if (classLevel is null || progression.ValueKind != JsonValueKind.Array ||
            progression.GetArrayLength() is < 1 or > 100)
            return null;
        var lastLevel = 0;
        int? count = null;
        foreach (var step in progression.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Array || step.GetArrayLength() != 2 ||
                step[0].ValueKind != JsonValueKind.Number ||
                !step[0].TryGetInt32(out var level) || level <= lastLevel ||
                step[1].ValueKind != JsonValueKind.Number ||
                !step[1].TryGetInt32(out var size) || size is < 1 or > 32)
                return null;
            lastLevel = level;
            if (classLevel >= level) count = size;
        }
        return count;
    }
}
