using System.Collections.Frozen;
using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Read-only, native-first character kernel. Holds an immutable catalog snapshot;
/// each request uses only indexed lookups. Unsupported mechanics are never guessed.
/// </summary>
public sealed class NativeCharacterProjector(NativeCatalogSnapshot catalog)
{
    private static readonly string[] AbilityKeys =
        ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"];
    private static readonly Dictionary<string, string> ExtendedSaves = new(StringComparer.Ordinal)
    {
        ["fortitude"] = "constitution", ["reflex"] = "dexterity", ["will"] = "wisdom"
    };

    public NativeCharacterProjection Project(NativeCharacterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = new SortedDictionary<string, NativeNumeric>(StringComparer.Ordinal);
        var issues = new List<NativeIssue>();
        var granted = new SortedSet<string>(StringComparer.Ordinal);
        var required = new SortedSet<string>(StringComparer.Ordinal);
        var resources = new List<NativeResource>();
        void Issue(string code, string path, string detail) => issues.Add(new(code, path, detail));

        if (!string.Equals(request.RulesetId, catalog.RulesetId, StringComparison.Ordinal) ||
            !string.Equals(request.Fingerprint, catalog.Fingerprint, StringComparison.Ordinal))
        {
            Issue("ruleset-version-mismatch", "ruleset", "The requested native version is not the loaded snapshot.");
            return Finish("invalid");
        }
        if (request.AbilityScores is null || request.Advancements is null)
        {
            Issue("invalid-input", "character", "AbilityScores and Advancements are required.");
            return Finish("invalid");
        }
        if (request.Advancements.Count > 100 || request.SelectedOptions?.Count > 200 ||
            request.AbilityScores.Count > 20)
        {
            Issue("request-too-large", "character", "Requested character exceeds evaluation limits.");
            return Finish("invalid");
        }
        var abilityMods = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in AbilityKeys)
        {
            ct.ThrowIfCancellationRequested();
            if (!request.AbilityScores.TryGetValue(key, out var score) || score is < 0 or > 1000)
            {
                values[$"ability.{key}.modifier"] = new("input-required", null, "Valid ability score required.");
                Issue("ability-required", $"ability.{key}", "Missing or invalid score.");
                continue;
            }
            var modifier = (int)Math.Floor((score - 10) / 2.0);
            abilityMods[key] = modifier;
            values[$"ability.{key}.score"] = new("resolved", score);
            values[$"ability.{key}.modifier"] = new("resolved", modifier);
        }

