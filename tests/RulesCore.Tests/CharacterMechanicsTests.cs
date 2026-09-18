using RulesCore.Domain.Rules;

namespace RulesCore.Tests;

public sealed class CharacterMechanicsTests
{
    [Fact]
    public void KnownCatalogSeparatesGenericCapabilityAndSourceApplicability()
    {
        var generic = Required("check.competency");
        Assert.Equal(CharacterMechanicApplicabilityKinds.Always, generic.Applicability.Kind);
        Assert.NotNull(generic.Check);
        Assert.Equal(
            CharacterCheckAbilityResolutionKinds.CallerSelected,
            generic.Check!.Ability.ResolutionKind);
        Assert.Equal(
            CharacterCheckCompetencyResolutionKinds.CallerSelected,
            generic.Check.Competency.ResolutionKind);
        Assert.Contains(CharacterCompetencyKinds.Skill, generic.Check.Competency.AllowedCompetencyKinds);
        Assert.Contains(CharacterCompetencyKinds.SpecializedSkill, generic.Check.Competency.AllowedCompetencyKinds);
        Assert.Contains(CharacterCompetencyKinds.Tool, generic.Check.Competency.AllowedCompetencyKinds);
        Assert.NotNull(generic.Check.CompetencyComposition);
        Assert.Equal("competencyKey", generic.Check.CompetencyComposition!.ConceptKeyInputKey);
        Assert.Equal(
            "competencyContribution",
            generic.Check.CompetencyComposition.ContributionInputKey);

        var fortitude = Required("save.fortitude");
        Assert.Equal(CharacterMechanicApplicabilityKinds.CharacterCapability, fortitude.Applicability.Kind);
        Assert.Equal(new[] { "save.fortitude" }, fortitude.Applicability.RequiredCapabilityKeys);

        var harvesting = Required("check.harvesting.total");
        Assert.Equal(CharacterMechanicApplicabilityKinds.ExternalPublicRules, harvesting.Applicability.Kind);
        Assert.Null(harvesting.Applicability.SourcePackageKey);
        Assert.NotNull(harvesting.Source);
        Assert.Null(harvesting.Source!.PackageKey);
        Assert.Null(harvesting.Source.PackageDisplayName);
        Assert.True(harvesting.Source.PresentationRequired);
        Assert.True(harvesting.Source.ReferenceLinkRequired);
        Assert.Equal(KnownCharacterMechanics.LootTavernReferenceKey, harvesting.Source.WorkKey);
        Assert.Equal("Harvesting & Crafting Lite", harvesting.Source.WorkDisplayName);
        Assert.Equal("5e", harvesting.Source.GameEdition);
        Assert.Equal("public-release", harvesting.Source.ReleaseKind);
        Assert.Equal(new DateOnly(2024, 7, 3), harvesting.Source.PublicationDate);
        Assert.Equal(
            "https://www.patreon.com/LootTavern/posts/helianas-and-to-107406117",
            harvesting.Source.ReferenceUri);
    }

