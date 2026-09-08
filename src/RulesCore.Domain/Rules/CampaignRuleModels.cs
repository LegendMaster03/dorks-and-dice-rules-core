using RulesCore.Domain.Sources;

namespace RulesCore.Domain.Rules;

public static class CampaignRuleDecisionKinds
{
    public const string SelectSource = "select-source";
    public const string InheritGlobal = "inherit-global";
    public const string JsonMergePatch = "json-merge-patch";
    public const string JsonRulePatch = "json-rule-patch";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [SelectSource, InheritGlobal, JsonMergePatch, JsonRulePatch],
        StringComparer.Ordinal);
}

public sealed class CampaignRulesetSelection
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public int SelectionNumber { get; set; }
    public Guid RulesetRevisionId { get; set; }
    public string SelectedByUserId { get; set; } = string.Empty;
    public DateTimeOffset SelectedAt { get; set; }

    public RulesetRevision RulesetRevision { get; set; } = null!;
    public ICollection<CampaignRulesetRevision> CampaignRulesetRevisions { get; set; } = new List<CampaignRulesetRevision>();
}

public sealed class CampaignRuleDecision
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid RuleConceptId { get; set; }
    public int DecisionNumber { get; set; }
    public string DecisionKind { get; set; } = CampaignRuleDecisionKinds.InheritGlobal;
    public Guid? SelectedSourceEntityRevisionId { get; set; }
    public string? PatchJson { get; set; }
    public string? PatchFingerprint { get; set; }
    public string? Note { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public RuleConcept RuleConcept { get; set; } = null!;
    public SourceEntityRevision? SelectedSourceEntityRevision { get; set; }
    public ICollection<CampaignRulesetRevisionEntry> CampaignRulesetEntries { get; set; } = new List<CampaignRulesetRevisionEntry>();
}

public sealed class CampaignRulesetRevision
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public int RevisionNumber { get; set; }
    public Guid BaselineSelectionId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string PublishedByUserId { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }

    public CampaignRulesetSelection BaselineSelection { get; set; } = null!;
    public ICollection<CampaignRulesetRevisionEntry> Entries { get; set; } = new List<CampaignRulesetRevisionEntry>();
}

public sealed class CampaignRulesetRevisionEntry
{
    public Guid Id { get; set; }
    public Guid CampaignRulesetRevisionId { get; set; }
    public Guid RuleConceptId { get; set; }
    public Guid BaselineRulesetRevisionEntryId { get; set; }
    public Guid? CampaignRuleDecisionId { get; set; }
    public Guid SourceEntityRevisionId { get; set; }

    public CampaignRulesetRevision CampaignRulesetRevision { get; set; } = null!;
    public RuleConcept RuleConcept { get; set; } = null!;
    public RulesetRevisionEntry BaselineRulesetRevisionEntry { get; set; } = null!;
    public CampaignRuleDecision? CampaignRuleDecision { get; set; }
    public SourceEntityRevision SourceEntityRevision { get; set; } = null!;
}