        int? proficiency = null;
        var savingThrowProficiencies = new HashSet<string>(StringComparer.Ordinal);
        var baseSaves = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalLevel = 0;
        var advancementUnresolved = false;
        var completedClasses = new Dictionary<string, int>(StringComparer.Ordinal);
        var seenOccurrences = new HashSet<string>(StringComparer.Ordinal);
        var spellcasters = 0;
        var warlockInvocationBudget = 0;
        foreach (var advancement in request.Advancements)
        {
            ct.ThrowIfCancellationRequested();
            if (advancement is null || string.IsNullOrWhiteSpace(advancement.OccurrenceId) ||
                string.IsNullOrWhiteSpace(advancement.ClassId) ||
                advancement.Level is < 1 or > 1000 || !catalog.TryGetById(advancement.ClassId, out var entity) ||
                entity is null || entity.Category != "class" ||
                !Object(entity.Definition, "definition", out var definition))
            {
                advancementUnresolved = true;
                Issue("unsupported-advancement", "advancements", "The class or advancement is unavailable in native content.");
                continue;
            }
            if (!Array(definition, "progression", out var progression))
            {
                advancementUnresolved = true;
                Issue("progression-unavailable", advancement.ClassId, "No executable progression table.");
                continue;
            }
            var rows = new Dictionary<int, JsonElement>();
            var invalidProgression = false;
            foreach (var row in progression.EnumerateArray())
            {
                var levelNumber = Number(row, "level");
                if (levelNumber is not > 0 || !rows.TryAdd(levelNumber.Value, row))
                {
                    invalidProgression = true;
                    break;
                }
            }
            if (invalidProgression)
            {
                advancementUnresolved = true;
                Issue("progression-invalid", advancement.ClassId,
                    "Native progression has duplicate, missing, or invalid level identifiers.");
                continue;
            }
            if (!rows.TryGetValue(advancement.Level, out var current))
            {
                advancementUnresolved = true;
                Issue("progression-unsupported", advancement.ClassId, "Selected class level has no native progression row.");
                continue;
            }
            if (Enumerable.Range(1, advancement.Level).Any(level => !rows.ContainsKey(level)))
            {
                advancementUnresolved = true;
                Issue("progression-gap", advancement.ClassId,
                    "Every prior class level must have an executable native progression row.");
                continue;
            }
            if (!seenOccurrences.Add(advancement.OccurrenceId))
            {
                advancementUnresolved = true;
                Issue("duplicate-advancement-occurrence", advancement.OccurrenceId,
                    "Advancement occurrence identities must be unique.");
                continue;
            }
            if (completedClasses.ContainsKey(advancement.ClassId))
            {
                advancementUnresolved = true;
                Issue("repeated-class-progression-unresolved", advancement.ClassId,
                    "Multiple occurrences of a single class need a native stacking policy.");
                continue;
            }
            totalLevel = checked(totalLevel + advancement.Level);
            completedClasses[advancement.ClassId] = advancement.Level;
            if (Array(definition, "savingThrowProficiencies", out var proficiencies))
                foreach (var save in proficiencies.EnumerateArray())
                    if (save.ValueKind == JsonValueKind.String) savingThrowProficiencies.Add(save.GetString()!);
            if (Number(current, "proficiencyBonus") is int pb)
                proficiency = Math.Max(proficiency ?? 0, pb);
            if (Object(current, "baseSaves", out var saves))
                foreach (var prop in saves.EnumerateObject())
                    if (prop.Value.TryGetInt32(out var baseValue))
                        baseSaves[prop.Name] = checked(baseSaves.GetValueOrDefault(prop.Name) + baseValue);

            for (var level = 1; level <= advancement.Level; level++)
            {
                if (!rows.TryGetValue(level, out var row))
                {
                    Issue("progression-gap", advancement.ClassId, $"Missing level {level}.");
                    continue;
                }
                if (Array(row, "featureGrants", out var features))
                    foreach (var feature in features.EnumerateArray())
                        if (feature.ValueKind == JsonValueKind.String)
                            granted.Add(feature.GetString()!);
            }
            if (Boolean(current, "subclassMilestone") == true)
                required.Add($"advancement.{advancement.ClassId}.subclass");
            if (advancement.ClassId == "class.warlock" &&
                Number(current, "invocationsElectiveTotal") is int count && count > 0)
                warlockInvocationBudget = count;

            var sorcery = Number(current, "sorceryPointMaximum");
            if (sorcery is > 0)
                resources.Add(Resource("resource.sorcery-points", sorcery.Value, "long-rest"));
            var pact = Number(current, "pactSlots");
            if (pact is > 0)
                resources.Add(Resource("resource.pact-magic", pact.Value, "unsupported",
                    Number(current, "pactSlotLevel")));
            if (Array(current, "spellSlotsBySpellLevel", out var slots))
            {
                spellcasters++;
                var tier = 0;
                foreach (var slot in slots.EnumerateArray())
                {
                    tier++;
                    if (slot.ValueKind == JsonValueKind.Number && slot.TryGetInt32(out var count) && count > 0)
                        resources.Add(Resource($"resource.spell-slot.{tier}", count, "unsupported", tier));
                }
            }
        }
        values["advancement.total-level"] = advancementUnresolved
            ? new("unsupported", null, "An advancement is not executable in the native snapshot.")
            : new("resolved", totalLevel);
        // Neither an incomplete level history nor max(per-class bonuses) is a valid
        // total-character-level proficiency calculation. Do not manufacture a value.
        if (completedClasses.Count > 1)
            Issue("multiclass-proficiency-unresolved", "proficiency.bonus",
                "Native total-level multiclass proficiency has no executable policy.");
        if (advancementUnresolved || completedClasses.Count > 1) proficiency = null;
        if (proficiency is int bonus)
            values["proficiency.bonus"] = new("resolved", bonus);
        else
        {
            values["proficiency.bonus"] = new("unsupported", null, "No effective native proficiency rule or explicit table bonus.");
            if (totalLevel > 0) Issue("proficiency-rule-unavailable", "proficiency.bonus", "No approved universal progression is executable.");
        }
        foreach (var key in AbilityKeys)
        {
            if (!abilityMods.TryGetValue(key, out var mod)) continue;
            values[$"save.{key}"] = advancementUnresolved || completedClasses.Count > 1
                ? new("unsupported", null, "Class save proficiencies are not validated for this advancement history.")
                : savingThrowProficiencies.Contains(key) && proficiency is int p
                ? new("resolved", checked(mod + p))
                : savingThrowProficiencies.Contains(key)
                    ? new("unsupported", null, "Proficiency progression unresolved.")
                    : new("resolved", mod);
        }
        if (completedClasses.Count > 1)
        {
            Issue("multiclass-save-policy-unresolved", "saving-throws",
                "Do not combine independent base-save tracks without an approved native rule.");
        }
        foreach (var (save, ability) in ExtendedSaves)
        {
            if (!abilityMods.TryGetValue(ability, out var mod)) continue;
            values[$"save.{save}"] = advancementUnresolved || completedClasses.Count > 1
                ? new("unsupported", null, "Multiclass base-save combination unresolved.")
                : baseSaves.TryGetValue(save, out var baseValue)
                    ? new("resolved", checked(mod + baseValue))
                : new("unsupported", null, "No explicit native extended-save progression.");
        }
        if (abilityMods.TryGetValue("dexterity", out var dex))
            values["initiative"] = request.ConditionIds is { Count: > 0 }
                ? new("unsupported", null, "Conditions have not been translated into initiative contributions.")
                : new("resolved", dex);
        if (request.BaseWalkSpeed is int walkSpeed)
        {
            var projectedSpeed = NativeConditionEffects.WalkSpeed(catalog,
                request.ConditionIds ?? [], walkSpeed);
            values["movement.walk"] = projectedSpeed.Result;
            issues.AddRange(projectedSpeed.Issues);
        }
        else Issue("movement-input-required", "movement.walk",
            "No authoritative base movement has been projected from native ancestry or features.");

