using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// The caller has already verified that a character owns each named rule source.
/// Source IDs alone do not establish ownership or satisfy feature prerequisites.
/// </summary>
public sealed record NativeEquipmentQualificationSource(string SourceId, string OccurrenceId);

public sealed record NativeEquipmentQualificationRequest(
    string RulesetId, string Fingerprint,
    IReadOnlyList<NativeEquipmentQualificationSource> Sources,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? SelectedChoices = null);

public sealed record NativeEquipmentQualificationProvenance(
    string SourceId, string SourceOccurrenceId, string GrantId);

public sealed record NativeEquipmentQualification(
    string QualificationId, string EquipmentCategory,
    IReadOnlyList<NativeEquipmentQualificationProvenance> Provenance);

public sealed record NativeEquipmentQualificationChoice(
    string ChoiceKey, int Count, IReadOnlyList<string> EligibleQualificationIds);

public sealed record NativeEquipmentQualificationProjection(
    int ContractVersion, string RulesetId, string Fingerprint, string Status,
    IReadOnlyList<NativeEquipmentQualification> Qualifications,
    IReadOnlyList<NativeEquipmentQualificationChoice> RequiredChoices,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>Coverage is intentionally binary; numerical category rules are separate.</summary>
public sealed record NativeEquipmentCoverage(
    string Status, bool? Proficient,
    IReadOnlyList<NativeEquipmentQualificationProvenance> Provenance,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// Resolves authored native qualification grants and scoped equipment coverage.
/// It neither awards equipment nor converts Skills/Competencies into equipment training.
/// </summary>
public sealed class NativeEquipmentQualifications(NativeCatalogSnapshot catalog)
{
    private static readonly HashSet<string> Categories =
        new(["weapon", "armor", "shield", "tool"], StringComparer.Ordinal);

    public NativeEquipmentQualificationProjection Resolve(NativeEquipmentQualificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<NativeIssue>();
        var required = new List<NativeEquipmentQualificationChoice>();
        var awarded = new SortedDictionary<string, List<NativeEquipmentQualificationProvenance>>(
            StringComparer.Ordinal);
        var seenSources = new HashSet<string>(StringComparer.Ordinal);
        var seenChoices = new HashSet<string>(StringComparer.Ordinal);
        var needsChoices = false;

        NativeEquipmentQualificationProjection Finish(string status)
        {
            // Never publish a partial character when a source is malformed or
            // an entitlement remains unselected.
            var output = status == "resolved"
                ? awarded.Select(pair => new NativeEquipmentQualification(pair.Key,
                    CategoryOf(pair.Key)!,
                    (IReadOnlyList<NativeEquipmentQualificationProvenance>)pair.Value.ToArray()))
                    .ToArray()
                : [];
            return new(1, catalog.RulesetId, catalog.Fingerprint, status, output,
                required.ToArray(), issues.ToArray());
        }

        if (request.RulesetId != catalog.RulesetId ||
            request.Fingerprint != catalog.Fingerprint)
        {
            issues.Add(new("ruleset-version-mismatch", "ruleset",
                "The requested ruleset version is not the active native snapshot."));
            return Finish("invalid");
        }
        if (request.Sources is null || request.Sources.Count > 100 ||
            request.SelectedChoices?.Count > 100 ||
            request.SelectedChoices?.Any(pair => pair.Key.Length > 320 ||
                pair.Value is null || pair.Value.Count > 32 ||
                pair.Value.Any(value => !ValidId(value))) == true)
        {
            issues.Add(new("invalid-equipment-qualification-input", "sources",
                "Sources and selections must fit the transport limits."));
            return Finish("invalid");
        }

        foreach (var source in request.Sources)
        {
            if (source is null || !ValidId(source.SourceId) ||
                !ValidId(source.OccurrenceId) ||
                !seenSources.Add(source.OccurrenceId))
            {
                issues.Add(new("invalid-equipment-source", "sources",
                    "Every source occurrence must have unique, bounded identities."));
                continue;
            }
            if (!catalog.TryGetById(source.SourceId, out var entity) || entity is null)
            {
                issues.Add(new("unknown-equipment-source", source.SourceId,
                    "The source must be an authored native rule."));
                continue;
            }

            var definition = entity.Definition;
            if (TryObject(definition, "definition", out var nested))
                definition = nested;
            if (!TryObject(definition, "equipmentProficiencyGrants", out var grants))
            {
                issues.Add(new("equipment-grants-not-reconciled", source.SourceId,
                    "The source has no executable equipment proficiency grants."));
                continue;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (grants.TryGetProperty("fixed", out var fixedGrants))
            {
                if (fixedGrants.ValueKind != JsonValueKind.Array ||
                    fixedGrants.GetArrayLength() > 100)
                {
                    issues.Add(new("invalid-equipment-grants", source.SourceId,
                        "Fixed grants must be a bounded array."));
                }
                else foreach (var grant in fixedGrants.EnumerateArray())
                {
                    var id = Text(grant, "id");
                    var target = Text(grant, "targetId");
                    if (!ValidId(id) || !ValidId(target) || !ids.Add(id!))
                    {
                        issues.Add(new("invalid-equipment-grant", source.SourceId,
                            "Grant identity and target must be unique and explicit."));
                        continue;
                    }
                    if (CategoryOf(target!) is null)
                    {
                        issues.Add(new("unknown-equipment-qualification", target!,
                            "Grant target must be a native equipment type or group."));
                        continue;
                    }
                    Add(target!, new(source.SourceId, source.OccurrenceId, id!));
                }
            }
            if (!grants.TryGetProperty("choices", out var choices))
                continue;
            if (choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() > 100)
            {
                issues.Add(new("invalid-equipment-choices", source.SourceId,
                    "Choices must be a bounded array."));
                continue;
            }
            foreach (var choice in choices.EnumerateArray())
            {
                var id = Text(choice, "id");
                var count = Number(choice, "count");
                var category = Text(choice, "fromCategory");
                var selectionKind = Text(choice, "selectionKind") ?? "equipment-type";
                if (!ValidId(id) || !ids.Add(id!) ||
                    count is null or < 1 or > 32 || !Categories.Contains(category ?? "") ||
                    selectionKind is not ("equipment-type" or "equipment-group"))
                {
                    issues.Add(new("invalid-equipment-choice", source.SourceId,
                        "Choice needs a unique ID, count, category, and supported selection kind."));
                    continue;
                }
                var groups = new List<string>();
                if (choice.TryGetProperty("fromGroupIds", out var restrictions))
                {
                    if (selectionKind != "equipment-type" ||
                        restrictions.ValueKind != JsonValueKind.Array ||
                        restrictions.GetArrayLength() is < 1 or > 32)
                    {
                        issues.Add(new("invalid-equipment-choice", id!,
                            "Group filters require a bounded type-selection list."));
                        continue;
                    }
                    foreach (var group in restrictions.EnumerateArray())
                    {
                        var target = group.ValueKind == JsonValueKind.String
                            ? group.GetString() : null;
                        if (!ValidId(target) || CategoryOf(target!) != category ||
                            !catalog.TryGetById(target!, out var scope) ||
                            scope?.Category != "equipment-group")
                            issues.Add(new("unknown-equipment-group", id!,
                                "Choice filter must reference a group of the selected category."));
                        else groups.Add(target!);
                    }
                }
                var choiceKey = source.OccurrenceId + "/" + id;
                seenChoices.Add(choiceKey);
                var eligible = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var candidate in EquipmentOfKind(selectionKind))
                {
                    if (CategoryOf(candidate.Id) != category) continue;
                    if (groups.Count == 0)
                    {
                        eligible.Add(candidate.Id);
                        continue;
                    }
                    foreach (var groupId in groups)
                    {
                        var coverage = GroupCovers(groupId, candidate.Id);
                        if (coverage is null)
                            issues.Add(new("unresolved-equipment-group", groupId,
                                "The equipment group has invalid or cyclic membership."));
                        if (coverage == true) { eligible.Add(candidate.Id); break; }
                    }
                }
                if (eligible.Count < count)
                    issues.Add(new("equipment-choice-options-unavailable", choiceKey,
                        "The native equipment catalog has too few verified eligible options."));
                required.Add(new(choiceKey, count.Value, eligible.ToArray()));
                if (request.SelectedChoices?.TryGetValue(choiceKey, out var selected) != true)
                {
                    needsChoices = true;
                    continue;
                }
                if (selected.Count != count.Value ||
                    selected.Distinct(StringComparer.Ordinal).Count() != count.Value ||
                    selected.Any(value => !eligible.Contains(value)))
                {
                    issues.Add(new("invalid-equipment-selection", choiceKey,
                        "Selections must be distinct eligible qualifications of the required count."));
                    continue;
                }
                foreach (var selectedId in selected)
                    Add(selectedId, new(source.SourceId, source.OccurrenceId, id!));
            }
        }

        foreach (var supplied in request.SelectedChoices?.Keys ?? [])
            if (!seenChoices.Contains(supplied))
                issues.Add(new("unexpected-equipment-selection", supplied,
                    "Selection does not correspond to an active authored choice."));

        return Finish(issues.Count != 0 ? "unavailable" :
            needsChoices ? "needs-choices" : "resolved");

        void Add(string targetId, NativeEquipmentQualificationProvenance provenance)
        {
            if (!awarded.TryGetValue(targetId, out var list))
                awarded[targetId] = list = [];
            list.Add(provenance);
        }
    }

    /// <summary>
    /// Tests a canonical type or a native item declaring equipmentTypeId.
    /// Unknown or unsupported types never silently count as nonproficient.
    /// </summary>
    public NativeEquipmentCoverage Covers(
        NativeEquipmentQualificationProjection projection, string equipmentId)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.Status != "resolved" ||
            projection.RulesetId != catalog.RulesetId ||
            projection.Fingerprint != catalog.Fingerprint)
            return new("unavailable", null, [], [
                new("equipment-projection-unavailable", "qualifications",
                    "Resolve a complete qualification projection for the active catalog first.")
            ]);
        if (!ValidId(equipmentId) ||
            !catalog.TryGetById(equipmentId, out var equipment) || equipment is null)
            return new("unresolved", null, [], [
                new("unknown-equipment-type", equipmentId ?? "equipment",
                    "No native equipment type or linked item was found.")
            ]);
        var typeId = equipment.Category == "item"
            ? Text(equipment.Definition, "equipmentTypeId") : equipmentId;
        if (typeId is null || !catalog.TryGetById(typeId, out var type) ||
            type?.Category != "equipment-type" || CategoryOf(typeId) is null)
            return new("unresolved", null, [], [
                new("unknown-equipment-type", equipmentId,
                    "A physical item must explicitly link to a canonical equipment type.")
            ]);

        var matches = new List<NativeEquipmentQualificationProvenance>();
        foreach (var grant in projection.Qualifications)
        {
            if (CategoryOf(grant.QualificationId) != CategoryOf(typeId))
                return new("unresolved", null, [], [
                    new("invalid-equipment-qualification", grant.QualificationId,
                        "Qualification category no longer matches its native definition.")
                ]);
            bool? covers = grant.QualificationId == typeId ? true :
                catalog.TryGetById(grant.QualificationId, out var group) &&
                group?.Category == "equipment-group"
                    ? GroupCovers(grant.QualificationId, typeId) : false;
            if (covers is null)
                return new("unresolved", null, [], [
                    new("unresolved-equipment-group", grant.QualificationId,
                        "The group membership graph is incomplete or cyclic.")
                ]);
            if (covers == true) matches.AddRange(grant.Provenance);
        }
        return new("resolved", matches.Count != 0, matches, []);
    }

    private IEnumerable<NativeRuleEntity> EquipmentOfKind(string kind)
    {
        // NativeCatalogSnapshot already owns the immutable catalog; page its
        // indexed category instead of rebuilding a separate mutable registry.
        for (var offset = 0; ; offset += 100)
        {
            var page = catalog.Search(category: kind, offset: offset, limit: 100);
            foreach (var entry in page.Items) yield return entry;
            if (offset + page.Items.Length >= page.Total) yield break;
        }
    }

    private bool? GroupCovers(string groupId, string typeId)
    {
        if (!catalog.TryGetById(typeId, out var target) ||
            target?.Category != "equipment-type" ||
            !TryArray(target.Definition, "groupIds", out var memberships))
            return null;
        var stack = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in memberships.EnumerateArray())
        {
            if (member.ValueKind != JsonValueKind.String) return null;
            var found = Walk(member.GetString()!);
            if (found != false) return found;
        }
        return false;

        bool? Walk(string current)
        {
            if (!stack.Add(current)) return null;
            try
            {
                if (!catalog.TryGetById(current, out var group) ||
                    group?.Category != "equipment-group" ||
                    CategoryOf(current) != CategoryOf(typeId))
                    return null;
                if (current == groupId) return true;
                if (!group.Definition.TryGetProperty("parentGroupIds", out var parents))
                    return false;
                if (parents.ValueKind != JsonValueKind.Array) return null;
                foreach (var parent in parents.EnumerateArray())
                {
                    if (parent.ValueKind != JsonValueKind.String) return null;
                    var result = Walk(parent.GetString()!);
                    if (result != false) return result;
                }
                return false;
            }
            finally { stack.Remove(current); }
        }
    }

    private string? CategoryOf(string id)
    {
        if (!catalog.TryGetById(id, out var entity) ||
            entity?.Category is not ("equipment-type" or "equipment-group"))
            return null;
        var category = Text(entity.Definition, "equipmentCategory");
        return Categories.Contains(category ?? "") ? category : null;
    }

    private static bool ValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 160 &&
        value == value.Trim();

    private static bool TryObject(JsonElement obj, string name, out JsonElement value)
    {
        value = default;
        return obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryArray(JsonElement obj, string name, out JsonElement value)
    {
        value = default;
        return obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array;
    }

    private static string? Text(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static int? Number(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number) ? number : null;
}
