namespace SmtOrderManager.Application.Boards;

/// <summary>
/// Request to add a board design with its bill of materials.
/// </summary>
/// <param name="Name">The board name.</param>
/// <param name="Description">An optional description.</param>
/// <param name="Length">The length in millimetres.</param>
/// <param name="Width">The width in millimetres.</param>
/// <param name="BillOfMaterials">The components on the board; each component at most once.</param>
public sealed record CreateBoardCommand(
    string Name,
    string? Description,
    decimal Length,
    decimal Width,
    IReadOnlyList<BomEntryInput> BillOfMaterials);
