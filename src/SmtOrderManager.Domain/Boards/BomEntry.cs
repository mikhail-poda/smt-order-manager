using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Boards;

/// <summary>
/// One component in a board's bill of materials, with the number of placements of that
/// component per board. A value object: it has no identity of its own, is immutable, and two
/// entries with the same values are equal.
/// </summary>
/// <remarks>
/// The properties are get-only rather than init-only, so a <c>with</c> expression cannot
/// bypass the validation in the constructor.
/// </remarks>
public sealed record BomEntry
{
    public BomEntry(Guid componentId, int quantity)
    {
        ComponentId = Guard.NotEmpty(componentId, nameof(ComponentId));
        Quantity = Guard.Positive(quantity, nameof(Quantity));
    }

    /// <summary>
    /// The referenced component, by identifier.
    /// </summary>
    public Guid ComponentId { get; }

    /// <summary>
    /// The number of placements of the component on one board.
    /// </summary>
    public int Quantity { get; }
}
