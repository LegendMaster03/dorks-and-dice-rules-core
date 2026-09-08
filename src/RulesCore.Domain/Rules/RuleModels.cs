using RulesCore.Domain.Sources;

namespace RulesCore.Domain.Rules;

public static class RuleDecisionKinds
{
    public const string SelectSource = "select-source";
}

public sealed class RuleConcept
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RuleConceptSourceBinding> SourceBindings { get; set; } = new List<RuleConceptSourceBinding>();
    public ICollection<GlobalRuleDecision> GlobalDecisions { get; set; } = new List<GlobalRuleDecision>();
    public ICollection<RulesetRevisionEntry> RulesetEntries { get; set; } = new List<RulesetRevisionEntry>();
}

public sealed class RuleConceptSourceBinding
{
    public Guid Id { get; set; }
    public Guid RuleConceptId { get; set; }
    public Guid SourceEntityId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public RuleConcept RuleConcept { get; set; } = null!;
    public SourceEntity SourceEntity { get; set; } = null!;
}

public sealed class GlobalRuleDecision
{
    public Guid Id { get; set; }
    public Guid RuleConceptId { get; set; }
    public int DecisionNumber { get; set; }
    public string DecisionKind { get; set; } = RuleDecisionKinds.SelectSource;
    public Guid SelectedSourceEntityRevisionId { get; set; }
    public string? Note { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public RuleConcept RuleConcept { get; set; } = null!;
    public SourceEntityRevision SelectedSourceEntityRevision { get; set; } = null!;
    public ICollection<RulesetRevisionEntry> RulesetEntries { get; set; } = new List<RulesetRevisionEntry>();
}

public sealed class RulesetRevision
{
    public Guid Id { get; set; }
    public int RevisionNumber { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string PublishedByUserId { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }

    public ICollection<RulesetRevisionEntry> Entries { get; set; } = new List<RulesetRevisionEntry>();
}

public sealed class RulesetRevisionEntry
{
    public Guid Id { get; set; }
    public Guid RulesetRevisionId { get; set; }
    public Guid RuleConceptId { get; set; }
    public Guid GlobalRuleDecisionId { get; set; }
    public Guid SourceEntityRevisionId { get; set; }

    public RulesetRevision RulesetRevision { get; set; } = null!;
    public RuleConcept RuleConcept { get; set; } = null!;
    public GlobalRuleDecision GlobalRuleDecision { get; set; } = null!;
    public SourceEntityRevision SourceEntityRevision { get; set; } = null!;
}
