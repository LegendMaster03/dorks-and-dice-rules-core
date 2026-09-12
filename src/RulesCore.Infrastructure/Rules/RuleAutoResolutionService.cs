using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Infrastructure.Rules;

public sealed record RuleAutoResolutionResult(
    bool Eligible,
    bool Applied,
    Guid? DecisionId,
    int? DecisionNumber,
    string Reason);

public static class RuleAutoResolutionService
{
    private const string AutoResolutionNote =
        "Auto-resolved: no rule-bearing content changed across the bound editions.";

    private static readonly HashSet<string> IgnoredRootProperties = new(
        [
            "name",
            "source",
            "page",
            "id",
            "uniqueId",
            "reprintedAs",
            "otherSources",
            "additionalSources",
            "previousVersion",
            "previousVersions",
            "versions",
            "seeAlso",
            "edition",
            "srd",
            "srd52",
            "basicRules",
            "basicRules2024",
            "freeRules2024"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static async Task<RuleAutoResolutionResult> TryResolveAsync(
        RulesCoreDbContext dbContext,
        Guid ruleConceptId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (ruleConceptId == Guid.Empty)
        {
            throw new ArgumentException("Rule concept ID can not be empty.", nameof(ruleConceptId));
        }
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(actorUserId));
        }

        var actor = actorUserId.Trim();
        await SourceFrameworkStore.EnsureSchemaAsync(dbContext, cancellationToken);

        var sourceIds = await dbContext.RuleConceptSourceBindings
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId)
            .Select(value => value.SourceEntityId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (sourceIds.Length < 2)
        {
            return NotEligible("At least two bound source implementations are required.");
        }

        var sources = await dbContext.SourceEntities
            .AsNoTracking()
            .Include(value => value.Revisions)
            .Include(value => value.SourceEdition)
                .ThenInclude(value => value.SourceWork)
                .ThenInclude(value => value.SourcePackage)
                .ThenInclude(value => value.UserGrants)
            .Where(value => sourceIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (sources.Length != sourceIds.Length)
        {
            return NotEligible("One or more bound source implementations no longer exist.");
        }
        if (sources.Any(value =>
            !value.SourceEdition.SourceWork.SourcePackage.IsPublic
            && !value.SourceEdition.SourceWork.SourcePackage.UserGrants.Any(grant => grant.UserId == actor)))
        {
            return NotEligible("The current account can not inspect every bound source implementation.");
        }

        var contexts = new List<SourceContext>(sources.Length);
        foreach (var source in sources)
        {
            var latest = source.Revisions
                .OrderByDescending(value => value.RevisionNumber)
                .FirstOrDefault();
            if (latest is null)
            {
                return NotEligible("Every bound source implementation must have an immutable revision.");
            }

            var metadata = await SourceFrameworkStore.GetEditionMetadataAsync(
                dbContext,
                source.SourceEditionId,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(metadata?.GameEdition))
            {
                return NotEligible("Every bound source implementation must identify its D&D edition.");
            }

            contexts.Add(new SourceContext(
                source,
                latest,
                metadata!,
                ComputeSemanticFingerprint(latest.RawJson)));
        }

        if (contexts
            .Select(value => value.Metadata.GameEdition)
            .Distinct(StringComparer.Ordinal)
            .Count() < 2)
        {
            return NotEligible("Auto-resolution only applies to comparisons spanning multiple editions.");
        }

        var semanticFingerprints = contexts
            .Select(value => value.SemanticFingerprint)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (semanticFingerprints.Length != 1)
        {
            return NotEligible("The bound editions contain a rule-bearing difference and require manual adjudication.");
        }

        var semanticFingerprint = semanticFingerprints[0];
        var latestDecision = await dbContext.GlobalRuleDecisions
            .AsNoTracking()
            .Where(value => value.RuleConceptId == ruleConceptId)
            .OrderByDescending(value => value.DecisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestDecision is not null)
        {
            if (!string.Equals(latestDecision.DecisionKind, RuleDecisionKinds.SelectSource, StringComparison.Ordinal)
                || latestDecision.PatchFingerprint is not null)
            {
                return NotEligible("An existing manual or patched decision is authoritative and will not be replaced automatically.");
            }

            var selectedRevision = await dbContext.SourceEntityRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.Id == latestDecision.SelectedSourceEntityRevisionId,
                    cancellationToken);
            if (selectedRevision is not null
                && string.Equals(
                    ComputeSemanticFingerprint(selectedRevision.RawJson),
                    semanticFingerprint,
                    StringComparison.Ordinal))
            {
                return new RuleAutoResolutionResult(
                    Eligible: true,
                    Applied: false,
                    latestDecision.Id,
                    latestDecision.DecisionNumber,
                    "The existing exact-source decision already resolves the unchanged cross-edition rule.");
            }

            return NotEligible("The current decision selects mechanically different content and requires manual review.");
        }

        var baseContext = contexts
            .OrderByDescending(value => value.Metadata.PublicationDate ?? DateOnly.MinValue)
            .ThenByDescending(value => value.Metadata.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.Source.SourceCode, StringComparer.Ordinal)
            .ThenBy(value => value.Source.Id)
            .First();
        var contributions = contexts
            .Where(value => value.Revision.Id != baseContext.Revision.Id)
            .OrderBy(value => value.Metadata.GameEdition, StringComparer.Ordinal)
            .ThenBy(value => value.Source.SourceCode, StringComparer.Ordinal)
            .Select(value => new RuleConsolidationContributionRequest(
                value.Revision.Id,
                RuleConsolidationContributionKinds.Reference,
                "Automatically reviewed as mechanically identical to the selected source revision."))
            .ToArray();

        var rules = new GlobalRulesService(dbContext);
        var decision = await rules.SetDecisionAsync(
            ruleConceptId,
            new SetGlobalRuleDecisionRequest(
                baseContext.Revision.Id,
                AutoResolutionNote,
                Contributions: contributions),
            actor,
            cancellationToken);

        return new RuleAutoResolutionResult(
            Eligible: true,
            Applied: decision.Created,
            decision.Value.Id,
            decision.Value.DecisionNumber,
            decision.Created
                ? "Equivalent cross-edition source implementations were resolved automatically."
                : "The equivalent cross-edition decision was already current.");
    }

    internal static string ComputeSemanticFingerprint(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalRuleContent(writer, document.RootElement, isRoot: true);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonicalRuleContent(
        Utf8JsonWriter writer,
        JsonElement element,
        bool isRoot)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                    .EnumerateObject()
                    .Where(value => !isRoot || !IgnoredRootProperties.Contains(value.Name))
                    .OrderBy(value => value.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalRuleContent(writer, property.Value, isRoot: false);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray())
                {
                    WriteCanonicalRuleContent(writer, child, isRoot: false);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static RuleAutoResolutionResult NotEligible(string reason) =>
        new(false, false, null, null, reason);

    private sealed record SourceContext(
        SourceEntity Source,
        SourceEntityRevision Revision,
        StoredSourceEditionMetadata Metadata,
        string SemanticFingerprint);
}