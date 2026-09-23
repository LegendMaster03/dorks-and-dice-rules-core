using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Sources;
using RulesCore.Domain.Sources;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public sealed class SourceGrantService(RulesCoreDbContext dbContext) : ISourceGrantService
{
    public async Task GrantAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeRequiredUserId(userId);
        if (!await dbContext.SourcePackages.AnyAsync(value => value.Id == sourcePackageId, cancellationToken))
        {
            throw new KeyNotFoundException($"Source package '{sourcePackageId}' does not exist.");
        }

        if (await dbContext.UserSourceGrants.AnyAsync(
                value => value.UserId == normalizedUserId && value.SourcePackageId == sourcePackageId,
                cancellationToken))
        {
            return;
        }

        dbContext.UserSourceGrants.Add(new UserSourceGrant
        {
            Id = Guid.NewGuid(),
            SourcePackageId = sourcePackageId,
            UserId = normalizedUserId,
            GrantedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeRequiredUserId(userId);
        var grant = await dbContext.UserSourceGrants.SingleOrDefaultAsync(
            value => value.UserId == normalizedUserId && value.SourcePackageId == sourcePackageId,
            cancellationToken);
        if (grant is null)
        {
            return false;
        }

        dbContext.UserSourceGrants.Remove(grant);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HasGrantAsync(
        string userId,
        Guid sourcePackageId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeRequiredUserId(userId);
        return await dbContext.SourcePackages.AnyAsync(
            value => value.Id == sourcePackageId
                && (value.IsPublic || value.UserGrants.Any(grant => grant.UserId == normalizedUserId)),
            cancellationToken);
    }

    private static string NormalizeRequiredUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID can not be blank.", nameof(userId));
        }
        return userId.Trim();
    }
}
