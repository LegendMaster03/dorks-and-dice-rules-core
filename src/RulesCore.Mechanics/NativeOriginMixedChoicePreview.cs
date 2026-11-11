using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// An untrusted proposal for one authored Origin benefit-choice. Neither the
/// source identity nor occurrence identifier establishes character ownership.
/// This contract is internal and has no HTTP endpoint.
/// </summary>
internal sealed record NativeOriginMixedChoiceRequest(
    string RulesetId, string Fingerprint, string SourceId, string SourceOccurrenceId,
    string BenefitId, string? OptionId = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? SelectedEquipmentTypes = null);

/// <summary>Catalog-backed possible effect; NEVER an earned character grant.</summary>
internal sealed record NativeOriginCandidateEffect(
    string Domain, string SourceId, string SourceOccurrenceId,
    string BenefitId, string OptionId, string GrantId,
    string? TargetId = null, int Ranks = 0, bool Proficient = false,
    int Count = 0, string? ChoiceConstraint = null);

internal sealed record NativeOriginCandidateChoice(
    string GrantId, int Count, IReadOnlyList<string> EligibleEquipmentTypeIds);

/// <summary>
/// A complete preview is not authorization. No candidate effects are exposed
/// for missing selections, malformed authored content or invalid proposals.
/// </summary>
internal sealed record NativeOriginMixedChoicePreview(
    string Status, string RulesetId, string Fingerprint,
    IReadOnlyList<NativeOriginCandidateEffect> CandidateEffects,
    IReadOnlyList<string> EligibleOptionIds,
    IReadOnlyList<NativeOriginCandidateChoice> RequiredEquipmentChoices,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// Checks one nested benefit-choice against the immutable native catalog.
/// It deliberately cannot authenticate a character or apply its candidate
/// results to Skills, Competencies, equipment, language or inventory.
/// </summary>
internal sealed class NativeOriginMixedChoicePreviewer(NativeCatalogSnapshot catalog)
{
    public NativeOriginMixedChoicePreview Preview(NativeOriginMixedChoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<NativeIssue>();
        var effects = new List<NativeOriginCandidateEffect>();
        var required = new List<NativeOriginCandidateChoice>();
        var options = new List<string>();
        var needsChoice = false;

        NativeOriginMixedChoicePreview Finish(string status) =>
            new(status, catalog.RulesetId, catalog.Fingerprint,
                status == "preview" ? effects.ToArray() : [],
                options.ToArray(), required.ToArray(), issues.ToArray());

        void Issue(string code, string path, string detail) =>
            issues.Add(new(code, path, detail));

        if (request.RulesetId != catalog.RulesetId ||
            request.Fingerprint != catalog.Fingerprint)
        {
            Issue("ruleset-version-mismatch", "ruleset", "Proposal does not match the active native snapshot.");
            return Finish("invalid");
        }

        if (!ValidComponent(request.SourceId) || !ValidComponent(request.SourceOccurrenceId) ||
            !ValidComponent(request.BenefitId) ||
            (request.OptionId is not null && !ValidComponent(request.OptionId)) ||
            request.SelectedEquipmentTypes?.Count > 32 ||
            request.SelectedEquipmentTypes?.Any(pair =>
                !ValidComponent(pair.Key) || pair.Value is null ||
                pair.Value.Count > 32 ||
                pair.Value.Any(id => !ValidComponent(id))) == true)
        {
            Issue("invalid-origin-proposal", "origin", "Source, benefit and choices must have bounded identities.");
            return Finish("invalid");
        }
        if (!catalog.TryGetById(request.SourceId, out var source) ||
            source?.Category is not ("background" or "upbringing"))
        {
            Issue("unknown-origin-source", request.SourceId, "Only native Backgrounds and Upbringings have this preview.");
            return Finish("unavailable");
        }
        if (!Array(source.Definition, "benefits", out var benefits) ||
            benefits.GetArrayLength() > 100)
        {
            Issue("origin-benefits-unavailable", request.SourceId, "Source benefits are not executable in this preview.");
            return Finish("unavailable");
        }

        var matched = benefits.EnumerateArray().Where(item =>
            Text(item, "id") == request.BenefitId).ToArray();
        if (matched.Length != 1 ||
            Text(matched[0], "grantType") != "benefit-choice" ||
            Number(matched[0], "choiceCount") != 1 ||
            !OnlyFields(matched[0], "id", "name", "description",
                "grantType", "choiceCount", "options") ||
            !Array(matched[0], "options", out var authored) ||
            authored.GetArrayLength() is < 2 or > 32)
        {
            Issue("unsupported-origin-choice", request.BenefitId,
                "The source must author exactly one bounded, exclusive benefit-choice.");
            return Finish("unavailable");
        }

        var byId = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var entry in authored.EnumerateArray())
        {
            var id = Text(entry, "id");
            if (!ValidComponent(id) ||
                !OnlyFields(entry, "id", "name", "description", "benefits",
                    "equipmentProficiencyGrants") ||
                !byId.TryAdd(id!, entry))
            {
                Issue("invalid-origin-option", request.BenefitId,
                    "Every authored option needs a distinct stable identity.");
                return Finish("unavailable");
            }
            options.Add(id!);
        }
        options.Sort(StringComparer.Ordinal);

        if (request.OptionId is null)
        {
            if (request.SelectedEquipmentTypes is { Count: > 0 })
            {
                Issue("unexpected-origin-equipment-selection", request.BenefitId,
                    "An equipment selection cannot precede its outer option.");
                return Finish("invalid");
            }
            return Finish("needs-choices");
        }
        if (!byId.TryGetValue(request.OptionId, out var option))
        {
            Issue("invalid-origin-option", request.OptionId, "The selected option does not belong to this benefit.");
            return Finish("invalid");
        }
        if (!Array(option, "benefits", out var optionBenefits) ||
            optionBenefits.GetArrayLength() != 1)
        {
            Issue("unsupported-origin-option-benefits", request.OptionId,
                "The current option contract requires one explicit effect.");
            return Finish("unavailable");
        }
        var selectedEffect = optionBenefits[0];
        if (selectedEffect.ValueKind != JsonValueKind.Object)
        {
            Issue("unsupported-origin-option-effect", request.OptionId,
                "An option effect must be an authored native object.");
            return Finish("unavailable");
        }
        var competencyTraining = Text(selectedEffect, "grantType") == "competency-training";
        var equipmentTraining = Text(selectedEffect, "grantType") == "qualification-training";
        var language = selectedEffect.TryGetProperty("extraLanguages", out _);
        if (new[] { competencyTraining, equipmentTraining, language }.Count(enabled => enabled) != 1)
        {
            Issue("unsupported-origin-option-effect", request.OptionId,
                "An option must have one recognized grant domain.");
            return Finish("unavailable");
        }

        if (competencyTraining)
        {
            var target = Text(selectedEffect, "competency");
            if (!OnlyFields(selectedEffect, "grantType", "competency", "description",
                    "competencyTraining") ||
                !ValidComponent(target) ||
                !catalog.TryGetById(target!, out var competency) ||
                competency?.Category != "competency" ||
                !Object(selectedEffect, "competencyTraining", out var training) ||
                !OnlyFields(training, "ranks", "proficient") ||
                Number(training, "ranks") is not int ranks || ranks is < 0 or > 1000 ||
                !Boolean(training, "proficient").HasValue ||
                (ranks == 0 && Boolean(training, "proficient") == false) ||
                option.TryGetProperty("equipmentProficiencyGrants", out _) ||
                request.SelectedEquipmentTypes is { Count: > 0 })
            {
                Issue("invalid-origin-competency-option", request.OptionId,
                    "The Competency option must have a canonical target, explicit effect and no equipment choice.");
                return Finish("unavailable");
            }
            effects.Add(new("competency", request.SourceId, request.SourceOccurrenceId,
                request.BenefitId, request.OptionId, request.BenefitId + "/" + request.OptionId,
                target, ranks, Boolean(training, "proficient")!.Value));
        }
        else if (language)
        {
            var count = Number(selectedEffect, "extraLanguages");
            var constraint = Text(selectedEffect, "choiceConstraint");
            if (!OnlyFields(selectedEffect, "extraLanguages", "choiceConstraint",
                    "description") ||
                count is not 1 || !ValidComponent(constraint) ||
                option.TryGetProperty("equipmentProficiencyGrants", out _) ||
                request.SelectedEquipmentTypes is { Count: > 0 })
            {
                Issue("invalid-origin-language-option", request.OptionId,
                    "The language choice requires one explicitly constrained language and no equipment.");
                return Finish("unavailable");
            }
            effects.Add(new("language-choice", request.SourceId, request.SourceOccurrenceId,
                request.BenefitId, request.OptionId, request.BenefitId + "/" + request.OptionId,
                Count: 1, ChoiceConstraint: constraint));
        }
        else
        {
            if (!OnlyFields(selectedEffect, "grantType", "qualificationCategory",
                    "equipmentTypeId", "equipmentTypeChoiceId", "choiceCategory",
                    "choiceCount", "description") ||
                Text(selectedEffect, "qualificationCategory") != "tool" ||
                !Object(option, "equipmentProficiencyGrants", out var grants) ||
                !OnlyFields(grants, "fixed", "choices") ||
                (grants.TryGetProperty("fixed", out _) ==
                 grants.TryGetProperty("choices", out _)))
            {
                Issue("invalid-origin-equipment-option", request.OptionId,
                    "Equipment effects must author exactly one supported fixed or choice block.");
                return Finish("unavailable");
            }
            if (Array(grants, "fixed", out var fixedGrants))
            {
                if (fixedGrants.GetArrayLength() != 1)
                {
                    Issue("invalid-origin-equipment-option", request.OptionId,
                        "Expected exactly one authored fixed equipment qualification.");
                    return Finish("unavailable");
                }
                var grant = fixedGrants[0];
                var id = Text(grant, "id");
                var target = Text(grant, "targetId");
                if (grant.EnumerateObject().Any(prop => prop.Name is not ("id" or "targetId")) ||
                    !ValidComponent(id) || !ValidComponent(target) ||
                    !ValidToolType(target!) ||
                    request.SelectedEquipmentTypes is { Count: > 0 } ||
                    Text(selectedEffect, "equipmentTypeId") != target ||
                    selectedEffect.TryGetProperty("equipmentTypeChoiceId", out _) ||
                    selectedEffect.TryGetProperty("choiceCategory", out _) ||
                    selectedEffect.TryGetProperty("choiceCount", out _))
                {
                    Issue("invalid-origin-equipment-option", request.OptionId,
                        "Only the authored fixed tool type can be previewed.");
                    return Finish("unavailable");
                }
                effects.Add(new("equipment-proficiency", request.SourceId, request.SourceOccurrenceId,
                    request.BenefitId, request.OptionId, id!, target));
            }
            else if (Array(grants, "choices", out var equipmentChoices))
            {
                if (equipmentChoices.GetArrayLength() != 1)
                {
                    Issue("invalid-origin-equipment-choice", request.OptionId,
                        "Current nested options require exactly one counted equipment choice.");
                    return Finish("unavailable");
                }
                var choice = equipmentChoices[0];
                var choiceId = Text(choice, "id");
                var count = Number(choice, "count");
                var category = Text(choice, "fromCategory");
                if (choice.EnumerateObject().Any(prop => prop.Name is not
                        ("id" or "count" or "fromCategory" or "fromGroupIds" or
                         "fromTypeIds" or "selectionKind")) ||
                    !ValidComponent(choiceId) || count is not 1 ||
                    category != "tool" || Text(choice, "selectionKind") is not (null or "equipment-type") ||
                    Text(selectedEffect, "equipmentTypeId") is not null ||
                    Number(selectedEffect, "choiceCount") != count ||
                    !ValidComponent(Text(selectedEffect, "choiceCategory")) ||
                    (Text(selectedEffect, "equipmentTypeChoiceId") is string link &&
                     link != choiceId) ||
                    !Array(choice, "fromGroupIds", out var groupIds) ||
                    groupIds.GetArrayLength() is < 1 or > 32)
                {
                    Issue("invalid-origin-equipment-choice", request.OptionId,
                        "A nested equipment choice needs one specific tool type and authored groups.");
                    return Finish("unavailable");
                }
                var groups = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in groupIds.EnumerateArray())
                {
                    var group = member.ValueKind == JsonValueKind.String ? member.GetString() : null;
                    if (!ValidComponent(group) || !groups.Add(group!) ||
                        !catalog.TryGetById(group!, out var groupEntity) ||
                        groupEntity?.Category != "equipment-group" ||
                        Text(groupEntity.Definition, "equipmentCategory") != "tool")
                    {
                        Issue("invalid-origin-equipment-group", request.OptionId,
                            "Tool choice filters must reference distinct native Tool groups.");
                        return Finish("unavailable");
                    }
                }
                var permitted = new SortedSet<string>(StringComparer.Ordinal);
                if (choice.TryGetProperty("fromTypeIds", out var explicitTypes))
                {
                    if (explicitTypes.ValueKind != JsonValueKind.Array ||
                        explicitTypes.GetArrayLength() is < 1 or > 100)
                    {
                        Issue("invalid-origin-equipment-types", request.OptionId,
                            "Explicit type restrictions must contain bounded native references.");
                        return Finish("unavailable");
                    }
                    foreach (var candidate in explicitTypes.EnumerateArray())
                    {
                        if (candidate.ValueKind != JsonValueKind.String ||
                            !ValidToolType(candidate.GetString()!) ||
                            !InAnyGroup(candidate.GetString()!, groups) ||
                            !permitted.Add(candidate.GetString()!))
                        {
                            Issue("invalid-origin-equipment-types", request.OptionId,
                                "Explicit type list contains a noncanonical or excluded tool.");
                            return Finish("unavailable");
                        }
                    }
                }
                else
                {
                    for (var offset = 0; ; offset += 100)
                    {
                        var page = catalog.Search(category: "equipment-type", offset: offset, limit: 100);
                        foreach (var equipment in page.Items)
                            if (ValidToolType(equipment.Id) && InAnyGroup(equipment.Id, groups))
                                permitted.Add(equipment.Id);
                        if (offset + page.Items.Length >= page.Total) break;
                    }
                }
                if (permitted.Count < count)
                {
                    Issue("origin-equipment-options-unavailable", choiceId!,
                        "The native equipment catalog does not contain the required eligible types.");
                    return Finish("unavailable");
                }
                required.Add(new(choiceId!, count.Value, permitted.ToArray()));
                if (request.SelectedEquipmentTypes?.TryGetValue(choiceId!, out var selected) != true)
                    needsChoice = true;
                else if (selected is null || selected.Count != count.Value ||
                    selected.Distinct(StringComparer.Ordinal).Count() != count.Value ||
                    selected.Any(id => !permitted.Contains(id)))
                {
                    Issue("invalid-origin-equipment-selection", choiceId!,
                        "Select the exact number of distinct eligible tool types.");
                }
                else
                    effects.Add(new("equipment-proficiency", request.SourceId,
                        request.SourceOccurrenceId, request.BenefitId, request.OptionId,
                        choiceId!, selected[0]));
            }
            else
            {
                Issue("invalid-origin-equipment-option", request.OptionId,
                    "Nested equipment grants must be an array of fixed or counted choices.");
                return Finish("unavailable");
            }
        }

        // A caller may not sneak an unrelated qualified type into the preview.
        var allowedChoices = required.Select(item => item.GrantId).ToHashSet(StringComparer.Ordinal);
        foreach (var key in request.SelectedEquipmentTypes?.Keys ?? [])
            if (!allowedChoices.Contains(key))
                Issue("unexpected-origin-equipment-selection", key,
                    "The equipment choice does not belong to the selected option.");

        return Finish(issues.Count != 0 ? "invalid" :
            needsChoice ? "needs-choices" : "preview");
    }

    private bool ValidToolType(string id) =>
        catalog.TryGetById(id, out var entity) &&
        entity?.Category == "equipment-type" &&
        Text(entity.Definition, "equipmentCategory") == "tool";

    private bool InAnyGroup(string typeId, HashSet<string> groupIds)
    {
        if (!catalog.TryGetById(typeId, out var type) ||
            type?.Category != "equipment-type" ||
            !Array(type.Definition, "groupIds", out var memberships))
            return false;
        foreach (var direct in memberships.EnumerateArray())
        {
            if (direct.ValueKind != JsonValueKind.String) return false;
            if (Walk(direct.GetString()!, new HashSet<string>(StringComparer.Ordinal)))
                return true;
        }
        return false;

        bool Walk(string id, HashSet<string> visited)
        {
            if (!visited.Add(id) ||
                !catalog.TryGetById(id, out var entity) ||
                entity?.Category != "equipment-group" ||
                Text(entity.Definition, "equipmentCategory") != "tool")
                return false;
            if (groupIds.Contains(id)) return true;
            if (!Array(entity.Definition, "parentGroupIds", out var parents))
                return false;
            foreach (var parent in parents.EnumerateArray())
                if (parent.ValueKind == JsonValueKind.String &&
                    Walk(parent.GetString()!, new HashSet<string>(visited, StringComparer.Ordinal)))
                    return true;
            return false;
        }
    }

    // Unknown fields can carry unmodeled conditions. Never silently turn
    // an authored conditional option into an unconditional preview.
    private static bool OnlyFields(JsonElement source, params string[] allowed) =>
        source.ValueKind == JsonValueKind.Object &&
        source.EnumerateObject().All(field => allowed.Contains(field.Name,
            StringComparer.Ordinal));

    private static bool ValidComponent(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 160 &&
        value == value.Trim() && !value.Contains('/');
    private static bool Object(JsonElement source, string name, out JsonElement node)
    {
        node = default;
        return source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out node) && node.ValueKind == JsonValueKind.Object;
    }
    private static bool Array(JsonElement source, string name, out JsonElement node)
    {
        node = default;
        return source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out node) && node.ValueKind == JsonValueKind.Array;
    }
    private static string? Text(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString() : null;
    private static int? Number(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.Number &&
        node.TryGetInt32(out var result) ? result : null;
    private static bool? Boolean(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(name, out var node) &&
        node.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? node.GetBoolean() : null;
}
