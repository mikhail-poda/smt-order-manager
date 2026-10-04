namespace SmtOrderManager.Application.Components;

/// <summary>
/// Request to add a component to the component library.
/// </summary>
public sealed record CreateComponentCommand(string Name, string? Description);
