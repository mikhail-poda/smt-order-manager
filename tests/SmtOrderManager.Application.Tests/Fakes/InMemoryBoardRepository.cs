using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Boards;

namespace SmtOrderManager.Application.Tests.Fakes;

internal sealed class InMemoryBoardRepository : InMemoryRepository<Board>, IBoardRepository
{
    public Task<IReadOnlyList<Board>> FindBoardsUsingComponentsAsync(
        IReadOnlyCollection<Guid> componentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(componentIds);

        return Task.FromResult(
            Query(board => board.BillOfMaterials.Any(entry => componentIds.Contains(entry.ComponentId))));
    }

    protected override Board Copy(Board aggregate) =>
        Board.Restore(
            aggregate.Id,
            aggregate.Name,
            aggregate.Description,
            aggregate.Length,
            aggregate.Width,
            aggregate.BillOfMaterials);

    protected override IEnumerable<string> SearchableTexts(Board aggregate) =>
        [aggregate.Name, aggregate.Description];
}
