namespace RulesCore.Domain.Rules;

public static class CharacterResolutionStates
{
    public const string Resolved = "resolved";
    public const string ApplicableUnresolved = "applicable-unresolved";
    public const string MissingCharacterInput = "missing-character-input";
    public const string MissingCapability = "missing-capability";
    public const string ChoiceRequired = "choice-required";
    public const string RollRequired = "roll-required";
    public const string SourceUnavailable = "source-unavailable";
    public const string NotApplicable = "not-applicable";
    public const string Conflict = "conflict";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [
            Resolved,
            ApplicableUnresolved,
            MissingCharacterInput,
            MissingCapability,
            ChoiceRequired,
            RollRequired,
            SourceUnavailable,
            NotApplicable,
            Conflict
        ],
        StringComparer.Ordinal);
}

public static class CharacterEffectOperations
{
    public const string Add = "add";
    public const string Set = "set";
    public const string Multiply = "multiply";
    public const string Divide = "divide";
    public const string Best = "best";
    public const string Worst = "worst";
    public const string Cap = "cap";
    public const string Floor = "floor";
    public const string Replace = "replace";
    public const string Grant = "grant";
    public const string Revoke = "revoke";
}

public static class CharacterEffectKinds
{
    public const string MechanicContribution = "mechanic-contribution";
    public const string Capability = "capability";
    public const string Action = "action";
    public const string Resource = "resource";
    public const string Movement = "movement";
    public const string Qualification = "qualification";
    public const string Spellcasting = "spellcasting";
    public const string Condition = "condition";
    public const string Recovery = "recovery";
    public const string Feature = "feature";
    public const string Other = "other";
}

public sealed record CharacterDependencyNode(
    string Key,
    IReadOnlyList<string> Dependencies);

public sealed record CharacterDependencyPlan(
    IReadOnlyList<string> EvaluationOrder,
    IReadOnlyList<string> MissingDependencyKeys,
    IReadOnlyList<string> CyclicNodeKeys)
{
    public bool CanEvaluate =>
        MissingDependencyKeys.Count == 0
        && CyclicNodeKeys.Count == 0;
}

public static class CharacterDependencyPlanner
{
    public static CharacterDependencyPlan Plan(IReadOnlyCollection<CharacterDependencyNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var byKey = new Dictionary<string, CharacterDependencyNode>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            ArgumentNullException.ThrowIfNull(node);
            var key = RequireKey(node.Key, nameof(node.Key));
            if (!byKey.TryAdd(key, node with { Key = key }))
            {
                throw new ArgumentException($"Duplicate Character mechanic dependency node '{key}'.", nameof(nodes));
            }
        }

        var missing = byKey.Values
            .SelectMany(value => value.Dependencies)
            .Select(value => RequireKey(value, "dependency"))
            .Where(value => !byKey.ContainsKey(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var inDegree = byKey.Keys.ToDictionary(value => value, _ => 0, StringComparer.Ordinal);
        var outgoing = byKey.Keys.ToDictionary(
            value => value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var node in byKey.Values)
        {
            foreach (var dependency in node.Dependencies
                         .Select(value => RequireKey(value, "dependency"))
                         .Distinct(StringComparer.Ordinal))
            {
                if (!byKey.ContainsKey(dependency))
                {
                    continue;
                }

                inDegree[node.Key]++;
                outgoing[dependency].Add(node.Key);
            }
        }

        var ready = new SortedSet<string>(
            inDegree.Where(value => value.Value == 0).Select(value => value.Key),
            StringComparer.Ordinal);
        var order = new List<string>(byKey.Count);

        while (ready.Count > 0)
        {
            var next = ready.Min!;
            ready.Remove(next);
            order.Add(next);

            foreach (var dependent in outgoing[next].OrderBy(value => value, StringComparer.Ordinal))
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        var cyclic = inDegree
            .Where(value => value.Value > 0)
            .Select(value => value.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new CharacterDependencyPlan(order, missing, cyclic);
    }

    private static string RequireKey(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Character mechanic keys can not be blank.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > 400)
        {
            throw new ArgumentException("Character mechanic keys can not exceed 400 characters.", parameterName);
        }

        return normalized;
    }
}

public static class StandardDndCharacterMath
{
    public static int AbilityModifier(int effectiveScore) =>
        checked((int)Math.Floor((effectiveScore - 10) / 2.0d));

    public static int ProficiencyBonusForCharacterLevel(int characterLevel)
    {
        if (characterLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterLevel),
                "Character level must be greater than zero.");
        }

        return checked(2 + ((characterLevel - 1) / 4));
    }
}
