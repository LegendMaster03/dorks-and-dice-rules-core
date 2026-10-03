using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Rules;

/// <summary>
/// Enriches the private first-party Rules Wiki catalog with scan/filter fields that are useful
/// for the cross-edition reference browser. The public resolved-rules catalog intentionally
/// continues to use <see cref="RuleBrowserSummaryProjector"/> unchanged.
/// </summary>
public sealed class WikiReferenceBrowserProjectionService(RulesCoreDbContext dbContext)
{
    public async Task<WikiReferenceCatalogView> EnrichAsync(
        WikiReferenceCatalogView catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.References.Count == 0) return catalog;

        var revisionIds = catalog.References
            .Select(value => value.BrowseVariation.SourceEntityRevisionId)
            .Distinct()
            .ToArray();
        var revisions = await dbContext.SourceEntityRevisions
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);

        var documents = new Dictionary<Guid, JsonElement>();
        foreach (var revision in revisions)
        {
            using var document = JsonDocument.Parse(revision.GetMechanicalContentJson());
            documents[revision.Id] = document.RootElement.Clone();
        }

        var references = catalog.References
            .Select(reference => documents.TryGetValue(
                    reference.BrowseVariation.SourceEntityRevisionId,
                    out var document)
                ? reference with
                {
                    BrowserFields = WikiReferenceBrowserProjection.Project(
                        reference.BrowseVariation.Category,
                        document)
                }
                : reference)
            .ToArray();

        return catalog with { References = references };
    }
}

