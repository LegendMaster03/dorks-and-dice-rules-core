using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RulesCore.Application.Hosting;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.IntegrationTests;

[Collection(SourceLayerPostgresCollection.Name)]
public sealed class RuleAdjudicationWorkIntegrationTests
{
    private const string IntrospectionPath = "/tool-host/rules-core/api/introspect";

    [Fact]
    public async Task DiscoveryAppliesSafeDeterministicResolutionBeforeAgentReviewAndDerivesPublication()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            const string actor = "deterministic-agent";

            var firstImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-identical-a-{Guid.NewGuid():N}", "2014", "Identical Skill", "A14", "int", true));
            var secondImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-identical-b-{Guid.NewGuid():N}", "2024", "Identical Skill", "A24", "int", true));
            packageIds.Add(firstImport.PackageId);
            packageIds.Add(secondImport.PackageId);

            var normalization = new SourceNormalizationService(db);
            var first = (await normalization.AcceptAsync(firstImport.Entities.Single().EntityId, actor))!;
            var second = (await normalization.AcceptAsync(secondImport.Entities.Single().EntityId, actor))!;
            Assert.Equal(first.Concept.Id, second.Concept.Id);
            Assert.False(await db.GlobalRuleDecisions.AnyAsync(value => value.RuleConceptId == first.Concept.Id));

            var workflow = new RuleAdjudicationWorkService(db);
            var discovery = await workflow.DiscoverAsync(actor);
            Assert.True(discovery.DeterministicDecisionsApplied >= 1);

            var item = Assert.Single(
                await workflow.ListAsync(actor, includePublishedCompleted: true),
                value => value.RuleConceptId == first.Concept.Id);
            Assert.Equal(RuleAdjudicationWorkStates.Completed, item.State);
            Assert.True(item.CompletedButUnpublished);
            Assert.False(item.Published);

            var detail = (await workflow.GetAsync(item.Id, actor))!;
            Assert.Contains(detail.AccessibleSourceRevisions, value => value.EditionDisplayName == "5e");
            Assert.Contains(detail.AccessibleSourceRevisions, value => value.EditionDisplayName == "5.5e");
            Assert.DoesNotContain(detail.AccessibleSourceRevisions, value => value.EditionDisplayName == "5etools");
            Assert.Contains(
                detail.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.DeterministicResolutionApplied);
            Assert.Contains(
                detail.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.DecisionAssociated);

            var firstWorkId = item.Id;
            await workflow.DiscoverAsync(actor);
            var repeated = Assert.Single(
                await workflow.ListAsync(actor, includePublishedCompleted: true),
                value => value.RuleConceptId == first.Concept.Id);
            Assert.Equal(firstWorkId, repeated.Id);

            await scope.ServiceProvider.GetRequiredService<IGlobalRulesService>().PublishAsync(actor);
            var published = Assert.Single(
                await workflow.ListAsync(actor, includePublishedCompleted: true),
                value => value.RuleConceptId == first.Concept.Id);
            Assert.True(published.Published);
            Assert.False(published.CompletedButUnpublished);
            Assert.Contains(
                (await workflow.GetAsync(firstWorkId, actor))!.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.PublicationObserved);
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task CompletedAutomaticAdjudicationReopensWhenNewSourceEvidenceContradictsIt()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            const string actor = "reopen-agent";
            var firstPackageKey = $"adjudication-reopen-a-{Guid.NewGuid():N}";
            var secondPackageKey = $"adjudication-reopen-b-{Guid.NewGuid():N}";

