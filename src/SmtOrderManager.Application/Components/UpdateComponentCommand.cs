namespace SmtOrderManager.Application.Components;

/// <summary>
/// Request to change a component. Name and description replace the current values.
/// </summary>
public sealed record UpdateComponentCommand(Guid Id, string Name, string? Description);
