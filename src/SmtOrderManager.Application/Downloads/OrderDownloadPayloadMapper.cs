using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Components;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// Maps the domain model to the download payload. This is the only place where the internal
/// model meets the published contract.
/// </summary>
internal static class OrderDownloadPayloadMapper
{
    /// <summary>
    /// Creates the payload for an order.
    /// </summary>
    /// <param name="order">The order to download.</param>
    /// <param name="boards">The boards referenced by the order. Additional boards are ignored.</param>
    /// <param name="components">The components referenced by those boards. Additional components are ignored.</param>
    /// <param name="demand">The total component demand of the order.</param>
    /// <param name="generatedAt">The point in time at which the payload is created.</param>
    /// <exception cref="ArgumentException">A referenced board or component is missing.</exception>
    public static OrderDownloadPayload Map(
        Order order,
        IReadOnlyCollection<Board> boards,
        IReadOnlyCollection<Component> components,
        IReadOnlyList<ComponentDemand> demand,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(boards);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(demand);

        var boardsById = boards.ToDictionary(board => board.Id);
        var componentNames = components.ToDictionary(component => component.Id, component => component.Name);

        var orderedBoards = order.Lines
            .Select(line => boardsById.TryGetValue(line.BoardId, out var board)
                ? board
                : throw new ArgumentException($"Board {line.BoardId} of order '{order.Name}' was not provided.", nameof(boards)))
            .ToList();

        return new OrderDownloadPayload(
            DownloadPayloadSchema.CurrentVersion,
            generatedAt,
            MapOrder(order),
            orderedBoards.ConvertAll(board => MapBoard(board, componentNames)).AsReadOnly(),
            demand
                .Select(item => new ComponentDemandPayload(
                    item.ComponentId,
                    ComponentName(item.ComponentId, componentNames),
                    item.TotalQuantity))
                .ToList()
                .AsReadOnly());
    }

    private static OrderPayload MapOrder(Order order) =>
        new(
            order.Id,
            order.Name,
            order.Description,
            order.OrderDate,
            order.Lines.Select(line => new OrderLinePayload(line.BoardId, line.Quantity)).ToList().AsReadOnly());

    private static BoardPayload MapBoard(Board board, Dictionary<Guid, string> componentNames) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.Length,
            board.Width,
            board.BillOfMaterials
                .Select(entry => new BomEntryPayload(
                    entry.ComponentId,
                    ComponentName(entry.ComponentId, componentNames),
                    entry.Quantity))
                .ToList()
                .AsReadOnly());

    private static string ComponentName(Guid componentId, Dictionary<Guid, string> componentNames) =>
        componentNames.TryGetValue(componentId, out var name)
            ? name
            : throw new ArgumentException($"Component {componentId} was not provided.", nameof(componentNames));
}
