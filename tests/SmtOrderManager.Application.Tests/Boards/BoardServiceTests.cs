using Microsoft.Extensions.Logging.Abstractions;
using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Tests.Fakes;
using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Components;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Tests.Boards;

public sealed class BoardServiceTests
{
    private readonly InMemoryBoardRepository _boards = new();
    private readonly InMemoryComponentRepository _components = new();
    private readonly InMemoryOrderRepository _orders = new();
    private readonly Component _resistor = Component.Create("RES-10K-0402", null);
    private readonly Component _capacitor = Component.Create("CAP-100N-0402", null);
    private readonly Component _led = Component.Create("LED-RED-0603", null);
    private readonly BoardService _service;

    public BoardServiceTests()
    {
        _components.Seed(_resistor, _capacitor, _led);
        _service = new BoardService(_boards, _components, _orders, NullLogger<BoardService>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_WithValidBatch_SavesAllAndReturnsComponentNames()
    {
        var result = await _service.CreateAsync(
            [
                new CreateBoardCommand("Controller board", "Main controller", 160m, 100m, [new BomEntryInput(_resistor.Id, 12), new BomEntryInput(_capacitor.Id, 4)]),
                new CreateBoardCommand("Sensor board", null, 50m, 30m, [new BomEntryInput(_resistor.Id, 3)]),
            ],
            Token);

        Assert.True(result.Succeeded);
        Assert.Equal(2, _boards.Count);
        var controller = result.Value[0];
        Assert.Equal("Controller board", controller.Name);
        Assert.Equal(
            [
                new BomEntryDetails(_resistor.Id, "RES-10K-0402", 12),
                new BomEntryDetails(_capacitor.Id, "CAP-100N-0402", 4)
            ],
            controller.BillOfMaterials);
    }

    [Fact]
    public async Task CreateAsync_WithMissingComponent_SavesNothingAndReportsIt()
    {
        var missingId = Guid.NewGuid();

        var result = await _service.CreateAsync(
            [
                new CreateBoardCommand("Controller board", null, 160m, 100m, [new BomEntryInput(_resistor.Id, 12)]),
                new CreateBoardCommand("Sensor board", null, 50m, 30m, [new BomEntryInput(_resistor.Id, 3), new BomEntryInput(missingId, 2)]),
            ],
            Token);

        Assert.Equal([new Violation("Item 2", $"Component {missingId} not found.")], result.Violations);
        Assert.Equal(0, _boards.Count);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateComponent_ReportsComponentByName()
    {
        var result = await _service.CreateAsync(
            [new CreateBoardCommand("Controller board", null, 160m, 100m, [new BomEntryInput(_resistor.Id, 12), new BomEntryInput(_resistor.Id, 3)])],
            Token);

        Assert.Equal(
            [new Violation("Item 1", "Component 'RES-10K-0402' appears more than once in the bill of materials.")],
            result.Violations);
        Assert.Equal(0, _boards.Count);
    }

    [Fact]
    public async Task CreateAsync_WithSeveralInvalidBoards_ReportsEveryBoard()
    {
        var result = await _service.CreateAsync(
            [
                new CreateBoardCommand("Controller board", null, 160m, 100m, []),
                new CreateBoardCommand("Sensor board", null, -1m, 30m, [new BomEntryInput(_resistor.Id, 3)]),
                new CreateBoardCommand("Display board", null, 80m, 40m, [new BomEntryInput(_led.Id, 0)]),
            ],
            Token);

        Assert.Equal(
            [
                new Violation("Item 1", "A board must contain at least one BOM entry."),
                new Violation("Item 2", "Length must be positive, but was -1."),
                new Violation("Item 3", "Quantity must be positive, but was 0.")
            ],
            result.Violations);
        Assert.Equal(0, _boards.Count);
    }

    [Fact]
    public async Task UpdateAsync_WithNewValues_ReplacesValuesAndBillOfMaterials()
    {
        var board = Board.Create(
            "Controller board",
            null,
            160m,
            100m,
            [new BomEntry(_resistor.Id, 12), new BomEntry(_capacitor.Id, 4)]);
        _boards.Seed(board);

        var result = await _service.UpdateAsync(
            [new UpdateBoardCommand(board.Id, "Controller board B", "Revision B", 170m, 110m, [new BomEntryInput(_capacitor.Id, 6), new BomEntryInput(_led.Id, 2)])],
            Token);

        Assert.True(result.Succeeded);
        var stored = await _boards.GetByIdAsync(board.Id, Token);
        Assert.Equal("Controller board B", stored!.Name);
        Assert.Equal("Revision B", stored.Description);
        Assert.Equal(170m, stored.Length);
        Assert.Equal(110m, stored.Width);
        Assert.Equal(
            [new BomEntry(_capacitor.Id, 6), new BomEntry(_led.Id, 2)],
            stored.BillOfMaterials);
    }

    [Fact]
    public async Task UpdateAsync_WithSeveralInvalidValues_ReportsAllAndKeepsStoredBoard()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [new BomEntry(_resistor.Id, 12)]);
        _boards.Seed(board);
        var missingId = Guid.NewGuid();

        var result = await _service.UpdateAsync(
            [new UpdateBoardCommand(board.Id, " ", null, 160m, 0m, [new BomEntryInput(missingId, 1)])],
            Token);

        Assert.Equal(
            [
                new Violation("Board 'Controller board'", $"Component {missingId} not found."),
                new Violation("Board 'Controller board'", "Name must not be blank."),
                new Violation("Board 'Controller board'", "Width must be positive, but was 0.")
            ],
            result.Violations);
        var stored = await _boards.GetByIdAsync(board.Id, Token);
        Assert.Equal("Controller board", stored!.Name);
        Assert.Equal(100m, stored.Width);
        Assert.Equal([new BomEntry(_resistor.Id, 12)], stored.BillOfMaterials);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownBoard_SavesNothingAndReportsIt()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [new BomEntry(_resistor.Id, 12)]);
        _boards.Seed(board);
        var unknownId = Guid.NewGuid();

        var result = await _service.UpdateAsync(
            [
                new UpdateBoardCommand(board.Id, "Controller board B", null, 160m, 100m, [new BomEntryInput(_resistor.Id, 12)]),
                new UpdateBoardCommand(unknownId, "Sensor board", null, 50m, 30m, [new BomEntryInput(_resistor.Id, 3)]),
            ],
            Token);

        Assert.Equal([new Violation($"Board {unknownId}", "Board not found.")], result.Violations);
        Assert.Equal("Controller board", (await _boards.GetByIdAsync(board.Id, Token))!.Name);
    }

