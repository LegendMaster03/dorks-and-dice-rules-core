using System.Data;
using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceAccessAdministrationService(RulesCoreDbContext dbContext)
    : ISourceAccessAdministrationService
{
    public async Task<IReadOnlyList<SourceAdministrationPackageView>> GetPackagesAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        return await dbContext.SourcePackages
            .AsNoTracking()
            .OrderBy(value => value.DisplayName)
            .ThenBy(value => value.Key)
            .Select(value => new SourceAdministrationPackageView(
                value.Id,
                value.Key,
                value.DisplayName,
                value.Provider,
                value.License,
                value.IsPublic,
                value.UserGrants.Any(grant => grant.UserId == userId),
                value.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<SourceAdministrationGrantMutationView?> GrantCurrentUserAsync(
        string currentUserId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        RequireGuid(sourcePackageId, nameof(sourcePackageId));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var package = await dbContext.SourcePackages
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == sourcePackageId, cancellationToken);
        if (package is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        EnsureRestricted(package);

        var exists = await dbContext.UserSourceGrants
            .AnyAsync(
                value => value.SourcePackageId == sourcePackageId
                    && value.UserId == userId,
                cancellationToken);
        if (!exists)
        {
            dbContext.UserSourceGrants.Add(new UserSourceGrant
            {
                Id = Guid.NewGuid(),
                SourcePackageId = sourcePackageId,
                UserId = userId,
                GrantedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SourceAdministrationGrantMutationView(
            ToView(package, currentUserHasGrant: true),
            Changed: !exists);
    }

    public async Task<SourceAdministrationGrantMutationView?> RevokeCurrentUserAsync(
        string currentUserId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId(currentUserId);
        RequireGuid(sourcePackageId, nameof(sourcePackageId));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var package = await dbContext.SourcePackages
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == sourcePackageId, cancellationToken);
        if (package is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        EnsureRestricted(package);

        var grant = await dbContext.UserSourceGrants
            .SingleOrDefaultAsync(
                value => value.SourcePackageId == sourcePackageId
                    && value.UserId == userId,
                cancellationToken);
        if (grant is not null)
        {
            dbContext.UserSourceGrants.Remove(grant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SourceAdministrationGrantMutationView(
            ToView(package, currentUserHasGrant: false),
            Changed: grant is not null);
    }

    private static SourceAdministrationPackageView ToView(
        SourcePackage package,
        bool currentUserHasGrant) =>
        new(
            package.Id,
            package.Key,
            package.DisplayName,
            package.Provider,
            package.License,
            package.IsPublic,
            currentUserHasGrant,
            package.CreatedAt);

    private static void EnsureRestricted(SourcePackage package)
    {
        if (package.IsPublic)
        {
            throw new InvalidOperationException(
                $"Source package '{package.Key}' is public and does not require a user source grant.");
        }
    }

    private static string RequireUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }

        var normalized = userId.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("User ID can not exceed 200 characters.", nameof(userId));
        }
        return normalized;
    }

    private static void RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value can not be empty.", parameterName);
        }
    }
}
