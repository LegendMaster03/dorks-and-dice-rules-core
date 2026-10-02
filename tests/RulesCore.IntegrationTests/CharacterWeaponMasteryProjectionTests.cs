using System.Reflection;
using System.Text.Json;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.IntegrationTests;

public sealed class CharacterWeaponMasteryProjectionTests
{
    [Fact]
    public void TableDrivenMasteryCountUsesTheActiveClassLevelRow()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "classTableGroups": [
                {
                  "colLabels": ["Proficiency Bonus", "Weapon Mastery"],
                  "rows": [
                    [2, 3],
                    [2, 3],
                    [2, 3],
                    [2, { "value": 4 }]
                  ]
                }
              ]
            }
            """);

        Assert.Equal(3, Invoke<int?>("ReadTableMasteryCount", document.RootElement, 1));
        Assert.Equal(4, Invoke<int?>("ReadTableMasteryCount", document.RootElement, 4));
    }

    [Fact]
    public void WeaponMasteryFeatureReferenceHonorsItsAcquisitionLevel()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "classFeatures": [
                "Second Wind|Fighter|XPHB|1",
                "Weapon Mastery|Fighter|XPHB|2"
              ]
            }
            """);

        Assert.False(Invoke<bool>("HasActiveWeaponMasteryFeature", document.RootElement, 1));
        Assert.True(Invoke<bool>("HasActiveWeaponMasteryFeature", document.RootElement, 2));
    }

    [Theory]
    [InlineData("Paladin", 2)]
    [InlineData("Ranger", 2)]
    [InlineData("Rogue", 2)]
    [InlineData("Wizard", null)]
    public void FixedCountMasteryClassesUseReviewedSourceSemantics(
        string className,
        int? expected)
    {
        Assert.Equal(expected, Invoke<int?>("FixedMasteryCount", className));
    }

    private static T Invoke<T>(string methodName, params object?[] arguments)
    {
        var projector = typeof(CharacterRulesProjectionService).Assembly.GetType(
            "RulesCore.Infrastructure.Rules.CharacterProjection.CharacterWeaponMasteryProjector",
            throwOnError: true)!;
        var method = projector.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<T>(method!.Invoke(null, arguments));
    }
}