        if (request.DefenseContributions is not null)
        {
            var contributions = new Dictionary<string, int>(request.DefenseContributions, StringComparer.Ordinal);
            if (!contributions.ContainsKey("dexterity") && abilityMods.TryGetValue("dexterity", out var dexterity))
                contributions.Add("dexterity", dexterity);
            var recognized = new HashSet<string>(["armor", "shield", "natural-armor", "dexterity", "dodge", "size", "deflection"], StringComparer.Ordinal);
            if (contributions.Keys.Any(k => !recognized.Contains(k)))
                Issue("defense-effects-unsupported", "defense", "Unknown or unconverted contribution.");
            else
            {
                foreach (var profile in Enum.GetValues<NativeDefenseProfile>())
                {
                    var result = NativeConditionEffects.ArmorClass(catalog,
                        request.ConditionIds ?? [], profile, contributions);
                    var profileId = profile switch {
                        NativeDefenseProfile.Normal => "normal",
                        NativeDefenseProfile.Touch => "touch",
                        NativeDefenseProfile.FlatFooted => "flat-footed",
                        NativeDefenseProfile.TouchFlatFooted => "touch-flat-footed",
                        _ => throw new InvalidOperationException("Unsupported defense profile.")
                    };
                    values[$"ac.{profileId}"] = result.Result;
                    issues.AddRange(result.Issues);
                }
                // Typed numbers are not yet independently granted by native items.
                Issue("equipment-grants-unverified", "defense",
                    "Supplied AC components are not derived from authorized native equipment grants.");
            }
        }
        else Issue("defense-input-required", "defense",
            "Typed equipment contributions are missing; AC is not projected.");

        ProjectSkillGroup("skill", request.Skills, proficiency);
        ProjectSkillGroup("competency", request.Competencies, proficiency);
        if (advancementUnresolved)
        {
            // Training eligibility and class-granted expertise can change the total.
            // Retain independently supplied ability values, but not skill outcomes.
            foreach (var id in values.Keys.Where(k => k.EndsWith(".total", StringComparison.Ordinal)).ToArray())
                values[id] = new("unsupported", null, "The character advancement history is incomplete.");
        }

