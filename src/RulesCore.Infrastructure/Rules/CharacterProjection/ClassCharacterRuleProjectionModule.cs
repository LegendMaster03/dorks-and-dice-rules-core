using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

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

        foreach (var (capabilityKey, displayName) in new[]
                 {
                     (Key: "save.fortitude", DisplayName: "Fortitude Save"),
                     (Key: "save.reflex", DisplayName: "Reflex Save"),
                     (Key: "save.will", DisplayName: "Will Save"),
                     (Key: "combat.base-attack-bonus", DisplayName: "Base Attack Bonus"),
                     (Key: "combat.grapple", DisplayName: "Grapple"),
                     (Key: "defense.ac.touch", DisplayName: "Touch Armor Class"),
                     (Key: "defense.ac.flat-footed", DisplayName: "Flat-Footed Armor Class"),
                     (Key: "competency.skill-ranks", DisplayName: "Skill Ranks"),
                     (Key: "resource.nonlethal-damage", DisplayName: "Nonlethal Damage")
                 })
        {
            context.AddCapability(
                capabilityKey,
                displayName,
                rule.Catalog.ConceptKey,
                rule.Provenance);
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
                rule.Provenance,
                rule.Catalog.EntityType,
                acquisitionLevel);
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
        context.AddCapability(
            isPactMagic ? "spellcasting.pact" : "spellcasting.standard",
            isPactMagic ? "Pact Magic" : "Standard Spellcasting",
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
        CharacterLanguageProficiencyProjector.Project(
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
                out var acquisitionLevel,
                out var featureReference);
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
                    rule.Provenance,
                    rule.Catalog.EntityType);
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
                rule.Provenance,
                rule.Catalog.EntityType,
                acquisitionLevel,
                featureDefinition: featureReference is null
                    ? null
                    : context.ResolveFeatureReference(featureReference, isSubclass));
        }
    }

    private static bool TryReadAdvancementFeature(
        JsonElement entry,
        bool isSubclass,
        out string displayName,
        out int acquisitionLevel,
        out string? featureReference)
    {
        displayName = string.Empty;
        acquisitionLevel = 0;
        featureReference = null;

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
        featureReference = reference.Trim();
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

