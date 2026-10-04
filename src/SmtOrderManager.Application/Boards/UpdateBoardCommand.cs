namespace SmtOrderManager.Application.Boards;

/// <summary>
/// Request to change a board. All values replace the current ones, including the whole bill
/// of materials.
/// </summary>
/// <param name="Id">The board to change.</param>
/// <param name="Name">The new name.</param>
/// <param name="Description">The new description.</param>
/// <param name="Length">The new length in millimetres.</param>
/// <param name="Width">The new width in millimetres.</param>
/// <param name="BillOfMaterials">The new bill of materials; each component at most once.</param>
public sealed record UpdateBoardCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Length,
    decimal Width,
    IReadOnlyList<BomEntryInput> BillOfMaterials);
