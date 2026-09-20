using Microsoft.EntityFrameworkCore;
using RulesCore.Infrastructure.Persistence;

namespace RulesCore.Infrastructure.Sources;

public static class SourceImportExecutionPolicy
{
    public const int DatabaseCommandTimeoutSeconds = 300;

    public static void Apply(RulesCoreDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        dbContext.Database.SetCommandTimeout(DatabaseCommandTimeoutSeconds);
    }
}
