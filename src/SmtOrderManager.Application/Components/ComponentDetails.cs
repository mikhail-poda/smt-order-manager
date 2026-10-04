using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Application.Components;

/// <summary>
/// Read model of a component for callers of the application layer. Callers never receive the
/// aggregate itself, so they cannot change it without going through a use case.
/// </summary>
public sealed record ComponentDetails(Guid Id, string Name, string Description)
{
    internal static ComponentDetails From(Component component) =>
        new(component.Id, component.Name, component.Description);
}
