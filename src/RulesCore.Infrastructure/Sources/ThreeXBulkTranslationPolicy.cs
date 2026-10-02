using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Promotes mechanically faithful 3e/3.5e source data into the shared Rules Core content shape.
/// Source-specific mechanics that do not have a faithful common field remain under _rulesCore.threeX
/// or the existing PCGen unmapped-segment payload.
/// </summary>
internal static class ThreeXBulkTranslationPolicy
{
    private static readonly IReadOnlyDictionary<string, string> SpellSchoolCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Abjuration"] = "A",
            ["Conjuration"] = "C",
            ["Divination"] = "D",
            ["Enchantment"] = "E",
            ["Evocation"] = "V",
            ["Illusion"] = "I",
            ["Necromancy"] = "N",
            ["Transmutation"] = "T"
        };

    private static readonly Regex LabelLine = Rx(
        @"(?m)^\s*(?:\*\*)?(?<label>[A-Za-z][A-Za-z0-9 /&()'’\-]{1,64}):(?:\*\*)?\s*(?<value>[^\r\n]*)$");
    private static readonly Regex HeadingCategory = Rx(@"\[(?<category>[^\]]+)\]");
    private static readonly Regex IntegerToken = Rx(@"(?<![A-Za-z0-9])(?<value>\d+)(?![A-Za-z0-9])");
    private static readonly Regex HitDie = Rx(@"(?:(?<number>\d+)\s*)?d(?<faces>\d+)");
    private static readonly Regex Money = Rx(@"(?<amount>\d[\d,]*(?:\.\d+)?)\s*(?<unit>cp|sp|gp|pp)\b");
    private static readonly Regex Weight = Rx(@"(?<amount>\d+(?:\.\d+)?)\s*(?:lb\.?|pounds?)\b");
    private static readonly Regex RaceAbilityBonus = Rx(
        @"(?<amount>[+-]\s*\d+)\s+(?<ability>Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma|Str|Dex|Con|Int|Wis|Cha)\b");
    private static readonly Regex LandSpeed = Rx(
        @"(?:base\s+land\s+speed(?:\s+is|\s*:)?|speed\s*:?)\s*(?<feet>\d+)\s*(?:feet|ft\.?)\b");
    private static readonly Regex SizeWord = Rx(
        @"\b(?<size>Fine|Diminutive|Tiny|Small|Medium|Large|Huge|Gargantuan|Colossal)\b");
    private static readonly Regex MarkdownOrHtmlTag = Rx(@"(?:</?[^>]+>|\*\*|__)", RegexOptions.Singleline);

    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ContentJson))
        {
            return record;
        }

        if (string.Equals(
                representation.FormatKey,
                LegacySrdSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeLegacy(record);
        }

        if (string.Equals(
                representation.FormatKey,
                PcGenSourceFormatAdapter.Format,
                StringComparison.OrdinalIgnoreCase))
        {
            return NormalizePcGen(record);
        }

        return record;
    }

    private static NormalizedSourceRecord NormalizeLegacy(NormalizedSourceRecord record)
    {
        if (!TryReadLegacyBody(record.RawJson, out var source, out var body))
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson!) as JsonObject;
        if (content is null)
        {
            return record;
        }

        var extension = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var threeX = extension["threeX"] as JsonObject ?? new JsonObject();
        var fields = ParseFields(body);

        switch (record.EntityType.Trim().ToLowerInvariant())
        {
            case "spell":
                MapLegacySpell(content, threeX, body, fields);
                break;
            case "power":
                MapLegacyPower(content, threeX, body, fields);
                break;
            case "feat":
                MapLegacyFeat(content, threeX, source, fields);
                break;
            case "race":
            case "species":
                MapLegacyRace(content, threeX, body, fields);
                break;
            case "item":
            case "equipment":
                MapLegacyItem(content, threeX, body, fields);
                break;
            case "domain":
                MapLegacyDomain(threeX, fields);
                break;
            case "divineability":
                MapLegacyDivineAbility(threeX, fields);
                break;
            case "skill":
                MapLegacySkill(content, threeX, source, body);
                break;
            case "class":
            case "prestigeclass":
            case "npcclass":
                MapLegacyClass(content, extension, threeX, record.EntityType, fields);
                break;
        }

        extension["threeX"] = threeX;
        content["_rulesCore"] = extension;
        return record with
        {
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static NormalizedSourceRecord NormalizePcGen(NormalizedSourceRecord record)
    {
        if (!TryReadPcGenSegments(record.RawJson, out var segments))
        {
            return record;
        }

        var content = JsonNode.Parse(record.ContentJson!) as JsonObject;
        if (content is null)
        {
            return record;
        }

        var extension = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var threeX = extension["threeX"] as JsonObject ?? new JsonObject();

        switch (record.EntityType.Trim().ToLowerInvariant())
        {
            case "deity":
                MapPcGenDeity(content, threeX, segments);
                break;
            case "language":
                MapPcGenLanguage(content, threeX, segments);
                break;
            case "domain":
                MapPcGenDomain(threeX, segments);
                break;
            case "template":
                MapPcGenTemplate(threeX, segments);
                break;
            case "weapon-proficiency":
            case "armor-proficiency":
            case "shield-proficiency":
                MapPcGenProficiency(threeX, record.EntityType, segments);
                break;
        }

        if (threeX.Count > 0)
        {
            extension["threeX"] = threeX;
        }
        content["_rulesCore"] = extension;
        return record with
        {
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static void MapLegacySpell(
        JsonObject content,
        JsonObject threeX,
        string body,
        IReadOnlyDictionary<string, string> fields)
    {
        if (TryField(fields, out var levelText, "Level"))
        {
            threeX["spellLevels"] = levelText;
            if (TryUniqueLevel(levelText, out var level))
            {
                content["level"] = level;
            }
        }

        var descriptor = FirstDescriptorLine(body);
        if (TrySpellSchool(descriptor, out var schoolCode, out var schoolText))
        {
            content["school"] = schoolCode;
            threeX["school"] = schoolText;
        }
        else if (TryField(fields, out var explicitSchool, "School")
                 && TrySpellSchool(explicitSchool, out schoolCode, out schoolText))
        {
            content["school"] = schoolCode;
            threeX["school"] = schoolText;
        }

        if (TryField(fields, out var componentsText, "Components"))
        {
            threeX["components"] = componentsText;
            var components = new JsonObject();
            var tokens = componentsText.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Any(value => string.Equals(value, "V", StringComparison.OrdinalIgnoreCase)))
            {
                components["v"] = true;
            }
            if (tokens.Any(value => string.Equals(value, "S", StringComparison.OrdinalIgnoreCase)))
            {
                components["s"] = true;
            }
            if (components.Count > 0)
            {
                content["components"] = components;
            }
        }

        CopyThreeX(threeX, fields, "Casting Time", "castingTime");
        CopyThreeX(threeX, fields, "Range", "range");
        CopyThreeX(threeX, fields, "Target", "target");
        CopyThreeX(threeX, fields, "Targets", "targets");
        CopyThreeX(threeX, fields, "Area", "area");
        CopyThreeX(threeX, fields, "Effect", "effect");
        CopyThreeX(threeX, fields, "Saving Throw", "savingThrow");
        CopyThreeX(threeX, fields, "Spell Resistance", "spellResistance");

        if (TryField(fields, out var durationText, "Duration"))
        {
            threeX["duration"] = durationText;
            if (string.Equals(durationText.Trim().TrimEnd('.'), "Instantaneous", StringComparison.OrdinalIgnoreCase))
            {
                content["duration"] = new JsonArray(new JsonObject { ["type"] = "instant" });
            }
        }
    }

    private static void MapLegacyPower(
        JsonObject content,
        JsonObject threeX,
        string body,
        IReadOnlyDictionary<string, string> fields)
    {
        if (TryField(fields, out var levelText, "Level"))
        {
            threeX["powerLevels"] = levelText;
            if (TryUniqueLevel(levelText, out var level))
            {
                content["level"] = level;
            }
        }

        var descriptor = FirstDescriptorLine(body);
        if (!string.IsNullOrWhiteSpace(descriptor))
        {
            threeX["discipline"] = descriptor;
        }

        if (TryField(fields, out var powerPoints, "Power Points")
            && TryFirstInt(powerPoints, out var points))
        {
            threeX["powerPoints"] = points;
        }

        CopyThreeX(threeX, fields, "Display", "display");
        CopyThreeX(threeX, fields, "Manifesting Time", "manifestingTime");
        CopyThreeX(threeX, fields, "Range", "range");
        CopyThreeX(threeX, fields, "Target", "target");
        CopyThreeX(threeX, fields, "Targets", "targets");
        CopyThreeX(threeX, fields, "Area", "area");
        CopyThreeX(threeX, fields, "Effect", "effect");
        CopyThreeX(threeX, fields, "Duration", "duration");
        CopyThreeX(threeX, fields, "Saving Throw", "savingThrow");
        CopyThreeX(threeX, fields, "Power Resistance", "powerResistance");
    }

    private static void MapLegacyFeat(
        JsonObject content,
        JsonObject threeX,
        JsonElement source,
        IReadOnlyDictionary<string, string> fields)
    {
        var originalHeading = ReadString(source, "originalHeading");
        if (!string.IsNullOrWhiteSpace(originalHeading))
        {
            var match = HeadingCategory.Match(CleanText(originalHeading));
            if (match.Success)
            {
                content["category"] = match.Groups["category"].Value.Trim();
            }
        }

        CopyThreeX(threeX, fields, "Prerequisite", "prerequisite");
        CopyThreeX(threeX, fields, "Prerequisites", "prerequisites");
        CopyThreeX(threeX, fields, "Benefit", "benefit");
        CopyThreeX(threeX, fields, "Normal", "normal");
        CopyThreeX(threeX, fields, "Special", "special");
    }

    private static void MapLegacyRace(
        JsonObject content,
        JsonObject threeX,
        string body,
        IReadOnlyDictionary<string, string> fields)
    {
        var ability = new JsonObject();
        foreach (Match match in RaceAbilityBonus.Matches(CleanText(body)))
        {
            if (!TryInt(match.Groups["amount"].Value.Replace(" ", string.Empty, StringComparison.Ordinal), out var amount)
                || !TryAbilityCode(match.Groups["ability"].Value, out var key))
            {
                continue;
            }
            ability[key] = (ability[key]?.GetValue<int>() ?? 0) + amount;
        }
        if (ability.Count > 0)
        {
            content["ability"] = new JsonArray(ability);
        }

        if (TryField(fields, out var explicitSize, "Size")
            && UniversalSizeCategories.TryResolve(explicitSize.Trim().TrimEnd('.'), out var resolvedSize))
        {
            content["size"] = new JsonArray(resolvedSize.SourceCode);
            threeX["size"] = explicitSize;
        }
        else
        {
            var size = SizeWord.Match(CleanText(body));
            if (size.Success
                && UniversalSizeCategories.TryResolve(size.Groups["size"].Value, out resolvedSize))
            {
                content["size"] = new JsonArray(resolvedSize.SourceCode);
            }
        }

        var speed = LandSpeed.Match(CleanText(body));
        if (speed.Success && TryInt(speed.Groups["feet"].Value, out var feet))
        {
            content["speed"] = feet;
            threeX["landSpeed"] = feet;
        }

        CopyThreeX(threeX, fields, "Favored Class", "favoredClass");
        CopyThreeX(threeX, fields, "Automatic Languages", "automaticLanguages");
        CopyThreeX(threeX, fields, "Bonus Languages", "bonusLanguages");
    }

    private static void MapLegacyItem(
        JsonObject content,
        JsonObject threeX,
        string body,
        IReadOnlyDictionary<string, string> fields)
    {
        if (TryField(fields, out var weightText, "Weight")
            && TryWeight(weightText, out var weight))
        {
            content["weight"] = weight;
        }
        else if (TryWeight(body, out weight))
        {
            content["weight"] = weight;
        }

        string? priceText = null;
        if (TryField(fields, out var marketPrice, "Market Price")) priceText = marketPrice;
        else if (TryField(fields, out var price, "Price")) priceText = price;
        if (TryMoneyAsCopper(priceText ?? body, out var value))
        {
            content["value"] = value;
        }

        CopyThreeX(threeX, fields, "Caster Level", "casterLevel");
        CopyThreeX(threeX, fields, "Manifester Level", "manifesterLevel");
        CopyThreeX(threeX, fields, "Aura", "aura");
        CopyThreeX(threeX, fields, "Prerequisites", "prerequisites");
        CopyThreeX(threeX, fields, "Cost to Create", "costToCreate");
    }

    private static void MapLegacyDomain(
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields)
    {
        CopyThreeX(threeX, fields, "Granted Power", "grantedPower");
    }

    private static void MapLegacyDivineAbility(
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields)
    {
        CopyThreeX(threeX, fields, "Prerequisite", "prerequisite");
        CopyThreeX(threeX, fields, "Prerequisites", "prerequisites");
        CopyThreeX(threeX, fields, "Benefit", "benefit");
        CopyThreeX(threeX, fields, "Suggested Portfolio Elements", "suggestedPortfolioElements");
    }

    private static void MapLegacySkill(
        JsonObject content,
        JsonObject threeX,
        JsonElement source,
        string body)
    {
        if (ReadString(threeX, "governingAbilityKey") is { } ability
            && TryAbilityCode(ability, out var abilityCode))
        {
            content["ability"] = abilityCode;
        }

        var originalHeading = ReadString(source, "originalHeading") ?? string.Empty;
        var combined = $"{CleanText(originalHeading)}\n{CleanText(body)}";
        if (combined.Contains("Armor Check Penalty", StringComparison.OrdinalIgnoreCase))
        {
            threeX["armorCheckPenalty"] = true;
        }
        if (combined.Contains("Trained Only", StringComparison.OrdinalIgnoreCase))
        {
            threeX["useUntrained"] = false;
        }
    }

    private static void MapLegacyClass(
        JsonObject content,
        JsonObject extension,
        JsonObject threeX,
        string entityType,
        IReadOnlyDictionary<string, string> fields)
    {
        if (TryField(fields, out var hitDieText, "Hit Die")
            && TryHitDie(hitDieText, out var number, out var faces))
        {
            content["hd"] = new JsonObject
            {
                ["number"] = number,
                ["faces"] = faces
            };
            threeX["hitDie"] = hitDieText;
        }

        var character = extension["character"] as JsonObject ?? new JsonObject();
        if (TryField(fields, out var skillPoints, "Skill Points at Each Additional Level", "Skill Points Per Level")
            && TryFirstInt(skillPoints, out var points))
        {
            character["skillPointsPerLevel"] = points;
        }
        if (TryField(fields, out var classSkills, "Class Skills"))
        {
            var values = SplitList(classSkills);
            if (values.Count > 0)
            {
                var array = new JsonArray();
                foreach (var value in values) array.Add(value);
                character["classSkills"] = array;
            }
        }
        if (character.Count > 0)
        {
            extension["character"] = character;
        }

        CopyThreeX(threeX, fields, "Alignment", "alignment");
        CopyThreeX(threeX, fields, "Weapon and Armor Proficiency", "weaponAndArmorProficiency");
        CopyThreeX(threeX, fields, "Class Skills", "classSkillsText");

        if (string.Equals(entityType, "prestigeClass", StringComparison.OrdinalIgnoreCase)
            && TryField(fields, out var prerequisite, "Requirements", "Prerequisites", "Prerequisite"))
        {
            content["prerequisite"] = prerequisite;
            threeX["prerequisite"] = prerequisite;
        }
    }

    private static void MapPcGenDeity(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyList<PcGenSegment> segments)
    {
        var alignment = Last(segments, "ALIGN");
        if (alignment is not null)
        {
            var codes = AlignmentCodes(alignment.Value);
            if (codes.Count > 0)
            {
                var array = new JsonArray();
                foreach (var code in codes) array.Add(code);
                content["alignment"] = array;
            }
        }

        var domains = Last(segments, "DOMAINS");
        if (domains is not null)
        {
            var domainText = domains.Value.Split('|', 2)[0];
            var values = SplitList(domainText);
            if (values.Count > 0)
            {
                var array = new JsonArray();
                foreach (var value in values) array.Add(value);
                content["domains"] = array;
            }
            threeX["domains"] = domains.Value;
        }

        var weapon = Last(segments, "DEITYWEAP");
        if (weapon is not null) threeX["favoredWeapon"] = weapon.Value;

        var pantheon = Last(segments, "PANTHEON");
        if (pantheon is not null) content["pantheon"] = pantheon.Value;

        foreach (var fact in All(segments, "FACT"))
        {
            var parts = fact.Value.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            if (string.Equals(parts[0], "Title", StringComparison.OrdinalIgnoreCase)) content["title"] = parts[1];
            else if (string.Equals(parts[0], "Symbol", StringComparison.OrdinalIgnoreCase)) content["symbol"] = parts[1];
        }
        foreach (var factSet in All(segments, "FACTSET"))
        {
            var parts = factSet.Value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && string.Equals(parts[0], "Pantheon", StringComparison.OrdinalIgnoreCase))
            {
                content["pantheon"] = string.Join(", ", parts.Skip(1));
            }
        }
    }

    private static void MapPcGenLanguage(
        JsonObject content,
        JsonObject threeX,
        IReadOnlyList<PcGenSegment> segments)
    {
        var type = Last(segments, "TYPE");
        if (type is not null)
        {
            var normalized = type.Value.Trim().ToLowerInvariant();
            if (normalized is "standard" or "exotic" or "secret")
            {
                content["type"] = normalized;
            }
            else
            {
                threeX["languageType"] = type.Value;
            }
        }

        var script = Last(segments, "SCRIPT");
        if (script is not null) content["script"] = script.Value;
    }

    private static void MapPcGenDomain(JsonObject threeX, IReadOnlyList<PcGenSegment> segments)
    {
        var spellList = Last(segments, "SPELLS");
        if (spellList is not null) threeX["spellAccess"] = spellList.Value;
        var skill = All(segments, "CSKILL").Select(value => value.Value).Where(value => value.Length > 0).ToArray();
        if (skill.Length > 0)
        {
            var array = new JsonArray();
            foreach (var value in skill) array.Add(value);
            threeX["classSkills"] = array;
        }
    }

    private static void MapPcGenTemplate(JsonObject threeX, IReadOnlyList<PcGenSegment> segments)
    {
        var cr = Last(segments, "CR");
        if (cr is not null) threeX["challengeRatingAdjustment"] = cr.Value;
        var levelAdjustment = Last(segments, "LEVELADJUSTMENT");
        if (levelAdjustment is not null) threeX["levelAdjustment"] = levelAdjustment.Value;
        var type = Last(segments, "RACETYPE") ?? Last(segments, "TYPE");
        if (type is not null) threeX["creatureTypeAdjustment"] = type.Value;
        var size = Last(segments, "SIZE");
        if (size is not null) threeX["sizeAdjustment"] = size.Value;
    }

    private static void MapPcGenProficiency(
        JsonObject threeX,
        string entityType,
        IReadOnlyList<PcGenSegment> segments)
    {
        threeX["proficiencyKind"] = entityType.Trim().ToLowerInvariant() switch
        {
            "weapon-proficiency" => "weapon",
            "armor-proficiency" => "armor",
            "shield-proficiency" => "shield",
            _ => entityType
        };
        var type = Last(segments, "TYPE");
        if (type is not null) threeX["proficiencyType"] = type.Value;
    }

    private static IReadOnlyDictionary<string, string> ParseFields(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in LabelLine.Matches(body ?? string.Empty))
        {
            var label = CleanText(match.Groups["label"].Value).Trim();
            var value = CleanText(match.Groups["value"].Value).Trim();
            if (label.Length > 0 && value.Length > 0)
            {
                result[label] = value;
            }
        }
        return result;
    }

    private static string? FirstDescriptorLine(string body)
    {
        foreach (var raw in (body ?? string.Empty).Split('\n'))
        {
            var line = CleanText(raw).Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (LabelLine.IsMatch(raw)) continue;
            return line;
        }
        return null;
    }

    private static bool TrySpellSchool(string? value, out string code, out string text)
    {
        code = string.Empty;
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = CleanText(value).Trim();
        foreach (var school in SpellSchoolCodes)
        {
            if (!normalized.StartsWith(school.Key, StringComparison.OrdinalIgnoreCase)) continue;
            code = school.Value;
            text = normalized;
            return true;
        }
        return false;
    }

    private static bool TryUniqueLevel(string value, out int level)
    {
        var levels = IntegerToken.Matches(value ?? string.Empty)
            .Select(match => match.Groups["value"].Value)
            .Where(value => TryInt(value, out _))
            .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();
        if (levels.Length == 1)
        {
            level = levels[0];
            return true;
        }
        level = 0;
        return false;
    }

    private static bool TryHitDie(string value, out int number, out int faces)
    {
        number = 1;
        faces = 0;
        var match = HitDie.Match(value ?? string.Empty);
        if (!match.Success || !TryInt(match.Groups["faces"].Value, out faces) || faces <= 0) return false;
        if (match.Groups["number"].Success && TryInt(match.Groups["number"].Value, out var parsed) && parsed > 0)
        {
            number = parsed;
        }
        return true;
    }

    private static bool TryMoneyAsCopper(string value, out long copper)
    {
        copper = 0;
        var match = Money.Match(value ?? string.Empty);
        if (!match.Success
            || !decimal.TryParse(
                match.Groups["amount"].Value.Replace(",", string.Empty, StringComparison.Ordinal),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return false;
        }
        var multiplier = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "cp" => 1m,
            "sp" => 10m,
            "gp" => 100m,
            "pp" => 1000m,
            _ => 0m
        };
        var converted = amount * multiplier;
        if (multiplier <= 0 || converted < 0 || converted > long.MaxValue || decimal.Truncate(converted) != converted)
        {
            return false;
        }
        copper = (long)converted;
        return true;
    }

    private static bool TryWeight(string value, out decimal weight)
    {
        weight = 0;
        var match = Weight.Match(value ?? string.Empty);
        return match.Success
            && decimal.TryParse(match.Groups["amount"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out weight)
            && weight >= 0;
    }

    private static bool TryAbilityCode(string value, out string code)
    {
        code = value.Trim().ToLowerInvariant() switch
        {
            "str" or "strength" => "str",
            "dex" or "dexterity" => "dex",
            "con" or "constitution" => "con",
            "int" or "intelligence" => "int",
            "wis" or "wisdom" => "wis",
            "cha" or "charisma" => "cha",
            _ => string.Empty
        };
        return code.Length > 0;
    }

    private static IReadOnlyList<string> AlignmentCodes(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized == "TN") return ["N"];
        if (normalized == "N") return ["N"];
        if (normalized.Length == 2
            && "LCN".Contains(normalized[0], StringComparison.Ordinal)
            && "GEN".Contains(normalized[1], StringComparison.Ordinal))
        {
            return [normalized[0].ToString(), normalized[1].ToString()];
        }
        return [];
    }

    private static IReadOnlyList<string> SplitList(string value) =>
        value.Split(
                [',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void CopyThreeX(
        JsonObject threeX,
        IReadOnlyDictionary<string, string> fields,
        string sourceName,
        string targetName)
    {
        if (fields.TryGetValue(sourceName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            threeX[targetName] = value;
        }
    }

    private static bool TryField(
        IReadOnlyDictionary<string, string> fields,
        out string value,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var candidate) && !string.IsNullOrWhiteSpace(candidate))
            {
                value = candidate;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    private static bool TryFirstInt(string value, out int result)
    {
        result = 0;
        var match = IntegerToken.Match(value ?? string.Empty);
        return match.Success && TryInt(match.Groups["value"].Value, out result);
    }

    private static bool TryInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static string CleanText(string value) =>
        MarkdownOrHtmlTag.Replace(value ?? string.Empty, string.Empty).Trim();

    private static bool TryReadLegacyBody(string rawJson, out JsonElement source, out string body)
    {
        source = default;
        body = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("body", out var bodyElement)
                || bodyElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            body = bodyElement.GetString() ?? string.Empty;
            source = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadPcGenSegments(string rawJson, out IReadOnlyList<PcGenSegment> segments)
    {
        segments = [];
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryProperty(document.RootElement, "segments", out var values)
                || values.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var result = new List<PcGenSegment>();
            var ordinal = 0;
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.Object
                    && TryReadString(value, "tag", out var tag))
                {
                    _ = TryReadString(value, "value", out var segmentValue);
                    result.Add(new PcGenSegment(ordinal, tag.Trim(), segmentValue.Trim()));
                }
                ordinal++;
            }
            segments = result;
            return result.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<PcGenSegment> All(IReadOnlyList<PcGenSegment> segments, string tag) =>
        segments.Where(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static PcGenSegment? Last(IReadOnlyList<PcGenSegment> segments, string tag) =>
        segments.LastOrDefault(value => string.Equals(value.Tag, tag, StringComparison.OrdinalIgnoreCase));

    private static string? ReadString(JsonElement value, string name) =>
        TryProperty(value, name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadString(JsonObject value, string name) =>
        value[name] is JsonValue property && property.TryGetValue<string>(out var result)
            ? result
            : null;

    private static bool TryReadString(JsonElement value, string name, out string result)
    {
        result = string.Empty;
        var found = ReadString(value, name);
        if (string.IsNullOrWhiteSpace(found)) return false;
        result = found;
        return true;
    }

    private static bool TryProperty(JsonElement value, string name, out JsonElement property)
    {
        if (value.TryGetProperty(name, out property)) return true;
        foreach (var candidate in value.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }
        property = default;
        return false;
    }

    private static Regex Rx(string pattern, RegexOptions extra = RegexOptions.None) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | extra);

    private sealed record PcGenSegment(int Index, string Tag, string Value);
}