/// <summary>
/// Rules Core-owned presentation projection for the private Rules Wiki contract. Values here are
/// summaries of structured normalized mechanics only; source-native text is never parsed in the Wiki.
/// Unknown mechanics remain available through the full normalized document on the detail contract.
/// </summary>
public static class WikiReferenceBrowserProjection
{
    private static readonly IReadOnlyDictionary<string, string> AbilityLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = "STR",
            ["dexterity"] = "DEX",
            ["constitution"] = "CON",
            ["intelligence"] = "INT",
            ["wisdom"] = "WIS",
            ["charisma"] = "CHA",
            ["str"] = "STR",
            ["dex"] = "DEX",
            ["con"] = "CON",
            ["int"] = "INT",
            ["wis"] = "WIS",
            ["cha"] = "CHA"
        };

    public static IReadOnlyList<ResolvedRuleBrowserFieldView> Project(
        string entityType,
        JsonElement document)
    {
        var fields = RuleBrowserSummaryProjector.Project(entityType, document).ToList();
        var keys = fields.Select(value => value.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        void Add(string key, string label, string? value)
        {
            if (keys.Contains(key) || string.IsNullOrWhiteSpace(value)) return;
            fields.Add(new ResolvedRuleBrowserFieldView(key, label, value.Trim()));
            keys.Add(key);
        }

        var normalized = RuleConceptEntityTypes.Normalize(entityType).ToLowerInvariant();
        switch (normalized)
        {
            case "spell":
                Add("school", "School", ReadSummary(
                    ReadProperty(document, "school")
                    ?? ReadThreeXField(document, "School")));
                Add("castingTime", "Casting Time", FormatCastingTime(document));
                Add("range", "Range", FormatRange(document));
                Add("components", "Components", FormatComponents(document));
                Add("duration", "Duration", FormatDuration(document));
                Add("concentration", "Concentration", FormatConcentration(document));
                Add("ritual", "Ritual", FormatBoolean(ReadPath(document, "meta", "ritual") ?? ReadProperty(document, "ritual")));
                Add("subschool", "Subschool", ReadSummary(
                    ReadProperty(document, "subschool")
                    ?? ReadThreeXField(document, "Subschool")));
                Add("descriptors", "Descriptors", ReadSummary(
                    ReadProperty(document, "descriptors")
                    ?? ReadProperty(document, "descriptor")
                    ?? ReadThreeXField(document, "Descriptors", "Descriptor")));
                Add("spellList", "Lists / Classes", FormatSpellLists(document));
                Add("savingThrow", "Saving Throw", ReadSummary(
                    ReadProperty(document, "savingThrow")
                    ?? ReadProperty(document, "save")
                    ?? ReadThreeXField(document, "Saving Throw")));
                Add("spellResistance", "Spell Resistance", ReadSummary(
                    ReadProperty(document, "spellResistance")
                    ?? ReadThreeXField(document, "Spell Resistance")));
                break;

            case "class":
            case "prestigeclass":
                AddClassFields(document, Add);
                if (normalized == "prestigeclass")
                {
                    Add("prerequisite", "Prerequisite", FormatPrerequisites(document));
                }
                break;

            case "race":
            case "species":
            case "subrace":
            case "subspecies":
                Add("speed", "Speed", FormatSpeed(ReadProperty(document, "speed")));
                Add("creatureType", "Creature Type", ReadSummary(
                    ReadProperty(document, "creatureType") ?? ReadProperty(document, "type")));
                Add("languages", "Languages", ReadSummary(ReadProperty(document, "languages")));
                break;

            case "background":
                Add("ability", "Ability", FormatAbility(ReadProperty(document, "ability")));
                Add("skills", "Skills", FormatProficiencies(ReadProperty(document, "skillProficiencies")));
                Add("tools", "Tools", FormatProficiencies(ReadProperty(document, "toolProficiencies")));
                Add("languages", "Languages", FormatProficiencies(ReadProperty(document, "languageProficiencies")));
                Add("feat", "Feat", ReadSummary(ReadProperty(document, "feats") ?? ReadProperty(document, "feat")));
                break;

            case "feat":
                Add("prerequisite", "Prerequisite", FormatPrerequisites(document));
                Add("repeatable", "Repeatable", FormatBoolean(ReadProperty(document, "repeatable")));
                break;

            case "optionalfeature":
                Add("featureType", "Feature Type", ReadSummary(
                    ReadProperty(document, "featureType") ?? ReadProperty(document, "type")));
                Add("prerequisite", "Prerequisite", FormatPrerequisites(document));
                break;

            case "skill":
                AddSkillFields(document, Add);
                break;

            case "item":
            case "magicitem":
            case "equipment":
                Add("type", "Type", ReadSummary(
                    ReadProperty(document, "type")
                    ?? ReadThreeXField(document, "Type")));
                Add("rarity", "Rarity", ReadSummary(
                    ReadProperty(document, "rarity")
                    ?? ReadThreeXField(document, "Rarity")));
                Add("attunement", "Attunement", FormatAttunement(document));
                Add("weaponCategory", "Weapon Category", ReadSummary(ReadProperty(document, "weaponCategory")));
                Add("properties", "Properties", ReadSummary(
                    ReadProperty(document, "property") ?? ReadProperty(document, "properties")));
                Add("value", "Value", FormatCurrency(
                    ReadProperty(document, "value")
                    ?? ReadProperty(document, "cost")
                    ?? ReadThreeXField(document, "Price", "Market Price", "Cost")));
                Add("weight", "Weight", FormatWeight(
                    ReadProperty(document, "weight")
                    ?? ReadThreeXField(document, "Weight")));
                Add("charges", "Charges", ReadSummary(
                    ReadProperty(document, "charges")
                    ?? ReadThreeXField(document, "Charges")));
                Add("enhancement", "Enhancement", ReadSummary(
                    ReadProperty(document, "enhancementBonus")
                    ?? ReadProperty(document, "bonusWeapon")
                    ?? ReadProperty(document, "bonusAc")
                    ?? ReadThreeXField(document, "Enhancement Bonus")));
                break;
        }

        return fields;
    }

    private static void AddClassFields(
        JsonElement document,
        Action<string, string, string?> add)
    {
        add("hitDie", "Hit Die", FormatHitDie(
            ReadProperty(document, "hd")
            ?? ReadProperty(document, "hitDie")
            ?? ReadThreeXField(document, "Hit Die", "Hit Dice")));

        var character = ReadPath(document, "_rulesCore", "character");
        if (character is not null && character.Value.ValueKind == JsonValueKind.Object)
        {
            add("bab", "BAB", ReadSummary(ReadProperty(character.Value, "baseAttackProgression")));
            var saves = ReadProperty(character.Value, "saveProgressions");
            if (saves is { ValueKind: JsonValueKind.Object })
            {
                add("fortitude", "Fortitude", ReadSummary(ReadProperty(saves.Value, "fortitude")));
                add("reflex", "Reflex", ReadSummary(ReadProperty(saves.Value, "reflex")));
                add("will", "Will", ReadSummary(ReadProperty(saves.Value, "will")));
            }
            add("skillPoints", "Skill Points", ReadSummary(
                ReadProperty(character.Value, "skillPointsPerLevel")
                ?? ReadProperty(character.Value, "skillPoints")
                ?? ReadProperty(character.Value, "startingSkillPoints")));
            add("classSkills", "Class Skills", ReadSummary(ReadProperty(character.Value, "classSkills")));
            add("spellcasting", "Spellcasting", ReadSummary(
                ReadProperty(character.Value, "spellcastingProfile")
                ?? ReadProperty(character.Value, "spellcastingAbility")));
        }

        add("bab", "BAB", ReadSummary(ReadThreeXField(
            document,
            "Base Attack Bonus",
            "Base Attack Progression")));
        add("skillPoints", "Skill Points", ReadSummary(ReadThreeXField(
            document,
            "Skill Points at Each Level",
            "Skill Points")));
    }

    private static void AddSkillFields(
        JsonElement document,
        Action<string, string, string?> add)
    {
        var competency = ReadPath(document, "_rulesCore", "competency");
        if (competency is null || competency.Value.ValueKind != JsonValueKind.Object) return;

        var ability = ReadString(ReadProperty(competency.Value, "governingAbilityKey"));
        if (!string.IsNullOrWhiteSpace(ability))
        {
            add("ability", "Ability", AbilityLabels.GetValueOrDefault(ability, ability.ToUpperInvariant()));
        }
        add("family", "Family", ReadSummary(ReadProperty(competency.Value, "familyName")));
        add("specialty", "Specialty", ReadSummary(ReadProperty(competency.Value, "specialty")));
        add("ranks", "Ranks", FormatSupport(ReadProperty(competency.Value, "supportsRanks")));
        add("classSkill", "Class Skill State", FormatSupport(ReadProperty(competency.Value, "supportsClassSkillState")));
        add("trainedOnly", "Trained Only", FormatBoolean(ReadProperty(competency.Value, "trainedOnly")));
        add("armorCheckPenalty", "Armor Check Penalty", FormatBoolean(ReadProperty(competency.Value, "armorCheckPenaltyApplies")));
    }

    private static string? FormatPrerequisites(JsonElement document)
    {
        var value = ReadProperty(document, "prerequisite")
            ?? ReadProperty(document, "prerequisites")
            ?? ReadPath(document, "_rulesCore", "character", "prerequisites")
            ?? ReadThreeXField(document, "Prerequisite", "Prerequisites", "Requirements");
        return ReadSummary(value, 180);
    }

    private static string? FormatSpellLists(JsonElement document)
    {
        var values = new List<string>();
        var classes = ReadProperty(document, "classes");
        if (classes is { ValueKind: JsonValueKind.Object })
        {
            foreach (var key in new[] { "fromClassList", "fromClassListVariant", "fromSubclass" })
            {
                var entries = ReadProperty(classes.Value, key);
                if (entries is not { ValueKind: JsonValueKind.Array }) continue;
                foreach (var entry in entries.Value.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.String)
                    {
                        values.Add(entry.GetString()!);
                        continue;
                    }
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    var name = ReadString(ReadProperty(entry, "name"));
                    var className = ReadString(ReadPath(entry, "class", "name"));
                    var subclassName = ReadString(ReadPath(entry, "subclass", "name"));
                    var display = string.Join(" — ", new[] { name, className, subclassName }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                    if (!string.IsNullOrWhiteSpace(display)) values.Add(display);
                }
            }
        }

        var groups = ReadProperty(document, "groups");
        if (groups is { ValueKind: JsonValueKind.Array })
        {
            foreach (var entry in groups.Value.EnumerateArray())
            {
                var value = entry.ValueKind == JsonValueKind.Object
                    ? ReadString(ReadProperty(entry, "name"))
                    : ReadString(entry);
                if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
            }
        }

        foreach (var propertyName in new[] { "classLevels", "spellLevels", "levelsByClass", "spellLists" })
        {
            var summary = ReadSummary(ReadProperty(document, propertyName), 180);
            if (HasText(summary)) values.Add(summary!);
        }

        var legacyLevel = ReadSummary(ReadThreeXField(document, "Level"), 180);
        if (HasText(legacyLevel)) values.Add(legacyLevel!);

        return JoinDistinct(values);
    }

    private static string? FormatCastingTime(JsonElement document)
    {
        var value = ReadProperty(document, "time")
            ?? ReadProperty(document, "castingTime")
            ?? ReadThreeXField(document, "Casting Time");
        if (value is null) return null;
        var entries = value.Value.ValueKind == JsonValueKind.Array
            ? value.Value.EnumerateArray().ToArray()
            : new[] { value.Value };
        var formatted = entries.Select(entry =>
        {
            if (entry.ValueKind != JsonValueKind.Object) return ReadSummary(entry);
            var number = ReadSummary(ReadProperty(entry, "number") ?? ReadProperty(entry, "amount"));
            var unit = ReadString(ReadProperty(entry, "unit") ?? ReadProperty(entry, "type"));
            if (!string.IsNullOrWhiteSpace(unit) && number != "1" && !unit.EndsWith('s')) unit += "s";
            var condition = ReadSummary(ReadProperty(entry, "condition"));
            return string.Join(" · ", new[] { string.Join(" ", new[] { number, unit }.Where(HasText)), condition }.Where(HasText));
        }).Where(HasText);
        return JoinDistinct(formatted!);
    }

    private static string? FormatRange(JsonElement document)
    {
        var value = ReadProperty(document, "range")
            ?? ReadThreeXField(document, "Range");
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.Object) return ReadSummary(value);
        var type = ReadString(ReadProperty(value.Value, "type"));
        var distance = ReadProperty(value.Value, "distance");
        if (distance is not { ValueKind: JsonValueKind.Object }) return Humanize(type) ?? ReadSummary(value);
        var amount = ReadSummary(ReadProperty(distance.Value, "amount") ?? ReadProperty(distance.Value, "number"));
        var unit = ReadString(ReadProperty(distance.Value, "type") ?? ReadProperty(distance.Value, "unit"));
        if (HasText(amount)) return $"{amount} {FormatDistanceUnit(unit)}".Trim();
        return Humanize(unit ?? type);
    }

    private static string? FormatComponents(JsonElement document)
    {
        var value = ReadProperty(document, "components")
            ?? ReadThreeXField(document, "Components");
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.Object) return ReadSummary(value);
        var parts = new List<string>();
        if (IsTruthy(ReadProperty(value.Value, "v"))) parts.Add("V");
        if (IsTruthy(ReadProperty(value.Value, "s"))) parts.Add("S");
        if (ReadProperty(value.Value, "m") is { } material && IsTruthy(material)) parts.Add("M");
        if (IsTruthy(ReadProperty(value.Value, "r"))) parts.Add("R");
        return parts.Count > 0 ? string.Join(", ", parts) : ReadSummary(value);
    }

    private static string? FormatDuration(JsonElement document)
    {
        var value = ReadProperty(document, "duration")
            ?? ReadThreeXField(document, "Duration");
        if (value is null) return null;
        var entries = value.Value.ValueKind == JsonValueKind.Array
            ? value.Value.EnumerateArray().ToArray()
            : new[] { value.Value };
        var result = new List<string>();
        foreach (var entry in entries)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                var summary = ReadSummary(entry);
                if (HasText(summary)) result.Add(summary!);
                continue;
            }
            var type = ReadString(ReadProperty(entry, "type"));
            string? text = type?.ToLowerInvariant() switch
            {
                "instant" => "Instantaneous",
                "permanent" => "Permanent",
                "special" => "Special",
                _ => Humanize(type)
            };
            var timed = ReadProperty(entry, "duration");
            if (timed is { ValueKind: JsonValueKind.Object })
            {
                var amount = ReadSummary(ReadProperty(timed.Value, "amount") ?? ReadProperty(timed.Value, "number"));
                var unit = ReadString(ReadProperty(timed.Value, "type") ?? ReadProperty(timed.Value, "unit"));
                text = string.Join(" ", new[] { IsTruthy(ReadProperty(entry, "upTo")) ? "Up to" : null, amount, Pluralize(unit, amount) }.Where(HasText));
            }
            if (IsTruthy(ReadProperty(entry, "concentration")) && HasText(text)) text = $"Concentration, {text}";
            if (HasText(text)) result.Add(text!);
        }
        return JoinDistinct(result);
    }

    private static string? FormatConcentration(JsonElement document)
    {
        var duration = ReadProperty(document, "duration")
            ?? ReadThreeXField(document, "Duration");
        if (duration is null) return null;
        if (duration.Value.ValueKind == JsonValueKind.String)
        {
            var text = duration.Value.GetString();
            return text?.Contains("concentration", StringComparison.OrdinalIgnoreCase) == true
                ? "Yes"
                : "No";
        }
        IEnumerable<JsonElement> entries = duration.Value.ValueKind == JsonValueKind.Array
            ? duration.Value.EnumerateArray().ToArray()
            : new[] { duration.Value };
        return entries.Any(entry => entry.ValueKind == JsonValueKind.Object
            && IsTruthy(ReadProperty(entry, "concentration"))) ? "Yes" : "No";
    }

    private static string? FormatSpeed(JsonElement? value)
    {
        if (value is null) return null;
        if (value.Value.ValueKind == JsonValueKind.Number) return $"{value.Value.GetRawText()} ft.";
        if (value.Value.ValueKind != JsonValueKind.Object) return ReadSummary(value);
        var parts = new List<string>();
        foreach (var property in value.Value.EnumerateObject())
        {
            var amount = ReadSummary(property.Value);
            if (HasText(amount)) parts.Add($"{Humanize(property.Name)} {amount} ft.");
        }
        return JoinDistinct(parts);
    }

    private static string? FormatAbility(JsonElement? value)
    {
        if (value is null) return null;
        if (value.Value.ValueKind == JsonValueKind.String)
        {
            var text = value.Value.GetString();
            return text is null ? null : AbilityLabels.GetValueOrDefault(text, text.ToUpperInvariant());
        }
        return ReadSummary(value);
    }

    private static string? FormatHitDie(JsonElement? value)
    {
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.Object) return ReadSummary(value);
        var number = ReadSummary(ReadProperty(value.Value, "number")) ?? "1";
        var faces = ReadSummary(ReadProperty(value.Value, "faces") ?? ReadProperty(value.Value, "die"));
        return HasText(faces) ? $"{(number == "1" ? string.Empty : number)}d{faces}" : ReadSummary(value);
    }

    private static string? FormatProficiencies(JsonElement? value) => ReadSummary(value, 160);

    private static string? FormatAttunement(JsonElement document)
    {
        var value = ReadProperty(document, "reqAttune")
            ?? ReadProperty(document, "attunement")
            ?? ReadThreeXField(document, "Attunement");
        if (value is null) return null;
        if (value.Value.ValueKind == JsonValueKind.True) return "Required";
        if (value.Value.ValueKind == JsonValueKind.False) return "Not required";
        var text = ReadSummary(value);
        return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ? "Required" : text;
    }

    private static string? FormatCurrency(JsonElement? value)
    {
        if (value is null) return null;
        if (value.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetDecimal(out var copper))
        {
            return ReadSummary(value);
        }
        if (copper == 0m) return "0 gp";
        if (copper % 100m == 0m) return $"{copper / 100m:0.##} gp";
        if (copper % 10m == 0m) return $"{copper / 10m:0.##} sp";
        return $"{copper:0.##} cp";
    }

    private static string? FormatWeight(JsonElement? value)
    {
        var summary = ReadSummary(value);
        if (!HasText(summary)) return null;
        return summary!.Contains("lb", StringComparison.OrdinalIgnoreCase)
            ? summary
            : $"{summary} lb.";
    }

    private static string? FormatSupport(JsonElement? value) => value is null ? null : FormatBoolean(value);

    private static string? FormatBoolean(JsonElement? value)
    {
        if (value is null) return null;
        return value.Value.ValueKind switch
        {
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            _ => ReadSummary(value)
        };
    }

    private static bool IsTruthy(JsonElement? value)
    {
        if (value is null) return false;
        return value.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.Value.GetString()),
            _ => true
        };
    }

    private static JsonElement? ReadProperty(JsonElement document, string name) =>
        document.ValueKind == JsonValueKind.Object && document.TryGetProperty(name, out var value)
            ? value
            : null;

    private static JsonElement? ReadPath(JsonElement document, params string[] path)
    {
        var current = document;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current)) return null;
        }
        return current;
    }

    private static JsonElement? ReadThreeXField(JsonElement document, params string[] names)
    {
        var fields = ReadPath(document, "_rulesCore", "threeX", "fields");
        if (fields is null || fields.Value.ValueKind != JsonValueKind.Object) return null;

        foreach (var name in names)
        {
            foreach (var property in fields.Value.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }
        return null;
    }

    private static string? ReadString(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } ? value.Value.GetString() : null;

    private static string? ReadSummary(JsonElement? value, int maximumLength = 120)
    {
        if (value is null) return null;
        var text = value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.Array => JoinDistinct(value.Value.EnumerateArray()
                .Select(item => ReadSummary(item, maximumLength))
                .Where(HasText)
                .Select(value => value!)),
            JsonValueKind.Object => FormatObject(value.Value, maximumLength),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(text)) return null;
        return text.Length <= maximumLength ? text : $"{text[..Math.Max(1, maximumLength - 1)]}…";
    }

    private static string? FormatObject(JsonElement value, int maximumLength)
    {
        var parts = new List<string>();
        foreach (var property in value.EnumerateObject())
        {
            var summary = ReadSummary(property.Value, maximumLength);
            if (!HasText(summary)) continue;
            parts.Add($"{Humanize(property.Name)} {summary}");
        }
        return JoinDistinct(parts);
    }

    private static string? JoinDistinct(IEnumerable<string> values)
    {
        var result = values
            .Where(HasText)
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return result.Length == 0 ? null : string.Join(", ", result);
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static string? Humanize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim().Replace('_', ' ').Replace('-', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    private static string FormatDistanceUnit(string? unit) => unit?.ToLowerInvariant() switch
    {
        "feet" or "foot" or "ft" => "ft.",
        "mile" => "mile",
        "miles" => "miles",
        null or "" => string.Empty,
        _ => unit
    };

    private static string? Pluralize(string? unit, string? amount)
    {
        if (string.IsNullOrWhiteSpace(unit)) return null;
        return amount == "1" || unit.EndsWith('s') ? unit : $"{unit}s";
    }
}