            var firstImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest(firstPackageKey, "2014", "Reopen Skill", "R14", "int", true));
            var secondImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest(secondPackageKey, "2024", "Reopen Skill", "R24", "int", true));
            packageIds.Add(firstImport.PackageId);
            packageIds.Add(secondImport.PackageId);

            var normalization = new SourceNormalizationService(db);
            var first = (await normalization.AcceptAsync(firstImport.Entities.Single().EntityId, actor))!;
            var second = (await normalization.AcceptAsync(secondImport.Entities.Single().EntityId, actor))!;
            Assert.Equal(first.Concept.Id, second.Concept.Id);

            var workflow = new RuleAdjudicationWorkService(db);
            await workflow.DiscoverAsync(actor);
            var completed = Assert.Single(
                await workflow.ListAsync(actor, includePublishedCompleted: true),
                value => value.RuleConceptId == first.Concept.Id);
            Assert.Equal(RuleAdjudicationWorkStates.Completed, completed.State);
            var workItemId = completed.Id;

            await importer.Import5eToolsDocumentAsync(
                SourceRequest(secondPackageKey, "2024", "Reopen Skill", "R24", "wis", true));

            await workflow.DiscoverAsync(actor);
            var reopened = Assert.Single(
                await workflow.ListAsync(
                    actor,
                    kind: RuleAdjudicationWorkKinds.GlobalRuleAdjudication,
                    includePublishedCompleted: true),
                value => value.RuleConceptId == first.Concept.Id);
            Assert.Equal(workItemId, reopened.Id);
            Assert.Equal(RuleAdjudicationWorkStates.Pending, reopened.State);

            var detail = (await workflow.GetAsync(reopened.Id, actor))!;
            Assert.NotNull(detail.DeterministicResolution);
            Assert.False(detail.DeterministicResolution!.Applied);
            Assert.Contains(detail.SemanticComparisons, value => value.ContradictionCount > 0);
            Assert.Contains(
                detail.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.Reopened
                    && value.ActorUserId is null);
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task DeterministicResolutionAbstainsWhenARevisionClosureContainsAnInaccessibleSource()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            const string actor = "partial-source-agent";

            var firstImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-partial-a-{Guid.NewGuid():N}", "2014", "Partial Source Skill", "PA", "int", true));
            var secondImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-partial-b-{Guid.NewGuid():N}", "2024", "Partial Source Skill", "PB", "int", true));
            var restrictedImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-partial-secret-{Guid.NewGuid():N}", "3.5e", "Partial Source Skill", "PS", "wis", false));
            packageIds.Add(firstImport.PackageId);
            packageIds.Add(secondImport.PackageId);
            packageIds.Add(restrictedImport.PackageId);

            var firstEntityId = firstImport.Entities.Single().EntityId;
            var secondEntityId = secondImport.Entities.Single().EntityId;
            var restrictedEntityId = restrictedImport.Entities.Single().EntityId;
            var firstCanonicalId = await ReadCanonicalEntityIdAsync(db, firstEntityId);
            var secondCanonicalId = await ReadCanonicalEntityIdAsync(db, secondEntityId);
            var restrictedCanonicalId = await ReadCanonicalEntityIdAsync(db, restrictedEntityId);
            Assert.NotEqual(firstCanonicalId, restrictedCanonicalId);

            if (secondCanonicalId != firstCanonicalId)
            {
                await RelateCanonicalRevisionAsync(db, firstCanonicalId, secondCanonicalId);
            }
            await RelateCanonicalRevisionAsync(db, firstCanonicalId, restrictedCanonicalId);

            var concept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest($"skill.partial-source-{Guid.NewGuid():N}", "skill", "Partial Source Skill"),
                "seed-rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(firstEntityId),
                "seed-rules-lawyer");

            var result = await RuleAutoResolutionService.TryResolveAsync(db, concept.Id, actor);
            Assert.False(result.Eligible);
            Assert.False(result.Applied);
            Assert.Contains("requires access to every active source implementation", result.Reason, StringComparison.Ordinal);
            Assert.False(await db.GlobalRuleDecisions.AnyAsync(value => value.RuleConceptId == concept.Id));

            await scope.ServiceProvider.GetRequiredService<ISourceGrantService>()
                .GrantAsync(actor, restrictedImport.PackageId);
            var fullyVisible = await RuleAutoResolutionService.TryResolveAsync(db, concept.Id, actor);
            Assert.False(fullyVisible.Eligible);
            Assert.Contains("Manual adjudication is required", fullyVisible.Reason, StringComparison.Ordinal);
            Assert.False(await db.GlobalRuleDecisions.AnyAsync(value => value.RuleConceptId == concept.Id));
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task ContradictionsRemainAdjudicationWorkAndManualDecisionsAreNotReplaced()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            const string actor = "contradiction-agent";

            var firstImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-conflict-a-{Guid.NewGuid():N}", "2014", "Conflicting Skill", "C14", "int", true));
            var secondImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-conflict-b-{Guid.NewGuid():N}", "2024", "Conflicting Skill", "C24", "wis", true));
            packageIds.Add(firstImport.PackageId);
            packageIds.Add(secondImport.PackageId);

            var normalization = new SourceNormalizationService(db);
            var first = (await normalization.AcceptAsync(firstImport.Entities.Single().EntityId, actor))!;
            var second = (await normalization.AcceptAsync(secondImport.Entities.Single().EntityId, actor))!;
            Assert.Equal(first.Concept.Id, second.Concept.Id);

            var workflow = new RuleAdjudicationWorkService(db);
            await workflow.DiscoverAsync(actor);
            var item = Assert.Single(
                await workflow.ListAsync(
                    actor,
                    kind: RuleAdjudicationWorkKinds.GlobalRuleAdjudication,
                    includePublishedCompleted: true),
                value => value.RuleConceptId == first.Concept.Id);
            Assert.Equal(RuleAdjudicationWorkStates.Pending, item.State);

            var detail = (await workflow.GetAsync(item.Id, actor))!;
            Assert.NotNull(detail.DeterministicResolution);
            Assert.False(detail.DeterministicResolution!.Applied);
            Assert.Contains(detail.SemanticComparisons, value => value.ContradictionCount > 0);

            var revisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == firstImport.Entities.Single().EntityId)
                .Select(value => value.Id)
                .SingleAsync();
            var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var manual = (await rules.SetDecisionAsync(
                first.Concept.Id,
                new SetGlobalRuleDecisionRequest(revisionId, "Manual contradiction decision."),
                "human-rules-lawyer")).Value;

            await workflow.DiscoverAsync(actor);
            var latest = await db.GlobalRuleDecisions
                .Where(value => value.RuleConceptId == first.Concept.Id)
                .OrderByDescending(value => value.DecisionNumber)
                .ToArrayAsync();
            Assert.Single(latest);
            Assert.Equal(manual.Id, latest[0].Id);
            Assert.False(RuleAutoResolutionService.IsAutomaticDecision(latest[0]));
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task NormalizationWorkRequiresExplicitRulesLawyerActionAndIndependentSourceGrant()
    {
        if (!HasDatabase()) return;

        var authenticationClient = new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>
        {
            ["agent-ticket"] = Context("normalization-agent", [RulesAuthority.RulesLawyerRole]),
            ["plain-ticket"] = Context("plain-user", [])
        });
        await using var factory = CreateFactory(authenticationClient);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var packageIds = new List<Guid>();

        try
        {
            Guid publicEntityId;
            Guid restrictedEntityId;
            Guid restrictedPackageId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                var publicImport = await importer.Import5eToolsDocumentAsync(
                    SourceRequest($"adjudication-normalization-public-{Guid.NewGuid():N}", "2014", "Queue Skill", "PUB", "int", true));
                var restrictedImport = await importer.Import5eToolsDocumentAsync(
                    SourceRequest($"adjudication-normalization-restricted-{Guid.NewGuid():N}", "2024", "Restricted Queue Skill", "SECRET", "dex", false));
                packageIds.Add(publicImport.PackageId);
                packageIds.Add(restrictedImport.PackageId);
                publicEntityId = publicImport.Entities.Single().EntityId;
                restrictedEntityId = restrictedImport.Entities.Single().EntityId;
                restrictedPackageId = restrictedImport.PackageId;

                await scope.ServiceProvider.GetRequiredService<ISourceGrantService>()
                    .GrantAsync("plain-user", restrictedPackageId);
            }

            using (var request = HostedRequest(HttpMethod.Post, "/api/global/rules/adjudication/discover", "plain-ticket"))
            using (var response = await client.SendAsync(request))
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            using (var request = HostedRequest(HttpMethod.Post, "/api/global/rules/adjudication/discover", "agent-ticket"))
            using (var response = await client.SendAsync(request))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            IReadOnlyList<RuleAdjudicationWorkSummaryView> beforeAcceptance;
            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/adjudication/work?kind={RuleAdjudicationWorkKinds.NormalizationReview}&includePublishedCompleted=true",
                       "agent-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                beforeAcceptance = (await response.Content.ReadFromJsonAsync<IReadOnlyList<RuleAdjudicationWorkSummaryView>>())!;
            }
            Assert.Contains(beforeAcceptance, value => value.SourceEntityId == publicEntityId);
            Assert.DoesNotContain(beforeAcceptance, value => value.SourceEntityId == restrictedEntityId);

            var normalizationItem = beforeAcceptance.Single(value => value.SourceEntityId == publicEntityId);
            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/adjudication/work/{normalizationItem.Id}",
                       "agent-ticket"))
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var detail = (await response.Content.ReadFromJsonAsync<RuleAdjudicationWorkDetailView>())!;
                Assert.Equal("5e", detail.NormalizationCandidate!.EditionDisplayName);
                Assert.NotEqual("5etools", detail.NormalizationCandidate.EditionDisplayName);
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                Assert.False(await db.RuleConceptSourceBindings.AnyAsync(value => value.SourceEntityId == publicEntityId));
            }

            using (var request = HostedRequest(
                       HttpMethod.Post,
                       $"/api/global/rules/normalization/entities/{publicEntityId}/accept",
                       "agent-ticket"))
            using (var response = await client.SendAsync(request))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using (var request = HostedRequest(HttpMethod.Post, "/api/global/rules/adjudication/discover", "agent-ticket"))
            using (var response = await client.SendAsync(request))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            RuleAdjudicationWorkSummaryView completed;
            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/adjudication/work?kind={RuleAdjudicationWorkKinds.NormalizationReview}&includePublishedCompleted=true",
                       "agent-ticket"))
            using (var response = await client.SendAsync(request))
            {
                var items = (await response.Content.ReadFromJsonAsync<IReadOnlyList<RuleAdjudicationWorkSummaryView>>())!;
                completed = items.Single(value => value.SourceEntityId == publicEntityId);
            }
            Assert.Equal(RuleAdjudicationWorkStates.Completed, completed.State);

            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/adjudication/work/{completed.Id}",
                       "agent-ticket"))
            using (var response = await client.SendAsync(request))
            {
                var detail = (await response.Content.ReadFromJsonAsync<RuleAdjudicationWorkDetailView>())!;
                Assert.Contains(
                    detail.History,
                    value => value.EventKind == RuleAdjudicationWorkEventKinds.NormalizationAccepted
                        && value.ActorUserId == "normalization-agent");
            }

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                var binding = await db.RuleConceptSourceBindings.SingleAsync(value => value.SourceEntityId == publicEntityId);
                Assert.Equal("normalization-agent", binding.CreatedByUserId);
                await scope.ServiceProvider.GetRequiredService<ISourceGrantService>()
                    .GrantAsync("normalization-agent", restrictedPackageId);
            }

            using (var request = HostedRequest(HttpMethod.Post, "/api/global/rules/adjudication/discover", "agent-ticket"))
            using (var response = await client.SendAsync(request))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using (var request = HostedRequest(
                       HttpMethod.Get,
                       $"/api/global/rules/adjudication/work?kind={RuleAdjudicationWorkKinds.NormalizationReview}&includePublishedCompleted=true",
                       "agent-ticket"))
            using (var response = await client.SendAsync(request))
            {
                var items = (await response.Content.ReadFromJsonAsync<IReadOnlyList<RuleAdjudicationWorkSummaryView>>())!;
                Assert.Contains(items, value => value.SourceEntityId == restrictedEntityId);
            }
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task SourceUpdateWorkUsesCanonicalEditionEvidenceAndTracksAdoptionToPublication()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            const string actor = "source-update-agent";
            var packageKey = $"adjudication-source-update-{Guid.NewGuid():N}";

            var firstImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest(packageKey, "2014", "Update Queue Skill", "UPD", "int", true));
            packageIds.Add(firstImport.PackageId);
            var sourceEntityId = firstImport.Entities.Single().EntityId;
            var firstRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == sourceEntityId)
                .Select(value => value.Id)
                .SingleAsync();

            var concept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest($"skill.update-queue-{Guid.NewGuid():N}", "skill", "Update Queue Skill"),
                actor)).Value;
            await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(sourceEntityId),
                actor);
            await rules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(firstRevisionId, "Reviewed source-update baseline."),
                actor);

            await importer.Import5eToolsDocumentAsync(
                SourceRequest(packageKey, "2014", "Update Queue Skill", "UPD", "wis", true));

            var workflow = new RuleAdjudicationWorkService(db);
            await workflow.DiscoverAsync(actor);
            var item = Assert.Single(
                await workflow.ListAsync(
                    actor,
                    kind: RuleAdjudicationWorkKinds.SourceUpdateReview,
                    includePublishedCompleted: true),
                value => value.RuleConceptId == concept.Id);
            Assert.Equal(RuleAdjudicationWorkStates.Pending, item.State);

            var detail = (await workflow.GetAsync(item.Id, actor))!;
            Assert.NotNull(detail.SourceUpdate);
            Assert.Equal("5e", detail.SourceUpdate!.Update.EditionDisplayName);
            Assert.NotEqual("5etools", detail.SourceUpdate.Update.EditionDisplayName);

            var update = detail.SourceUpdate.Update;
            var adopted = await new SourceRevisionReviewService(db).AdoptLatestAsync(
                concept.Id,
                new AdoptLatestSourceRevisionRequest(
                    update.GlobalRuleDecisionId,
                    update.LatestSourceEntityRevisionId,
                    update.LatestFingerprint),
                actor);
            Assert.NotNull(adopted);

            var completed = (await workflow.GetAsync(item.Id, actor))!;
            Assert.Equal(RuleAdjudicationWorkStates.Completed, completed.WorkItem.State);
            Assert.True(completed.WorkItem.CompletedButUnpublished);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.SourceUpdateResolved);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.DecisionAssociated
                    && value.GlobalRuleDecisionId == adopted!.GlobalRuleDecisionId);

            await rules.PublishAsync(actor);
            var published = (await workflow.GetAsync(item.Id, actor))!;
            Assert.True(published.WorkItem.Published);
            Assert.False(published.WorkItem.CompletedButUnpublished);
            Assert.Contains(
                published.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.PublicationObserved);
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task RejectedSourceUpdateAuditUsesTheRejectingRulesLawyer()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var packageKey = $"adjudication-source-rejection-{Guid.NewGuid():N}";

            var firstImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest(packageKey, "2014", "Rejected Update Skill", "REJ", "int", true));
            packageIds.Add(firstImport.PackageId);
            var sourceEntityId = firstImport.Entities.Single().EntityId;
            var firstRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == sourceEntityId)
                .Select(value => value.Id)
                .SingleAsync();

            var concept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest($"skill.rejected-update-{Guid.NewGuid():N}", "skill", "Rejected Update Skill"),
                "seed-rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(sourceEntityId),
                "seed-rules-lawyer");
            await rules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(firstRevisionId, "Reviewed rejection baseline."),
                "seed-rules-lawyer");

            await importer.Import5eToolsDocumentAsync(
                SourceRequest(packageKey, "2014", "Rejected Update Skill", "REJ", "wis", true));

            var workflow = new RuleAdjudicationWorkService(db);
            await workflow.DiscoverAsync("agent-source-reviewer");
            var item = Assert.Single(
                await workflow.ListAsync(
                    "agent-source-reviewer",
                    kind: RuleAdjudicationWorkKinds.SourceUpdateReview,
                    includePublishedCompleted: true),
                value => value.RuleConceptId == concept.Id);
            var detail = (await workflow.GetAsync(item.Id, "agent-source-reviewer"))!;
            var update = detail.SourceUpdate!.Update;
            const string rejectionReason = "The new revision is not adopted for the Dorks & Dice baseline.";

            var rejected = await new SourceRevisionRejectionService(db).RejectLatestAsync(
                concept.Id,
                new RejectLatestSourceRevisionRequest(
                    update.GlobalRuleDecisionId,
                    update.LatestSourceEntityRevisionId,
                    update.LatestFingerprint,
                    rejectionReason),
                "human-source-reviewer");
            Assert.NotNull(rejected);

            var completed = (await workflow.GetAsync(item.Id, "agent-source-reviewer"))!;
            Assert.Equal(RuleAdjudicationWorkStates.Completed, completed.WorkItem.State);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.SourceUpdateResolved
                    && value.ActorUserId == "human-source-reviewer"
                    && value.Message!.Contains(rejectionReason, StringComparison.Ordinal));
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task ManualNormalizationBindingCompletesAgainstTheActualConcept()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
            var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            const string actor = "manual-normalization-reviewer";

            var imported = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-manual-normalization-{Guid.NewGuid():N}", "2014", "Manual Bound Skill", "MAN", "int", true));
            packageIds.Add(imported.PackageId);
            var sourceEntityId = imported.Entities.Single().EntityId;

            var suggested = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest("skill.manual-bound-skill", "skill", "Manual Bound Skill"),
                actor)).Value;
            var actual = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest("skill.manual-bound-skill-reviewed", "skill", "Manual Bound Skill Reviewed"),
                actor)).Value;

            var workflow = new RuleAdjudicationWorkService(db);
            await workflow.DiscoverAsync(actor);
            var pending = Assert.Single(
                await workflow.ListAsync(
                    actor,
                    kind: RuleAdjudicationWorkKinds.NormalizationReview,
                    includePublishedCompleted: true),
                value => value.SourceEntityId == sourceEntityId);
            Assert.Equal(suggested.Id, pending.RuleConceptId);

            await rules.BindSourceEntityAsync(
                actual.Id,
                new BindRuleConceptSourceRequest(sourceEntityId),
                actor);

            var completed = (await workflow.GetAsync(pending.Id, actor))!;
            Assert.Equal(RuleAdjudicationWorkStates.Completed, completed.WorkItem.State);
            Assert.Equal(actual.Id, completed.WorkItem.RuleConceptId);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.NormalizationAccepted
                    && value.RuleConceptId == actual.Id
                    && value.ActorUserId == actor);
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    [Fact]
    public async Task ClarificationEscalationManualResolutionAuditAndStaleWritesAreEnforced()
    {
        if (!HasDatabase()) return;

        await using var factory = CreateFactory(new FakeToolHostAuthenticationClient(new Dictionary<string, ToolHostAuthenticationContext>()));
        var packageIds = new List<Guid>();

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
            var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
            var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

            var publicImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-manual-public-{Guid.NewGuid():N}", "public", "Manual Queue Skill", "PUBLIC", "int", true));
            var restrictedImport = await importer.Import5eToolsDocumentAsync(
                SourceRequest($"adjudication-manual-restricted-{Guid.NewGuid():N}", "restricted", "Manual Queue Skill", "RESTRICTED_SECRET", "dex", false));
            packageIds.Add(publicImport.PackageId);
            packageIds.Add(restrictedImport.PackageId);
            await grants.GrantAsync("human-rules-lawyer", restrictedImport.PackageId);

            var rules = scope.ServiceProvider.GetRequiredService<IGlobalRulesService>();
            var concept = (await rules.CreateConceptAsync(
                new CreateRuleConceptRequest($"skill.manual-queue-{Guid.NewGuid():N}", "skill", "Manual Queue Skill"),
                "human-rules-lawyer")).Value;
            await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(publicImport.Entities.Single().EntityId),
                "human-rules-lawyer");
            await rules.BindSourceEntityAsync(
                concept.Id,
                new BindRuleConceptSourceRequest(restrictedImport.Entities.Single().EntityId),
                "human-rules-lawyer");

            var publicRevisionId = await db.SourceEntityRevisions
                .Where(value => value.SourceEntityId == publicImport.Entities.Single().EntityId)
                .Select(value => value.Id)
                .SingleAsync();

            var workflow = new RuleAdjudicationWorkService(db);
            await workflow.DiscoverAsync("agent-rules-lawyer");
            var item = Assert.Single(
                await workflow.ListAsync(
                    "agent-rules-lawyer",
                    kind: RuleAdjudicationWorkKinds.GlobalRuleAdjudication,
                    includePublishedCompleted: true),
                value => value.RuleConceptId == concept.Id);

            var initialDetail = (await workflow.GetAsync(item.Id, "agent-rules-lawyer"))!;
            Assert.Equal(1, initialDetail.Rule!.RestrictedBindingCount);
            Assert.Single(initialDetail.AccessibleSourceRevisions);
            Assert.DoesNotContain(
                initialDetail.AccessibleSourceRevisions,
                value => value.SourceEntityId == restrictedImport.Entities.Single().EntityId);
            Assert.DoesNotContain(
                "RESTRICTED_SECRET",
                JsonSerializer.Serialize(initialDetail),
                StringComparison.Ordinal);

            var review = (await workflow.BeginReviewAsync(
                item.Id,
                new RuleAdjudicationVersionRequest(item.Version),
                "agent-rules-lawyer"))!;
            var waiting = (await workflow.RequestClarificationAsync(
                item.Id,
                new RequestRuleAdjudicationClarificationRequest(
                    review.Version,
                    "Does this campaign-independent wording intentionally preserve the older exception?"),
                "agent-rules-lawyer"))!;

            await Assert.ThrowsAsync<RuleAdjudicationConcurrencyException>(() =>
                workflow.DeferAsync(
                    item.Id,
                    new DeferRuleAdjudicationWorkRequest(review.Version, "Stale request."),
                    "agent-rules-lawyer"));

            var clarificationAnswer =
                "Yes. Preserve the exception unless the source explicitly removes it. "
                + new string('x', 4200);
            var resumed = (await workflow.AnswerClarificationAsync(
                item.Id,
                new AnswerRuleAdjudicationClarificationRequest(
                    waiting.Version,
                    clarificationAnswer),
                "human-rules-lawyer"))!;
            Assert.Equal(RuleAdjudicationWorkStates.AgentReview, resumed.State);

            var escalated = (await workflow.EscalateAsync(
                item.Id,
                new EscalateRuleAdjudicationWorkRequest(
                    resumed.Version,
                    "The exact cross-edition exception needs a human Rules Lawyer choice."),
                "agent-rules-lawyer"))!;
            Assert.Equal(RuleAdjudicationWorkStates.ManualResolutionRequired, escalated.State);

            var reviewed = (await workflow.GetAsync(item.Id, "agent-rules-lawyer"))!;
            Assert.Null(reviewed.DecisionGuard!.ExpectedLatestDecisionId);

            var humanDecision = (await rules.SetDecisionAsync(
                concept.Id,
                new SetGlobalRuleDecisionRequest(publicRevisionId, "Human manual decision."),
                "human-rules-lawyer")).Value;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                rules.SetDecisionAsync(
                    concept.Id,
                    new SetGlobalRuleDecisionRequest(
                        publicRevisionId,
                        "Stale agent decision.",
                        ExpectedLatestDecisionId: reviewed.DecisionGuard.ExpectedLatestDecisionId,
                        EnforceExpectedLatestDecision: true),
                    "agent-rules-lawyer"));

            var completed = (await workflow.GetAsync(item.Id, "agent-rules-lawyer"))!;
            Assert.Equal(RuleAdjudicationWorkStates.Completed, completed.WorkItem.State);
            Assert.True(completed.WorkItem.CompletedButUnpublished);
            Assert.Equal(humanDecision.Id, completed.Rule!.LatestDecision!.Id);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.ClarificationRequested
                    && value.ActorUserId == "agent-rules-lawyer");
            Assert.Equal(clarificationAnswer, completed.Clarification!.Answer);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.ClarificationAnswered
                    && value.ActorUserId == "human-rules-lawyer"
                    && value.Message == clarificationAnswer);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.ManualEscalation);
            Assert.Contains(
                completed.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.DecisionAssociated
                    && value.GlobalRuleDecisionId == humanDecision.Id);

            var historyCount = completed.History.Count;
            await rules.PublishAsync("human-rules-lawyer");
            var published = (await workflow.GetAsync(item.Id, "agent-rules-lawyer"))!;
            Assert.True(published.WorkItem.Published);
            Assert.False(published.WorkItem.CompletedButUnpublished);
            Assert.True(published.History.Count >= historyCount);
            Assert.Contains(
                published.History,
                value => value.EventKind == RuleAdjudicationWorkEventKinds.PublicationObserved);
        }
        finally
        {
            await CleanupAsync(factory, packageIds);
        }
    }

    private static bool HasDatabase() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RulesCore"));

    private static WebApplicationFactory<Program> CreateFactory(
        IToolHostAuthenticationClient authenticationClient) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IToolHostAuthenticationClient>();
                services.AddSingleton(authenticationClient);
            });
        });

    private static HttpRequestMessage HostedRequest(HttpMethod method, string path, string ticket)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ToolHostAuthenticationHeaders.Ticket, ticket);
        request.Headers.Add(ToolHostAuthenticationHeaders.IntrospectionPath, IntrospectionPath);
        return request;
    }

    private static ToolHostAuthenticationContext Context(
        string userId,
        IReadOnlyList<string> globalRoles) =>
        new(
            ContractVersion: 1,
            ToolSlug: "rules-core",
            SiteMode: "dorks-and-dice",
            User: new ToolHostUserContext(userId, userId),
            GlobalRoles: globalRoles,
            Campaigns: []);

    private static Import5eToolsDocumentRequest SourceRequest(
        string packageKey,
        string editionKey,
        string entityName,
        string sourceCode,
        string ability,
        bool isPublic) =>
        new(
            PackageKey: packageKey,
            PackageDisplayName: $"Adjudication package {editionKey}",
            Provider: "integration-test",
            License: "test-only",
            IsPublic: isPublic,
            WorkKey: $"{packageKey}-work",
            WorkDisplayName: $"Adjudication work {editionKey}",
            EditionKey: editionKey,
            EditionDisplayName: $"Adjudication edition {editionKey}",
            Json: $$"""
                {
                  "skill": [
                    {
                      "name": "{{entityName}}",
                      "source": "{{sourceCode}}",
                      "ability": "{{ability}}"
                    }
                  ]
                }
                """);

    private static async Task<Guid> ReadCanonicalEntityIdAsync(
        RulesCoreDbContext db,
        Guid sourceEntityId) =>
        await db.Database.SqlQueryRaw<Guid>(
                """
                SELECT occurrence.canonical_entity_id AS "Value"
                FROM source_entity_revision revision
                JOIN source_entity_occurrence_binding binding
                    ON binding.source_entity_revision_id = revision.source_entity_revision_id
                JOIN canonical_source_occurrence occurrence
                    ON occurrence.canonical_source_occurrence_id = binding.canonical_source_occurrence_id
                WHERE revision.source_entity_id = {0}
                    AND occurrence.canonical_entity_id IS NOT NULL
                ORDER BY revision.revision_number DESC
                LIMIT 1
                """,
                sourceEntityId)
            .SingleAsync();

    private static Task<int> RelateCanonicalRevisionAsync(
        RulesCoreDbContext db,
        Guid fromCanonicalEntityId,
        Guid toCanonicalEntityId) =>
        db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO canonical_entity_relationship (
                canonical_entity_relationship_id,
                from_canonical_entity_id,
                to_canonical_entity_id,
                relationship_kind,
                evidence_kind,
                confidence,
                created_at)
            VALUES (
                {{Guid.NewGuid()}},
                {{fromCanonicalEntityId}},
                {{toCanonicalEntityId}},
                'revision',
                'integration-test',
                1.0,
                {{DateTimeOffset.UtcNow}})
            ON CONFLICT (from_canonical_entity_id, to_canonical_entity_id, relationship_kind)
            DO NOTHING;
            """);

    private static async Task CleanupAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyCollection<Guid> packageIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM rule_adjudication_work_event; DELETE FROM rule_adjudication_work_item;");
        }
        catch
        {
            // A test that failed before discovery may not have created the lazy workflow schema.
        }

        await db.CampaignRulesetRevisionEntries.ExecuteDeleteAsync();
        await db.CampaignRulesetRevisions.ExecuteDeleteAsync();
        await db.CampaignRuleDecisions.ExecuteDeleteAsync();
        await db.CampaignRulesetSelections.ExecuteDeleteAsync();
        await db.RulesetRevisionEntries.ExecuteDeleteAsync();
        await db.RulesetRevisions.ExecuteDeleteAsync();
        await db.GlobalRuleDecisions.ExecuteDeleteAsync();
        await db.RuleConceptSourceBindings.ExecuteDeleteAsync();
        await db.RuleConcepts.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();

        if (packageIds.Count > 0)
        {
            await db.SourcePackages
                .Where(value => packageIds.Contains(value.Id))
                .ExecuteDeleteAsync();
        }
    }

    private sealed class FakeToolHostAuthenticationClient(
        IReadOnlyDictionary<string, ToolHostAuthenticationContext> contexts)
        : IToolHostAuthenticationClient
    {
        public Task<ToolHostAuthenticationContext?> RedeemAsync(
            string ticket,
            string introspectionPath,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(introspectionPath, IntrospectionPath, StringComparison.Ordinal))
            {
                return Task.FromResult<ToolHostAuthenticationContext?>(null);
            }

            contexts.TryGetValue(ticket, out var context);
            return Task.FromResult(context);
        }
    }
}
