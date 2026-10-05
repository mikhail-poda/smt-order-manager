namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// The stored form of a board with its bill of materials. Dimensions are in millimetres.
/// </summary>
internal sealed record BoardDocument(
    Guid Id,
    string Name,
    string Description,
    decimal Length,
    decimal Width,
    IReadOnlyList<BomEntryDocument> BillOfMaterials) : IDocument;

/// <summary>
/// The stored form of a BOM entry.
/// </summary>
internal sealed record BomEntryDocument(Guid ComponentId, int Quantity);
