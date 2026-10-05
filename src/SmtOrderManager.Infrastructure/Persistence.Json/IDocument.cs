namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// A stored aggregate, identified by the identifier of its aggregate root.
/// </summary>
internal interface IDocument
{
    Guid Id { get; }
}
