namespace RulesCore.Tests;

public sealed class ProjectBoundaryTests
{
    [Fact]
    public void DomainDoesNotReferenceOtherRulesCoreProjects()
    {
        var references = typeof(Domain.DomainAssembly)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain(references, name => name!.StartsWith("RulesCore.", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplicationDoesNotReferenceInfrastructure()
    {
        var references = typeof(Application.ApplicationAssembly)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("RulesCore.Infrastructure", references);
    }
}
