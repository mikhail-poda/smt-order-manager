using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Components;

/// <summary>
/// A part type in the component library, for example a 10 kΩ resistor in an 0402 package.
/// A component stands for any number of identical physical parts, not for a single part.
/// </summary>
public sealed class Component : AggregateRoot
{
    private Component(Guid id, string name, string? description)
        : base(id)
    {
        Name = Guard.NotBlank(name, nameof(Name));
        Description = NormalizeDescription(description);
    }

    /// <summary>
    /// The descriptive name of the component. Never blank, without surrounding whitespace.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// An optional free-text description. Empty when no description is given.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Creates a new component with a newly generated identifier.
    /// </summary>
    public static Component Create(string name, string? description) =>
        new(Guid.NewGuid(), name, description);

    /// <summary>
    /// Rebuilds a persisted component with its existing identifier. The same rules apply as
    /// for a new component, so invalid stored data is detected when it is loaded.
    /// </summary>
    public static Component Restore(Guid id, string name, string? description) =>
        new(id, name, description);

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(Name));

    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    private static string NormalizeDescription(string? description) => description?.Trim() ?? string.Empty;
}
