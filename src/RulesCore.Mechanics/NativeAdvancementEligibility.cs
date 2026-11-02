using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// A candidate eligibility decision, not a mutation or approval to publish content.
/// Unknown mechanics never become a positive eligibility decision.
/// </summary>
public sealed record NativeEligibilityRequest(
    string RulesetId,
    string Fingerprint,
    string CandidateId,
    NativeCharacterRequest Character,
    int? HitDieValue = null);

public sealed record NativeEligibilityRequirement(
    string Key, string Status, string Detail);

public sealed record NativeEligibilityResult(
    int ContractVersion,
    string RulesetId,
    string Fingerprint,
    string CandidateId,
    string? CandidateKind,
    string Status,
    bool? Eligible,
    IReadOnlyList<NativeEligibilityRequirement> Requirements);

/// <summary>
/// Native Class/Warlock-option prerequisites used by the Character Sheet builder.
/// This does not infer a multiclass policy or undocumented invocation clauses.
/// </summary>
public sealed class NativeAdvancementEligibility(NativeCatalogSnapshot catalog)
{
    public NativeEligibilityResult Evaluate(NativeEligibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requirements = new List<NativeEligibilityRequirement>();
        string? kind = null;
        void Add(string key, string status, string detail) => requirements.Add(new(key, status, detail));
        NativeEligibilityResult Finish(string? forceStatus = null)
        {
            var state = forceStatus ?? (requirements.Any(r => r.Status == "failed")
                ? "ineligible" : requirements.Any(r => r.Status == "unresolved")
                    ? "unresolved" : "eligible");
            return new NativeEligibilityResult(1, catalog.RulesetId, catalog.Fingerprint,
                request.CandidateId ?? "", kind, state,
                state == "eligible" ? true : state == "ineligible" ? false : null,
                requirements.ToArray());
        }

        if (request.RulesetId != catalog.RulesetId || request.Fingerprint != catalog.Fingerprint)
        {
            Add("ruleset", "unresolved", "The selected catalog fingerprint differs from the loaded ruleset.");
            return Finish("invalid");
        }
        if (request.Character is null || request.Character.AbilityScores is null ||
            request.Character.Advancements is null ||
            request.Character.RulesetId != catalog.RulesetId ||
            request.Character.Fingerprint != catalog.Fingerprint ||
            string.IsNullOrWhiteSpace(request.CandidateId) || request.CandidateId.Length > 160 ||
            request.Character.Advancements.Count > 100 || request.Character.SelectedOptions?.Count > 200 ||
            request.Character.SelectedOptions?.Any(string.IsNullOrWhiteSpace) == true)
        {
            Add("character", "unresolved", "A valid, version-pinned native character and candidate ID are required.");
            return Finish("invalid");
        }
        if (!catalog.TryGetById(request.CandidateId, out var candidate) || candidate is null)
        {
            Add("candidate", "unresolved", "The candidate has no native catalog identity.");
            return Finish();
        }
        kind = candidate.Category;
        if (kind is not ("class" or "patron" or "pact" or "invocation"))
        {
            Add("candidate-kind", "unresolved", "This option kind has no executable eligibility policy.");
            return Finish();
        }

        // An advancement contains the attained level for each unique class, not one
        // occurrence per level. Reject ambiguous histories before looking at any option.
        var classLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        var occurrenceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var advancement in request.Character.Advancements)
        {
            if (advancement is null || string.IsNullOrWhiteSpace(advancement.ClassId) ||
                string.IsNullOrWhiteSpace(advancement.OccurrenceId) || advancement.Level is < 1 or > 1000 ||
                !occurrenceIds.Add(advancement.OccurrenceId) ||
                !classLevels.TryAdd(advancement.ClassId, advancement.Level) ||
                !catalog.TryGetById(advancement.ClassId, out var heldClass) ||
                heldClass?.Category != "class" || !ReadDefinition(heldClass, out _))
            {
                Add("advancements", "unresolved", "Existing class advancements contain missing, repeated, or nonnative entries.");
                return Finish();
            }
            if (!ReadDefinition(heldClass!, out var heldDefinition) ||
                !heldDefinition.TryGetProperty("progression", out var heldRows) ||
                heldRows.ValueKind != JsonValueKind.Array)
            {
                Add("advancements", "unresolved", "An existing class has no executable progression.");
                return Finish();
            }
            var levelRows = heldRows.EnumerateArray().Select(r => ReadInt(r, "level")).ToArray();
            if (levelRows.Length != levelRows.Distinct().Count() ||
                Enumerable.Range(1, advancement.Level).Any(level => !levelRows.Contains(level)))
            {
                Add("advancements", "unresolved", "The attained class level has missing or ambiguous native progression rows.");
                return Finish();
            }
        }