        if (!advancementUnresolved && completedClasses.ContainsKey("class.warlock"))
        {
            var patronCount = 0;
            var pactCount = 0;
            foreach (var selected in request.SelectedOptions ?? [])
                if (catalog.TryGetById(selected, out var selection) && selection is not null)
                {
                    if (String(selection.Definition, "parentClass") != "class.warlock") continue;
                    if (selection.Category == "patron") patronCount++;
                    if (selection.Category == "pact") pactCount++;
                }
            if (patronCount == 0) required.Add("advancement.class.warlock.patron");
            if (pactCount == 0) required.Add("advancement.class.warlock.pact");
            if (patronCount > 1 || pactCount > 1)
                Issue("exclusive-selection-conflict", "class.warlock",
                    "Only one Patron and one Pact may be selected in this native class profile.");
        }
        var invocationExpenditure = 0;
        var selectedOccurrences = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in request.SelectedOptions ?? [])
        {
            ct.ThrowIfCancellationRequested();
            if (advancementUnresolved)
            {
                Issue("option-advancement-unresolved", option ?? "",
                    "Option legality needs a complete character advancement history.");
                continue;
            }
            if (!selectedOccurrences.Add(option))
            {
                Issue("option-occurrence-unresolved", option,
                    "Repeat selections require separate native purchase and target identities.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(option) || !catalog.TryGetById(option, out var rule) || rule is null)
            {
                Issue("option-not-native", option, "Option is not in the authoritative native snapshot.");
                continue;
            }
            if (rule.Category is "pact" or "patron" or "invocation" &&
                String(rule.Definition, "parentClass") != "class.warlock")
                Issue("option-parent-unavailable", option,
                    "This class option does not belong to the requested Warlock progression.");
            if (rule.Category is "pact" or "patron" &&
                Number(rule.Definition, "availableAtWarlockLevel") is int min &&
                completedClasses.GetValueOrDefault("class.warlock") < min)
                Issue("prerequisite-failed", option, "Warlock class level prerequisite not met.");
            else if (rule.Category == "invocation")
            {
                if (Number(rule.Definition, "minimumWarlockLevel") is int level &&
                    completedClasses.GetValueOrDefault("class.warlock") < level)
                    Issue("prerequisite-failed", option, "Warlock class level prerequisite not met.");
                var requiredPact = String(rule.Definition, "requiredPact");
                if (requiredPact is not null && !(request.SelectedOptions?.Contains(requiredPact) ?? false))
                    Issue("prerequisite-failed", option, "Required native pact has not been selected.");
                if (Number(rule.Definition, "cost") is int cost && cost >= 0)
                    invocationExpenditure = checked(invocationExpenditure + cost);
                else Issue("invocation-cost-unresolved", option, "No approved point cost exists.");
            }
            // A catalog entry's existence does not prove all its prerequisites or effects executable.
            if (rule.Category is not ("pact" or "patron" or "invocation"))
                Issue("option-evaluation-unavailable", option, "No normalized option eligibility evaluator for this kind.");
        }
        if (!advancementUnresolved && completedClasses.ContainsKey("class.warlock"))
        {
            if (invocationExpenditure < warlockInvocationBudget)
                required.Add($"advancement.class.warlock.invocations:{warlockInvocationBudget - invocationExpenditure}");
            if (invocationExpenditure > warlockInvocationBudget)
                Issue("invocation-budget-exceeded", "class.warlock",
                    "Selected invocations exceed the current native class level's elective budget.");
        }
        foreach (var id in granted.ToArray())
            if (!catalog.TryGetById(id, out var feature) || feature is null ||
                feature.Category != "classFeature")
            {
                granted.Remove(id);
                Issue("grant-definition-unavailable", id,
                    "Granted feature is absent from authoritative native class feature content.");
            }
        if (advancementUnresolved)
        {
            // A caller must never consume apparently valid class benefits from
            // only the accepted subset of a rejected advancement history.
            granted.Clear();
            resources.Clear();
            required.Clear();
            Issue("class-derived-results-withheld", "advancements",
                "Features, resources, and choices depend on the rejected advancement history.");
        }
        if (spellcasters > 1)
        {
            resources.RemoveAll(r => r.Id.StartsWith("resource.spell-slot.", StringComparison.Ordinal));
            Issue("multiclass-spell-slots-unsupported", "resources", "No approved multiclass casting combination evaluator.");
        }
        if (!advancementUnresolved && request.HitPointGains is not null &&
            request.HitPointGains.Count > 0 &&
            abilityMods.TryGetValue("constitution", out var constitution) && totalLevel > 0)
        {
            var count = request.HitPointGains.Count;
            if (count == totalLevel)
                values["hit-points.maximum"] = new("resolved",
                    checked(request.HitPointGains.Values.Sum() + constitution * totalLevel));
            else
                Issue("hit-point-history-incomplete", "hit-points", "Expected a gain for every class level.");
        }
        else if (advancementUnresolved)
            values["hit-points.maximum"] = new("unsupported", null,
                "Cannot calculate total HP with an unresolved advancement.");
        else if (totalLevel > 0)
            Issue("hit-point-input-required", "hit-points", "Per-level hit point gains required; no assumed dice rolls.");

        return Finish(issues.Count == 0 ? "resolved" : "partial");

        NativeResource Resource(string id, int maximum, string recovery, int? spellLevel = null)
        {
            int? current = null;
            if (request.CurrentResources?.TryGetValue(id, out var used) == true)
            {
                if (used < 0 || used > maximum)
                    Issue("resource-out-of-range", id, "Resource current value is outside [0, maximum].");
                else current = used;
            }
            return new NativeResource(id, maximum, current, recovery, spellLevel);
        }

        void ProjectSkillGroup(string prefix,
            IReadOnlyDictionary<string, NativeSkillTraining>? training, int? prof)
        {
            if (training is null) return;
            foreach (var (id, state) in training)
            {
                ct.ThrowIfCancellationRequested();
                if (!catalog.TryGetById(id, out var rule) || rule is null ||
                    !string.Equals(rule.Category, prefix, StringComparison.Ordinal))
                {
                    Issue("unknown-training-identity", id, "Not a native skill or competency of this kind.");
                    continue;
                }
                if (state.Ranks < 0)
                {
                    Issue("negative-ranks", id, "Ranks can not be negative.");
                    continue;
                }
                // Composite parents are evaluated from their independently ranked children.
                if (prefix == "skill" && Array(rule.Definition, "childSkills", out _)) continue;
                if (state.Proficient && state.Ranks > 0)
                {
                    Issue("training-profile-conflict", id, "Ranked and proficiency facets are separate profiles.");
                    values[$"{id}.total"] = new("unsupported", null, "Select one approved numeric profile.");
                    continue;
                }
                var ability = state.Ability ?? String(rule.Definition, "defaultAbility");
                if (ability is null && prefix == "competency" &&
                    Array(rule.Definition, "facets", out var facets))
                    ability = facets.EnumerateArray().Select(f => String(f, "governingAbility"))
                        .FirstOrDefault(x => x is not null);
                if (ability is null || !abilityMods.TryGetValue(ability, out var abilityValue))
                {
                    values[$"{id}.total"] = new("unsupported", null, "No approved governing ability for the selected facet.");
                    Issue("training-ability-unresolved", id, "An explicit ability/facet is required.");
                    continue;
                }
                if (state.Proficient && prof is null)
                {
                    values[$"{id}.total"] = new("unsupported", null, "Proficiency bonus unresolved.");
                    continue;
                }
                values[$"{id}.total"] = new("resolved", checked(abilityValue + state.Ranks +
                    (state.Proficient ? prof!.Value : 0) + state.MiscModifier));
            }
            // Composite children are calculated first, parent modifiers afterwards.
            if (prefix == "skill")
                foreach (var (id, state) in training)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!catalog.TryGetById(id, out var rule) || rule is null ||
                        !Array(rule.Definition, "childSkills", out var children)) continue;
                    var totals = new List<int>();
                    foreach (var child in children.EnumerateArray())
                    {
                        if (child.ValueKind != JsonValueKind.String ||
                            !values.TryGetValue($"{child.GetString()}.total", out var value) ||
                            value.Status != "resolved" || value.Value is null)
                        {
                            totals.Clear();
                            break;
                        }
                        totals.Add(value.Value.Value);
                    }
                    if (totals.Count == 0 || state.Ranks > 0 || state.Proficient)
                    {
                        Issue("composite-unresolved", id,
                            "Composite requires resolved children and an unranked parent modifier.");
                        values[$"{id}.total"] = new("unsupported", null, "Composite components incomplete.");
                    }
                    else
                        values[$"{id}.total"] = new("resolved", checked(totals.Sum() / totals.Count + state.MiscModifier));
                }
        }

        NativeCharacterProjection Finish(string status) => new(1, catalog.RulesetId, catalog.Fingerprint,
            status, values.ToFrozenDictionary(StringComparer.Ordinal), granted.ToArray(),
            resources.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray(), required.ToArray(),
            issues.OrderBy(i => i.Path, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal).ToArray());
    }

    private static bool Object(JsonElement element, string name, out JsonElement result)
    {
        result = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out result) &&
               result.ValueKind == JsonValueKind.Object;
    }
    private static bool Array(JsonElement element, string name, out JsonElement result)
    {
        result = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out result) &&
               result.ValueKind == JsonValueKind.Array;
    }
    private static int? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var p) &&
        p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? n : null;
    private static bool? Boolean(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var p) &&
        p.ValueKind is JsonValueKind.True or JsonValueKind.False ? p.GetBoolean() : null;
    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var p) &&
        p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
