using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.IntegrationTests;

public sealed class CharacterAdvancementEligibilityServiceTests
{
    [Fact]
    public async Task SubclassEligibilityUsesEffectiveParentRelationshipAndAcquisitionLevel()
    {
        var classConceptKey = "class.threshold-mage";
        var subclassConceptKey = "subclass.threshold-path";
        var candidate = Rule(
            subclassConceptKey,
            "subclass",
            "Threshold Path",
            """
            {
              "subclassFeatures": [
                "Path Initiate|Threshold Mage|SRC|Threshold Path|SRC|3",
                "Path Mastery|Threshold Mage|SRC|Threshold Path|SRC|7"
              ]
            }
            """,
            [new ResolvedRuleRelationshipView(
                "parent-class",
                Guid.NewGuid(),
                classConceptKey,
                "class",
                "Threshold Mage")]);
        var catalog = new FakeCatalog(candidate);
        var projection = new RecordingProjectionService();
        var service = new CharacterAdvancementEligibilityService(catalog, projection);

        var before = await service.EvaluateGlobalAsync(
            new CharacterAdvancementEligibilityRequest(
                subclassConceptKey,
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            classConceptKey,
                            2,
                            "parent-occurrence")
                    ]),
                "parent-occurrence"),
            userId: null);

        Assert.NotNull(before);
        Assert.Equal(CharacterAdvancementEligibilityStates.Ineligible, before!.State);
        Assert.False(before.Eligible);
        Assert.NotNull(before.ParentClass);
        Assert.Equal(3, before.ParentClass!.RequiredLevel);
        Assert.Equal(2, before.ParentClass.CurrentLevel);
        Assert.False(before.ParentClass.Satisfied);

        var atThreshold = await service.EvaluateGlobalAsync(
            new CharacterAdvancementEligibilityRequest(
                subclassConceptKey,
                new CharacterRulesProjectionRequest(
                    Advancements:
                    [
                        new CharacterAdvancementFactInput(
                            classConceptKey,
                            3,
                            "parent-occurrence")
                    ]),
                "parent-occurrence"),
            userId: null);

        Assert.NotNull(atThreshold);
        Assert.Equal(CharacterAdvancementEligibilityStates.Eligible, atThreshold!.State);
        Assert.True(atThreshold.Eligible);
        Assert.True(atThreshold.ParentClass!.Satisfied);
        Assert.NotNull(projection.LastGlobalRequest);
        Assert.Contains(
            projection.LastGlobalRequest!.SelectedConcepts!,
            value => value.ConceptKey == subclassConceptKey);
    }

    [Fact]
    public async Task PrestigeEligibilityPreservesOwnedFeatsAndReturnsProjectedRequirements()
    {
        var prestigeConceptKey = "prestigeClass.loremaster";
        var featConceptKey = "feat.skill-focus-knowledge";
        var candidate = Rule(
            prestigeConceptKey,
            "prestigeClass",
            "Loremaster",
            "{}",
            []);
        var catalog = new FakeCatalog(candidate);
        var projection = new RecordingProjectionService(
            request =>
            {
                var ownsFeat = (request.SelectedConcepts ?? []).Any(value =>
                    value.ConceptKey == featConceptKey);
                var requirement = new CharacterPrerequisiteRequirementView(
                    "prestigeClass.loremaster.prerequisite.0.0",
                    "feat",
                    featConceptKey,
                    ">=",
                    1,
                    "Skill Focus (Knowledge)",
                    ownsFeat,
                    CharacterResolutionStates.Resolved,
                    ownsFeat ? "Required feat is owned." : "Required feat is missing.",
                    "prestigeClass.loremaster.prerequisite-group.0",
                    1);
                return Projection(
                    [new CharacterPrerequisiteView(
                        prestigeConceptKey,
                        CharacterResolutionStates.Resolved,
                        ownsFeat,
                        [requirement],
                        EmptyProvenance())]);
            });
        var service = new CharacterAdvancementEligibilityService(catalog, projection);

        var result = await service.EvaluateGlobalAsync(
            new CharacterAdvancementEligibilityRequest(
                prestigeConceptKey,
                new CharacterRulesProjectionRequest(
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(featConceptKey, "feat-occurrence")
                    ])),
            userId: null);

        Assert.NotNull(result);
        Assert.True(result!.Eligible);
        Assert.Equal(CharacterAdvancementEligibilityStates.Eligible, result.State);
        var prerequisite = Assert.IsType<CharacterPrerequisiteView>(result.Prerequisites);
        var featRequirement = Assert.Single(prerequisite.Requirements);
        Assert.Equal("feat", featRequirement.Kind);
        Assert.True(featRequirement.Satisfied);
        Assert.NotNull(projection.LastGlobalRequest);
        Assert.Contains(
            projection.LastGlobalRequest!.SelectedConcepts!,
            value => value.ConceptKey == featConceptKey);
        Assert.Contains(
            projection.LastGlobalRequest.SelectedConcepts!,
            value => value.ConceptKey == prestigeConceptKey);
    }

    private static ResolvedRuleCatalogItemView Rule(
        string conceptKey,
        string entityType,
        string displayName,
        string json,
        IReadOnlyList<ResolvedRuleRelationshipView> relationships)
    {
        using var document = JsonDocument.Parse(json);
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

    private static CharacterRulesProjectionView Projection(
        IReadOnlyList<CharacterPrerequisiteView>? prerequisites = null) =>
        new(
            "global",
            null,
            RevisionNumber: 1,
            PublishedAt: DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            Mechanics: [],
            Capabilities: [],
            Grants: [],
            Effects: [],
            Movement: [],
            Qualifications: [],
            Actions: [],
            Features: [],
            Resources: [],
            Spellcasting: [],
            Procedures: [],
            Choices: [],
            Prerequisites: prerequisites ?? [],
            Conflicts: [],
            Equipment: []);

    private static CharacterMechanicProvenanceView EmptyProvenance() =>
        new([], [], []);

    private sealed class RecordingProjectionService(
        Func<CharacterRulesProjectionRequest, CharacterRulesProjectionView>? result = null)
        : ICharacterRulesProjectionService
    {
        public CharacterRulesProjectionRequest? LastGlobalRequest { get; private set; }

        public Task<CharacterRulesProjectionView> ResolveGlobalAsync(
            CharacterRulesProjectionRequest request,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            LastGlobalRequest = request;
            return Task.FromResult(result?.Invoke(request) ?? Projection());
        }

        public Task<CharacterRulesProjectionView> ResolveCampaignAsync(
            Guid campaignId,
            CharacterRulesProjectionRequest request,
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result?.Invoke(request) ?? Projection());
    }

    private sealed class FakeCatalog(params ResolvedRuleCatalogItemView[] rules)
        : IResolvedRulesCatalogService
    {
        private readonly IReadOnlyList<ResolvedRuleCatalogItemView> values = rules;

        public Task<ResolvedRulesCatalogView> GetGlobalAsync(
            string? userId,
            string? entityType = null,
            string? query = null,
            int limit = 200,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(View("global", null));

        public Task<ResolvedRulesCatalogView> GetGlobalPageAsync(
            string? userId,
            string? entityType = null,
            string? query = null,
            int limit = 200,
            int offset = 0,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(View("global", null));

        public Task<ResolvedRulesCatalogView> GetCampaignAsync(
            Guid campaignId,
            string userId,
            string? entityType = null,
            string? query = null,
            int limit = 200,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(View("campaign", campaignId));

        public Task<ResolvedRulesCatalogView> GetCampaignPageAsync(
            Guid campaignId,
            string userId,
            string? entityType = null,
            string? query = null,
            int limit = 200,
            int offset = 0,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(View("campaign", campaignId));

        private ResolvedRulesCatalogView View(string scope, Guid? campaignId) =>
            new(
                scope,
                campaignId,
                RevisionNumber: 1,
                PublishedAt: DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
                TotalCount: values.Count,
                EntityTypeFacets: [],
                SourceFacets: [],
                Rules: values);
    }
}
