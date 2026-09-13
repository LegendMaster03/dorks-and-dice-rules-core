namespace RulesCore.Application.Rules;

public sealed record SetGlobalSourceIgnoredRequest(
    bool Ignored,
    string? Reason = null);

public sealed record GlobalIgnoredSourceView(
    Guid SourcePackageId,
    string PackageKey,
    string PackageDisplayName,
    string Provider,
    string? Reason,
    string IgnoredByUserId,
    DateTimeOffset IgnoredAt);