        var selected = request.Character.SelectedOptions ?? [];
        if (selected.Any(id => !catalog.TryGetById(id, out var entry) || entry is null))
        {
            Add("selected-options", "unresolved", "A selected option has no native identity.");
            return Finish();
        }
        if (selected.Distinct(StringComparer.Ordinal).Count() != selected.Count)
        {
            Add("selected-options", "unresolved", "Repeated option IDs need purchase occurrence and target identities.");
            return Finish();
        }
        // Candidate identity is never inferred from the name or from a source edition.
        if (kind == "class")
        {
            if (!ReadDefinition(candidate, out var definition) ||
                !definition.TryGetProperty("progression", out var progression) ||
                progression.ValueKind != JsonValueKind.Array)
            {
                Add("class-progression", "unresolved", "This native class has no executable progression.");
                return Finish();
            }
            var nextLevel = classLevels.GetValueOrDefault(candidate.Id) + 1;
            if (classLevels.Count > (classLevels.ContainsKey(candidate.Id) ? 1 : 0))
                Add("multiclass-policy", "unresolved", "Multiclass advancement is not yet governed by an approved executable policy.");
            var row = progression.EnumerateArray().Count(element =>
                ReadInt(element, "level") == nextLevel);
            if (row == 0)
                Add("class-level", "failed", "No native progression row exists at the requested next class level.");
            else if (row > 1)
                Add("class-level", "unresolved", "Duplicate native progression rows prevent selection.");
            else
                Add("class-level", "satisfied", "The requested level exists in the native class progression.");

            // A hit-point gain is a class-rule input, not an arbitrary positive
            // value accepted by Character Sheet. An omitted value is allowed
            // during eligibility discovery, but a supplied value is validated.
            if (request.HitDieValue is int gain)
            {
                if (!definition.TryGetProperty("hitDie", out var die) ||
                    ReadInt(die, "number") != 1 ||
                    ReadInt(die, "faces") is not int faces || faces is < 1 or > 1000)
                    Add("hit-die", "unresolved",
                        "The native class does not expose a supported hit die profile.");
                else
                    Add("hit-die", gain is >= 1 && gain <= faces ? "satisfied" : "failed",
                        $"The supplied hit point gain must be between 1 and {faces}.");
            }
            return Finish();
        }

        if (ReadString(candidate.Definition, "parentClass") != "class.warlock")
        {
            Add("parent-class", "unresolved", "This option has no supported parent-class rule.");
            return Finish();
        }
        var warlockLevel = classLevels.GetValueOrDefault("class.warlock");
        if (warlockLevel == 0)
            Add("parent-class", "failed", "Requires an existing Warlock class advancement.");
        else Add("parent-class", "satisfied", "A Warlock class advancement is present.");

        var minimumLevelKey = kind == "invocation" ? "minimumWarlockLevel" : "availableAtWarlockLevel";
        if (ReadInt(candidate.Definition, minimumLevelKey) is int requiredLevel && requiredLevel > 0)
        {
            Add("minimum-class-level", warlockLevel >= requiredLevel ? "satisfied" : "failed",
                $"Requires Warlock level {requiredLevel}; current level is {warlockLevel}.");
        }
        else Add("minimum-class-level", "unresolved", "The native option does not specify a valid minimum level.");

