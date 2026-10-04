using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Boards;

/// <summary>
/// A printed circuit board design together with its bill of materials. A board stands for a
/// reusable design, not for a single produced board.
/// </summary>
public sealed class Board : AggregateRoot
{
    private readonly List<BomEntry> _billOfMaterials;

    private Board(
        Guid id,
        string name,
        string? description,
        decimal length,
        decimal width,
        IEnumerable<BomEntry> billOfMaterials)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(billOfMaterials);

        Name = Guard.NotBlank(name, nameof(Name));
        Description = NormalizeDescription(description);
        Length = Guard.Positive(length, nameof(Length));
        Width = Guard.Positive(width, nameof(Width));
        _billOfMaterials = ValidateBillOfMaterials(billOfMaterials);
    }

    /// <summary>
    /// The descriptive name of the board. Never blank, without surrounding whitespace.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// An optional free-text description. Empty when no description is given.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// The board length in millimetres. Always positive.
    /// </summary>
    public decimal Length { get; private set; }

    /// <summary>
    /// The board width in millimetres. Always positive.
    /// </summary>
    public decimal Width { get; private set; }

    /// <summary>
    /// The components mounted on the board. Contains at least one entry and each component
    /// at most once. The list is read-only; changes go through <see cref="SetBomEntry"/> and
    /// <see cref="RemoveBomEntry"/>.
    /// </summary>
    public IReadOnlyList<BomEntry> BillOfMaterials => _billOfMaterials.AsReadOnly();

    /// <summary>
    /// Creates a new board with a newly generated identifier.
    /// </summary>
    public static Board Create(
        string name,
        string? description,
        decimal length,
        decimal width,
        IEnumerable<BomEntry> billOfMaterials) =>
        new(Guid.NewGuid(), name, description, length, width, billOfMaterials);

    /// <summary>
    /// Rebuilds a persisted board with its existing identifier. The same rules apply as for a
    /// new board, so invalid stored data is detected when it is loaded.
    /// </summary>
    public static Board Restore(
        Guid id,
        string name,
        string? description,
        decimal length,
        decimal width,
        IEnumerable<BomEntry> billOfMaterials) =>
        new(id, name, description, length, width, billOfMaterials);

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(Name));

    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    /// <summary>
    /// Changes both dimensions. Both values are validated before either is changed, so an
    /// invalid value leaves the board unchanged.
    /// </summary>
    public void ChangeDimensions(decimal length, decimal width)
    {
        var validLength = Guard.Positive(length, nameof(Length));
        var validWidth = Guard.Positive(width, nameof(Width));

        Length = validLength;
        Width = validWidth;
    }

    /// <summary>
    /// Adds a BOM entry for the component, or replaces the existing entry for that component
    /// with a new one. A replaced entry keeps its position in the list.
    /// </summary>
    public void SetBomEntry(Guid componentId, int quantity)
    {
        var entry = new BomEntry(componentId, quantity);
        var index = IndexOf(componentId);

        if (index >= 0)
        {
            _billOfMaterials[index] = entry;
        }
        else
        {
            _billOfMaterials.Add(entry);
        }
    }

    /// <summary>
    /// Removes the BOM entry for the component. The last entry cannot be removed, because a
    /// board without components would represent an empty PCB.
    /// </summary>
    public void RemoveBomEntry(Guid componentId)
    {
        var index = IndexOf(componentId);

        if (index < 0)
        {
            throw new DomainException($"Component {componentId} is not in the bill of materials of board '{Name}'.");
        }

        if (_billOfMaterials.Count == 1)
        {
            throw new DomainException($"The last BOM entry of board '{Name}' cannot be removed.");
        }

        _billOfMaterials.RemoveAt(index);
    }

    private int IndexOf(Guid componentId) =>
        _billOfMaterials.FindIndex(entry => entry.ComponentId == componentId);

    private static List<BomEntry> ValidateBillOfMaterials(IEnumerable<BomEntry> billOfMaterials)
    {
        var entries = billOfMaterials.ToList();

        if (entries.Count == 0)
        {
            throw new DomainException("A board must contain at least one BOM entry.");
        }

        var duplicates = entries
            .GroupBy(entry => entry.ComponentId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new DomainException(
                $"Each component may appear only once in a bill of materials. Duplicates: {string.Join(", ", duplicates)}.");
        }

        return entries;
    }

    private static string NormalizeDescription(string? description) => description?.Trim() ?? string.Empty;
}