    [Fact]
    public void GenericCompetencyCheckUsesResolvedInputsWithoutOwningCharacterState()
    {
        var definition = Required("check.competency");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 12,
                ["abilityModifier"] = 3,
                ["competencyContribution"] = 2,
                ["targetDc"] = 17
            },
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["abilityKey"] = "intelligence",
                ["competencyKey"] = "skill.arcana"
            });

        Assert.Equal(17, result.Value);
        Assert.Equal(17, result.Target);
        Assert.True(result.MeetsTarget);
        Assert.True(result.RequirementsSatisfied);
    }

    [Fact]
    public void HarvestingCombinesComponentResultsAndReportsSameActorRollRule()
    {
        var definition = Required("check.harvesting.total");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["assessmentResult"] = 14,
                ["carvingResult"] = 12,
                ["targetDc"] = 25
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["sameActor"] = true
            },
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal(26, result.Value);
        Assert.True(result.MeetsTarget);
        var rollRule = Assert.Single(result.AppliedRollRules);
        Assert.Equal(CharacterMechanicRollModes.Disadvantage, rollRule.RollMode);
        Assert.Equal(
            new[] { "check.harvesting.assessment", "check.harvesting.carving" },
            rollRule.TargetMechanicKeys);
    }

    [Fact]
    public void HarvestingHelpersContributeFullOrHalfProficiencyToTheTotal()
    {
        var definition = Required("check.harvesting.total");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["assessmentResult"] = 14,
                ["carvingResult"] = 12
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["sameActor"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["creatureSize"] = "Large"
            },
            new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["helpers"] =
                [
                    Helper(proficiencyBonus: 4, isProficient: true),
                    Helper(proficiencyBonus: 5, isProficient: false)
                ]
            });

        Assert.Equal(32, result.Value);
        var helpers = Assert.Single(result.ContributorGroups);
        Assert.Equal("helpers", helpers.Key);
        Assert.Equal(2, helpers.ContributorCount);
        Assert.Equal(4, helpers.MaximumContributorCount);
        Assert.Equal(6, helpers.Value);
    }

    [Fact]
    public void HarvestingWithNoHelpersDoesNotInventAHelpActionBonus()
    {
        var definition = Required("check.harvesting.total");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["assessmentResult"] = 10,
                ["carvingResult"] = 11
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["sameActor"] = false,
                ["helpAction"] = true
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["creatureSize"] = "Medium"
            });

        Assert.Equal(21, result.Value);
        var helpers = Assert.Single(result.ContributorGroups);
        Assert.Equal(0, helpers.ContributorCount);
        Assert.Equal(2, helpers.MaximumContributorCount);
        Assert.Equal(0, helpers.Value);
        Assert.Empty(result.AppliedRollRules);
    }

    [Theory]
    [InlineData("Tiny", 0)]
    [InlineData("Small", 1)]
    [InlineData("Medium", 2)]
    [InlineData("Large", 4)]
    [InlineData("Huge", 6)]
    [InlineData("Gargantuan", 10)]
    public void HarvestingHelperCapsFollowCreatureSize(string creatureSize, int expectedMaximum)
    {
        var definition = Required("check.harvesting.total");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["assessmentResult"] = 8,
                ["carvingResult"] = 9
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["sameActor"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["creatureSize"] = creatureSize
            },
            new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["helpers"] = Enumerable.Range(0, expectedMaximum)
                    .Select(_ => Helper(proficiencyBonus: 2, isProficient: true))
                    .ToArray()
            });

        var helpers = Assert.Single(result.ContributorGroups);
        Assert.Equal(expectedMaximum, helpers.MaximumContributorCount);
        Assert.Equal(expectedMaximum, helpers.ContributorCount);
        Assert.Equal(expectedMaximum * 2, helpers.Value);
    }

    [Fact]
    public void HarvestingRejectsHelpersBeyondTheCreatureSizeCap()
    {
        var definition = Required("check.harvesting.total");

        Assert.Throws<InvalidOperationException>(() =>
            CharacterMechanicEvaluator.Evaluate(
                definition,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["assessmentResult"] = 10,
                    ["carvingResult"] = 10
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["sameActor"] = false
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["creatureSize"] = "Small"
                },
                new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["helpers"] =
                    [
                        Helper(proficiencyBonus: 2, isProficient: true),
                        Helper(proficiencyBonus: 2, isProficient: false)
                    ]
                }));
    }

    [Fact]
    public void HarvestingSameActorDisadvantageRemainsOnAssessmentAndCarvingOnly()
    {
        var definition = Required("check.harvesting.total");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["assessmentResult"] = 12,
                ["carvingResult"] = 13
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["sameActor"] = true,
                ["helpAction"] = true
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["creatureSize"] = "Small"
            },
            new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["helpers"] = [Helper(proficiencyBonus: 3, isProficient: false)]
            });

        Assert.Equal(26, result.Value);
        var rollRule = Assert.Single(result.AppliedRollRules);
        Assert.Equal(
            new[] { "check.harvesting.assessment", "check.harvesting.carving" },
            rollRule.TargetMechanicKeys);
        Assert.Equal(1, Assert.Single(result.ContributorGroups).Value);
    }

    [Fact]
    public void HarvestingRejectsHelperWhoDidNotParticipateForEntireDuration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CharacterMechanicEvaluator.Evaluate(
                Required("check.harvesting.total"),
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["assessmentResult"] = 10,
                    ["carvingResult"] = 10
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["sameActor"] = false
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["creatureSize"] = "Medium"
                },
                new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["helpers"] =
                    [
                        Helper(
                            proficiencyBonus: 3,
                            isProficient: true,
                            participatedForEntireDuration: false)
                    ]
                }));

        Assert.Contains("participatedForEntireDuration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HarvestingRejectsAssessmentParticipantSubmittedAsHelper()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CharacterMechanicEvaluator.Evaluate(
                Required("check.harvesting.total"),
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["assessmentResult"] = 10,
                    ["carvingResult"] = 10
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["sameActor"] = false
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["creatureSize"] = "Medium"
                },
                new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["helpers"] =
                    [
                        Helper(
                            proficiencyBonus: 3,
                            isProficient: true,
                            isAssessmentParticipant: true)
                    ]
                }));

        Assert.Contains("isAssessmentParticipant", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HarvestingRejectsCarvingParticipantSubmittedAsHelper()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CharacterMechanicEvaluator.Evaluate(
                Required("check.harvesting.total"),
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["assessmentResult"] = 10,
                    ["carvingResult"] = 10
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["sameActor"] = false
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["creatureSize"] = "Medium"
                },
                new Dictionary<string, IReadOnlyList<CharacterMechanicContributorInputValues>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["helpers"] =
                    [
                        Helper(
                            proficiencyBonus: 3,
                            isProficient: false,
                            isCarvingParticipant: true)
                    ]
                }));

        Assert.Contains("isCarvingParticipant", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManufacturingDoesNotTurnMissingToolProficiencyIntoMissingProficiencyData()
    {
        var definition = Required("check.crafting.manufacturing");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 11,
                ["abilityModifier"] = 2
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasToolProficiency"] = false,
                ["hasQualifiedGuidance"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["toolKey"] = "tool.example",
                ["abilityKey"] = "dexterity"
            });

        Assert.Equal(13, result.Value);
        Assert.True(result.RequirementsSatisfied);
        var rollRule = Assert.Single(result.AppliedRollRules);
        Assert.Equal(CharacterMechanicRollModes.Disadvantage, rollRule.RollMode);
    }

    [Fact]
    public void EnchantingReportsSpellcastingRequirementSeparatelyFromCheckArithmetic()
    {
        var definition = Required("check.crafting.enchanting");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 10,
                ["spellcastingAbilityModifier"] = 4,
                ["competencyContribution"] = 3
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasSpellcastingAbility"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["creatureTypeCompetencyKey"] = "skill.example"
            });

        Assert.Equal(17, result.Value);
        Assert.False(result.RequirementsSatisfied);
        Assert.Equal(new[] { "hasSpellcastingAbility" }, result.UnsatisfiedRequirementKeys);
    }




    [Fact]
    public void HarvestingAndCraftingExposeSourceOwnedResolutionModes()
    {
        var assessment = Required("check.harvesting.assessment");
        Assert.NotNull(assessment.Check);
        Assert.Equal(
            CharacterCheckAbilityResolutionKinds.Fixed,
            assessment.Check!.Ability.ResolutionKind);
        Assert.Equal("intelligence", assessment.Check.Ability.FixedAbilityKey);
        Assert.Equal(
            CharacterCheckCompetencyResolutionKinds.RuleResolved,
            assessment.Check.Competency.ResolutionKind);
        Assert.NotNull(assessment.Check.CompetencyComposition);
        Assert.Equal(
            "competencyContribution",
            assessment.Check.CompetencyComposition!.ContributionInputKey);

        var carving = Required("check.harvesting.carving");
        Assert.NotNull(carving.Check);
        Assert.Equal(
            CharacterCheckAbilityResolutionKinds.Fixed,
            carving.Check!.Ability.ResolutionKind);
        Assert.Equal("dexterity", carving.Check.Ability.FixedAbilityKey);
        Assert.Equal(
            CharacterCheckCompetencyResolutionKinds.RuleResolved,
            carving.Check.Competency.ResolutionKind);
        Assert.NotNull(carving.Check.CompetencyComposition);
        Assert.Equal(
            "competencyContribution",
            carving.Check.CompetencyComposition!.ContributionInputKey);
        Assert.DoesNotContain(
            carving.Inputs,
            value => value.Key == "carvingAbilitySource");
        Assert.Contains(
            carving.Inputs,
            value => value.Key == "dexterityModifier");

        var manufacturing = Required("check.crafting.manufacturing");
        Assert.NotNull(manufacturing.Check);
        Assert.Equal(
            CharacterCheckAbilityResolutionKinds.RuleResolved,
            manufacturing.Check!.Ability.ResolutionKind);
        Assert.Equal(
            new[] { CharacterCompetencyKinds.Tool },
            manufacturing.Check.Competency.AllowedCompetencyKinds);
        Assert.NotNull(manufacturing.Check.CompetencyComposition);
        Assert.Equal(
            "toolProficiencyContribution",
            manufacturing.Check.CompetencyComposition!.ContributionInputKey);

        var enchanting = Required("check.crafting.enchanting");
        Assert.NotNull(enchanting.Check);
        Assert.Equal(
            CharacterCheckAbilityResolutionKinds.CharacterResolved,
            enchanting.Check!.Ability.ResolutionKind);
        Assert.Equal(
            CharacterCheckCompetencyResolutionKinds.RuleResolved,
            enchanting.Check.Competency.ResolutionKind);
        Assert.NotNull(enchanting.Check.CompetencyComposition);
        Assert.Equal(
            "competencyContribution",
            enchanting.Check.CompetencyComposition!.ContributionInputKey);
    }

    [Fact]
    public void ManufacturingOnlyRequiresToolProficiencyContributionWhenProficient()
    {
        var definition = Required("check.crafting.manufacturing");

        var unproficient = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 8,
                ["abilityModifier"] = 3
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasToolProficiency"] = false,
                ["hasQualifiedGuidance"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["toolKey"] = "tool.example",
                ["abilityKey"] = "intelligence"
            });
        Assert.Equal(11, unproficient.Value);
        Assert.Single(unproficient.AppliedRollRules);

        var proficient = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 8,
                ["abilityModifier"] = 3,
                ["toolProficiencyContribution"] = 2
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasToolProficiency"] = true,
                ["hasQualifiedGuidance"] = false
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["toolKey"] = "tool.example",
                ["abilityKey"] = "intelligence"
            });
        Assert.Equal(13, proficient.Value);
        Assert.Empty(proficient.AppliedRollRules);
    }

    [Fact]
    public void QualifiedGuidanceRemovesManufacturingDisadvantageWithoutGrantingToolProficiency()
    {
        var definition = Required("check.crafting.manufacturing");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["d20Roll"] = 9,
                ["abilityModifier"] = 3
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasToolProficiency"] = false,
                ["hasQualifiedGuidance"] = true
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["toolKey"] = "tool.example",
                ["abilityKey"] = "intelligence"
            });

        Assert.Equal(12, result.Value);
        Assert.Empty(result.AppliedRollRules);
        Assert.DoesNotContain(
            definition.Inputs.Where(value => value.ParticipatesInValue),
            value => value.Key == "toolProficiencyContribution"
                && value.IncludeWhenBooleanValue != true);
    }

    [Fact]
    public void MechanicKindsDistinguishSavingThrowsDefensesAndCombatValues()
    {
        Assert.Equal(CharacterMechanicKinds.SavingThrow, Required("save.fortitude").Kind);
        Assert.Equal(CharacterMechanicKinds.SavingThrow, Required("save.reflex").Kind);
        Assert.Equal(CharacterMechanicKinds.SavingThrow, Required("save.will").Kind);

        Assert.Equal(CharacterMechanicKinds.Defense, Required("defense.ac.touch").Kind);
        Assert.Equal(CharacterMechanicKinds.Defense, Required("defense.spell-resistance").Kind);
        Assert.Equal(CharacterMechanicKinds.Defense, Required("defense.damage-reduction").Kind);

        Assert.Equal(
            CharacterMechanicKinds.CombatValue,
            Required("combat.base-attack-bonus").Kind);
        Assert.Equal(CharacterMechanicKinds.CombatValue, Required("combat.grapple").Kind);
    }

    [Fact]
    public void GrappleUsesTheThreeXSpecificSizeModifierInput()
    {
        var definition = Required("combat.grapple");

        Assert.Contains(
            definition.Inputs,
            value => value.Key == "grappleSizeModifier"
                && value.ValueKind == CharacterMechanicInputValueKinds.Integer);
        Assert.DoesNotContain(
            definition.Inputs,
            value => value.Key == "sizeModifier");
    }

    [Fact]
    public void SkillRanksIdentifyTheCompetencyWhoseRanksAreBeingSupplied()
    {
        var definition = Required("competency.skill-ranks");

        Assert.Contains(
            definition.Inputs,
            value => value.Key == "competencyKey"
                && value.ValueKind == CharacterMechanicInputValueKinds.String
                && value.Origin == CharacterMechanicInputOrigins.SourceInput
                && value.Required);
        Assert.Contains(
            definition.Inputs,
            value => value.Key == "value"
                && value.ValueKind == CharacterMechanicInputValueKinds.Integer
                && value.Origin == CharacterMechanicInputOrigins.CharacterState
                && value.Required);
    }

    [Fact]
    public void ThreeXTouchArmorClassUsesOnlyCallerResolvedApplicableContributions()
    {
        var definition = Required("defense.ac.touch");
        var result = CharacterMechanicEvaluator.Evaluate(
            definition,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["dexterityContribution"] = 3,
                ["sizeModifier"] = 1,
                ["deflectionBonus"] = 2,
                ["dodgeContribution"] = 1
            },
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal(17, result.Value);
    }

    private static CharacterMechanicContributorInputValues Helper(
        int proficiencyBonus,
        bool isProficient,
        bool participatedForEntireDuration = true,
        bool isAssessmentParticipant = false,
        bool isCarvingParticipant = false) =>
        new(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["proficiencyBonus"] = proficiencyBonus
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["isProficient"] = isProficient,
                ["participatedForEntireDuration"] = participatedForEntireDuration,
                ["isAssessmentParticipant"] = isAssessmentParticipant,
                ["isCarvingParticipant"] = isCarvingParticipant
            },
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static CharacterMechanicDefinition Required(string key) =>
        KnownCharacterMechanics.FindByKey(key)
        ?? throw new Xunit.Sdk.XunitException($"Known mechanic '{key}' was not registered.");
}
