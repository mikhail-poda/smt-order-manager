using System.Reflection;

namespace SmtOrderManager.Infrastructure.Tests.Architecture;

/// <summary>
/// Makes the dependency rule of Clean Architecture executable for the infrastructure layer:
/// it implements the abstractions of the application layer, but never depends on the host.
/// </summary>
public sealed class DependencyRuleTests
{
    // Loaded by name, so the test does not depend on any type that exists in the assembly.
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("SmtOrderManager.Infrastructure");

    [Fact]
    public void InfrastructureAssembly_DoesNotReferenceCli()
    {
        var references = InfrastructureAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToList();

        Assert.DoesNotContain("SmtOrderManager.Cli", references);
    }
}