    [Fact]
    public async Task SearchAsync_WithText_ReturnsMatchesSortedByNameWithComponentNames()
    {
        _boards.Seed(
            Board.Create("Sensor board", null, 50m, 30m, [new BomEntry(_resistor.Id, 3)]),
            Board.Create("Display", "LED matrix", 80m, 40m, [new BomEntry(_led.Id, 64)]),
            Board.Create("Controller board", null, 160m, 100m, [new BomEntry(_capacitor.Id, 4)]));

        var result = await _service.SearchAsync("board", Token);

        Assert.Equal(["Controller board", "Sensor board"], result.Select(details => details.Name));
        Assert.Equal("CAP-100N-0402", Assert.Single(result[0].BillOfMaterials).ComponentName);
    }

    [Fact]
    public async Task RemoveAsync_WithExistingBoard_RemovesIt()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [new BomEntry(_resistor.Id, 12)]);
        _boards.Seed(board);

        var result = await _service.RemoveAsync([board.Id], Token);

        Assert.Equal(1, result.Value);
        Assert.Equal(0, _boards.Count);
    }

    [Fact]
    public async Task RemoveAsync_WithUnknownBoard_RemovesNothingAndReportsIt()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [new BomEntry(_resistor.Id, 12)]);
        _boards.Seed(board);
        var unknownId = Guid.NewGuid();

        var result = await _service.RemoveAsync([board.Id, unknownId], Token);

        Assert.Equal([new Violation($"Board {unknownId}", "Board not found.")], result.Violations);
        Assert.Equal(1, _boards.Count);
    }

    [Fact]
    public async Task RemoveAsync_WithBoardUsedByOrders_RemovesNothingAndReportsEveryOrder()
    {
        var issuedAt = new DateTimeOffset(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));
        var controller = Board.Create("Controller board", null, 160m, 100m, [new BomEntry(_resistor.Id, 12)]);
        var unused = Board.Create("Sensor board", null, 50m, 30m, [new BomEntry(_resistor.Id, 3)]);
        _boards.Seed(controller, unused);
        var downloaded = Order.Create("Week 40", null, issuedAt, [new OrderLine(controller.Id, 100)]);
        downloaded.MarkDownloaded(issuedAt.AddHours(2));
        _orders.Seed(
            Order.Create("Week 41", null, issuedAt, [new OrderLine(controller.Id, 500)]),
            downloaded);

        var result = await _service.RemoveAsync([unused.Id, controller.Id], Token);

        Assert.Equal(
            [
                new Violation("Board 'Controller board'", "Board is used by order 'Week 40'."),
                new Violation("Board 'Controller board'", "Board is used by order 'Week 41'.")
            ],
            result.Violations);
        Assert.Equal(2, _boards.Count);
    }
}