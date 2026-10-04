using System.Reflection;

namespace SmtOrderManager.Application.Tests.Architecture;

/// <summary>
/// Makes the dependency rule of Clean Architecture executable for the application layer: it
/// may depend on the domain, but never on an outer layer.
/// </summary>
public sealed class DependencyRuleTests
{
    // Loaded by name, so the test does not depend on any type that exists in the assembly.
    private static readonly Assembly ApplicationAssembly = Assembly.Load("SmtOrderManager.Application");

    [Theory]
    [InlineData("SmtOrderManager.Infrastructure")]
    [InlineData("SmtOrderManager.Cli")]
    public void ApplicationAssembly_DoesNotReferenceOuterLayer(string outerLayer)
    {
        var references = ApplicationAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToList();

        Assert.DoesNotContain(outerLayer, references);
    }
}
