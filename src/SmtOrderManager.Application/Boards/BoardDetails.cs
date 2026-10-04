using SmtOrderManager.Domain.Boards;

namespace SmtOrderManager.Application.Boards;

/// <summary>
/// Read model of a board for callers of the application layer, with component names resolved
/// for display.
/// </summary>
public sealed record BoardDetails(
    Guid Id,
    string Name,
    string Description,
    decimal Length,
    decimal Width,
    IReadOnlyList<BomEntryDetails> BillOfMaterials)
{
    internal static BoardDetails From(Board board, Dictionary<Guid, string> componentNames) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.Length,
            board.Width,
            board.BillOfMaterials
                .Select(entry => new BomEntryDetails(
                    entry.ComponentId,
                    componentNames.GetValueOrDefault(entry.ComponentId, BomEntryDetails.UnknownComponentName),
                    entry.Quantity))
                .ToList()
                .AsReadOnly());
}

/// <summary>
/// Read model of one BOM entry.
/// </summary>
public sealed record BomEntryDetails(Guid ComponentId, string ComponentName, int Quantity)
{
    /// <summary>
    /// Shown when a referenced component no longer exists. Restricted deletion prevents this,
    /// so it only appears with inconsistent stored data.
    /// </summary>
    public const string UnknownComponentName = "(unknown component)";
}
