using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Character identity and package choices are supplied by the consumer. Only
/// explicit, authored native materialization definitions can generate grants.
/// Prose / older equipment options never become invented item identities.
/// </summary>
public sealed record NativeStartingEquipmentRequest(
    string RulesetId,
    string Fingerprint,
    IReadOnlyList<NativeAdvancement> Advancements,
    IReadOnlyDictionary<string, string>? SelectedPackages = null);

public sealed record NativeStartingEquipmentGrant(
    string GrantKey, string Kind, string TargetKey, string DisplayName, int Quantity);
public sealed record NativeStartingEquipmentPackage(string Id, string Name);
public sealed record NativeStartingEquipmentChoice(
    string ChoiceKey, IReadOnlyList<NativeStartingEquipmentPackage> Options);
public sealed record NativeStartingEquipmentProjection(
    int ContractVersion, string RulesetId, string Fingerprint, string Status,
    IReadOnlyList<NativeStartingEquipmentGrant> Grants,
    IReadOnlyList<NativeStartingEquipmentChoice> Choices,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// A materialization block is intentionally different from an ordinary
/// startingEquipment description. Existing package prose and unfinished
/// reconciliations are never assumed to be executable.
/// </summary>
public sealed class NativeStartingEquipmentResolver(NativeCatalogSnapshot catalog)
{
    public NativeStartingEquipmentProjection Resolve(NativeStartingEquipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var grants = new List<NativeStartingEquipmentGrant>();
        var choices = new List<NativeStartingEquipmentChoice>();
        var issues = new List<NativeIssue>();
        var unresolved = false;
        void Issue(string code, string key, string detail) {
            issues.Add(new NativeIssue(code, key, detail)); unresolved = true;
        }

        if (request.RulesetId != catalog.RulesetId ||
            request.Fingerprint != catalog.Fingerprint)
            return Result("invalid", [
                new("ruleset-version-mismatch", "ruleset", "Native ruleset snapshot changed.")
            ]);
        if (request.Advancements is null || request.Advancements.Count is < 1 or > 32 ||
            request.SelectedPackages?.Count > 64)
            return Result("invalid", [
                new("invalid-input", "advancements", "A bounded class history is required.")
            ]);

        var seenOccurrences = new HashSet<string>(StringComparer.Ordinal);
        var knownChoices = new HashSet<string>(StringComparer.Ordinal);
        foreach (var occurrence in request.Advancements)
        {
            if (occurrence is null || occurrence.Level is < 1 or > 100 ||
                string.IsNullOrWhiteSpace(occurrence.OccurrenceId) ||
                occurrence.OccurrenceId.Length > 80 ||
                !seenOccurrences.Add(occurrence.OccurrenceId) ||
                string.IsNullOrWhiteSpace(occurrence.ClassId) ||
                !catalog.TryGetById(occurrence.ClassId, out var entity) ||
                entity?.Category != "class")
            {
                return Result("invalid", [
                    new("invalid-class", "advancements", "Saved class occurrence has no verified native class identity.")
                ]);
            }
            var definition = entity.Definition;
            if (!TryObject(definition, "definition", out var details) ||
                !TryObject(details, "startingEquipment", out var equipment) ||
                !TryObject(equipment, "materialization", out var materialization))
            {
                Issue("native-equipment-not-reconciled", occurrence.ClassId,
                    "The class starting equipment is authored but has no executable native grants.");
                continue;
            }
            var source = "native." + occurrence.OccurrenceId + ".";
            var countBefore = grants.Count;
            if (materialization.TryGetProperty("fixedGrants", out var fixedGrants)) {
                if (fixedGrants.ValueKind != JsonValueKind.Array)
                    Issue("invalid-native-equipment", occurrence.ClassId,
                        "Fixed grants must be a list.");
                else
                    Collect(fixedGrants, source, grants, Issue);
            }
            if (materialization.TryGetProperty("packages", out var packages))
            {
                if (packages.ValueKind != JsonValueKind.Array ||
                    packages.GetArrayLength() is < 1 or > 32)
                {
                    Issue("invalid-native-equipment", occurrence.ClassId,
                        "Packages must be a nonempty bounded list.");
                    continue;
                }
                var choiceKey = "starting-equipment." + occurrence.OccurrenceId;
                knownChoices.Add(choiceKey);
                var allOptions = new List<NativeStartingEquipmentPackage>();
                var selected = request.SelectedPackages?.GetValueOrDefault(choiceKey);
                JsonElement? selectedPackage = null;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in packages.EnumerateArray())
                {
                    var id = Text(option, "id");
                    var name = Text(option, "name");
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 120 ||
                        string.IsNullOrWhiteSpace(name) || name.Length > 160 ||
                        !ids.Add(id)) {
                        Issue("invalid-native-equipment", choiceKey,
                            "A native equipment package has missing or duplicate identity.");
                        continue;
                    }
                    allOptions.Add(new(id, name));
                    if (id == selected) selectedPackage = option;
                }
                if (selected is null) {
                    choices.Add(new(choiceKey, allOptions));
                } else if (selectedPackage is not JsonElement chosen ||
                    !TryArray(chosen, "grants", out var packageGrants))
                {
                    Issue("invalid-equipment-selection", choiceKey,
                        "Saved package choice is not a valid native equipment package.");
                }
                else Collect(packageGrants, source + selected + ".", grants, Issue);
            }
            // Materialization is complete only with at least one grant or a
            // selected package. A class may explicitly grant nothing.
            if (grants.Count == countBefore &&
                !materialization.TryGetProperty("packages", out _) &&
                Text(materialization, "empty") != "true" &&
                (!materialization.TryGetProperty("empty", out var empty) ||
                    empty.ValueKind != JsonValueKind.True))
                Issue("native-equipment-not-reconciled", occurrence.ClassId,
                    "The native materialization has no verified grants.");
        }
        foreach (var choice in request.SelectedPackages?.Keys ?? [])
            if (!knownChoices.Contains(choice))
                Issue("invalid-equipment-selection", choice, "The selected package does not belong to an active class.");

        if (unresolved) return Result("unavailable", issues);
        if (choices.Count != 0) return Result("needs-choices", issues, choices);
        return Result("resolved", issues, [], grants);

        NativeStartingEquipmentProjection Result(
            string status, IReadOnlyList<NativeIssue> problems,
            IReadOnlyList<NativeStartingEquipmentChoice>? pending = null,
            IReadOnlyList<NativeStartingEquipmentGrant>? awarded = null) =>
            new(1, catalog.RulesetId, catalog.Fingerprint, status,
                awarded ?? [], pending ?? [], problems);
    }

    private void Collect(JsonElement list, string prefix,
        List<NativeStartingEquipmentGrant> grants, Action<string, string, string> issue)
    {
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 128) {
            issue("invalid-native-equipment", prefix, "Grants must be a bounded list.");
            return;
        }
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in list.EnumerateArray())
        {
            var key = Text(entry, "id");
            var kind = Text(entry, "kind");
            var target = kind switch {
                "item" => Text(entry, "itemId"),
                "custom" => Text(entry, "customName"),
                "currency" => Text(entry, "currencyKey"),
                _ => null
            };
            var quantity = Int(entry, "quantity");
            var valid = !string.IsNullOrWhiteSpace(key) && key.Length <= 120 &&
                used.Add(key) && !string.IsNullOrWhiteSpace(target) &&
                target.Length <= 200 && quantity is > 0 and <= 100000 &&
                kind is "item" or "custom" or "currency";
            if (!valid) {
                issue("invalid-native-equipment", prefix,
                    "A grant has an invalid identity, target, kind, or quantity.");
                continue;
            }
            if (kind == "item" &&
                (!catalog.TryGetById(target!, out var item) || item?.Category != "item")) {
                issue("native-item-unavailable", target!,
                    "Item identity must exist in the active native item catalog.");
                continue;
            }
            var name = kind == "item"
                ? catalog.TryGetById(target!, out var resolvedItem) ? resolvedItem!.Name : target!
                : target!;
            grants.Add(new(prefix + key, kind!, target!, name, quantity!.Value));
        }
    }

    private static bool TryObject(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out value) &&
            value.ValueKind == JsonValueKind.Object;
    }
    private static bool TryArray(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out value) &&
            value.ValueKind == JsonValueKind.Array;
    }
    private static string? Text(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object &&
        node.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? Int(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object &&
        node.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result) ? result : null;
}

