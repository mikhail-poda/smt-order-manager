using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Boards;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// Stores boards with their bills of materials in <see cref="FileName"/>.
/// </summary>
internal sealed class JsonBoardRepository(JsonFileStore<BoardDocument> store)
    : JsonRepository<Board, BoardDocument>(store), IBoardRepository
{
    public const string FileName = "boards.json";

    public Task<IReadOnlyList<Board>> FindBoardsUsingComponentsAsync(
        IReadOnlyCollection<Guid> componentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(componentIds);

        var wanted = componentIds.ToHashSet();

        return FindAsync(
            document => document.BillOfMaterials.Any(entry => wanted.Contains(entry.ComponentId)),
            cancellationToken);
    }

    protected override BoardDocument ToDocument(Board aggregate) =>
        new(
            aggregate.Id,
            aggregate.Name,
            aggregate.Description,
            aggregate.Length,
            aggregate.Width,
            [.. aggregate.BillOfMaterials.Select(entry => new BomEntryDocument(entry.ComponentId, entry.Quantity))]);

    protected override Board ToAggregate(BoardDocument document) =>
        Board.Restore(
            document.Id,
            document.Name,
            document.Description,
            document.Length,
            document.Width,
            document.BillOfMaterials.Select(entry => new BomEntry(entry.ComponentId, entry.Quantity)));

    protected override IEnumerable<string> SearchableTexts(BoardDocument document) =>
        [document.Name, document.Description];
}
