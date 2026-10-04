namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// The data sent to an SMT line when an order is downloaded. This is the published contract
/// toward the line: it is mapped from the domain model, never the domain model itself, so both
/// can evolve independently. See <see cref="DownloadPayloadSchema"/> for the versioning rules.
/// </summary>
/// <param name="SchemaVersion">The schema version of this payload.</param>
/// <param name="GeneratedAt">The point in time at which the payload was created.</param>
/// <param name="Order">The order to produce.</param>
/// <param name="Boards">The boards referenced by the order, in the order of the order lines.</param>
/// <param name="ComponentDemand">The total number of placements per component for the whole order.</param>
public sealed record OrderDownloadPayload(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    OrderPayload Order,
    IReadOnlyList<BoardPayload> Boards,
    IReadOnlyList<ComponentDemandPayload> ComponentDemand);

/// <summary>
/// The order part of the download payload.
/// </summary>
/// <param name="Id">The order identifier.</param>
/// <param name="Name">The order name.</param>
/// <param name="Description">The description; empty when there is none.</param>
/// <param name="OrderDate">The point in time at which the order was issued, with its offset from UTC.</param>
/// <param name="Lines">The boards to produce and how many of each.</param>
public sealed record OrderPayload(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset OrderDate,
    IReadOnlyList<OrderLinePayload> Lines);

/// <summary>
/// One board of the order with the number of boards to produce.
/// </summary>
public sealed record OrderLinePayload(Guid BoardId, int Quantity);

/// <summary>
/// A board design referenced by the order, with its bill of materials. The unit is part of the
/// dimension names, so a consumer cannot misread them.
/// </summary>
/// <param name="Id">The board identifier, as referenced by <see cref="OrderLinePayload.BoardId"/>.</param>
/// <param name="Name">The board name.</param>
/// <param name="Description">The description; empty when there is none.</param>
/// <param name="LengthMm">The board length in millimetres.</param>
/// <param name="WidthMm">The board width in millimetres.</param>
/// <param name="BillOfMaterials">The components on one board.</param>
public sealed record BoardPayload(
    Guid Id,
    string Name,
    string Description,
    decimal LengthMm,
    decimal WidthMm,
    IReadOnlyList<BomEntryPayload> BillOfMaterials);

/// <summary>
/// One component on a board, with the number of placements per board. The component name is
/// included for readability on the line.
/// </summary>
public sealed record BomEntryPayload(Guid ComponentId, string ComponentName, int Quantity);

/// <summary>
/// The total number of placements of one component across all ordered boards.
/// </summary>
public sealed record ComponentDemandPayload(Guid ComponentId, string ComponentName, long TotalQuantity);
