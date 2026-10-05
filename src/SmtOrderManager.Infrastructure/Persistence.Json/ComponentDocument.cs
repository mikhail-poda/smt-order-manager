namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// The stored form of a component.
/// </summary>
internal sealed record ComponentDocument(Guid Id, string Name, string Description) : IDocument;
