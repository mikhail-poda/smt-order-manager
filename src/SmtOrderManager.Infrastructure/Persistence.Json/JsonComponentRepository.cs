using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// Stores the component library in <see cref="FileName"/>.
/// </summary>
internal sealed class JsonComponentRepository(JsonFileStore<ComponentDocument> store)
    : JsonRepository<Component, ComponentDocument>(store), IComponentRepository
{
    public const string FileName = "components.json";

    protected override ComponentDocument ToDocument(Component aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.Description);

    protected override Component ToAggregate(ComponentDocument document) =>
        Component.Restore(document.Id, document.Name, document.Description);

    protected override IEnumerable<string> SearchableTexts(ComponentDocument document) =>
        [document.Name, document.Description];
}
