using Microsoft.EntityFrameworkCore;

namespace RulesCore.Infrastructure.Persistence;

public sealed class RulesCoreDbContext(DbContextOptions<RulesCoreDbContext> options) : DbContext(options)
{
}
