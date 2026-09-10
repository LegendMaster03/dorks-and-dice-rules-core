namespace RulesCore.Application.Sources;

public sealed record SourceAdministrationPackageView(
    Guid Id,
    string Key,
    string DisplayName,
    string Provider,
    string? License,
    bool IsPublic,
    bool CurrentUserHasGrant,
    DateTimeOffset CreatedAt);

public sealed record SourceAdministrationGrantMutationView(
    SourceAdministrationPackageView Package,
    bool Changed);

public interface ISourceAccessAdministrationService
{
    Task<IReadOnlyList<SourceAdministrationPackageView>> GetPackagesAsync(
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<SourceAdministrationGrantMutationView?> GrantCurrentUserAsync(
        string currentUserId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default);

    Task<SourceAdministrationGrantMutationView?> RevokeCurrentUserAsync(
        string currentUserId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default);
}
