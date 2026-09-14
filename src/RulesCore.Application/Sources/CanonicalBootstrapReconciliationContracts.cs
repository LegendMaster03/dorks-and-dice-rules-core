namespace RulesCore.Application.Sources;

public static class CanonicalBootstrapReconciliationClassifications
{
    public const string ExactIdentity = "exact-identity";
    public const string CorroboratedExactIdentity = "corroborated-exact-identity";
    public const string Reprint = "reprint";
    public const string Revision = "revision";
    public const string Rename = "rename";
    public const string Variant = "variant";
    public const string SameNameDifferentEntity = "same-name-different-entity";
    public const string BadSourceData = "bad-source-data";
    public const string ParserError = "parser-error";
    public const string Unresolved = "unresolved";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        ExactIdentity,
        CorroboratedExactIdentity,
        Reprint,
        Revision,
        Rename,
        Variant,
        SameNameDifferentEntity,
        BadSourceData,
        ParserError,
        Unresolved
    };

    public static bool IsKnown(string classification) => Known.Contains(classification);

    public static bool RegistersTrustedAlias(string classification) =>
        classification is ExactIdentity or CorroboratedExactIdentity;
}

public sealed record RecordCanonicalBootstrapReconciliationRequest(
    string AliasScheme,
    string AliasValue,
    string SemanticFingerprint,
    string Classification,
    Guid? CanonicalEntityId,
    Guid? RelatedCanonicalEntityId,
    string EvidenceKind,
    double Confidence,
    string DecidedBy,
    string? Notes = null);

public sealed record CanonicalBootstrapReconciliationView(
    Guid Id,
    string AliasScheme,
    string AliasValue,
    string SemanticFingerprint,
    string Classification,
    Guid? CanonicalEntityId,
    Guid? RelatedCanonicalEntityId,
    string EvidenceKind,
    double Confidence,
    string? Notes,
    string DecidedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
