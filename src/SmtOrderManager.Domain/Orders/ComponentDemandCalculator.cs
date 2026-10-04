using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Orders;

/// <summary>
/// Calculates the total component demand of an order: for each component, the sum of
/// (number of boards ordered × placements of the component on that board) over all order
/// lines. A stateless domain service, because the calculation needs the order and all boards
/// it references, so it does not belong to a single aggregate.
/// </summary>
public static class ComponentDemandCalculator
{
    /// <summary>
    /// Calculates the demand per component for the order.
    /// </summary>
    /// <param name="order">The order to calculate the demand for.</param>
    /// <param name="boards">
    /// The boards referenced by the order. Additional boards are ignored.
    /// </param>
    /// <returns>
    /// One entry per component, in the order in which the components first appear when the
    /// order lines and their bills of materials are read from top to bottom.
    /// </returns>
    /// <exception cref="DomainException">A board referenced by the order is missing.</exception>
    public static IReadOnlyList<ComponentDemand> Calculate(Order order, IReadOnlyCollection<Board> boards)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(boards);

        var boardsById = boards.ToDictionary(board => board.Id);
        EnsureAllBoardsAreProvided(order, boardsById);

        // Keeps the components in order of first appearance, so the result is deterministic.
        var totals = new OrderedDictionary<Guid, long>();

        foreach (var line in order.Lines)
        {
            foreach (var entry in boardsById[line.BoardId].BillOfMaterials)
            {
                var demand = (long)line.Quantity * entry.Quantity;
                totals[entry.ComponentId] = totals.TryGetValue(entry.ComponentId, out var current)
                    ? checked(current + demand)
                    : demand;
            }
        }

        return totals
            .Select(total => new ComponentDemand(total.Key, total.Value))
            .ToList()
            .AsReadOnly();
    }

    private static void EnsureAllBoardsAreProvided(Order order, Dictionary<Guid, Board> boardsById)
    {
        var missing = order.Lines
            .Select(line => line.BoardId)
            .Where(boardId => !boardsById.ContainsKey(boardId))
            .ToList();

        if (missing.Count > 0)
        {
            throw new DomainException(
                $"Order '{order.Name}' references boards that were not provided: {string.Join(", ", missing)}.");
        }
    }
}
