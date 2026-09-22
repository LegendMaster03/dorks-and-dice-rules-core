using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed class RaceCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        context.IsSelected(rule.Catalog.ConceptKey)
        && (string.Equals(rule.Catalog.EntityType, "race", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.Catalog.EntityType, "species", StringComparison.OrdinalIgnoreCase));

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        context.AddFeature(
            $"feature.{rule.Catalog.ConceptKey}",
            rule.Catalog.DisplayName,
            "race-species",
            CharacterResolutionStates.Resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance);

        ProjectSize(rule, context);
        ProjectMovement(rule, context);
    }


    private static void ProjectSize(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "size", out var size))
        {
            return;
        }

        string? value = null;
        if (size.ValueKind == JsonValueKind.String)
        {
            value = size.GetString();
        }
        else if (size.ValueKind == JsonValueKind.Array)
        {
            var first = size.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.String)
            {
                value = first.GetString();
            }
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            context.AddSizeCategory(value, rule.Catalog.ConceptKey);
        }
    }

    private static void ProjectMovement(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "speed", out var speed))
        {
            return;
        }

        if (speed.ValueKind == JsonValueKind.Number && speed.TryGetInt32(out var walk))
        {
            context.Movement["movement.walk"] = new CharacterMovementModeView(
                "movement.walk",
                "Walk",
                CharacterResolutionStates.Resolved,
                walk,
                "ft",
                [new CharacterMechanicContributionView(
                    $"{rule.Catalog.ConceptKey}.speed",
                    rule.Catalog.DisplayName,
                    CharacterEffectOperations.Set,
                    walk,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance)],
                rule.Provenance);
            return;
        }

        if (speed.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in speed.EnumerateObject())
        {
            int? value = property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out var number)
                    ? number
                    : CharacterProjectionJson.Integer(property.Value, "number")
                        ?? CharacterProjectionJson.Integer(property.Value, "amount");
            var key = $"movement.{property.Name.ToLowerInvariant()}";
            context.Movement[key] = new CharacterMovementModeView(
                key,
                CharacterProjectionJson.Humanize(property.Name),
                value is null
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                value,
                "ft",
                value is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.speed.{property.Name}",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        value,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }

}

