namespace SmtOrderManager.Domain.Common;

/// <summary>
/// Base class for aggregate roots. An aggregate root is identified by its <see cref="Id"/>:
/// two instances are equal when they have the same type and the same identifier, regardless
/// of their other values.
/// </summary>
public abstract class AggregateRoot : IEquatable<AggregateRoot>
{
    protected AggregateRoot(Guid id)
    {
        Id = Guard.NotEmpty(id, nameof(Id));
    }

    /// <summary>
    /// The technical identifier. References between aggregates use this value.
    /// </summary>
    public Guid Id { get; }

    public bool Equals(AggregateRoot? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other.GetType() == GetType() && other.Id == Id;
    }

    public override bool Equals(object? obj) => Equals(obj as AggregateRoot);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(AggregateRoot? left, AggregateRoot? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(AggregateRoot? left, AggregateRoot? right) => !(left == right);
}
