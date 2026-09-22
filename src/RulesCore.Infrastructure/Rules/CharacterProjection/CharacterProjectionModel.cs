using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed record CharacterProjectionRule(
    ResolvedRuleCatalogItemView Catalog,
    JsonElement Document,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterWeaponAttackProfile(
    string ConceptKey,
    string DisplayName,
    string ItemType,
    string? WeaponCategory,
    bool Finesse,
    int AttackBonus,
    int DamageBonus,
    string DamageExpression,
    string? DamageType,
    string? Range,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterWeaponCatalogEntry(
    string ConceptKey,
    string DisplayName,
    string ItemType,
    string? WeaponCategory,
    IReadOnlySet<string> Properties,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterSpellSlotProgression(
    string ConceptKey,
    string DisplayName,
    int ClassLevel,
    string? CasterProgression,
    IReadOnlyList<int> SlotsBySpellLevel,
    IReadOnlyList<IReadOnlyList<int>> SlotsByClassLevel,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterPactMagicProgression(
    string ConceptKey,
    string DisplayName,
    int ClassLevel,
    int SlotCount,
    int SlotLevel,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterHitDieProfile(
    string ConceptKey,
    string DisplayName,
    int ClassLevel,
    int? Faces,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterFeatureCatalogEntry(
    string ConceptKey,
    string EntityType,
    string DisplayName,
    string ClassName,
    string? ClassSource,
    string? SubclassName,
    string? SubclassSource,
    int AcquisitionLevel,
    string? FeatureSource,
    IReadOnlyList<CharacterRuleEffectView> Effects,
    CharacterMechanicProvenanceView Provenance);

internal interface ICharacterRuleProjectionModule
{
    bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context);
    void Project(CharacterProjectionRule rule, CharacterProjectionContext context);
}

internal sealed class CharacterProjectionContext
{
    private static readonly StringComparer Keys = StringComparer.OrdinalIgnoreCase;

    public CharacterProjectionContext(CharacterRulesProjectionRequest request)
    {
        Request = request;
        SelectedConcepts = new HashSet<string>(
            request.SelectedConcepts?.Select(value => Normalize(value.ConceptKey)) ?? [],
            Keys);
        EquippedItems = new HashSet<string>(
            request.EquippedItemConceptKeys?.Select(Normalize) ?? [],
            Keys);
        InventoryItems = new HashSet<string>(
            request.ItemConceptKeys?.Select(Normalize) ?? [],
            Keys);
        InventoryItems.UnionWith(EquippedItems);
        KnownSpells = new HashSet<string>(
            request.KnownSpellConceptKeys?.Select(Normalize) ?? [],
            Keys);
        PreparedSpells = new HashSet<string>(
            request.PreparedSpellConceptKeys?.Select(Normalize) ?? [],
            Keys);
        ActiveConditions = new HashSet<string>(
            request.ConditionKeys?.Select(Normalize) ?? [],
            Keys);
        TrainingKeys = new HashSet<string>(
            request.TrainingKeys?.Select(Normalize) ?? [],
            Keys);
        ClassSkillKeys = new HashSet<string>(
            request.ClassSkillKeys?.Select(Normalize) ?? [],
            Keys);
        HasClassSkillInput = request.ClassSkillKeys is not null;
        HasTrainingInput = request.TrainingKeys is not null;
        HasCompetencyRanksInput = request.CompetencyRanks is not null;
        Capabilities = new HashSet<string>(
            request.CapabilityKeys?.Select(Normalize) ?? [],
            Keys);
        Choices = (request.Choices ?? [])
            .GroupBy(value => Normalize(value.ChoiceKey), Keys)
            .ToDictionary(group => group.Key, group => group.Last().Value.Trim(), Keys);
        Rolls = (request.Rolls ?? [])
            .GroupBy(value => Normalize(value.RollKey), Keys)
            .ToDictionary(group => group.Key, group => group.Last().Value, Keys);
        BaseAbilityScores = NormalizeIntegerDictionary(request.BaseAbilityScores);
        CompetencyRanks = NormalizeIntegerDictionary(request.CompetencyRanks);
        IntegerFacts = NormalizeIntegerDictionary(request.IntegerFacts);
        BooleanFacts = NormalizeBooleanDictionary(request.BooleanFacts);
        StringFacts = NormalizeStringDictionary(request.StringFacts);
        CurrentResources = NormalizeIntegerDictionary(request.CurrentResources);
        HitPointGains = request.HitPointGains ?? [];
        RequestedMechanics = request.RequestedMechanicKeys is null
            ? null
            : new HashSet<string>(request.RequestedMechanicKeys.Select(Normalize), Keys);
    }

    public CharacterRulesProjectionRequest Request { get; }
    public HashSet<string> SelectedConcepts { get; }
    public HashSet<string> EquippedItems { get; }
    public HashSet<string> InventoryItems { get; }
    public HashSet<string> KnownSpells { get; }
    public HashSet<string> PreparedSpells { get; }
    public HashSet<string> ActiveConditions { get; }
    public HashSet<string> TrainingKeys { get; }
    public HashSet<string> ClassSkillKeys { get; }
    public bool HasClassSkillInput { get; }
    public bool HasTrainingInput { get; }
    public bool HasCompetencyRanksInput { get; }
    public HashSet<string> Capabilities { get; }
    public Dictionary<string, string> Choices { get; }
    public Dictionary<string, int> Rolls { get; }
    public Dictionary<string, int> BaseAbilityScores { get; }
    public Dictionary<string, int> CompetencyRanks { get; }
    public Dictionary<string, int> IntegerFacts { get; }
    public Dictionary<string, bool> BooleanFacts { get; }
    public Dictionary<string, string> StringFacts { get; }
    public Dictionary<string, int> CurrentResources { get; }
    public IReadOnlyList<CharacterHitPointGainInput> HitPointGains { get; }
    public HashSet<string>? RequestedMechanics { get; }

    public Dictionary<string, List<CharacterMechanicContributionView>> AbilityContributions { get; } =
        new(Keys);
    public Dictionary<string, HashSet<string>> RequiredAbilityChoices { get; } =
        new(Keys);
    public HashSet<string> SaveProficiencyAbilities { get; } = new(Keys);
    public bool UsesStandardProficiency { get; set; }
    public int StandardProficiencyLevel { get; set; }
    public string? SizeCategory { get; private set; }
    public List<CharacterMechanicContributionView> ThreeXBaseAttackContributions { get; } = [];
    public Dictionary<string, List<CharacterMechanicContributionView>> ThreeXSaveContributions { get; } =
        new(Keys);
    public Dictionary<string, HashSet<string>> DerivedClassSkillGrantSources { get; } =
        new(Keys);
    public HashSet<string> DerivedClassSkillTypes { get; } = new(Keys);
    public bool HasDerivedClassSkillData { get; set; }
    public Dictionary<string, string> RuleDisplayNames { get; } = new(Keys);
    public Dictionary<string, string> RuleEntityTypes { get; } = new(Keys);
    public Dictionary<string, string> CompetencyConceptKeysByDisplayName { get; } = new(Keys);
    public string? StartingClassConceptKey { get; private set; }
    public bool StartingClassChoiceRequired { get; private set; }
    public Dictionary<string, CharacterChoiceOptionView> CompetencyChoiceOptionsByName { get; } =
        new(Keys);
    public Dictionary<string, CharacterChoiceOptionView> SkillChoiceOptionsByConceptKey { get; } =
        new(Keys);
    public Dictionary<string, CharacterChoiceOptionView> ToolChoiceOptionsByConceptKey { get; } =
        new(Keys);
    public Dictionary<string, HashSet<string>> ToolChoiceConceptKeysByCategory { get; } =
        new(Keys);
    public Dictionary<string, CharacterChoiceOptionView> LanguageChoiceOptionsByConceptKey { get; } =
        new(Keys);
    public Dictionary<string, HashSet<string>> LanguageChoiceConceptKeysByCategory { get; } =
        new(Keys);
    public HashSet<string> KnownLanguageChoiceIdentities { get; } = new(Keys);
    public Dictionary<string, CharacterWeaponCatalogEntry> WeaponCatalog { get; } = new(Keys);
    public Dictionary<string, CharacterWeaponAttackProfile> WeaponAttacks { get; } = new(Keys);
    public Dictionary<string, CharacterSpellSlotProgression> SpellSlotProgressions { get; } = new(Keys);
    public Dictionary<string, CharacterPactMagicProgression> PactMagicProgressions { get; } = new(Keys);
    public Dictionary<string, CharacterHitDieProfile> HitDice { get; } = new(Keys);
    public List<CharacterFeatureCatalogEntry> FeatureCatalog { get; } = [];

    public Dictionary<string, CharacterResolvedMechanicView> Mechanics { get; } = new(Keys);
    public Dictionary<string, CharacterCapabilityView> CapabilityViews { get; } = new(Keys);
    public Dictionary<string, CharacterMovementModeView> Movement { get; } = new(Keys);
    public Dictionary<string, CharacterQualificationView> Qualifications { get; } = new(Keys);
    public Dictionary<string, CharacterActionView> Actions { get; } = new(Keys);
    public Dictionary<string, CharacterFeatureView> Features { get; } = new(Keys);
    public Dictionary<string, CharacterEquipmentDefinitionView> Equipment { get; } = new(Keys);
    public Dictionary<string, CharacterResourceView> Resources { get; } = new(Keys);
    public Dictionary<string, CharacterSpellcastingView> Spellcasting { get; } = new(Keys);
    public Dictionary<string, CharacterProcedureView> Procedures { get; } = new(Keys);
    public Dictionary<string, CharacterChoiceView> ChoiceViews { get; } = new(Keys);
    public Dictionary<string, CharacterPrerequisiteView> Prerequisites { get; } = new(Keys);
    public List<CharacterGrantView> Grants { get; } = [];
    public List<CharacterRuleEffectView> Effects { get; } = [];
    public List<CharacterProjectionConflictView> Conflicts { get; } = [];

    public bool IsSelected(string conceptKey) =>
        SelectedConcepts.Contains(conceptKey)
        || Request.Advancements?.Any(value =>
            string.Equals(value.ConceptKey?.Trim(), conceptKey, StringComparison.OrdinalIgnoreCase)) == true;

    public int AdvancementLevel(string conceptKey) =>
        Request.Advancements?
            .Where(value => string.Equals(
                value.ConceptKey?.Trim(),
                conceptKey,
                StringComparison.OrdinalIgnoreCase))
            .Sum(value => Math.Max(value.Level, 0))
        ?? 0;


    public void RegisterRuleIdentity(
        string conceptKey,
        string displayName,
        string entityType)
    {
        if (string.IsNullOrWhiteSpace(conceptKey)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(entityType))
        {
            return;
        }

        var normalized = conceptKey.Trim();
        RuleDisplayNames[normalized] = displayName.Trim();
        RuleEntityTypes[normalized] = entityType.Trim();
    }

    public void ResolveStartingClass()
    {
        const string choiceKey = "advancement.starting-class";
        var classConceptKeys = (Request.Advancements ?? [])
            .Where(value => value.Level > 0)
            .Select(value => value.ConceptKey?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Where(value =>
                RuleEntityTypes.TryGetValue(value, out var entityType)
                && string.Equals(entityType, "class", StringComparison.OrdinalIgnoreCase))
            .Distinct(Keys)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        if (classConceptKeys.Length == 0)
        {
            return;
        }
        if (classConceptKeys.Length == 1)
        {
            StartingClassConceptKey = classConceptKeys[0];
            return;
        }

        var options = classConceptKeys
            .Select(value => new CharacterChoiceOptionView(
                value,
                RuleDisplayNames.GetValueOrDefault(value) ?? CharacterProjectionJson.Humanize(value),
                value))
            .ToArray();

        var supplied = StringFacts.GetValueOrDefault(choiceKey);
        if (string.IsNullOrWhiteSpace(supplied))
        {
            supplied = Choices.GetValueOrDefault(choiceKey);
        }

        var selected = options.FirstOrDefault(value =>
            string.Equals(value.Value, supplied?.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.DisplayName, supplied?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            StartingClassConceptKey = selected.Value;
            ChoiceViews[choiceKey] = new CharacterChoiceView(
                choiceKey,
                "choice-group.advancement.starting-class",
                "Starting Class",
                "starting-class",
                CharacterResolutionStates.Resolved,
                options,
                selected.Value,
                selected.Value,
                EmptyProvenance());
            return;
        }

        StartingClassChoiceRequired = true;
        ChoiceViews[choiceKey] = new CharacterChoiceView(
            choiceKey,
            "choice-group.advancement.starting-class",
            "Starting Class",
            "starting-class",
            CharacterResolutionStates.ChoiceRequired,
            options,
            supplied,
            null,
            EmptyProvenance());
        Mechanics[choiceKey] = new CharacterResolvedMechanicView(
            choiceKey,
            "advancement",
            "Starting Class",
            CharacterResolutionStates.ChoiceRequired,
            null,
            null,
            null,
            [],
            [],
            [choiceKey],
            [],
            [],
            EmptyProvenance());

        if (!string.IsNullOrWhiteSpace(supplied))
        {
            Conflicts.Add(new CharacterProjectionConflictView(
                "conflict.advancement.starting-class",
                "invalid-runtime-choice",
                $"Starting class '{supplied}' is not one of the Character's selected base classes.",
                [choiceKey],
                classConceptKeys));
        }
    }

    public bool? IsStartingClass(string conceptKey)
    {
        if (!RuleEntityTypes.TryGetValue(conceptKey, out var entityType)
            || !string.Equals(entityType, "class", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (StartingClassChoiceRequired)
        {
            return null;
        }
        return StartingClassConceptKey is not null
            && string.Equals(
                StartingClassConceptKey,
                conceptKey,
                StringComparison.OrdinalIgnoreCase);
    }

    public void RegisterCompetencyIdentity(
        string displayName,
        string conceptKey,
        string? familyName,
        string? specialty,
        string competencyKind)
    {
        if (string.IsNullOrWhiteSpace(conceptKey))
        {
            return;
        }

        var normalizedConceptKey = conceptKey.Trim();
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? CharacterProjectionJson.Humanize(normalizedConceptKey)
            : displayName.Trim();
        var option = new CharacterChoiceOptionView(
            normalizedConceptKey,
            normalizedDisplayName,
            normalizedConceptKey);

        CompetencyConceptKeysByDisplayName[normalizedConceptKey] = normalizedConceptKey;
        CompetencyChoiceOptionsByName[normalizedConceptKey] = option;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            CompetencyConceptKeysByDisplayName[displayName.Trim()] = normalizedConceptKey;
            CompetencyChoiceOptionsByName[displayName.Trim()] = option;
        }
        if (!string.IsNullOrWhiteSpace(familyName)
            && !string.IsNullOrWhiteSpace(specialty))
        {
            var compositeName = $"{familyName.Trim()} ({specialty.Trim()})";
            CompetencyConceptKeysByDisplayName[compositeName] = normalizedConceptKey;
            CompetencyChoiceOptionsByName[compositeName] = option;
        }

        if (string.Equals(
                competencyKind,
                CharacterCompetencyKinds.Skill,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                competencyKind,
                CharacterCompetencyKinds.SpecializedSkill,
                StringComparison.OrdinalIgnoreCase))
        {
            SkillChoiceOptionsByConceptKey[normalizedConceptKey] = option;
        }
        else if (string.Equals(
                     competencyKind,
                     CharacterCompetencyKinds.Tool,
                     StringComparison.OrdinalIgnoreCase))
        {
            ToolChoiceOptionsByConceptKey[normalizedConceptKey] = option;
        }
    }

    public void RegisterToolChoiceCategory(
        string conceptKey,
        string categoryKey)
    {
        if (string.IsNullOrWhiteSpace(conceptKey)
            || string.IsNullOrWhiteSpace(categoryKey))
        {
            return;
        }

        var normalizedConceptKey = conceptKey.Trim();
        if (!ToolChoiceOptionsByConceptKey.ContainsKey(normalizedConceptKey))
        {
            return;
        }

        var normalizedCategory = Normalize(categoryKey);
        if (!ToolChoiceConceptKeysByCategory.TryGetValue(
                normalizedCategory,
                out var conceptKeys))
        {
            conceptKeys = new HashSet<string>(Keys);
            ToolChoiceConceptKeysByCategory[normalizedCategory] = conceptKeys;
        }
        conceptKeys.Add(normalizedConceptKey);
    }

    public void RegisterWeaponCatalogEntry(
        string conceptKey,
        string displayName,
        string itemType,
        string? weaponCategory,
        IEnumerable<string> properties,
        CharacterMechanicProvenanceView provenance)
    {
        if (string.IsNullOrWhiteSpace(conceptKey)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(itemType))
        {
            return;
        }

        var normalizedType = itemType.Trim().ToUpperInvariant();
        if (normalizedType is not "M" and not "R")
        {
            return;
        }

        WeaponCatalog[conceptKey.Trim()] = new CharacterWeaponCatalogEntry(
            conceptKey.Trim(),
            displayName.Trim(),
            normalizedType,
            string.IsNullOrWhiteSpace(weaponCategory) ? null : weaponCategory.Trim(),
            new HashSet<string>(
                properties
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => Normalize(value)),
                Keys),
            provenance);
    }

    public bool TryMatchWeaponFilter(
        string filter,
        out IReadOnlyList<CharacterWeaponCatalogEntry> matches)
    {
        matches = [];
        if (string.IsNullOrWhiteSpace(filter))
        {
            return false;
        }

        var predicates = new List<Func<CharacterWeaponCatalogEntry, bool>>();
        foreach (var rawClause in filter.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = rawClause.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            var key = Normalize(parts[0]);
            var values = parts[1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Normalize)
                .Where(value => value.Length > 0)
                .ToArray();
            if (values.Length == 0)
            {
                return false;
            }

            switch (key)
            {
                case "type":
                    predicates.Add(entry => values.Any(value =>
                        value switch
                        {
                            "martial weapon" => string.Equals(
                                entry.WeaponCategory,
                                "martial",
                                StringComparison.OrdinalIgnoreCase),
                            "simple weapon" => string.Equals(
                                entry.WeaponCategory,
                                "simple",
                                StringComparison.OrdinalIgnoreCase),
                            "melee weapon" => string.Equals(
                                entry.ItemType,
                                "M",
                                StringComparison.OrdinalIgnoreCase),
                            "ranged weapon" => string.Equals(
                                entry.ItemType,
                                "R",
                                StringComparison.OrdinalIgnoreCase),
                            _ => false
                        }));
                    if (values.Any(value => value is not
                            ("martial weapon" or "simple weapon" or "melee weapon" or "ranged weapon")))
                    {
                        return false;
                    }
                    break;

                case "property":
                    predicates.Add(entry => values.Any(value => entry.Properties.Contains(value)));
                    break;

                default:
                    return false;
            }
        }

        matches = WeaponCatalog.Values
            .Where(entry => predicates.All(predicate => predicate(entry)))
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.ConceptKey, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    public void RegisterLanguageChoiceIdentity(
        string conceptKey,
        string displayName,
        string? categoryKey)
    {
        if (string.IsNullOrWhiteSpace(conceptKey)
            || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var normalizedConceptKey = conceptKey.Trim();
        LanguageChoiceOptionsByConceptKey[normalizedConceptKey] =
            new CharacterChoiceOptionView(
                normalizedConceptKey,
                displayName.Trim(),
                normalizedConceptKey);

        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return;
        }

        var normalizedCategory = Normalize(categoryKey);
        if (!LanguageChoiceConceptKeysByCategory.TryGetValue(
                normalizedCategory,
                out var conceptKeys))
        {
            conceptKeys = new HashSet<string>(Keys);
            LanguageChoiceConceptKeysByCategory[normalizedCategory] = conceptKeys;
        }
        conceptKeys.Add(normalizedConceptKey);
    }

    public CharacterChoiceOptionView ResolveSkillChoiceOption(string sourceValue)
    {
        var normalized = Normalize(sourceValue);
        if (CompetencyChoiceOptionsByName.TryGetValue(normalized, out var direct)
            && direct.ConceptKey is not null
            && SkillChoiceOptionsByConceptKey.ContainsKey(direct.ConceptKey))
        {
            return direct;
        }

        var normalizedName = NormalizeName(normalized);
        var byName = SkillChoiceOptionsByConceptKey.Values
            .FirstOrDefault(option =>
                string.Equals(
                    NormalizeName(option.DisplayName),
                    normalizedName,
                    StringComparison.Ordinal)
                || string.Equals(
                    NormalizeName(option.Value.Split('.').LastOrDefault() ?? option.Value),
                    normalizedName,
                    StringComparison.Ordinal));
        return byName
            ?? new CharacterChoiceOptionView(
                normalized,
                CharacterProjectionJson.Humanize(normalized),
                null);
    }

    public IReadOnlyList<CharacterChoiceOptionView> AllSkillChoiceOptions() =>
        SkillChoiceOptionsByConceptKey.Values
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

    public CharacterChoiceOptionView ResolveToolChoiceOption(string sourceValue)
    {
        var normalized = Normalize(sourceValue);
        if (CompetencyChoiceOptionsByName.TryGetValue(normalized, out var direct)
            && direct.ConceptKey is not null
            && ToolChoiceOptionsByConceptKey.ContainsKey(direct.ConceptKey))
        {
            return direct;
        }

        var normalizedName = NormalizeName(normalized);
        var byName = ToolChoiceOptionsByConceptKey.Values
            .FirstOrDefault(option =>
                string.Equals(
                    NormalizeName(option.DisplayName),
                    normalizedName,
                    StringComparison.Ordinal)
                || string.Equals(
                    NormalizeName(option.Value.Split('.').LastOrDefault() ?? option.Value),
                    normalizedName,
                    StringComparison.Ordinal));
        return byName
            ?? new CharacterChoiceOptionView(
                normalized,
                CharacterProjectionJson.Humanize(normalized),
                null);
    }

    public IReadOnlyList<CharacterChoiceOptionView> AllToolChoiceOptions() =>
        ToolChoiceOptionsByConceptKey.Values
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<CharacterChoiceOptionView> ToolChoiceOptionsForCategory(
        string categoryKey)
    {
        var normalizedCategory = Normalize(categoryKey);
        if (!ToolChoiceConceptKeysByCategory.TryGetValue(
                normalizedCategory,
                out var conceptKeys))
        {
            return [];
        }

        return conceptKeys
            .Select(value => ToolChoiceOptionsByConceptKey.GetValueOrDefault(value))
            .Where(value => value is not null)
            .Cast<CharacterChoiceOptionView>()
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public CharacterChoiceOptionView ResolveLanguageChoiceOption(string sourceValue)
    {
        var normalized = Normalize(sourceValue);
        if (LanguageChoiceOptionsByConceptKey.TryGetValue(normalized, out var direct))
        {
            return direct;
        }

        var normalizedName = NormalizeName(normalized);
        var byName = LanguageChoiceOptionsByConceptKey.Values
            .FirstOrDefault(option =>
                string.Equals(
                    NormalizeName(option.DisplayName),
                    normalizedName,
                    StringComparison.Ordinal)
                || string.Equals(
                    NormalizeName(option.Value.Split('.').LastOrDefault() ?? option.Value),
                    normalizedName,
                    StringComparison.Ordinal));
        return byName
            ?? new CharacterChoiceOptionView(
                normalized,
                CharacterProjectionJson.Humanize(normalized),
                null);
    }

    public IReadOnlyList<CharacterChoiceOptionView> AllLanguageChoiceOptions() =>
        LanguageChoiceOptionsByConceptKey.Values
            .Where(value => !KnownLanguageChoiceIdentities.Contains(
                value.ConceptKey ?? value.Value))
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<CharacterChoiceOptionView> LanguageChoiceOptionsForCategory(
        string categoryKey)
    {
        var normalizedCategory = Normalize(categoryKey);
        if (!LanguageChoiceConceptKeysByCategory.TryGetValue(
                normalizedCategory,
                out var conceptKeys))
        {
            return [];
        }

        return conceptKeys
            .Select(value => LanguageChoiceOptionsByConceptKey.GetValueOrDefault(value))
            .Where(value => value is not null)
            .Cast<CharacterChoiceOptionView>()
            .Where(value => !KnownLanguageChoiceIdentities.Contains(
                value.ConceptKey ?? value.Value))
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public void AddLanguageKnowledge(
        CharacterChoiceOptionView option,
        string sourceConceptKey,
        CharacterMechanicProvenanceView provenance)
    {
        KnownLanguageChoiceIdentities.Add(option.ConceptKey ?? option.Value);
        var qualificationKey = $"qualification.languages.{Slug(option.DisplayName)}";
        Qualifications[qualificationKey] = new CharacterQualificationView(
            qualificationKey,
            "languages",
            option.DisplayName,
            true,
            CharacterResolutionStates.Resolved,
            [sourceConceptKey],
            provenance);
        AddCapability(
            qualificationKey,
            option.DisplayName,
            sourceConceptKey,
            provenance);
    }

    public void AddSkillTraining(
        CharacterChoiceOptionView option,
        string sourceConceptKey,
        CharacterMechanicProvenanceView provenance)
    {
        var trainingKey = option.ConceptKey ?? option.Value;
        TrainingKeys.Add(trainingKey);

        var qualificationKey = $"qualification.skills.{Slug(option.DisplayName)}";
        Qualifications[qualificationKey] = new CharacterQualificationView(
            qualificationKey,
            "skills",
            option.DisplayName,
            true,
            CharacterResolutionStates.Resolved,
            [sourceConceptKey],
            provenance);
        AddCapability(
            qualificationKey,
            option.DisplayName,
            sourceConceptKey,
            provenance);
    }

    public void AddToolTraining(
        CharacterChoiceOptionView option,
        string sourceConceptKey,
        CharacterMechanicProvenanceView provenance)
    {
        var trainingKey = option.ConceptKey ?? option.Value;
        TrainingKeys.Add(trainingKey);

        var qualificationKey = $"qualification.tools.{Slug(option.DisplayName)}";
        Qualifications[qualificationKey] = new CharacterQualificationView(
            qualificationKey,
            "tools",
            option.DisplayName,
            true,
            CharacterResolutionStates.Resolved,
            [sourceConceptKey],
            provenance);
        AddCapability(
            qualificationKey,
            option.DisplayName,
            sourceConceptKey,
            provenance);
    }

    public bool TryFindAdvancementLevel(string targetName, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        var normalizedTarget = NormalizeName(targetName);
        var found = false;
        foreach (var advancement in Request.Advancements ?? [])
        {
            var conceptKey = advancement.ConceptKey?.Trim();
            if (string.IsNullOrWhiteSpace(conceptKey))
            {
                continue;
            }

            var displayMatch = RuleDisplayNames.TryGetValue(conceptKey, out var displayName)
                && string.Equals(
                    NormalizeName(displayName),
                    normalizedTarget,
                    StringComparison.Ordinal);
            var conceptTail = conceptKey.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();
            var keyMatch = !string.IsNullOrWhiteSpace(conceptTail)
                && string.Equals(
                    NormalizeName(conceptTail),
                    normalizedTarget,
                    StringComparison.Ordinal);
            if (!displayMatch && !keyMatch)
            {
                continue;
            }

            found = true;
            level = checked(level + Math.Max(advancement.Level, 0));
        }
        return found;
    }

    public bool TryFindCompetencyRanks(
        string targetName,
        out int ranks,
        out string? conceptKey)
    {
        ranks = 0;
        conceptKey = null;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        if (CompetencyRanks.TryGetValue(targetName.Trim(), out ranks))
        {
            conceptKey = targetName.Trim();
            return true;
        }

        if (!CompetencyConceptKeysByDisplayName.TryGetValue(targetName.Trim(), out var resolvedConceptKey))
        {
            return false;
        }

        conceptKey = resolvedConceptKey;
        return CompetencyRanks.TryGetValue(resolvedConceptKey, out ranks);
    }

    private static string NormalizeName(string value) =>
        string.Concat(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit));

    private static string Slug(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-', '.', '|'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public void AddSizeCategory(
        string sizeCategory,
        string sourceConceptKey,
        CharacterMechanicProvenanceView provenance)
    {
        if (string.IsNullOrWhiteSpace(sizeCategory))
        {
            return;
        }

        const string mechanicKey = "character.size-category";
        var normalized = NormalizeSizeCategory(sizeCategory);
        var contribution = new CharacterMechanicContributionView(
            $"{sourceConceptKey}.size-category",
            "Size category",
            CharacterEffectOperations.Set,
            null,
            normalized,
            sourceConceptKey,
            provenance);

        if (SizeCategory is null)
        {
            SizeCategory = normalized;
            Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
                mechanicKey,
                "character-metadata",
                "Size",
                CharacterResolutionStates.Resolved,
                null,
                normalized,
                null,
                [],
                [],
                [],
                [],
                [contribution],
                provenance);
            return;
        }

        if (string.Equals(SizeCategory, normalized, StringComparison.OrdinalIgnoreCase))
        {
            if (Mechanics.TryGetValue(mechanicKey, out var existing))
            {
                Mechanics[mechanicKey] = existing with
                {
                    Contributions = existing.Contributions
                        .Append(contribution)
                        .GroupBy(
                            value => new { value.ContributionKey, value.SourceConceptKey },
                            EqualityComparer<object>.Default)
                        .Select(group => group.First())
                        .ToArray()
                };
            }
            return;
        }

        var existingContributions = Mechanics.TryGetValue(mechanicKey, out var current)
            ? current.Contributions
            : [];
        var contributions = existingContributions.Append(contribution).ToArray();
        Mechanics[mechanicKey] = new CharacterResolvedMechanicView(
            mechanicKey,
            "character-metadata",
            "Size",
            CharacterResolutionStates.Conflict,
            null,
            null,
            null,
            [],
            [],
            [],
            [],
            contributions,
            EmptyProvenance());

        Conflicts.Add(new CharacterProjectionConflictView(
            "conflict.character-size",
            CharacterResolutionStates.Conflict,
            $"Selected Character rules provide conflicting size categories '{SizeCategory}' and '{normalized}'.",
            [mechanicKey, "combat.grapple"],
            contributions
                .Select(value => value.SourceConceptKey)
                .Where(value => value is not null)
                .Cast<string>()
                .Distinct(Keys)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()));
    }

    private static string NormalizeSizeCategory(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "F" or "FINE" => "Fine",
            "D" or "DIMINUTIVE" => "Diminutive",
            "T" or "TINY" => "Tiny",
            "S" or "SMALL" => "Small",
            "M" or "MEDIUM" => "Medium",
            "L" or "LARGE" => "Large",
            "H" or "HUGE" => "Huge",
            "G" or "GARGANTUAN" => "Gargantuan",
            "C" or "COLOSSAL" => "Colossal",
            _ => value.Trim()
        };

    public void AddThreeXSaveContribution(
        string saveKey,
        CharacterMechanicContributionView contribution)
    {
        var normalized = Normalize(saveKey);
        if (!ThreeXSaveContributions.TryGetValue(normalized, out var values))
        {
            values = [];
            ThreeXSaveContributions.Add(normalized, values);
        }
        values.Add(contribution);
    }

    public void AddClassSkill(
        string value,
        string sourceConceptKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        HasDerivedClassSkillData = true;
        var normalized = value.Trim();
        if (normalized.StartsWith("TYPE.", StringComparison.OrdinalIgnoreCase))
        {
            DerivedClassSkillTypes.Add(normalized["TYPE.".Length..]);
            return;
        }

        if (!DerivedClassSkillGrantSources.TryGetValue(normalized, out var sources))
        {
            sources = new HashSet<string>(Keys);
            DerivedClassSkillGrantSources.Add(normalized, sources);
        }
        sources.Add(sourceConceptKey);
    }

    public IReadOnlyList<string> FindClassSkillGrantSources(
        string displayName,
        string? familyName)
    {
        var result = new HashSet<string>(Keys);
        if (DerivedClassSkillGrantSources.TryGetValue(displayName, out var direct))
        {
            result.UnionWith(direct);
        }
        if (!string.IsNullOrWhiteSpace(familyName)
            && DerivedClassSkillTypes.Contains(familyName))
        {
            foreach (var sources in DerivedClassSkillGrantSources.Values)
            {
                result.UnionWith(sources);
            }
        }
        return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public void RequireAbilityChoice(string abilityKey, string choiceKey)
    {
        var ability = CharacterProjectionJson.NormalizeAbilityKey(abilityKey);
        if (!RequiredAbilityChoices.TryGetValue(ability, out var choices))
        {
            choices = new HashSet<string>(Keys);
            RequiredAbilityChoices.Add(ability, choices);
        }
        choices.Add(choiceKey);
    }

    public IReadOnlyList<string> RequiredAbilityChoicesFor(string abilityKey) =>
        RequiredAbilityChoices.TryGetValue(
            CharacterProjectionJson.NormalizeAbilityKey(abilityKey),
            out var choices)
            ? choices.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            : [];

    public void AddAbilityContribution(
        string abilityKey,
        CharacterMechanicContributionView contribution)
    {
        var key = CharacterProjectionJson.NormalizeAbilityKey(abilityKey);
        if (!AbilityContributions.TryGetValue(key, out var values))
        {
            values = [];
            AbilityContributions.Add(key, values);
        }
        values.Add(contribution);
    }

    public void AddCapability(
        string key,
        string displayName,
        string sourceConceptKey,
        CharacterMechanicProvenanceView provenance)
    {
        var normalized = Normalize(key);
        Capabilities.Add(normalized);
        if (CapabilityViews.TryGetValue(normalized, out var existing))
        {
            var grants = existing.GrantedByConceptKeys
                .Append(sourceConceptKey)
                .Distinct(Keys)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CapabilityViews[normalized] = existing with { GrantedByConceptKeys = grants };
            return;
        }

        CapabilityViews[normalized] = new CharacterCapabilityView(
            normalized,
            string.IsNullOrWhiteSpace(displayName) ? normalized : displayName.Trim(),
            [sourceConceptKey],
            provenance);
    }

    public void AddFeature(
        string featureKey,
        string displayName,
        string kind,
        string state,
        string? sourceConceptKey,
        CharacterMechanicProvenanceView provenance,
        string? grantingSourceKind = null,
        int? acquisitionLevel = null,
        string? occurrenceKey = null,
        CharacterFeatureCatalogEntry? featureDefinition = null)
    {
        var normalized = Normalize(featureKey);
        Features[normalized] = new CharacterFeatureView(
            normalized,
            displayName,
            kind,
            state,
            sourceConceptKey,
            featureDefinition?.Effects ?? [],
            provenance,
            occurrenceKey ?? normalized,
            grantingSourceKind ?? kind,
            acquisitionLevel,
            featureDefinition?.ConceptKey,
            featureDefinition?.EntityType,
            featureDefinition?.Provenance);
    }

    public CharacterFeatureCatalogEntry? ResolveFeatureReference(
        string reference,
        bool isSubclass)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var parts = reference.Split('|');
        var levelIndex = isSubclass ? 5 : 3;
        if (parts.Length <= levelIndex
            || !int.TryParse(parts[levelIndex], out var level)
            || level <= 0)
        {
            return null;
        }

        var name = parts[0].Trim();
        var className = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        var classSource = parts.Length > 2 ? parts[2].Trim() : string.Empty;
        var subclassName = isSubclass && parts.Length > 3 ? parts[3].Trim() : null;
        var subclassSource = isSubclass && parts.Length > 4 ? parts[4].Trim() : null;
        var featureSourceIndex = isSubclass ? 6 : 4;
        var featureSource = parts.Length > featureSourceIndex
            && !string.IsNullOrWhiteSpace(parts[featureSourceIndex])
                ? parts[featureSourceIndex].Trim()
                : null;

        var matches = FeatureCatalog
            .Where(value =>
                string.Equals(value.DisplayName, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(value.ClassName, className, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    value.ClassSource ?? string.Empty,
                    classSource,
                    StringComparison.OrdinalIgnoreCase)
                && value.AcquisitionLevel == level
                && (!isSubclass
                    || (string.Equals(
                            value.SubclassName,
                            subclassName,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            value.SubclassSource ?? string.Empty,
                            subclassSource ?? string.Empty,
                            StringComparison.OrdinalIgnoreCase)))
                && (featureSource is null
                    || string.Equals(
                        value.FeatureSource,
                        featureSource,
                        StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    public void AddEffect(CharacterRuleEffectView effect)
    {
        Effects.Add(effect);

        var conditionSatisfied =
            string.IsNullOrWhiteSpace(effect.ConditionKey)
            || ActiveConditions.Contains(effect.ConditionKey)
            || (BooleanFacts.TryGetValue(effect.ConditionKey, out var conditionState)
                && conditionState);
        if (!conditionSatisfied)
        {
            return;
        }

        if (string.Equals(effect.Kind, CharacterEffectKinds.Capability, StringComparison.OrdinalIgnoreCase)
            && string.Equals(effect.Operation, CharacterEffectOperations.Grant, StringComparison.OrdinalIgnoreCase)
            && effect.SourceConceptKey is not null)
        {
            AddCapability(
                effect.TargetKey,
                CharacterProjectionJson.Humanize(effect.TargetKey),
                effect.SourceConceptKey,
                effect.Provenance);
        }
        if (string.Equals(effect.Kind, CharacterEffectKinds.MechanicContribution, StringComparison.OrdinalIgnoreCase)
            && string.Equals(effect.Operation, CharacterEffectOperations.Add, StringComparison.OrdinalIgnoreCase)
            && effect.NumericValue is int value
            && effect.TargetKey.StartsWith("ability.", StringComparison.OrdinalIgnoreCase)
            && effect.TargetKey.EndsWith(".score", StringComparison.OrdinalIgnoreCase))
        {
            var ability = effect.TargetKey["ability.".Length..^".score".Length];
            var temporary =
                !string.IsNullOrWhiteSpace(effect.ConditionKey)
                || (effect.SourceConceptKey is not null
                    && ActiveConditions.Contains(effect.SourceConceptKey));
            AddAbilityContribution(
                ability,
                new CharacterMechanicContributionView(
                    effect.EffectKey,
                    CharacterProjectionJson.Humanize(effect.EffectKey),
                    effect.Operation,
                    value,
                    null,
                    effect.SourceConceptKey,
                    effect.Provenance,
                    temporary ? "temporary" : "persistent",
                    effect.ConditionKey));
        }
    }

    public bool ShouldIncludeMechanic(string key) =>
        RequestedMechanics is null || RequestedMechanics.Contains(key);

    public static CharacterMechanicProvenanceView EmptyProvenance() =>
        new([], [], []);

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Character fact keys can not be blank.");
        }
        return value.Trim();
    }

    private static Dictionary<string, int> NormalizeIntegerDictionary(
        IReadOnlyDictionary<string, int>? values) =>
        values?.ToDictionary(
            pair => Normalize(pair.Key),
            pair => pair.Value,
            Keys)
        ?? new Dictionary<string, int>(Keys);

    private static Dictionary<string, bool> NormalizeBooleanDictionary(
        IReadOnlyDictionary<string, bool>? values) =>
        values?.ToDictionary(
            pair => Normalize(pair.Key),
            pair => pair.Value,
            Keys)
        ?? new Dictionary<string, bool>(Keys);

    private static Dictionary<string, string> NormalizeStringDictionary(
        IReadOnlyDictionary<string, string>? values) =>
        values?.ToDictionary(
            pair => Normalize(pair.Key),
            pair => pair.Value?.Trim() ?? string.Empty,
            Keys)
        ?? new Dictionary<string, string>(Keys);
}

internal static class CharacterProjectionJson
{
    private static readonly Dictionary<string, string> AbilityKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["str"] = "strength",
            ["strength"] = "strength",
            ["dex"] = "dexterity",
            ["dexterity"] = "dexterity",
            ["con"] = "constitution",
            ["constitution"] = "constitution",
            ["int"] = "intelligence",
            ["intelligence"] = "intelligence",
            ["wis"] = "wisdom",
            ["wisdom"] = "wisdom",
            ["cha"] = "charisma",
            ["charisma"] = "charisma"
        };

    public static string NormalizeAbilityKey(string value) =>
        AbilityKeys.TryGetValue(value?.Trim() ?? string.Empty, out var key)
            ? key
            : (value?.Trim().ToLowerInvariant()
                ?? throw new ArgumentNullException(nameof(value)));

    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out value);
    }

    public static string? String(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    public static bool? Boolean(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }
        if (value.ValueKind == JsonValueKind.String
            && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }
        return null;
    }

    public static string NormalizeResourceSystemKey(string value) =>
        string.Join(
            '-',
            value.Trim().ToLowerInvariant()
                .Split(
                    [' ', '/', '_', '-'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static int? Integer(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }
        return value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out number)
                ? number
                : null;
    }

    public static decimal? Decimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }
        return value.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out number)
                ? number
                : null;
    }

    public static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return [];
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var item = value.GetString();
            return string.IsNullOrWhiteSpace(item) ? [] : [item.Trim()];
        }
        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToArray();
    }

    public static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var tail = value.Trim().Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? value.Trim();
        return string.Join(
            ' ',
            tail.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    public static string? RangeText(JsonElement document)
    {
        if (!TryGetProperty(document, "range", out var range))
        {
            return null;
        }
        if (range.ValueKind == JsonValueKind.String)
        {
            return range.GetString();
        }
        if (range.ValueKind != JsonValueKind.Object)
        {
            return range.ToString();
        }

        if (TryGetProperty(range, "distance", out var distance)
            && distance.ValueKind == JsonValueKind.Object)
        {
            var type = String(distance, "type");
            var amount = Integer(distance, "amount");
            if (amount is not null)
            {
                return $"{amount} {type ?? "units"}";
            }
            if (!string.IsNullOrWhiteSpace(type))
            {
                return type;
            }
        }

        return String(range, "type") ?? range.ToString();
    }
}