internal sealed class ClassCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        context.IsSelected(rule.Catalog.ConceptKey)
        && (string.Equals(rule.Catalog.EntityType, "class", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.Catalog.EntityType, "subclass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.Catalog.EntityType, "prestigeClass", StringComparison.OrdinalIgnoreCase));

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        var level = context.AdvancementLevel(rule.Catalog.ConceptKey);
        context.AddFeature(
            $"feature.{rule.Catalog.ConceptKey}",
            rule.Catalog.DisplayName,
            rule.Catalog.EntityType,
            CharacterResolutionStates.Resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance);

        if (!string.Equals(rule.Catalog.EntityType, "subclass", StringComparison.OrdinalIgnoreCase))
        {
            context.StandardProficiencyLevel = checked(context.StandardProficiencyLevel + Math.Max(level, 0));
        }

        var startingClass = context.IsStartingClass(rule.Catalog.ConceptKey);
        ProjectSavingThrowTraining(rule, context, startingClass);
        ProjectHitDie(rule, context, level);
        ProjectSpellcasting(rule, context, level);
        ProjectClassQualifications(rule, context, startingClass);
        ProjectClassFeatures(rule, context, level);
        ProjectNormalizedThreeXClass(rule, context, level);
    }


    private static void ProjectNormalizedThreeXClass(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        int level)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "_rulesCore", out var rulesCore)
            || !CharacterProjectionJson.TryGetProperty(rulesCore, "character", out var character)
            || character.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var skillPoints = CharacterProjectionJson.Integer(character, "skillPointsPerLevel");
        if (skillPoints is int points)
        {
            var key = $"advancement.{rule.Catalog.ConceptKey}.skill-points-per-level";
            context.Mechanics[key] = new CharacterResolvedMechanicView(
                key,
                "advancement",
                $"{rule.Catalog.DisplayName} Skill Points per Level",
                CharacterResolutionStates.Resolved,
                points,
                null,
                null,
                [],
                [],
                [],
                [],
                [new CharacterMechanicContributionView(
                    key,
                    rule.Catalog.DisplayName,
                    CharacterEffectOperations.Set,
                    points,
                    null,
                    rule.Catalog.ConceptKey,
                    rule.Provenance)],
                rule.Provenance);
        }

        if (CharacterProjectionJson.TryGetProperty(character, "classSkills", out var classSkills)
            && classSkills.ValueKind == JsonValueKind.Array)
        {
            context.HasDerivedClassSkillData = true;
            foreach (var skill in classSkills.EnumerateArray()
                         .Where(value => value.ValueKind == JsonValueKind.String)
                         .Select(value => value.GetString())
                         .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                context.AddClassSkill(skill!, rule.Catalog.ConceptKey);
            }
        }

        var maximumLevel = CharacterProjectionJson.Integer(character, "maximumLevel");
        if (maximumLevel is int maximum && level > maximum)
        {
            context.Conflicts.Add(new CharacterProjectionConflictView(
                $"conflict.{rule.Catalog.ConceptKey}.maximum-level",
                CharacterResolutionStates.Conflict,
                $"{rule.Catalog.DisplayName} is limited to {maximum} levels by the effective rule, but the Character supplies level {level}.",
                [],
                [rule.Catalog.ConceptKey]));
        }

        var baseAttackProgression = CharacterProjectionJson.String(character, "baseAttackProgression");
        if (level > 0 && !string.IsNullOrWhiteSpace(baseAttackProgression))
        {
            var value = ThreeXClassProgressionMath.BaseAttackBonus(level, baseAttackProgression);
            context.ThreeXBaseAttackContributions.Add(new CharacterMechanicContributionView(
                $"{rule.Catalog.ConceptKey}.base-attack-bonus",
                $"{rule.Catalog.DisplayName} BAB",
                CharacterEffectOperations.Add,
                value,
                baseAttackProgression,
                rule.Catalog.ConceptKey,
                rule.Provenance));
        }

        if (level > 0
            && CharacterProjectionJson.TryGetProperty(character, "saveProgressions", out var saveProgressions)
            && saveProgressions.ValueKind == JsonValueKind.Object)
        {
            foreach (var save in saveProgressions.EnumerateObject())
            {
                if (save.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                var progression = save.Value.GetString();
                if (string.IsNullOrWhiteSpace(progression))
                {
                    continue;
                }

                var value = ThreeXClassProgressionMath.BaseSave(level, progression);
                context.AddThreeXSaveContribution(
                    save.Name,
                    new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.save.{save.Name}",
                        $"{rule.Catalog.DisplayName} {CharacterProjectionJson.Humanize(save.Name)} base save",
                        CharacterEffectOperations.Add,
                        value,
                        progression,
                        rule.Catalog.ConceptKey,
                        rule.Provenance));
            }
        }

        var spellcastingProfile = CharacterProjectionJson.String(character, "spellcastingProfile");
        var spellcastingAbility = CharacterProjectionJson.String(character, "spellcastingAbility");
        if (string.Equals(spellcastingProfile, "dnd-3x", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(spellcastingAbility))
        {
            var ability = CharacterProjectionJson.NormalizeAbilityKey(spellcastingAbility);
            context.AddCapability(
                "spellcasting",
                "Spellcasting",
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.AddCapability(
                "spellcasting.dnd-3x",
                "3.x Spellcasting",
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.Spellcasting[$"spellcasting.{rule.Catalog.ConceptKey}"] =
                new CharacterSpellcastingView(
                    $"spellcasting.{rule.Catalog.ConceptKey}",
                    $"{rule.Catalog.DisplayName} Spellcasting",
                    CharacterResolutionStates.ApplicableUnresolved,
                    ability,
                    "spell-slots-3x",
                    null,
                    null,
                    [],
                    [],
                    rule.Provenance);
        }

        ProjectNormalizedAdvancementFeatures(rule, context, character, level);
        NormalizedCharacterPrerequisiteProjector.Project(rule, context, character);
    }

    private static void ProjectNormalizedAdvancementFeatures(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character,
        int level)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "advancementFeatures", out var features)
            || features.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var feature in features.EnumerateArray())
        {
            if (feature.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var acquisitionLevel = CharacterProjectionJson.Integer(feature, "level");
            var displayName = CharacterProjectionJson.String(feature, "name");
            if (acquisitionLevel is null
                || string.IsNullOrWhiteSpace(displayName)
                || acquisitionLevel > level)
            {
                index++;
                continue;
            }

            var key = $"feature.{rule.Catalog.ConceptKey}.level-{acquisitionLevel}.{Slug(displayName)}.{index++}";
            context.AddFeature(
                key,
                displayName,
                CharacterProjectionJson.String(feature, "kind") ?? "advancement-feature",
                CharacterResolutionStates.Resolved,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }
    }

    private static void ProjectSavingThrowTraining(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        bool? startingClass)
    {
        var values = CharacterProjectionJson.Strings(rule.Document, "proficiency");
        if (values.Count == 0)
        {
            values = CharacterProjectionJson.Strings(rule.Document, "savingThrows");
        }
        if (values.Count == 0)
        {
            return;
        }

        // A native 5.x saving-throw proficiency field establishes the standard
        // proficiency progression even when this class is not the starting class.
        context.UsesStandardProficiency = true;
        if (startingClass != true)
        {
            return;
        }

        foreach (var raw in values)
        {
            var ability = CharacterProjectionJson.NormalizeAbilityKey(raw);
            context.SaveProficiencyAbilities.Add(ability);
            context.AddCapability(
                $"save.{ability}.proficient",
                $"{CharacterProjectionJson.Humanize(ability)} saving throw proficiency",
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }
    }

    private static void ProjectHitDie(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        int level)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "hd", out var hd))
        {
            return;
        }

        int? faces = null;
        if (hd.ValueKind == JsonValueKind.Object)
        {
            faces = CharacterProjectionJson.Integer(hd, "faces");
        }
        else if (hd.ValueKind == JsonValueKind.String)
        {
            var text = hd.GetString()?.Trim();
            if (text is not null && text.StartsWith('d')
                && int.TryParse(text[1..], out var parsed))
            {
                faces = parsed;
            }
        }

        context.HitDice[rule.Catalog.ConceptKey] = new CharacterHitDieProfile(
            rule.Catalog.ConceptKey,
            rule.Catalog.DisplayName,
            Math.Max(level, 0),
            faces,
            rule.Provenance);

        var key = $"resource.hit-die.{rule.Catalog.ConceptKey}";
        context.CurrentResources.TryGetValue(key, out var current);
        context.Resources[key] = new CharacterResourceView(
            key,
            $"{rule.Catalog.DisplayName} Hit Dice",
            faces is null
                ? CharacterResolutionStates.ApplicableUnresolved
                : CharacterResolutionStates.Resolved,
            context.CurrentResources.ContainsKey(key) ? current : null,
            level > 0 ? level : null,
            null,
            level > 0
                ? [new CharacterMechanicContributionView(
                    $"{rule.Catalog.ConceptKey}.levels",
                    $"{rule.Catalog.DisplayName} levels",
                    CharacterEffectOperations.Set,
                    level,
                    faces is null ? null : $"d{faces}",
                    rule.Catalog.ConceptKey,
                    rule.Provenance)]
                : [],
            rule.Provenance);
    }

    private static void ProjectSpellcasting(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        int level)
    {
        var raw = CharacterProjectionJson.String(rule.Document, "spellcastingAbility")
            ?? CharacterProjectionJson.String(rule.Document, "casterAbility");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var ability = CharacterProjectionJson.NormalizeAbilityKey(raw);
        var casterProgression = CharacterProjectionJson.String(rule.Document, "casterProgression");
        var isPactMagic = string.Equals(
            casterProgression,
            "pact",
            StringComparison.OrdinalIgnoreCase);
        string? resourceSystemKey = isPactMagic ? "pact-magic" : null;

        if (level > 0 && isPactMagic)
        {
            if (TryReadPactMagicProgression(
                    rule.Document,
                    level,
                    out var pactSlotCount,
                    out var pactSlotLevel))
            {
                context.PactMagicProgressions[rule.Catalog.ConceptKey] =
                    new CharacterPactMagicProgression(
                        rule.Catalog.ConceptKey,
                        rule.Catalog.DisplayName,
                        level,
                        pactSlotCount,
                        pactSlotLevel,
                        rule.Provenance);
            }
        }
        else if (level > 0
                 && TryReadSpellSlotProgression(
                     rule.Document,
                     level,
                     out var slots,
                     out var slotTable))
        {
            context.SpellSlotProgressions[rule.Catalog.ConceptKey] =
                new CharacterSpellSlotProgression(
                    rule.Catalog.ConceptKey,
                    rule.Catalog.DisplayName,
                    level,
                    casterProgression,
                    slots,
                    slotTable,
                    rule.Provenance);
            resourceSystemKey = "spell-slots";
        }

        context.UsesStandardProficiency = true;
        context.AddCapability(
            "spellcasting",
            "Spellcasting",
            rule.Catalog.ConceptKey,
            rule.Provenance);
        context.Spellcasting[$"spellcasting.{rule.Catalog.ConceptKey}"] =
            new CharacterSpellcastingView(
                $"spellcasting.{rule.Catalog.ConceptKey}",
                $"{rule.Catalog.DisplayName} Spellcasting",
                CharacterResolutionStates.ApplicableUnresolved,
                ability,
                resourceSystemKey,
                $"spellcasting.{rule.Catalog.ConceptKey}.save-dc",
                $"spellcasting.{rule.Catalog.ConceptKey}.attack",
                [],
                [],
                rule.Provenance);
    }

    private static bool TryReadPactMagicProgression(
        JsonElement document,
        int classLevel,
        out int slotCount,
        out int slotLevel)
    {
        slotCount = 0;
        slotLevel = 0;
        if (!CharacterProjectionJson.TryGetProperty(document, "classTableGroups", out var groups)
            || groups.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object
                || !CharacterProjectionJson.TryGetProperty(group, "colLabels", out var labels)
                || labels.ValueKind != JsonValueKind.Array
                || !CharacterProjectionJson.TryGetProperty(group, "rows", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var labelArray = labels.EnumerateArray().ToArray();
            var slotCountIndex = FindColumnIndex(labelArray, "Spell Slots");
            var slotLevelIndex = FindColumnIndex(labelArray, "Slot Level");
            if (slotCountIndex < 0 || slotLevelIndex < 0)
            {
                continue;
            }

            var rowArray = rows.EnumerateArray().ToArray();
            if (classLevel <= 0 || classLevel > rowArray.Length)
            {
                return false;
            }

            var row = rowArray[classLevel - 1];
            if (row.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var cells = row.EnumerateArray().ToArray();
            if (slotCountIndex >= cells.Length || slotLevelIndex >= cells.Length
                || !TryReadTableInteger(cells[slotCountIndex], out slotCount)
                || slotCount < 0
                || !TryReadTableInteger(cells[slotLevelIndex], out slotLevel)
                || slotLevel <= 0)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static int FindColumnIndex(
        IReadOnlyList<JsonElement> labels,
        string expectedLabel)
    {
        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index].ValueKind == JsonValueKind.String
                ? labels[index].GetString()
                : labels[index].ToString();
            if (!string.IsNullOrWhiteSpace(label)
                && label.Contains(expectedLabel, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryReadTableInteger(JsonElement value, out int result)
    {
        result = 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result))
        {
            return true;
        }

        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        if (int.TryParse(text.Trim(), out result))
        {
            return true;
        }

        var digits = new string(
            text.SkipWhile(character => !char.IsDigit(character))
                .TakeWhile(char.IsDigit)
                .ToArray());
        return digits.Length > 0 && int.TryParse(digits, out result);
    }

    private static bool TryReadSpellSlotProgression(
        JsonElement document,
        int classLevel,
        out IReadOnlyList<int> slots,
        out IReadOnlyList<IReadOnlyList<int>> slotTable)
    {
        slots = [];
        slotTable = [];
        if (!CharacterProjectionJson.TryGetProperty(document, "classTableGroups", out var groups)
            || groups.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var group in groups.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object
                || !CharacterProjectionJson.TryGetProperty(group, "rowsSpellProgression", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var parsedRows = new List<IReadOnlyList<int>>();
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var values = new List<int>();
                foreach (var value in row.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.Number
                        || !value.TryGetInt32(out var count)
                        || count < 0)
                    {
                        return false;
                    }
                    values.Add(count);
                }
                if (values.Count == 0)
                {
                    return false;
                }
                parsedRows.Add(values);
            }

            if (classLevel <= 0 || classLevel > parsedRows.Count)
            {
                return false;
            }

            slotTable = parsedRows;
            slots = parsedRows[classLevel - 1];
            return true;
        }

        return false;
    }

    private static void ProjectClassQualifications(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        bool? startingClass)
    {
        if (startingClass is null)
        {
            return;
        }

        JsonElement proficiencies;
        var pathPrefix = "starting-proficiencies";
        if (startingClass == true)
        {
            if (!CharacterProjectionJson.TryGetProperty(
                    rule.Document,
                    "startingProficiencies",
                    out proficiencies)
                || proficiencies.ValueKind != JsonValueKind.Object)
            {
                return;
            }
        }
        else
        {
            if (!CharacterProjectionJson.TryGetProperty(
                    rule.Document,
                    "multiclassing",
                    out var multiclassing)
                || multiclassing.ValueKind != JsonValueKind.Object
                || !CharacterProjectionJson.TryGetProperty(
                    multiclassing,
                    "proficienciesGained",
                    out proficiencies)
                || proficiencies.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            pathPrefix = "multiclass-proficiencies";
        }

        CharacterStartingProficiencyProjector.ProjectSkills(
            rule,
            context,
            proficiencies);
        CharacterStructuredProficiencyProjector.Project(
            rule,
            context,
            proficiencies,
            pathPrefix);

        foreach (var category in proficiencies.EnumerateObject()
                     .Where(value => !string.Equals(
                         value.Name,
                         "skills",
                         StringComparison.OrdinalIgnoreCase)))
        {
            IEnumerable<string> values = category.Value.ValueKind switch
            {
                JsonValueKind.Array => category.Value.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()!)
                    .Where(value => !string.IsNullOrWhiteSpace(value)),
                JsonValueKind.String => [category.Value.GetString()!],
                _ => []
            };

            foreach (var value in values)
            {
                var key = $"qualification.{category.Name}.{Slug(value)}";
                context.Qualifications[key] = new CharacterQualificationView(
                    key,
                    category.Name,
                    value,
                    true,
                    CharacterResolutionStates.Resolved,
                    [rule.Catalog.ConceptKey],
                    rule.Provenance);
                context.AddCapability(
                    key,
                    value,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
            }
        }
    }

    private static void ProjectClassFeatures(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        int level)
    {
        JsonElement features;
        var isSubclass = string.Equals(
            rule.Catalog.EntityType,
            "subclass",
            StringComparison.OrdinalIgnoreCase);
        if (isSubclass)
        {
            if (!CharacterProjectionJson.TryGetProperty(
                    rule.Document,
                    "subclassFeatures",
                    out features))
            {
                return;
            }
        }
        else if (!CharacterProjectionJson.TryGetProperty(
                     rule.Document,
                     "classFeatures",
                     out features)
                 && !CharacterProjectionJson.TryGetProperty(
                     rule.Document,
                     "prestigeClassFeatures",
                     out features))
        {
            return;
        }

        var entries = features.ValueKind == JsonValueKind.Array
            ? features.EnumerateArray().ToArray()
            : [features];

        var index = 0;
        foreach (var entry in entries)
        {
            var parsed = TryReadAdvancementFeature(
                entry,
                isSubclass,
                out var displayName,
                out var acquisitionLevel);
            if (!parsed)
            {
                var fallback = ReadFeatureDisplayName(entry, isSubclass);
                if (string.IsNullOrWhiteSpace(fallback))
                {
                    index++;
                    continue;
                }

                context.AddFeature(
                    $"feature.{rule.Catalog.ConceptKey}.progression.unresolved.{index++}",
                    fallback,
                    "advancement-feature",
                    CharacterResolutionStates.ApplicableUnresolved,
                    rule.Catalog.ConceptKey,
                    rule.Provenance);
                continue;
            }

            if (acquisitionLevel > level || level <= 0)
            {
                index++;
                continue;
            }

            var key =
                $"feature.{rule.Catalog.ConceptKey}.level-{acquisitionLevel}.{Slug(displayName)}.{index++}";
            context.AddFeature(
                key,
                displayName,
                "advancement-feature",
                CharacterResolutionStates.Resolved,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }
    }

    private static bool TryReadAdvancementFeature(
        JsonElement entry,
        bool isSubclass,
        out string displayName,
        out int acquisitionLevel)
    {
        displayName = string.Empty;
        acquisitionLevel = 0;

        string? reference = null;
        if (entry.ValueKind == JsonValueKind.String)
        {
            reference = entry.GetString();
        }
        else if (entry.ValueKind == JsonValueKind.Object)
        {
            var referenceProperty = isSubclass
                ? "subclassFeature"
                : "classFeature";
            reference = CharacterProjectionJson.String(entry, referenceProperty);

            if (string.IsNullOrWhiteSpace(reference))
            {
                var directName = CharacterProjectionJson.String(entry, "name");
                var directLevel = CharacterProjectionJson.Integer(entry, "level");
                if (!string.IsNullOrWhiteSpace(directName)
                    && directLevel is > 0)
                {
                    displayName = directName.Trim();
                    acquisitionLevel = directLevel.Value;
                    return true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var parts = reference.Split('|');
        var levelIndex = isSubclass ? 5 : 3;
        if (parts.Length <= levelIndex
            || string.IsNullOrWhiteSpace(parts[0])
            || !int.TryParse(parts[levelIndex], out var parsedLevel)
            || parsedLevel <= 0)
        {
            return false;
        }

        displayName = parts[0].Trim();
        acquisitionLevel = parsedLevel;
        return true;
    }

    private static string? ReadFeatureDisplayName(
        JsonElement entry,
        bool isSubclass)
    {
        if (entry.ValueKind == JsonValueKind.String)
        {
            return entry.GetString()?
                .Split('|', 2)[0]
                .Trim();
        }
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var directName = CharacterProjectionJson.String(entry, "name");
        if (!string.IsNullOrWhiteSpace(directName))
        {
            return directName.Trim();
        }

        var reference = CharacterProjectionJson.String(
            entry,
            isSubclass ? "subclassFeature" : "classFeature");
        return string.IsNullOrWhiteSpace(reference)
            ? null
            : reference.Split('|', 2)[0].Trim();
    }

    private static string Slug(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split([' ', '/', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

internal sealed class ItemCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        context.EquippedItems.Contains(rule.Catalog.ConceptKey)
        && string.Equals(rule.Catalog.EntityType, "item", StringComparison.OrdinalIgnoreCase);

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        context.AddFeature(
            $"feature.{rule.Catalog.ConceptKey}",
            rule.Catalog.DisplayName,
            "equipped-item",
            CharacterResolutionStates.Resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance);

        var itemType = NormalizeItemType(
            CharacterProjectionJson.String(rule.Document, "type"));
        var ac = CharacterProjectionJson.Integer(rule.Document, "ac")
            ?? CharacterProjectionJson.Integer(rule.Document, "armorClass");
        if (ac is int armorClass)
        {
            var (operation, target) = itemType switch
            {
                "LA" or "MA" or "HA" => (
                    CharacterEffectOperations.Set,
                    "defense.ac.armor-base"),
                "S" => (
                    CharacterEffectOperations.Add,
                    "defense.ac.shield-bonus"),
                _ => (
                    CharacterEffectOperations.Add,
                    "defense.ac.unclassified")
            };
            context.AddEffect(new CharacterRuleEffectView(
                $"{rule.Catalog.ConceptKey}.armor-class",
                CharacterEffectKinds.MechanicContribution,
                operation,
                target,
                armorClass,
                itemType,
                null,
                rule.Catalog.ConceptKey,
                rule.Provenance));
        }

        var damage = CharacterProjectionJson.String(rule.Document, "dmg1")
            ?? CharacterProjectionJson.String(rule.Document, "damage");
        var damageType = CharacterProjectionJson.String(rule.Document, "dmgType");
        var weaponCategory = CharacterProjectionJson.String(rule.Document, "weaponCategory");
        if (!string.IsNullOrWhiteSpace(damage)
            || !string.IsNullOrWhiteSpace(weaponCategory))
        {
            var actionKey = $"action.attack.{rule.Catalog.ConceptKey}";
            var range = CharacterProjectionJson.RangeText(rule.Document);
            context.Actions[actionKey] = new CharacterActionView(
                actionKey,
                rule.Catalog.DisplayName,
                "attack",
                CharacterResolutionStates.ApplicableUnresolved,
                $"attack.{rule.Catalog.ConceptKey}",
                damage,
                damageType,
                range,
                null,
                null,
                null,
                null,
                [],
                rule.Provenance);

            if (itemType is "M" or "R" && !string.IsNullOrWhiteSpace(damage))
            {
                context.WeaponAttacks[rule.Catalog.ConceptKey] =
                    new CharacterWeaponAttackProfile(
                        rule.Catalog.ConceptKey,
                        rule.Catalog.DisplayName,
                        itemType,
                        string.IsNullOrWhiteSpace(weaponCategory)
                            ? null
                            : weaponCategory.Trim(),
                        HasItemProperty(rule.Document, "F"),
                        ReadSignedInteger(rule.Document, "bonusWeaponAttack")
                            ?? ReadSignedInteger(rule.Document, "bonusWeapon")
                            ?? 0,
                        ReadSignedInteger(rule.Document, "bonusWeaponDamage")
                            ?? ReadSignedInteger(rule.Document, "bonusWeapon")
                            ?? 0,
                        damage,
                        damageType,
                        range,
                        rule.Provenance);
            }
        }

        var attunement = CharacterProjectionJson.String(rule.Document, "reqAttune")
            ?? CharacterProjectionJson.String(rule.Document, "requiresAttunement");
        if (!string.IsNullOrWhiteSpace(attunement))
        {
            var factKey = $"item.{rule.Catalog.ConceptKey}.attuned";
            if (!context.BooleanFacts.TryGetValue(factKey, out var attuned) || !attuned)
            {
                context.Conflicts.Add(new CharacterProjectionConflictView(
                    $"requirement.{factKey}",
                    "missing-character-input",
                    $"The equipped item '{rule.Catalog.DisplayName}' has an attunement requirement whose Character state is not satisfied.",
                    [],
                    [rule.Catalog.ConceptKey]));
            }
        }
    }

    private static string? NormalizeItemType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value.Split('|', 2)[0].Trim().ToUpperInvariant();
    }

    private static bool HasItemProperty(JsonElement document, string propertyCode)
    {
        foreach (var field in new[] { "property", "propertyAdd" })
        {
            if (!CharacterProjectionJson.TryGetProperty(document, field, out var properties)
                || properties.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var property in properties.EnumerateArray())
            {
                string? raw = property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString(),
                    JsonValueKind.Object => CharacterProjectionJson.String(property, "uid")
                        ?? CharacterProjectionJson.String(property, "abbreviation"),
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }
                var abbreviation = raw.Split('|', 2)[0].Trim();
                if (string.Equals(abbreviation, propertyCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static int? ReadSignedInteger(JsonElement document, string property)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, property, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
        {
            return numeric;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return int.TryParse(
            text,
            System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }
}

internal sealed class SpellCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        (context.KnownSpells.Contains(rule.Catalog.ConceptKey)
            || context.PreparedSpells.Contains(rule.Catalog.ConceptKey))
        && string.Equals(rule.Catalog.EntityType, "spell", StringComparison.OrdinalIgnoreCase);

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        context.AddFeature(
            $"feature.{rule.Catalog.ConceptKey}",
            rule.Catalog.DisplayName,
            "spell",
            CharacterResolutionStates.Resolved,
            rule.Catalog.ConceptKey,
            rule.Provenance);

        var actionType = ReadActionType(rule.Document);
        var actionKey = $"action.spell.{rule.Catalog.ConceptKey}";
        context.Actions[actionKey] = new CharacterActionView(
            actionKey,
            rule.Catalog.DisplayName,
            actionType,
            CharacterResolutionStates.ApplicableUnresolved,
            null,
            null,
            null,
            CharacterProjectionJson.RangeText(rule.Document),
            null,
            null,
            "spellcasting.resource",
            null,
            ["spellcasting"],
            rule.Provenance);
    }

    private static string? ReadActionType(JsonElement document)
    {
        if (!CharacterProjectionJson.TryGetProperty(document, "time", out var time)
            || time.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var first = time.EnumerateArray().FirstOrDefault();
        return first.ValueKind == JsonValueKind.Object
            ? CharacterProjectionJson.String(first, "unit")
            : null;
    }
}

internal sealed class GenericCharacterRuleProjectionModule : ICharacterRuleProjectionModule
{
    public bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context) =>
        context.IsSelected(rule.Catalog.ConceptKey)
        || context.ActiveConditions.Contains(rule.Catalog.ConceptKey)
        || rule.Catalog.ConceptKey.StartsWith("house.", StringComparison.OrdinalIgnoreCase);

    public void Project(CharacterProjectionRule rule, CharacterProjectionContext context)
    {
        if (context.IsSelected(rule.Catalog.ConceptKey)
            && !context.Features.ContainsKey($"feature.{rule.Catalog.ConceptKey}"))
        {
            context.AddFeature(
                $"feature.{rule.Catalog.ConceptKey}",
                rule.Catalog.DisplayName,
                rule.Catalog.EntityType,
                CharacterResolutionStates.Resolved,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }

        if (context.IsSelected(rule.Catalog.ConceptKey))
        {
            CharacterAbilityProjector.Project(
                rule,
                context);
            CharacterStartingProficiencyProjector.ProjectSourceSkills(
                rule,
                context,
                rule.Document);
            CharacterStructuredProficiencyProjector.Project(
                rule,
                context,
                rule.Document,
                "source-proficiencies");
        }

        if (context.ActiveConditions.Contains(rule.Catalog.ConceptKey))
        {
            context.AddFeature(
                $"condition.{rule.Catalog.ConceptKey}",
                rule.Catalog.DisplayName,
                "condition",
                CharacterResolutionStates.ApplicableUnresolved,
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }

        ProjectPrerequisites(rule, context);
        ProjectNormalizedCharacterExtension(rule, context);
        ProjectKnownHouseRule(rule, context);
    }

    private static void ProjectPrerequisites(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (!context.IsSelected(rule.Catalog.ConceptKey))
        {
            return;
        }

        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "prerequisite", out var prerequisite)
            && !CharacterProjectionJson.TryGetProperty(rule.Document, "prerequisites", out prerequisite))
        {
            return;
        }

        context.Prerequisites[rule.Catalog.ConceptKey] = new CharacterPrerequisiteView(
            rule.Catalog.ConceptKey,
            CharacterResolutionStates.ApplicableUnresolved,
            null,
            [new CharacterPrerequisiteRequirementView(
                $"{rule.Catalog.ConceptKey}.source-prerequisite",
                "source-defined",
                null,
                null,
                null,
                null,
                null,
                CharacterResolutionStates.ApplicableUnresolved,
                "The source defines a prerequisite, but this source representation does not yet expose a normalized prerequisite expression.")],
            rule.Provenance);
    }

    private static void ProjectNormalizedCharacterExtension(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        if (!CharacterProjectionJson.TryGetProperty(rule.Document, "_rulesCore", out var rulesCore)
            || !CharacterProjectionJson.TryGetProperty(rulesCore, "character", out var character)
            || character.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var capability in CharacterProjectionJson.Strings(character, "capabilities"))
        {
            context.AddCapability(
                capability,
                CharacterProjectionJson.Humanize(capability),
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }

        NormalizedCharacterPrerequisiteProjector.Project(rule, context, character);
        ProjectEffects(rule, context, character);
        ProjectMovement(rule, context, character);
        ProjectResources(rule, context, character);
        ProjectActions(rule, context, character);
        ProjectProcedures(rule, context, character);
        ProjectPassives(rule, context, character);
        ProjectQualifications(rule, context, character);
    }

    private static void ProjectEffects(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "effects", out var effects)
            || effects.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var item in effects.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var kind = CharacterProjectionJson.String(item, "kind") ?? CharacterEffectKinds.Other;
            var operation = CharacterProjectionJson.String(item, "operation") ?? CharacterEffectOperations.Add;
            var target = CharacterProjectionJson.String(item, "target");
            if (string.IsNullOrWhiteSpace(target))
            {
                index++;
                continue;
            }

            var effect = new CharacterRuleEffectView(
                CharacterProjectionJson.String(item, "key")
                    ?? $"{rule.Catalog.ConceptKey}.effect.{index}",
                kind,
                operation,
                target,
                CharacterProjectionJson.Integer(item, "value"),
                CharacterProjectionJson.String(item, "textValue"),
                CharacterProjectionJson.String(item, "condition"),
                rule.Catalog.ConceptKey,
                rule.Provenance);
            context.AddEffect(effect);
            index++;
        }
    }

    private static void ProjectMovement(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "movement", out var movement)
            || movement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in movement.EnumerateObject())
        {
            var value = property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out var direct)
                    ? direct
                    : CharacterProjectionJson.Integer(property.Value, "value");
            var unit = property.Value.ValueKind == JsonValueKind.Object
                ? CharacterProjectionJson.String(property.Value, "unit")
                : null;
            var key = property.Name.StartsWith("movement.", StringComparison.OrdinalIgnoreCase)
                ? property.Name
                : $"movement.{property.Name}";
            context.Movement[key] = new CharacterMovementModeView(
                key,
                CharacterProjectionJson.Humanize(property.Name),
                value is null
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                value,
                unit ?? "ft",
                value is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.{key}",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        value,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }

    private static void ProjectResources(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "resources", out var resources)
            || resources.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in resources.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            context.CurrentResources.TryGetValue(key, out var current);
            var maximum = CharacterProjectionJson.Integer(item, "maximum");
            context.Resources[key] = new CharacterResourceView(
                key,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                maximum is null
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                context.CurrentResources.ContainsKey(key) ? current : null,
                maximum,
                CharacterProjectionJson.String(item, "recoveryProcedure"),
                maximum is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.{key}.maximum",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        maximum,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }

    private static void ProjectActions(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "actions", out var actions)
            || actions.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in actions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            context.Actions[key] = new CharacterActionView(
                key,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                CharacterProjectionJson.String(item, "actionType"),
                CharacterResolutionStates.Resolved,
                CharacterProjectionJson.String(item, "attackMechanic"),
                CharacterProjectionJson.String(item, "damage"),
                CharacterProjectionJson.String(item, "damageType"),
                CharacterProjectionJson.String(item, "range"),
                CharacterProjectionJson.String(item, "reach"),
                CharacterProjectionJson.String(item, "target"),
                CharacterProjectionJson.String(item, "resource"),
                CharacterProjectionJson.Integer(item, "resourceCost"),
                CharacterProjectionJson.Strings(item, "requiredCapabilities"),
                rule.Provenance);
        }
    }

    private static void ProjectProcedures(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "procedures", out var procedures)
            || procedures.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in procedures.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var missingCapabilities = CharacterProjectionJson.Strings(item, "requiredCapabilities")
                .Where(value => !context.Capabilities.Contains(value))
                .ToArray();
            var missingFacts = CharacterProjectionJson.Strings(item, "requiredFacts")
                .Where(value => !context.IntegerFacts.ContainsKey(value)
                    && !context.BooleanFacts.ContainsKey(value)
                    && !context.StringFacts.ContainsKey(value))
                .ToArray();
            var requiredChoices = CharacterProjectionJson.Strings(item, "requiredChoices")
                .Where(value => !context.Choices.ContainsKey(value))
                .ToArray();
            var requiredRolls = CharacterProjectionJson.Strings(item, "requiredRolls")
                .Where(value => !context.Rolls.ContainsKey(value))
                .ToArray();

            var state = missingCapabilities.Length > 0
                ? CharacterResolutionStates.MissingCapability
                : requiredChoices.Length > 0
                    ? CharacterResolutionStates.ChoiceRequired
                    : requiredRolls.Length > 0
                        ? CharacterResolutionStates.RollRequired
                        : missingFacts.Length > 0
                            ? CharacterResolutionStates.MissingCharacterInput
                            : CharacterResolutionStates.Resolved;

            context.Procedures[key] = new CharacterProcedureView(
                key,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                state,
                CharacterProjectionJson.String(item, "presentationRole"),
                CharacterProjectionJson.Strings(item, "requiredCapabilities"),
                CharacterProjectionJson.Strings(item, "requiredFacts"),
                CharacterProjectionJson.Strings(item, "requiredChoices"),
                CharacterProjectionJson.Strings(item, "requiredRolls"),
                [],
                rule.Provenance);
        }
    }

    private static void ProjectPassives(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "passives", out var passives)
            || passives.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in passives.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            var value = CharacterProjectionJson.Integer(item, "value");
            context.Mechanics[key] = new CharacterResolvedMechanicView(
                key,
                "passive",
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                value is null
                    ? CharacterResolutionStates.ApplicableUnresolved
                    : CharacterResolutionStates.Resolved,
                value,
                null,
                null,
                [],
                [],
                [],
                [],
                value is null
                    ? []
                    : [new CharacterMechanicContributionView(
                        $"{rule.Catalog.ConceptKey}.{key}",
                        rule.Catalog.DisplayName,
                        CharacterEffectOperations.Set,
                        value,
                        null,
                        rule.Catalog.ConceptKey,
                        rule.Provenance)],
                rule.Provenance);
        }
    }

    private static void ProjectQualifications(
        CharacterProjectionRule rule,
        CharacterProjectionContext context,
        JsonElement character)
    {
        if (!CharacterProjectionJson.TryGetProperty(character, "qualifications", out var qualifications)
            || qualifications.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in qualifications.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var key = CharacterProjectionJson.String(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            var category = CharacterProjectionJson.String(item, "category") ?? "other";
            context.Qualifications[key] = new CharacterQualificationView(
                key,
                category,
                CharacterProjectionJson.String(item, "displayName") ?? CharacterProjectionJson.Humanize(key),
                true,
                CharacterResolutionStates.Resolved,
                [rule.Catalog.ConceptKey],
                rule.Provenance);
            context.AddCapability(
                key,
                CharacterProjectionJson.Humanize(key),
                rule.Catalog.ConceptKey,
                rule.Provenance);
        }
    }

    private static void ProjectKnownHouseRule(
        CharacterProjectionRule rule,
        CharacterProjectionContext context)
    {
        var mechanic = CharacterProjectionJson.String(rule.Document, "mechanic");
        var choosesResourceSystem =
            string.Equals(mechanic, "caster-resource-choice", StringComparison.OrdinalIgnoreCase)
            || CharacterProjectionJson.Boolean(
                rule.Document,
                "casterChoosesResourceSystem") == true;
        if (!choosesResourceSystem)
        {
            return;
        }

        var choiceKey = "spellcasting.resource-system";
        var available = CharacterProjectionJson.Strings(
                rule.Document,
                "availableResourceSystems")
            .Select(CharacterProjectionJson.NormalizeResourceSystemKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasChoice = context.Choices.TryGetValue(choiceKey, out var rawChoice);
        var choice = hasChoice
            ? CharacterProjectionJson.NormalizeResourceSystemKey(rawChoice!)
            : null;
        var valid = choice is null
            || available.Length == 0
            || available.Contains(choice, StringComparer.OrdinalIgnoreCase);

        if (choice is not null && !valid)
        {
            context.Conflicts.Add(new CharacterProjectionConflictView(
                "conflict.spellcasting.resource-system",
                "invalid-runtime-choice",
                $"Spellcasting resource system '{rawChoice}' is not allowed by the effective house rule.",
                [],
                [rule.Catalog.ConceptKey]));
        }

        context.Spellcasting["spellcasting.resource-choice"] =
            new CharacterSpellcastingView(
                "spellcasting.resource-choice",
                rule.Catalog.DisplayName,
                !hasChoice || !valid
                    ? CharacterResolutionStates.ChoiceRequired
                    : CharacterResolutionStates.Resolved,
                null,
                valid ? choice : null,
                null,
                null,
                [],
                !hasChoice || !valid ? [choiceKey] : [],
                rule.Provenance);
    }
}
