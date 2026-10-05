using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Boards;

namespace SmtOrderManager.Infrastructure.Tests.Persistence;

/// <summary>
/// The board mapping and the board-specific queries of the repository contract. Each
/// persistence provider derives a test class from it.
/// </summary>
public abstract class BoardRepositoryContractTests
{
    private static readonly Guid ResistorId = Guid.NewGuid();
    private static readonly Guid CapacitorId = Guid.NewGuid();
    private static readonly Guid InductorId = Guid.NewGuid();

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsyncAfterRestart_RestoresEveryField()
    {
        var board = Board.Create(
            "Controller",
            "Main controller board",
            100.25m,
            80.5m,
            [new BomEntry(ResistorId, 12), new BomEntry(CapacitorId, 3)]);
        await CreateRepository().SaveAsync([board], Token);

        var loaded = await CreateRepository().GetByIdAsync(board.Id, Token);

        Assert.NotNull(loaded);
        Assert.Equal(board.Id, loaded.Id);
        Assert.Equal("Controller", loaded.Name);
        Assert.Equal("Main controller board", loaded.Description);
        Assert.Equal(100.25m, loaded.Length);
        Assert.Equal(80.5m, loaded.Width);
        Assert.Equal(new[] { new BomEntry(ResistorId, 12), new BomEntry(CapacitorId, 3) }, loaded.BillOfMaterials);
    }

    [Fact]
    public async Task SaveAsync_AfterChangingBillOfMaterials_StoresNewEntries()
    {
        var repository = CreateRepository();
        var board = Board.Create("Controller", null, 100m, 80m, [new BomEntry(ResistorId, 12)]);
        await repository.SaveAsync([board], Token);

        board.SetBomEntry(ResistorId, 10);
        board.SetBomEntry(CapacitorId, 3);
        await repository.SaveAsync([board], Token);

        var loaded = await CreateRepository().GetByIdAsync(board.Id, Token);
        Assert.Equal(new[] { new BomEntry(ResistorId, 10), new BomEntry(CapacitorId, 3) }, loaded!.BillOfMaterials);
    }

    [Fact]
    public async Task FindBoardsUsingComponentsAsync_ReturnsBoardsContainingAnyComponent()
    {
        var repository = CreateRepository();
        var controller = Board.Create("Controller", null, 100m, 80m, [new BomEntry(ResistorId, 12)]);
        var sensor = Board.Create("Sensor", null, 40m, 30m, [new BomEntry(CapacitorId, 3)]);
        var power = Board.Create("Power", null, 60m, 50m, [new BomEntry(InductorId, 2)]);
        await repository.SaveAsync([controller, sensor, power], Token);

        var found = await repository.FindBoardsUsingComponentsAsync([ResistorId, CapacitorId], Token);

        RepositoryAssert.SameIds(new[] { controller.Id, sensor.Id }, found);
    }

    [Fact]
    public async Task FindBoardsUsingComponentsAsync_WithUnusedComponent_ReturnsEmpty()
    {
        var repository = CreateRepository();
        await repository.SaveAsync([Board.Create("Controller", null, 100m, 80m, [new BomEntry(ResistorId, 12)])], Token);

        Assert.Empty(await repository.FindBoardsUsingComponentsAsync([InductorId], Token));
    }

    [Fact]
    public async Task SearchAsync_MatchesDescription()
    {
        var repository = CreateRepository();
        var controller = Board.Create("Controller", "Main controller board", 100m, 80m, [new BomEntry(ResistorId, 12)]);
        await repository.SaveAsync(
            [controller, Board.Create("Sensor", null, 40m, 30m, [new BomEntry(CapacitorId, 3)])],
            Token);

        var found = await repository.SearchAsync("main", Token);

        RepositoryAssert.SameIds(new[] { controller.Id }, found);
    }

    /// <summary>
    /// Creates a new repository instance on the test's storage, as after an application restart.
    /// </summary>
    protected abstract IBoardRepository CreateRepository();
}
