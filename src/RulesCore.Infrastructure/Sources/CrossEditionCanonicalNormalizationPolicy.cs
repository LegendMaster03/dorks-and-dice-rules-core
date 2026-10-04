using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RulesCore.Application.Sources;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Projects source-specific mechanics which Rules Core understands into stable, source-neutral
/// mechanical structures. Native/source-specific evidence remains untouched.
/// </summary>
internal static partial class CrossEditionCanonicalNormalizationPolicy
{
    public const string SchemaVersion = "rules-core-cross-edition-v1";

    private static readonly IReadOnlyDictionary<string, string> SpellSchools =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "abjuration",
            ["Abjuration"] = "abjuration",
            ["C"] = "conjuration",
            ["Conjuration"] = "conjuration",
            ["D"] = "divination",
            ["Divination"] = "divination",
            ["E"] = "enchantment",
            ["Enchantment"] = "enchantment",
            ["V"] = "evocation",
            ["Evocation"] = "evocation",
            ["I"] = "illusion",
            ["Illusion"] = "illusion",
            ["N"] = "necromancy",
            ["Necromancy"] = "necromancy",
            ["T"] = "transmutation",
            ["Transmutation"] = "transmutation"
        };

    private static readonly IReadOnlyDictionary<string, string> FeatCategoryLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["D"] = "Dragonmark",
            ["DG"] = "Dark Gift",
            ["G"] = "General",
            ["O"] = "Origin",
            ["FS"] = "Fighting Style",
            ["EB"] = "Epic Feat",
            ["Epic Boon"] = "Epic Feat",
            ["Epic"] = "Epic Feat"
        };

    private static readonly IReadOnlyDictionary<string, string> ItemTypeLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "Ammunition",
            ["AT"] = "Artisan Tool",
            ["EXP"] = "Explosive",
            ["FD"] = "Food or Drink",
            ["G"] = "Adventuring Gear",
            ["GS"] = "Gaming Set",
            ["HA"] = "Heavy Armor",
            ["INS"] = "Instrument",
            ["LA"] = "Light Armor",
            ["M"] = "Melee Weapon",
            ["MA"] = "Medium Armor",
            ["P"] = "Potion",
            ["R"] = "Ranged Weapon",
            ["RD"] = "Rod",
            ["RG"] = "Ring",
            ["S"] = "Shield",
            ["SC"] = "Scroll",
            ["SCF"] = "Spellcasting Focus",
            ["ST"] = "Staff",
            ["T"] = "Tool",
            ["WD"] = "Wand",
            ["W"] = "Wondrous Item"
        };

    private static readonly string[] AbilityKeys = ["str", "dex", "con", "int", "wis", "cha"];

    [GeneratedRegex(@"(?<!\w)(?<value>[+-]?\d+)(?!\w)", RegexOptions.CultureInvariant)]
    private static partial Regex SignedInteger();

    [GeneratedRegex(@"(?<faces>\d+)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex HitDieFaces();

    [GeneratedRegex(@"(?<amount>\d[\d,]*(?:\.\d+)?)\s*(?<unit>pp|gp|sp|cp)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Currency();

    [GeneratedRegex(@"(?<name>[^,;]+?)\s+(?<level>\d+)(?=$|[,;])", RegexOptions.CultureInvariant)]
    private static partial Regex SpellListLevel();

    [GeneratedRegex(@"\btouch\s+(?<value>\d+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TouchArmorClass();

    [GeneratedRegex(@"\bflat[- ]footed\s+(?<value>\d+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FlatFootedArmorClass();

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

        JsonObject? content;
        try
        {
            content = JsonNode.Parse(record.ContentJson) as JsonObject;
        }
        catch (JsonException)
        {
            return record;
        }

        if (content is null)
        {
            return record;
        }

        var extension = content["_rulesCore"] as JsonObject ?? new JsonObject();
        var context = extension["context"] as JsonObject ?? new JsonObject();
        if (!context.ContainsKey("sourceFormat"))
        {
            context["sourceFormat"] = representation.FormatKey;
        }
        if (!context.ContainsKey("nativeEntityType"))
        {
            context["nativeEntityType"] = record.EntityType;
        }
        var edition = ResolveEdition(representation, record, context);
        if (!string.IsNullOrWhiteSpace(edition) && !context.ContainsKey("edition"))
        {
            context["edition"] = edition;
        }
        context["canonicalSchemaVersion"] = SchemaVersion;
        extension["context"] = context;

        var entityType = record.EntityType.Trim().ToLowerInvariant();
        switch (entityType)
        {
            case "class":
            case "prestigeclass":
                NormalizeClass(content, extension);
                break;
            case "feat":
                NormalizeFeat(content, extension);
                break;
            case "skill":
            case "tool":
                NormalizeCompetency(content, extension, edition);
                break;
            case "spell":
                NormalizeSpell(representation, record, content, extension);
                break;
            case "item":
            case "equipment":
            case "magicitem":
                NormalizeItem(content, extension);
                break;
            case "race":
            case "species":
                NormalizeSpecies(content, extension);
                break;
            case "background":
                NormalizeBackground(content, extension);
                break;
            case "optionalfeature":
            case "feature":
                NormalizeFeature(content, extension);
                break;
        }

        NormalizeCombat(content, extension);
        content["_rulesCore"] = extension;

        return record with
        {
            ContentJson = content.ToJsonString(new JsonSerializerOptions { WriteIndented = false })
        };
    }

    private static void NormalizeClass(JsonObject content, JsonObject extension)
    {
        var character = extension["character"] as JsonObject ?? new JsonObject();
        var fields = ReadThreeXFields(extension);

        if (content["hd"] is { } hd)
        {
            character["hitDie"] = hd.DeepClone();
        }
        else if (FirstField(fields, "Hit Die", "Hit Dice") is { } hitDie
                 && TryParseHitDie(hitDie, out var parsedHitDie))
        {
            character["hitDie"] = parsedHitDie;
            content["hd"] ??= parsedHitDie.DeepClone();
        }

        if (!character.ContainsKey("skillPointsPerLevel")
            && FirstField(fields, "Skill Points at Each Level", "Skill Points Per Level", "Skill Points") is { } skillPoints
            && TryFirstInt(skillPoints, out var points)
            && points >= 0)
        {
            character["skillPointsPerLevel"] = points;
        }

        if (!character.ContainsKey("classSkills")
            && FirstField(fields, "Class Skills", "Class Skill") is { } classSkills)
        {
            var values = SplitDisplayList(classSkills);
            if (values.Count > 0)
            {
                character["classSkills"] = ToJsonArray(values);
            }
        }

        if (!character.ContainsKey("baseAttackProgression")
            && FirstField(fields, "Base Attack Progression", "Base Attack Bonus Progression") is { } bab
            && NormalizeProgression(bab, out var baseAttackProgression))
        {
            character["baseAttackProgression"] = baseAttackProgression;
        }

        var saveProgressions = character["saveProgressions"] as JsonObject ?? new JsonObject();
        TryAddSaveProgression(saveProgressions, "fortitude", FirstField(fields, "Fortitude Save", "Fort Save"));
        TryAddSaveProgression(saveProgressions, "reflex", FirstField(fields, "Reflex Save", "Ref Save"));
        TryAddSaveProgression(saveProgressions, "will", FirstField(fields, "Will Save"));
        if (saveProgressions.Count > 0)
        {
            character["saveProgressions"] = saveProgressions;
        }

        if (!character.ContainsKey("prerequisiteText")
            && FirstField(fields, "Requirements", "Requirement", "Prerequisites", "Prerequisite") is { } prerequisite)
        {
            character["prerequisiteText"] = prerequisite;
        }

        if (character.Count > 0)
        {
            extension["character"] = character;
        }
    }

    private static void NormalizeFeat(JsonObject content, JsonObject extension)
    {
        var character = extension["character"] as JsonObject ?? new JsonObject();
        var feat = character["feat"] as JsonObject ?? new JsonObject();
        var fields = ReadThreeXFields(extension);

        CopyScalar(content, "repeatable", feat, "repeatable");
        var category = ReadNodeString(content["category"])
            ?? FirstField(fields, "Type", "Category", "Feat Type");
        if (!string.IsNullOrWhiteSpace(category))
        {
            feat["category"] = NormalizeFeatCategory(category);
        }
        if (!character.ContainsKey("prerequisiteText")
            && FirstField(fields, "Prerequisites", "Prerequisite", "Requirements") is { } prerequisite)
        {
            character["prerequisiteText"] = prerequisite.Trim();
        }

        if (feat.Count > 0)
        {
            character["feat"] = feat;
        }
        if (character.Count > 0)
        {
            extension["character"] = character;
        }
    }

    private static void NormalizeCompetency(JsonObject content, JsonObject extension, string? edition)
    {
        var competency = extension["competency"] as JsonObject;
        if (competency is null)
        {
            competency = new JsonObject
            {
                ["kind"] = "skill",
                ["facetType"] = string.Equals(
                    ReadString(extension["context"] as JsonObject, "nativeEntityType"),
                    "tool",
                    StringComparison.OrdinalIgnoreCase)
                    ? "tool"
                    : "skill",
                ["supportsRanks"] = IsThreeXEdition(edition),
                ["supportsClassSkillState"] = IsThreeXEdition(edition),
                ["supportsTrainingState"] = true
            };
        }

        if (!competency.ContainsKey("governingAbilityKey"))
        {
            var ability = ReadString(content, "ability");
            if (TryNormalizeAbility(ability, out var normalized))
            {
                competency["governingAbilityKey"] = normalized;
            }
        }

        extension["competency"] = competency;
    }

    private static void NormalizeSpell(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record,
        JsonObject content,
        JsonObject extension)
    {
        var spell = new JsonObject();
        var fields = ReadThreeXFields(extension);

        var scalarLevel = TryNodeInt(content["level"], out var parsedScalarLevel)
            ? parsedScalarLevel
            : -1;
        if (scalarLevel >= 0)
        {
            spell["level"] = scalarLevel;
        }

        var schoolText = ReadNodeString(content["school"]) ?? FirstField(fields, "School");
        if (NormalizeSchool(schoolText, out var school))
        {
            spell["school"] = school;
        }

        var subschools = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddStrings(subschools, content["subschool"]);
        AddStrings(subschools, content["subschools"]);
        AddDisplayValues(subschools, FirstField(fields, "Subschool", "Subschool(s)"));
        if (subschools.Count > 0)
        {
            spell["subschools"] = ToJsonArray(subschools);
        }

        var descriptors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddStrings(descriptors, content["descriptors"]);
        AddStrings(descriptors, content["descriptor"]);
        AddDisplayValues(descriptors, FirstField(fields, "Descriptors", "Descriptor"));
        AddBracketDescriptors(descriptors, schoolText);
        if (descriptors.Count > 0)
        {
            spell["descriptors"] = ToJsonArray(descriptors);
        }

        var lists = ReadSpellLists(content, fields, record.RawJson);
        MergeSpellLookupEvidence(lists, representation.NormalizationCompanionEvidence, record, scalarLevel);
        if (lists.Count > 0)
        {
            spell["lists"] = lists;
        }

        var castingTime = content["time"]?.DeepClone();
        if (castingTime is null
            && FirstField(fields, "Casting Time") is { } castingText)
        {
            castingTime = new JsonArray(new JsonObject { ["text"] = castingText.Trim() });
        }
        if (castingTime is not null)
        {
            spell["castingTime"] = EnsureArray(castingTime);
        }

        var range = content["range"]?.DeepClone();
        if (range is null && FirstField(fields, "Range") is { } rangeText)
        {
            range = new JsonObject
            {
                ["type"] = "text",
                ["text"] = rangeText.Trim()
            };
        }
        if (range is not null)
        {
            spell["range"] = EnsureObjectOrText(range);
        }

        var components = NormalizeComponents(content["components"], fields);
        if (components.Count > 0)
        {
            spell["components"] = components;
        }

        var duration = content["duration"]?.DeepClone();
        if (duration is null && FirstField(fields, "Duration") is { } durationText)
        {
            duration = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = durationText.Trim()
            });
        }
        if (duration is not null)
        {
            spell["duration"] = EnsureArray(duration);
        }

        if (TryReadConcentration(content, fields, out var concentration))
        {
            spell["concentration"] = concentration;
        }
        if (TryReadRitual(content, out var ritual))
        {
            spell["ritual"] = ritual;
        }

        CopyTextSemantic(spell, "target", ReadNodeString(content["target"]) ?? FirstField(fields, "Target", "Targets"));
        CopyTextSemantic(spell, "area", ReadNodeString(content["area"]) ?? FirstField(fields, "Area"));
        CopyTextSemantic(spell, "effect", ReadNodeString(content["effect"]) ?? FirstField(fields, "Effect"));
        CopyTextSemantic(
            spell,
            "savingThrow",
            ReadTextValue(content["savingThrow"])
                ?? ReadTextValue(content["save"])
                ?? FirstField(fields, "Saving Throw"));
        CopyTextSemantic(
            spell,
            "spellResistance",
            ReadNodeString(content["spellResistance"])
                ?? FirstField(fields, "Spell Resistance"));

        if (spell.Count > 0)
        {
            extension["spell"] = spell;
        }
    }

    private static JsonArray ReadSpellLists(
        JsonObject content,
        JsonObject? fields,
        string rawJson)
    {
        var rows = new List<SpellListRow>();

        if (content["classes"] is JsonObject classes)
        {
            AddFiveEClassRows(rows, classes["fromClassList"], "class");
            AddFiveEClassRows(rows, classes["fromClassListVariant"], "class-variant");
            if (classes["fromSubclass"] is JsonArray subclasses)
            {
                foreach (var node in subclasses)
                {
                    if (node is not JsonObject row) continue;
                    var classObject = row["class"] as JsonObject;
                    var subclassObject = row["subclass"] as JsonObject;
                    var className = ReadString(classObject, "name");
                    var subclassName = ReadString(subclassObject, "name")
                        ?? ReadString(subclassObject, "shortName");
                    if (string.IsNullOrWhiteSpace(className) || string.IsNullOrWhiteSpace(subclassName))
                    {
                        continue;
                    }
                    rows.Add(new SpellListRow(
                        "subclass",
                        $"{className}: {subclassName}",
                        null,
                        ReadString(subclassObject, "source") ?? ReadString(classObject, "source"),
                        null));
                }
            }
        }

        var legacyLevel = FirstField(fields, "Level", "Levels");
        if (!string.IsNullOrWhiteSpace(legacyLevel))
        {
            foreach (Match match in SpellListLevel().Matches(legacyLevel))
            {
                if (!int.TryParse(match.Groups["level"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
                {
                    continue;
                }
                foreach (var name in ExpandSlashListName(match.Groups["name"].Value))
                {
                    rows.Add(new SpellListRow("class", name, level, null, null));
                }
            }
        }

        foreach (var segment in ReadPcGenSegments(rawJson))
        {
            var kind = segment.Tag.Equals("DOMAINS", StringComparison.OrdinalIgnoreCase)
                ? "domain"
                : segment.Tag.Equals("CLASSES", StringComparison.OrdinalIgnoreCase)
                    ? "class"
                    : null;
            if (kind is null) continue;
            foreach (var assignment in segment.Value.Split(
                         '|',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var equals = assignment.LastIndexOf('=');
                if (equals <= 0 || equals + 1 >= assignment.Length) continue;
                if (!int.TryParse(
                        assignment[(equals + 1)..].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var level))
                {
                    continue;
                }
                foreach (var name in ExpandSlashListName(assignment[..equals]))
                {
                    rows.Add(new SpellListRow(kind, name, level, null, null));
                }
            }
        }

        return SpellListRowsToJson(rows);
    }

    private static void MergeSpellLookupEvidence(
        JsonArray existing,
        IReadOnlyList<NormalizedSourceCompanionContent> evidence,
        NormalizedSourceRecord record,
        int scalarLevel)
    {
        if (evidence.Count == 0) return;
        var rows = ReadSpellListRows(existing).ToList();

        foreach (var companion in evidence)
        {
            if (!string.Equals(
                    companion.CompanionKind,
                    FiveEToolsCompanionSourceFormatAdapter.SpellSourceLookupCompanionKind,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(companion.Name, record.Name, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(record.SourceCode)
                    && !string.Equals(companion.SourceCode, record.SourceCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var grant in FiveEToolsCompanionSourceFormatAdapter.ReadSpellSourceLookupGrants(companion.RawJson))
            {
                rows.Add(new SpellListRow(
                    grant.Kind,
                    grant.Name,
                    scalarLevel >= 0 ? scalarLevel : null,
                    grant.SourceCode,
                    "generated-spell-source-lookup"));
            }
        }

        existing.Clear();
        foreach (var node in SpellListRowsToJson(rows))
        {
            existing.Add(node?.DeepClone());
        }
    }

    private static void NormalizeItem(JsonObject content, JsonObject extension)
    {
        var item = new JsonObject();
        var fields = ReadThreeXFields(extension);

        var type = ReadNodeString(content["type"]) ?? FirstField(fields, "Type", "Item Type");
        if (!string.IsNullOrWhiteSpace(type))
        {
            item["type"] = NormalizeItemType(type);
        }

        CopyTextSemantic(item, "weaponCategory", ReadNodeString(content["weaponCategory"]));
        CopyTextSemantic(item, "armorCategory", ReadNodeString(content["armorCategory"]));
        CopyTextSemantic(item, "rarity", ReadNodeString(content["rarity"]));

        var attunement = content["reqAttune"] ?? content["attunement"];
        if (attunement is not null)
        {
            item["attunement"] = attunement.DeepClone();
        }

        if (TryReadCopperValue(content["value"] ?? content["cost"], out var copper))
        {
            item["value"] = new JsonObject { ["copperPieces"] = copper };
        }
        else if (FirstField(fields, "Price", "Market Price", "Cost", "Value") is { } priceText)
        {
            if (TryParseCurrencyAsCopper(priceText, out copper))
            {
                item["value"] = new JsonObject { ["copperPieces"] = copper };
            }
            else
            {
                item["value"] = new JsonObject { ["text"] = priceText.Trim() };
            }
        }

        if (TryReadDecimal(content["weight"], out var pounds))
        {
            item["weight"] = new JsonObject { ["pounds"] = pounds };
        }
        else if (FirstField(fields, "Weight") is { } weightText)
        {
            if (TryFirstDecimal(weightText, out pounds))
            {
                item["weight"] = new JsonObject { ["pounds"] = pounds };
            }
            else
            {
                item["weight"] = new JsonObject { ["text"] = weightText.Trim() };
            }
        }

        var charges = content["charges"];
        if (TryNodeInt(charges, out var chargeCount)
            || TryFirstInt(FirstField(fields, "Charges") ?? string.Empty, out chargeCount))
        {
            item["charges"] = chargeCount;
        }
        else if (FirstField(fields, "Charges") is { } chargesText)
        {
            item["chargesText"] = chargesText.Trim();
        }

        var enhancement = content["enhancementBonus"] ?? content["bonusWeapon"] ?? content["bonusAc"];
        if (TryNodeInt(enhancement, out var bonus)
            || TryFirstInt(FirstField(fields, "Enhancement Bonus") ?? string.Empty, out bonus))
        {
            item["enhancementBonus"] = bonus;
        }

        AddTextArray(
            item,
            "specialProperties",
            ReadNodeString(content["specialProperties"])
                ?? FirstField(fields, "Special Properties", "Special Ability", "Special Abilities"));

        if (item.Count > 0)
        {
            extension["item"] = item;
        }
    }

    private static void NormalizeSpecies(JsonObject content, JsonObject extension)
    {
        var species = new JsonObject();
        var fields = ReadThreeXFields(extension);

        var sizes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSizeValues(sizes, content["size"]);
        AddSizeValues(sizes, FirstField(fields, "Size"));
        if (sizes.Count > 0)
        {
            species["sizes"] = ToJsonArray(sizes);
        }

        var speed = NormalizeSpeed(content["speed"]);
        if (speed.Count == 0 && FirstField(fields, "Speed", "Movement") is { } speedText)
        {
            var first = Regex.Match(speedText, @"(?<feet>\d+)\s*ft", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (first.Success
                && int.TryParse(first.Groups["feet"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var feet))
            {
                speed["walk"] = feet;
            }
        }
        if (speed.Count > 0)
        {
            species["speed"] = speed;
        }

        var creatureTypes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDisplayValues(creatureTypes, ReadNodeString(content["creatureType"]));
        AddDisplayValues(creatureTypes, FirstField(fields, "Type", "Creature Type"));
        if (creatureTypes.Count > 0)
        {
            species["creatureTypes"] = ToJsonArray(creatureTypes.Select(value => value.ToLowerInvariant()));
        }

        var abilityAdjustments = NormalizeAbilityAdjustments(content["ability"]);
        if (abilityAdjustments.Count > 0)
        {
            species["abilityAdjustments"] = abilityAdjustments;
        }

        var languages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddStrings(languages, content["languages"]);
        AddDisplayValues(languages, FirstField(fields, "Languages", "Language"));
        if (languages.Count > 0)
        {
            species["languages"] = ToJsonArray(languages);
        }

        if (content["_copy"] is JsonObject copy
            && ReadString(copy, "name") is { } parentName)
        {
            var parent = new JsonObject { ["name"] = parentName };
            if (ReadString(copy, "source") is { } parentSource)
            {
                parent["source"] = parentSource;
            }
            species["parent"] = parent;
        }

        if (species.Count > 0)
        {
            extension["species"] = species;
        }
    }

    private static void NormalizeBackground(JsonObject content, JsonObject extension)
    {
        var character = extension["character"] as JsonObject ?? new JsonObject();
        var background = new JsonObject();
        CopyIfPresent(content, background, "ability");
        CopyIfPresent(content, background, "skillProficiencies");
        CopyIfPresent(content, background, "toolProficiencies");
        CopyIfPresent(content, background, "languageProficiencies");
        CopyIfPresent(content, background, "feats");
        if (background.Count > 0)
        {
            character["background"] = background;
            extension["character"] = character;
        }
    }

    private static void NormalizeFeature(JsonObject content, JsonObject extension)
    {
        var character = extension["character"] as JsonObject ?? new JsonObject();
        var feature = new JsonObject();
        CopyIfPresent(content, feature, "featureType");
        CopyIfPresent(content, feature, "type");
        CopyIfPresent(content, feature, "prerequisite");
        CopyIfPresent(content, feature, "prerequisites");
        if (feature.Count > 0)
        {
            character["feature"] = feature;
            extension["character"] = character;
        }
    }

    private static void NormalizeCombat(JsonObject content, JsonObject extension)
    {
        var threeX = extension["threeX"] as JsonObject;
        if (threeX is null) return;
        var fields = threeX["fields"] as JsonObject;
        var combat = new JsonObject();

        var armorText = ReadNodeString(threeX["armorClass"])
            ?? FirstField(fields, "Armor Class", "AC");
        if (!string.IsNullOrWhiteSpace(armorText))
        {
            var armorClass = new JsonObject { ["text"] = armorText.Trim() };
            if (TryFirstInt(armorText, out var total)) armorClass["total"] = total;
            var touch = TouchArmorClass().Match(armorText);
            if (touch.Success
                && int.TryParse(touch.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var touchValue))
            {
                armorClass["touch"] = touchValue;
            }
            var flat = FlatFootedArmorClass().Match(armorText);
            if (flat.Success
                && int.TryParse(flat.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flatValue))
            {
                armorClass["flatFooted"] = flatValue;
            }
            combat["armorClass"] = armorClass;
        }

        CopyNumericSemantic(combat, "baseAttackBonus", threeX["baseAttackBonus"], FirstField(fields, "Base Attack Bonus", "BAB"));
        CopyNumericSemantic(combat, "grapple", threeX["grapple"], FirstField(fields, "Grapple"));
        CopyTextSemantic(
            combat,
            "damageReduction",
            ReadNodeString(threeX["damageReduction"]) ?? FirstField(fields, "Damage Reduction"));
        CopyTextSemantic(
            combat,
            "spellResistance",
            ReadNodeString(threeX["spellResistance"]) ?? FirstField(fields, "Spell Resistance"));
        CopyTextSemantic(
            combat,
            "missChance",
            ReadNodeString(threeX["missChance"]) ?? FirstField(fields, "Miss Chance"));

        if (combat.Count > 0)
        {
            extension["combat"] = combat;
        }
    }

    private static JsonObject NormalizeComponents(JsonNode? native, JsonObject? fields)
    {
        var result = new JsonObject();
        if (native is JsonObject components)
        {
            if (TryNodeBool(components["v"], out var verbal)) result["verbal"] = verbal;
            if (TryNodeBool(components["s"], out var somatic)) result["somatic"] = somatic;
            if (components["m"] is { } material)
            {
                if (material is JsonValue materialValue
                    && materialValue.TryGetValue<string>(out var materialText))
                {
                    result["material"] = materialText;
                }
                else if (TryNodeBool(material, out var hasMaterial))
                {
                    result["material"] = hasMaterial;
                }
                else
                {
                    result["material"] = material.DeepClone();
                }
            }
            if (components["r"] is { } royalty)
            {
                result["royalty"] = royalty.DeepClone();
            }
        }

        var componentText = FirstField(fields, "Components");
        if (!string.IsNullOrWhiteSpace(componentText))
        {
            var tokens = componentText.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Any(value => value.Equals("V", StringComparison.OrdinalIgnoreCase))) result["verbal"] = true;
            if (tokens.Any(value => value.Equals("S", StringComparison.OrdinalIgnoreCase))) result["somatic"] = true;
            if (tokens.Any(value => value.Equals("M", StringComparison.OrdinalIgnoreCase)) && !result.ContainsKey("material"))
                result["material"] = true;
            if (tokens.Any(value => value.Equals("F", StringComparison.OrdinalIgnoreCase))) result["focus"] = true;
            if (tokens.Any(value => value.Equals("DF", StringComparison.OrdinalIgnoreCase))) result["divineFocus"] = true;
        }

        CopyTextSemantic(result, "material", FirstField(fields, "Material Component", "Material Components"));
        CopyTextSemantic(result, "focus", FirstField(fields, "Focus"));
        CopyTextSemantic(result, "divineFocus", FirstField(fields, "Divine Focus"));
        CopyTextSemantic(result, "xpCost", FirstField(fields, "XP Cost"));
        return result;
    }

    private static bool TryReadConcentration(JsonObject content, JsonObject? fields, out bool concentration)
    {
        concentration = false;
        if (content["duration"] is JsonArray duration)
        {
            foreach (var node in duration)
            {
                if (node is JsonObject item && TryNodeBool(item["concentration"], out var value))
                {
                    concentration = value;
                    return true;
                }
            }
        }

        var text = FirstField(fields, "Duration");
        if (!string.IsNullOrWhiteSpace(text)
            && text.Contains("concentration", StringComparison.OrdinalIgnoreCase))
        {
            concentration = true;
            return true;
        }
        return false;
    }

    private static bool TryReadRitual(JsonObject content, out bool ritual)
    {
        ritual = false;
        if (content["meta"] is JsonObject meta && TryNodeBool(meta["ritual"], out ritual))
        {
            return true;
        }
        return TryNodeBool(content["ritual"], out ritual);
    }

    private static void AddFiveEClassRows(
        ICollection<SpellListRow> rows,
        JsonNode? value,
        string kind)
    {
        if (value is not JsonArray array) return;
        foreach (var node in array)
        {
            if (node is not JsonObject item) continue;
            var name = ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            rows.Add(new SpellListRow(kind, name, null, ReadString(item, "source"), null));
        }
    }

    private static JsonArray SpellListRowsToJson(IEnumerable<SpellListRow> rows)
    {
        var result = new JsonArray();
        foreach (var row in rows
                     .Where(value => !string.IsNullOrWhiteSpace(value.Name))
                     .DistinctBy(
                         value => $"{value.Kind}\n{value.Name}\n{value.Level}\n{value.SourceCode}",
                         StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value.Kind, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(value => value.Level))
        {
            var item = new JsonObject
            {
                ["kind"] = row.Kind,
                ["name"] = row.Name
            };
            if (row.Level.HasValue) item["level"] = row.Level.Value;
            if (!string.IsNullOrWhiteSpace(row.SourceCode)) item["source"] = row.SourceCode;
            if (!string.IsNullOrWhiteSpace(row.Evidence)) item["evidence"] = row.Evidence;
            result.Add(item);
        }
        return result;
    }

    private static IEnumerable<SpellListRow> ReadSpellListRows(JsonArray rows)
    {
        foreach (var node in rows)
        {
            if (node is not JsonObject row) continue;
            var kind = ReadString(row, "kind");
            var name = ReadString(row, "name");
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(name)) continue;
            int? level = TryNodeInt(row["level"], out var parsedLevel) ? parsedLevel : null;
            yield return new SpellListRow(
                kind,
                name,
                level,
                ReadString(row, "source"),
                ReadString(row, "evidence"));
        }
    }

    private static IEnumerable<string> ExpandSlashListName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) yield break;
        var parts = trimmed.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
        {
            yield return trimmed;
            yield break;
        }
        foreach (var part in parts) yield return part;
    }

    private static IReadOnlyList<PcGenSegment> ReadPcGenSegments(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("segments", out var segments)
                || segments.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return segments.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Object)
                .Select(value =>
                {
                    var tag = ReadString(value, "Tag") ?? ReadString(value, "tag") ?? string.Empty;
                    var rawValue = ReadString(value, "Value") ?? ReadString(value, "value") ?? string.Empty;
                    return new PcGenSegment(tag, rawValue);
                })
                .Where(value => value.Tag.Length > 0)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static JsonObject? ReadThreeXFields(JsonObject extension) =>
        extension["threeX"] is JsonObject threeX
        && threeX["fields"] is JsonObject fields
            ? fields
            : null;

    private static string? FirstField(JsonObject? fields, params string[] names)
    {
        if (fields is null) return null;
        foreach (var name in names)
        {
            if (fields.TryGetPropertyValue(name, out var node)
                && ReadNodeString(node) is { } value
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }
        return null;
    }

    private static bool TryParseHitDie(string text, out JsonObject hitDie)
    {
        hitDie = new JsonObject();
        var match = Regex.Match(text, @"(?:^|\b)(?:1)?d(?<faces>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            match = HitDieFaces().Match(text.Trim());
        }
        if (!match.Success
            || !int.TryParse(match.Groups["faces"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var faces)
            || faces <= 0)
        {
            return false;
        }
        hitDie["number"] = 1;
        hitDie["faces"] = faces;
        return true;
    }

    private static void TryAddSaveProgression(JsonObject target, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || target.ContainsKey(key)) return;
        if (NormalizeSaveProgression(value, out var progression))
        {
            target[key] = progression;
        }
    }

    private static string NormalizeFeatCategory(string value)
    {
        var trimmed = value.Trim();
        return FeatCategoryLabels.TryGetValue(trimmed, out var label) ? label : trimmed;
    }

    private static string NormalizeItemType(string value)
    {
        var trimmed = value.Trim();
        var separator = trimmed.IndexOf('|');
        var code = separator >= 0 ? trimmed[..separator] : trimmed;
        return ItemTypeLabels.TryGetValue(code, out var label) ? label : trimmed;
    }

    private static bool NormalizeProgression(string value, out string progression)
    {
        var normalized = value.Trim().ToLowerInvariant();
        progression = normalized switch
        {
            "good" or "full" or "high" => "full",
            "average" or "medium" or "3/4" or "three-quarters" or "three quarters" => "three-quarters",
            "poor" or "low" or "1/2" or "half" => "half",
            _ => string.Empty
        };
        return progression.Length > 0;
    }

    private static bool NormalizeSaveProgression(string value, out string progression)
    {
        var normalized = value.Trim().ToLowerInvariant();
        progression = normalized switch
        {
            "good" or "high" => "good",
            "poor" or "low" => "poor",
            _ => string.Empty
        };
        return progression.Length > 0;
    }

    private static bool NormalizeSchool(string? value, out string school)
    {
        school = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        var bracket = trimmed.IndexOf('[');
        if (bracket > 0) trimmed = trimmed[..bracket].Trim();
        var parenthesis = trimmed.IndexOf('(');
        if (parenthesis > 0) trimmed = trimmed[..parenthesis].Trim();
        if (SpellSchools.TryGetValue(trimmed, out school!)) return true;
        school = trimmed.ToLowerInvariant();
        return school.Length > 0;
    }

    private static void AddBracketDescriptors(ISet<string> target, string? schoolText)
    {
        if (string.IsNullOrWhiteSpace(schoolText)) return;
        var start = schoolText.IndexOf('[');
        var end = schoolText.LastIndexOf(']');
        if (start < 0 || end <= start) return;
        AddDisplayValues(target, schoolText[(start + 1)..end]);
    }

    private static JsonObject NormalizeSpeed(JsonNode? node)
    {
        var result = new JsonObject();
        if (TryReadDecimal(node, out var walk))
        {
            result["walk"] = walk;
            return result;
        }
        if (node is not JsonObject speed) return result;
        foreach (var pair in speed)
        {
            if (TryReadDecimal(pair.Value, out var amount))
            {
                result[pair.Key] = amount;
            }
        }
        return result;
    }

    private static JsonObject NormalizeAbilityAdjustments(JsonNode? node)
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<JsonObject> objects = node switch
        {
            JsonObject obj => [obj],
            JsonArray array => array.OfType<JsonObject>(),
            _ => []
        };
        foreach (var obj in objects)
        {
            foreach (var key in AbilityKeys)
            {
                if (TryNodeInt(obj[key], out var value))
                {
                    totals[key] = totals.GetValueOrDefault(key) + value;
                }
            }
        }
        var result = new JsonObject();
        foreach (var value in totals.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            result[value.Key] = value.Value;
        }
        return result;
    }

    private static void AddSizeValues(ISet<string> target, JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array) AddSizeValues(target, item);
            return;
        }
        AddSizeValues(target, ReadNodeString(node));
    }

    private static void AddSizeValues(ISet<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var candidate in value.Split(
                     [',', '/', ';'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            target.Add(UniversalSizeCategories.Normalize(candidate));
        }
    }

    private static void AddStrings(ISet<string> target, JsonNode? node)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                AddDisplayValues(target, text);
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (ReadNodeString(item) is { } itemText)
                    {
                        AddDisplayValues(target, itemText);
                    }
                }
                break;
        }
    }

    private static void AddDisplayValues(ISet<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var item in SplitDisplayList(value))
        {
            target.Add(item);
        }
    }

    private static IReadOnlyList<string> SplitDisplayList(string value) =>
        value.Split(
                [',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void AddTextArray(JsonObject target, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var values = SplitDisplayList(value);
        if (values.Count > 0)
        {
            target[key] = ToJsonArray(values);
        }
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var result = new JsonArray();
        foreach (var value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static JsonArray EnsureArray(JsonNode value) =>
        value is JsonArray array
            ? (JsonArray)array.DeepClone()
            : new JsonArray(value.DeepClone());

    private static JsonObject EnsureObjectOrText(JsonNode value)
    {
        if (value is JsonObject obj) return (JsonObject)obj.DeepClone();
        return new JsonObject { ["type"] = "text", ["text"] = ReadNodeString(value) ?? value.ToJsonString() };
    }

    private static void CopyIfPresent(JsonObject source, JsonObject target, string key)
    {
        if (source[key] is { } value)
        {
            target[key] = value.DeepClone();
        }
    }

    private static void CopyScalar(JsonObject source, string sourceKey, JsonObject target, string targetKey)
    {
        if (source[sourceKey] is JsonValue value)
        {
            target[targetKey] = value.DeepClone();
        }
    }

    private static void CopyTextSemantic(JsonObject target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }

    private static void CopyNumericSemantic(
        JsonObject target,
        string key,
        JsonNode? primary,
        string? fallback)
    {
        if (TryNodeInt(primary, out var value)
            || TryFirstInt(fallback ?? string.Empty, out value))
        {
            target[key] = value;
        }
    }

    private static bool TryReadCopperValue(JsonNode? node, out decimal copper)
    {
        if (TryReadDecimal(node, out copper)) return true;
        if (node is JsonObject obj)
        {
            if (TryReadDecimal(obj["amount"], out var amount))
            {
                var unit = ReadNodeString(obj["coin"]) ?? ReadNodeString(obj["unit"]);
                return TryConvertCurrency(amount, unit, out copper);
            }
        }
        copper = 0;
        return false;
    }

    private static bool TryParseCurrencyAsCopper(string text, out decimal copper)
    {
        copper = 0;
        var match = Currency().Match(text);
        if (!match.Success
            || !decimal.TryParse(
                match.Groups["amount"].Value.Replace(",", string.Empty, StringComparison.Ordinal),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return false;
        }
        return TryConvertCurrency(amount, match.Groups["unit"].Value, out copper);
    }

    private static bool TryConvertCurrency(decimal amount, string? unit, out decimal copper)
    {
        copper = unit?.Trim().ToLowerInvariant() switch
        {
            "cp" => amount,
            "sp" => amount * 10,
            "gp" => amount * 100,
            "pp" => amount * 1000,
            _ => -1
        };
        return copper >= 0;
    }

    private static bool TryFirstDecimal(string value, out decimal result)
    {
        result = 0;
        var match = Regex.Match(value, @"\d[\d,]*(?:\.\d+)?", RegexOptions.CultureInvariant);
        return match.Success
            && decimal.TryParse(
                match.Value.Replace(",", string.Empty, StringComparison.Ordinal),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out result);
    }

    private static bool TryFirstInt(string value, out int result)
    {
        result = 0;
        var match = SignedInteger().Match(value ?? string.Empty);
        return match.Success
            && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryNodeInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue scalar) return false;
        if (scalar.TryGetValue<int>(out value)) return true;
        if (scalar.TryGetValue<long>(out var longValue)
            && longValue is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }
        return scalar.TryGetValue<string>(out var text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadDecimal(JsonNode? node, out decimal value)
    {
        value = 0;
        if (node is not JsonValue scalar) return false;
        if (scalar.TryGetValue<decimal>(out value)) return true;
        if (scalar.TryGetValue<double>(out var doubleValue))
        {
            value = (decimal)doubleValue;
            return true;
        }
        return scalar.TryGetValue<string>(out var text)
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryNodeBool(JsonNode? node, out bool value)
    {
        value = false;
        if (node is not JsonValue scalar) return false;
        if (scalar.TryGetValue<bool>(out value)) return true;
        return scalar.TryGetValue<string>(out var text)
            && bool.TryParse(text, out value);
    }

    private static string? ReadTextValue(JsonNode? node)
    {
        if (ReadNodeString(node) is { } scalar && !string.IsNullOrWhiteSpace(scalar))
        {
            return scalar;
        }
        if (node is not JsonArray array) return null;
        var values = array
            .Select(ReadNodeString)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        return values.Length == 0 ? null : string.Join(", ", values);
    }

    private static string? ReadNodeString(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<string>(out var text)) return text;
        return value.ToJsonString().Trim('"');
    }

    private static string? ReadString(JsonObject? obj, string key) =>
        obj is not null ? ReadNodeString(obj[key]) : null;

    private static string? ReadString(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(key, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryNormalizeAbility(string? value, out string ability)
    {
        ability = value?.Trim().ToLowerInvariant() switch
        {
            "str" or "strength" => "strength",
            "dex" or "dexterity" => "dexterity",
            "con" or "constitution" => "constitution",
            "int" or "intelligence" => "intelligence",
            "wis" or "wisdom" => "wisdom",
            "cha" or "charisma" => "charisma",
            _ => string.Empty
        };
        return ability.Length > 0;
    }

    private static string? ResolveEdition(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record,
        JsonObject context)
    {
        var contextEdition = ReadString(context, "edition");
        if (!string.IsNullOrWhiteSpace(contextEdition)) return contextEdition;

        if (record.PublicationLocalKey is not null && representation.Publications is not null)
        {
            var publication = representation.Publications.FirstOrDefault(value =>
                string.Equals(value.LocalKey, record.PublicationLocalKey, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(publication?.GameEdition))
            {
                return publication.GameEdition;
            }
        }

        return representation.Publications?
            .Select(value => value.GameEdition)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool IsThreeXEdition(string? edition) =>
        string.Equals(edition, "3e", StringComparison.OrdinalIgnoreCase)
        || string.Equals(edition, "3.5e", StringComparison.OrdinalIgnoreCase);

    private sealed record PcGenSegment(string Tag, string Value);
    private sealed record SpellListRow(
        string Kind,
        string Name,
        int? Level,
        string? SourceCode,
        string? Evidence);
}
