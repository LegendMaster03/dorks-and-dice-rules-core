using System.Text.Json;

namespace RulesCore.Application.Rules;

public static class RuleArrayOperationKinds
{
    public const string Append = "append";
    public const string Remove = "remove";
    public const string ReplaceByKey = "replace-by-key";
    public const string InsertBefore = "insert-before";
    public const string InsertAfter = "insert-after";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Append, Remove, ReplaceByKey, InsertBefore, InsertAfter],
        StringComparer.Ordinal);
}

public sealed record RuleArrayItemSelectorRequest(
    string? Key,
    JsonElement Value);

public sealed record RuleArrayOperationRequest(
    string Operation,
    string Path,
    JsonElement? Value = null,
    RuleArrayItemSelectorRequest? Match = null,
    RuleArrayItemSelectorRequest? Anchor = null);

public sealed record RuleStructuredPatchRequest(
    JsonElement? MergePatch = null,
    IReadOnlyList<RuleArrayOperationRequest>? ArrayOperations = null);