        var timesSelected = selected.Count(id => id == candidate.Id);
        if (kind is "patron" or "pact")
        {
            var competing = selected.Where(id => id != candidate.Id &&
                catalog.TryGetById(id, out var other) && other is not null &&
                other.Category == kind && ReadString(other.Definition, "parentClass") == "class.warlock")
                .ToArray();
            if (timesSelected > 0 || competing.Length > 0)
                Add("exclusive-choice", "failed", "A Warlock may not select another option in this exclusive pillar.");
            else Add("exclusive-choice", "satisfied", "The pillar has no existing selection.");
            return Finish();
        }

        // Invocations can be repeatable only with a distinct eligible target. The
        // current request supplies IDs, not purchase occurrences and selected targets.
        if (timesSelected > 0)
            Add("repeat-selection", ReadBool(candidate.Definition, "repeatable") == true
                    ? "unresolved" : "failed",
                "Repeated invocations require a separate purchase and target identity.");
        if (ReadString(candidate.Definition, "requiredPact") is string requiredPact &&
            !string.IsNullOrWhiteSpace(requiredPact))
            Add("required-pact", selected.Contains(requiredPact) ? "satisfied" : "failed",
                $"Requires native Pact {requiredPact}.");
        if (ReadString(candidate.Definition, "cantripPrerequisite") is { Length: > 0 })
            Add("cantrip-prerequisite", "unresolved",
                "Known cantrip eligibility and the invocation target are not available in this contract.");
        // A new explicit prerequisite field must not be silently ignored when
        // the catalog evolves. Textual description restrictions remain a known gap.
        foreach (var field in candidate.Definition.EnumerateObject())
            if ((field.Name.StartsWith("required", StringComparison.OrdinalIgnoreCase) ||
                 field.Name.StartsWith("minimum", StringComparison.OrdinalIgnoreCase) ||
                 field.Name.EndsWith("Prerequisite", StringComparison.OrdinalIgnoreCase)) &&
                field.Name is not ("requiredPact" or "minimumWarlockLevel" or "cantripPrerequisite"))
                Add("unmodeled-prerequisite", "unresolved",
                    $"Native requirement '{field.Name}' needs an executable eligibility rule.");

        if (catalog.TryGetById("class.warlock", out var warlock) && warlock is not null &&
            ReadDefinition(warlock, out var warlockDef) &&
            warlockDef.TryGetProperty("progression", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            var current = rows.EnumerateArray().FirstOrDefault(r => ReadInt(r, "level") == warlockLevel);
            if (current.ValueKind != JsonValueKind.Undefined &&
                ReadInt(current, "invocationsElectiveTotal") is int budget && budget >= 0 &&
                ReadInt(candidate.Definition, "cost") is int cost && cost >= 0)
            {
                var spent = 0;
                var valid = true;
                foreach (var id in selected)
                {
                    if (!catalog.TryGetById(id, out var option) || option is null)
                    { valid = false; break; }
                    if (option.Category != "invocation") continue;
                    if (ReadInt(option.Definition, "cost") is not int price || price < 0)
                    { valid = false; break; }
                    spent = checked(spent + price);
                }
                Add("invocation-budget", !valid ? "unresolved" : spent + cost <= budget ? "satisfied" : "failed",
                    valid ? $"Elective budget is {budget}; spent {spent}; candidate costs {cost}." :
                        "Existing invocation cost is not executable.");
            }
            else Add("invocation-budget", "unresolved", "No native invocation budget applies to this character level.");
        }
        else Add("invocation-budget", "unresolved", "The native Warlock progression is not available.");
        return Finish();
    }

    private static bool ReadDefinition(NativeRuleEntity rule, out JsonElement definition)
    {
        definition = default;
        return rule.Definition.ValueKind == JsonValueKind.Object &&
            rule.Definition.TryGetProperty("definition", out definition) &&
            definition.ValueKind == JsonValueKind.Object;
    }
    private static string? ReadString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? ReadInt(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;
    private static bool? ReadBool(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value) &&
        (value.ValueKind is JsonValueKind.True or JsonValueKind.False) ? value.GetBoolean() : null;
}
