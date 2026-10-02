using System.Reflection;
using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.IntegrationTests;

public sealed class CharacterSubclassAdvancementNormalizationTests
{
    [Fact]
    public void MultipleSubclassFactsBoundToSameParentAreRejectedBeforeProjection()
    {
        var classConceptKey = "class.threshold-mage";
        var subclassConceptKey = "subclass.threshold-path";
        var classRule = Rule(
            classConceptKey,
            "class",
            "Threshold Mage",
            []);
        var subclassRule = Rule(
            subclassConceptKey,
            "subclass",
            "Threshold Path",
            [new ResolvedRuleRelationshipView(
                "parent-class",
                Guid.NewGuid(),
                classConceptKey,
                "class",
                "Threshold Mage")]);
        var request = new CharacterRulesProjectionRequest(
            Advancements:
            [
                new CharacterAdvancementFactInput(
                    classConceptKey,
                    7,
                    OccurrenceKey: "class-occurrence"),
                new CharacterAdvancementFactInput(
                    subclassConceptKey,
                    7,
                    OccurrenceKey: "subclass-occurrence-a",
                    ParentConceptKey: classConceptKey,
                    ParentOccurrenceKey: "class-occurrence"),
                new CharacterAdvancementFactInput(
                    subclassConceptKey,
                    7,
                    OccurrenceKey: "subclass-occurrence-b",
                    ParentConceptKey: classConceptKey,
                    ParentOccurrenceKey: "class-occurrence")
            ]);

        var normalization = Normalize(request, [classRule, subclassRule]);
        var normalizedRequest = Property<CharacterRulesProjectionRequest>(
            normalization,
            "Request");
        var conflicts = Property<IReadOnlyList<CharacterProjectionConflictView>>(
            normalization,
            "Conflicts");

        var remaining = Assert.Single(normalizedRequest.Advancements!);
        Assert.Equal(classConceptKey, remaining.ConceptKey);
        var conflict = Assert.Single(conflicts, value =>
            value.Kind == "subclass-selection-multiple");
        Assert.Contains(classConceptKey, conflict.RelatedConceptKeys);
        Assert.Contains(subclassConceptKey, conflict.RelatedConceptKeys);
    }

    private static object Normalize(
        CharacterRulesProjectionRequest request,
        IReadOnlyList<ResolvedRuleCatalogItemView> rules)
    {
        var normalizer = typeof(CharacterRulesProjectionService).Assembly.GetType(
            "RulesCore.Infrastructure.Rules.CharacterProjection.CharacterSubclassAdvancementNormalizer",
            throwOnError: true)!;
        var method = normalizer.GetMethod(
            "Normalize",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(null, new object?[] { request, rules })!;
    }

    private static T Property<T>(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<T>(property!.GetValue(instance));
    }

    private static ResolvedRuleCatalogItemView Rule(
        string conceptKey,
        string entityType,
        string displayName,
        IReadOnlyList<ResolvedRuleRelationshipView> relationships)
    {
        using var document = JsonDocument.Parse("{}");
        return new ResolvedRuleCatalogItemView(
            Guid.NewGuid(),
            conceptKey,
            entityType,
            displayName,
            "selected",
            false,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            displayName,
            "TEST",
            "test-package",
            "Test Package",
            "fixture",
            "Fixture",
            [],
            relationships,
            document.RootElement.Clone());
    }
}
