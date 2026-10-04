using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Orders;

/// <summary>
/// One board in an order, with the number of boards to produce. A value object: it has no
/// identity of its own, is immutable, and two lines with the same values are equal.
/// </summary>
/// <remarks>
/// The properties are get-only rather than init-only, so a <c>with</c> expression cannot
/// bypass the validation in the constructor.
/// </remarks>
public sealed record OrderLine
{
    public OrderLine(Guid boardId, int quantity)
    {
        BoardId = Guard.NotEmpty(boardId, nameof(BoardId));
        Quantity = Guard.Positive(quantity, nameof(Quantity));
    }

    /// <summary>
    /// The referenced board, by identifier.
    /// </summary>
    public Guid BoardId { get; }

    /// <summary>
    /// The number of boards to produce.
    /// </summary>
    public int Quantity { get; }
}
