using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Application.Tests.Fakes;

internal sealed class InMemoryComponentRepository : InMemoryRepository<Component>, IComponentRepository
{
    protected override Component Copy(Component aggregate) =>
        Component.Restore(aggregate.Id, aggregate.Name, aggregate.Description);

    protected override IEnumerable<string> SearchableTexts(Component aggregate) =>
        [aggregate.Name, aggregate.Description];
}
