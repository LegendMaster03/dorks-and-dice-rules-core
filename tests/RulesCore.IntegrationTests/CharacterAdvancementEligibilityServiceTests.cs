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
    public async Task PrestigeEligibilityRemainsUnresolvedUntilPrerequisiteDefinitionIsComplete()
    {
        var prestigeConceptKey = "prestigeClass.incomplete";
        var candidate = Rule(
            prestigeConceptKey,
            "prestigeClass",
            "Incomplete Prestige",
            "{}",
            []);
        var service = new CharacterAdvancementEligibilityService(
            new FakeCatalog(candidate),
            new RecordingProjectionService());

        var result = await service.EvaluateGlobalAsync(
            new CharacterAdvancementEligibilityRequest(
                prestigeConceptKey,
                new CharacterRulesProjectionRequest()),
            userId: null);

        Assert.NotNull(result);
        Assert.Equal(CharacterAdvancementEligibilityStates.Unresolved, result!.State);
        Assert.Null(result.Eligible);
        Assert.Null(result.Prerequisites);
    }

    [Fact]
    public async Task PrestigeEligibilityConsumesNormalizedFeatRequirement()
    {
        var prestigeConceptKey = "prestigeClass.loremaster";
        var featConceptKey = "feat.skill-focus-knowledge";
        var candidate = Rule(
            prestigeConceptKey,
            "prestigeClass",
            "Loremaster",
            CompletePrestigeDocument(),
            []);
        var catalog = new FakeCatalog(candidate);
        var projection = new RecordingProjectionService(request =>
        {
            var ownsFeat = request.SelectedConcepts?.Any(value =>
                string.Equals(value.ConceptKey, featConceptKey, StringComparison.OrdinalIgnoreCase)) == true;
            return Projection(
                [NormalizedFeatPrerequisite(
                    prestigeConceptKey,
                    [(featConceptKey, "Skill Focus (Knowledge)", ownsFeat)],
                    matchCount: 1)]);
        });
        var service = new CharacterAdvancementEligibilityService(catalog, projection);

        var missing = await service.EvaluateGlobalAsync(
            new CharacterAdvancementEligibilityRequest(
                prestigeConceptKey,
                new CharacterRulesProjectionRequest()),
            userId: null);

        Assert.NotNull(missing);
        Assert.False(missing!.Eligible);
        Assert.Equal(CharacterAdvancementEligibilityStates.Ineligible, missing.State);
        var missingPrerequisite = Assert.IsType<CharacterPrerequisiteView>(missing.Prerequisites);
        var missingFeat = Assert.Single(missingPrerequisite.Requirements);
        Assert.Equal("feat", missingFeat.Kind);
        Assert.Equal(featConceptKey, missingFeat.TargetKey);
        Assert.False(missingFeat.Satisfied);

        var owned = await service.EvaluateGlobalAsync(
            new CharacterAdvancementEligibilityRequest(
                prestigeConceptKey,
                new CharacterRulesProjectionRequest(
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(featConceptKey, "feat-occurrence")
                    ])),
            userId: null);

        Assert.NotNull(owned);
        Assert.True(owned!.Eligible);
        Assert.Equal(CharacterAdvancementEligibilityStates.Eligible, owned.State);
        var ownedPrerequisite = Assert.IsType<CharacterPrerequisiteView>(owned.Prerequisites);
        var ownedFeat = Assert.Single(ownedPrerequisite.Requirements);
        Assert.Equal("feat", ownedFeat.Kind);
        Assert.True(ownedFeat.Satisfied);
        Assert.NotNull(projection.LastGlobalRequest);
        Assert.Contains(
            projection.LastGlobalRequest!.SelectedConcepts!,
            value => value.ConceptKey == featConceptKey);
        Assert.Contains(
            projection.LastGlobalRequest.SelectedConcepts!,
            value => value.ConceptKey == prestigeConceptKey);
    }

    [Fact]
    public async Task PrestigeEligibilityPreservesNormalizedNOfMFeatRequirements()
    {
        var prestigeConceptKey = "prestigeClass.feat-master";
        var firstFeatKey = "feat.first";
        var secondFeatKey = "feat.second";
        var thirdFeatKey = "feat.third";
        var candidate = Rule(
            prestigeConceptKey,
            "prestigeClass",
            "Feat Master",
            CompletePrestigeDocument(),
            []);
        var catalog = new FakeCatalog(candidate);
        var projection = new RecordingProjectionService(request =>
        {
            var selected = new HashSet<string>(
                request.SelectedConcepts?.Select(value => value.ConceptKey) ?? [],
                StringComparer.OrdinalIgnoreCase);
            return Projection(
                [NormalizedFeatPrerequisite(
                    prestigeConceptKey,
                    [
                        (firstFeatKey, "First Feat", selected.Contains(firstFeatKey)),
                        (secondFeatKey, "Second Feat", selected.Contains(secondFeatKey)),
                        (thirdFeatKey, "Third Feat", selected.Contains(thirdFeatKey))
                    ],
                    matchCount: 2)]);
        });
        var service = new CharacterAdvancementEligibilityService(catalog, projection);

        var result = await service.EvaluateGlobalAsync(
            new CharacterAdvancementEligibilityRequest(
                prestigeConceptKey,
                new CharacterRulesProjectionRequest(
                    SelectedConcepts:
                    [
                        new CharacterSelectedConceptInput(firstFeatKey),
                        new CharacterSelectedConceptInput(thirdFeatKey)
                    ])),
            userId: null);

        Assert.NotNull(result);
        Assert.True(result!.Eligible);
        var prerequisite = Assert.IsType<CharacterPrerequisiteView>(result.Prerequisites);
        Assert.Equal(3, prerequisite.Requirements.Count);
        Assert.All(prerequisite.Requirements, value => Assert.Equal(2, value.GroupMatchCount));
        Assert.Equal(2, prerequisite.Requirements.Count(value => value.Satisfied == true));
    }

    private static string CompletePrestigeDocument() =>
        """
        {
          "_rulesCore": {
            "character": {
              "prerequisitesComplete": true
            }
          }
        }
        """;

    private static CharacterPrerequisiteView NormalizedFeatPrerequisite(
        string conceptKey,
        IReadOnlyList<(string ConceptKey, string DisplayName, bool Owned)> feats,
        int matchCount)
    {
        var satisfiedCount = feats.Count(value => value.Owned);
        var satisfied = satisfiedCount >= matchCount;
        return new CharacterPrerequisiteView(
            conceptKey,
            CharacterResolutionStates.Resolved,
            satisfied,
            feats.Select((feat, index) => new CharacterPrerequisiteRequirementView(
                $"{conceptKey}.prerequisite.feat.{index}",
                "feat",
                feat.ConceptKey,
                ">=",
                1,
                feat.DisplayName,
                feat.Owned,
                CharacterResolutionStates.Resolved,
                feat.Owned
                    ? $"Feat '{feat.DisplayName}' is present."
                    : $"Feat '{feat.DisplayName}' is not present.",
                $"{conceptKey}.prerequisite.feats",
                matchCount)).ToArray(),
            EmptyProvenance());
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
