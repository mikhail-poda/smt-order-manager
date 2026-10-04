using Microsoft.Extensions.Logging.Abstractions;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Orders;
using SmtOrderManager.Application.Tests.Fakes;
using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Tests.Orders;

public sealed class OrderServiceTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));

    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryBoardRepository _boards = new();
    private readonly Board _controller = CreateBoard("Controller board");
    private readonly Board _sensor = CreateBoard("Sensor board");
    private readonly Board _display = CreateBoard("Display board");
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _boards.Seed(_controller, _sensor, _display);
        _service = new OrderService(_orders, _boards, NullLogger<OrderService>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_WithValidBatch_SavesDraftsAndReturnsBoardNames()
    {
        var result = await _service.CreateAsync(
            [
                new CreateOrderCommand("Week 41", "Controller and sensor boards", IssuedAt, [new OrderLineInput(_controller.Id, 500), new OrderLineInput(_sensor.Id, 200)]),
                new CreateOrderCommand("Week 42", null, IssuedAt.AddDays(7), [new OrderLineInput(_display.Id, 50)]),
            ],
            Token);

        Assert.True(result.Succeeded);
        Assert.Equal(2, _orders.Count);
        var week41 = result.Value[0];
        Assert.Equal(OrderStatus.Draft, week41.Status);
        Assert.Null(week41.DownloadedAt);
        Assert.Equal(
            [
                new OrderLineDetails(_controller.Id, "Controller board", 500),
                new OrderLineDetails(_sensor.Id, "Sensor board", 200)
            ],
            week41.Lines);
    }

    [Fact]
    public async Task CreateAsync_WithMissingBoard_SavesNothingAndReportsIt()
    {
        var missingId = Guid.NewGuid();

        var result = await _service.CreateAsync(
            [
                new CreateOrderCommand("Week 41", null, IssuedAt, [new OrderLineInput(_controller.Id, 500)]),
                new CreateOrderCommand("Week 42", null, IssuedAt, [new OrderLineInput(_sensor.Id, 200), new OrderLineInput(missingId, 10)]),
            ],
            Token);

        Assert.Equal([new Violation("Item 2", $"Board {missingId} not found.")], result.Violations);
        Assert.Equal(0, _orders.Count);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateBoard_ReportsBoardByName()
    {
        var result = await _service.CreateAsync(
            [new CreateOrderCommand("Week 41", null, IssuedAt, [new OrderLineInput(_controller.Id, 500), new OrderLineInput(_controller.Id, 200)])],
            Token);

        Assert.Equal(
            [new Violation("Item 1", "Board 'Controller board' appears more than once in the order.")],
            result.Violations);
        Assert.Equal(0, _orders.Count);
    }

    [Fact]
    public async Task CreateAsync_WithSeveralInvalidOrders_ReportsEveryOrder()
    {
        var result = await _service.CreateAsync(
            [
                new CreateOrderCommand("Week 41", null, IssuedAt, []),
                new CreateOrderCommand("Week 42", null, default, [new OrderLineInput(_sensor.Id, 200)]),
                new CreateOrderCommand("Week 43", null, IssuedAt, [new OrderLineInput(_display.Id, 0)]),
            ],
            Token);

        Assert.Equal(
            [
                new Violation("Item 1", "An order must contain at least one order line."),
                new Violation("Item 2", "OrderDate must be set."),
                new Violation("Item 3", "Quantity must be positive, but was 0.")
            ],
            result.Violations);
        Assert.Equal(0, _orders.Count);
    }

    [Fact]
    public async Task UpdateAsync_WithNewValues_ReplacesValuesAndLines()
    {
        var order = Order.Create(
            "Week 41",
            null,
            IssuedAt,
            [new OrderLine(_controller.Id, 500), new OrderLine(_sensor.Id, 200)]);
        _orders.Seed(order);

        var result = await _service.UpdateAsync(
            [new UpdateOrderCommand(order.Id, "Week 41 rush", "Rush order", IssuedAt.AddDays(1), [new OrderLineInput(_sensor.Id, 300), new OrderLineInput(_display.Id, 50)])],
            Token);

        Assert.True(result.Succeeded);
        var stored = await _orders.GetByIdAsync(order.Id, Token);
        Assert.Equal("Week 41 rush", stored!.Name);
        Assert.Equal("Rush order", stored.Description);
        Assert.Equal(IssuedAt.AddDays(1), stored.OrderDate);
        Assert.Equal([new OrderLine(_sensor.Id, 300), new OrderLine(_display.Id, 50)], stored.Lines);
    }

    [Fact]
    public async Task UpdateAsync_WithDownloadedOrder_SavesNothingAndReportsIt()
    {
        var draft = Order.Create("Week 41", null, IssuedAt, [new OrderLine(_controller.Id, 500)]);
        var downloaded = Order.Create("Week 40", null, IssuedAt, [new OrderLine(_sensor.Id, 200)]);
        downloaded.MarkDownloaded(IssuedAt.AddHours(2));
        _orders.Seed(draft, downloaded);

        var result = await _service.UpdateAsync(
            [
                new UpdateOrderCommand(draft.Id, "Week 41 rush", null, IssuedAt, [new OrderLineInput(_controller.Id, 500)]),
                new UpdateOrderCommand(downloaded.Id, "Week 40 rush", null, IssuedAt, [new OrderLineInput(_sensor.Id, 200)]),
            ],
            Token);

        Assert.Equal(
            [new Violation("Order 'Week 40'", "Order has been downloaded and can no longer be changed.")],
            result.Violations);
        Assert.Equal("Week 41", (await _orders.GetByIdAsync(draft.Id, Token))!.Name);
        Assert.Equal("Week 40", (await _orders.GetByIdAsync(downloaded.Id, Token))!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownOrder_SavesNothingAndReportsIt()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [new OrderLine(_controller.Id, 500)]);
        _orders.Seed(order);
        var unknownId = Guid.NewGuid();

        var result = await _service.UpdateAsync(
            [
                new UpdateOrderCommand(order.Id, "Week 41 rush", null, IssuedAt, [new OrderLineInput(_controller.Id, 500)]),
                new UpdateOrderCommand(unknownId, "Week 42", null, IssuedAt, [new OrderLineInput(_sensor.Id, 200)]),
            ],
            Token);

        Assert.Equal([new Violation($"Order {unknownId}", "Order not found.")], result.Violations);
        Assert.Equal("Week 41", (await _orders.GetByIdAsync(order.Id, Token))!.Name);
    }

    [Fact]
    public async Task SearchAsync_WithText_ReturnsMatchesNewestFirstWithStatus()
    {
        var downloaded = Order.Create("Week 40", null, IssuedAt.AddDays(-7), [new OrderLine(_controller.Id, 100)]);
        downloaded.MarkDownloaded(IssuedAt);
        _orders.Seed(
            downloaded,
            Order.Create("Week 41", null, IssuedAt, [new OrderLine(_sensor.Id, 200)]),
            Order.Create("Prototype", "Display samples", IssuedAt, [new OrderLine(_display.Id, 5)]));

        var result = await _service.SearchAsync("week", Token);

        Assert.Equal(["Week 41", "Week 40"], result.Select(details => details.Name));
        Assert.Equal(OrderStatus.Downloaded, result[1].Status);
        Assert.Equal("Controller board", Assert.Single(result[1].Lines).BoardName);
    }

    [Fact]
    public async Task RemoveAsync_WithDraftOrder_RemovesIt()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [new OrderLine(_controller.Id, 500)]);
        _orders.Seed(order);

        var result = await _service.RemoveAsync([order.Id], Token);

        Assert.Equal(1, result.Value);
        Assert.Equal(0, _orders.Count);
    }

    [Fact]
    public async Task RemoveAsync_WithDownloadedOrder_RemovesNothingAndReportsIt()
    {
        var draft = Order.Create("Week 41", null, IssuedAt, [new OrderLine(_controller.Id, 500)]);
        var downloaded = Order.Create("Week 40", null, IssuedAt, [new OrderLine(_sensor.Id, 200)]);
        downloaded.MarkDownloaded(IssuedAt.AddHours(2));
        _orders.Seed(draft, downloaded);

        var result = await _service.RemoveAsync([draft.Id, downloaded.Id], Token);

        Assert.Equal(
            [new Violation("Order 'Week 40'", "Order has been downloaded and can no longer be removed.")],
            result.Violations);
        Assert.Equal(2, _orders.Count);
    }

    private static Board CreateBoard(string name) =>
        Board.Create(name, null, 100m, 80m, [new BomEntry(Guid.NewGuid(), 1)]);
}
