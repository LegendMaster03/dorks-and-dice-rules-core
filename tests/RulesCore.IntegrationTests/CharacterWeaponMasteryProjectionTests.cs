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

        Assert.Equal(3, (int?)Invoke("ReadTableMasteryCount", document.RootElement, 1));
        Assert.Equal(4, (int?)Invoke("ReadTableMasteryCount", document.RootElement, 4));
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

        Assert.False((bool)Invoke("HasActiveWeaponMasteryFeature", document.RootElement, 1)!);
        Assert.True((bool)Invoke("HasActiveWeaponMasteryFeature", document.RootElement, 2)!);
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
        Assert.Equal(expected, (int?)Invoke("FixedMasteryCount", className));
    }

    private static object? Invoke(string methodName, params object?[] arguments)
    {
        var projector = typeof(CharacterRulesProjectionService).Assembly.GetType(
            "RulesCore.Infrastructure.Rules.CharacterProjection.CharacterWeaponMasteryProjector",
            throwOnError: true)!;
        var method = projector.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(null, arguments);
    }
}
