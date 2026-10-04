namespace SmtOrderManager.Application.Boards;

/// <summary>
/// One component of a board's bill of materials in a create or update request.
/// </summary>
/// <param name="ComponentId">The component, by identifier.</param>
/// <param name="Quantity">The number of placements of the component on one board.</param>
public sealed record BomEntryInput(Guid ComponentId, int Quantity);
