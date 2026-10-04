using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Tests.Fakes;
using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Components;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Tests.Downloads;

public sealed class OrderDownloadServiceTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 4, 0, 0, TimeSpan.Zero);

    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryBoardRepository _boards = new();
    private readonly InMemoryComponentRepository _components = new();
    private readonly FakeSmtLine _line = new();
    private readonly Order _order;
    private readonly OrderDownloadService _service;

    public OrderDownloadServiceTests()
    {
        var resistor = Component.Create("RES-10K-0402", null);
        var controller = Board.Create("Controller board", null, 160m, 100m, [new BomEntry(resistor.Id, 12)]);
        var sensor = Board.Create("Sensor board", null, 50m, 30m, [new BomEntry(resistor.Id, 3)]);
        _order = Order.Create(
            "Week 41",
            null,
            IssuedAt,
            [new OrderLine(controller.Id, 500), new OrderLine(sensor.Id, 200)]);

        _components.Seed(resistor);
        _boards.Seed(controller, sensor);
        _orders.Seed(_order);

        _service = new OrderDownloadService(
            _orders,
            _boards,
            _components,
            _line,
            new FixedTimeProvider(Now),
            NullLogger<OrderDownloadService>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DownloadAsync_WhenLineAccepts_MarksOrderDownloadedAndReturnsAnswer()
    {
        var result = await _service.DownloadAsync(_order.Id, Token);

        var answer = result.Value;
        Assert.True(answer.Accepted);
        Assert.Equal(_order.Id, answer.OrderId);
        Assert.Equal("Week 41", answer.OrderName);
        Assert.Equal(FakeSmtLine.LineId, answer.LineId);
        Assert.Equal(FakeSmtLine.ReceivedAt, answer.ReceivedAt);
        Assert.Equal(FakeSmtLine.JobReference, answer.JobReference);
        Assert.Empty(answer.Reasons);

        var stored = await _orders.GetByIdAsync(_order.Id, Token);
        Assert.Equal(OrderStatus.Downloaded, stored!.Status);
        Assert.Equal(FakeSmtLine.ReceivedAt, stored.DownloadedAt);
    }

    [Fact]
    public async Task DownloadAsync_WithOrder_SendsSerializedPayload()
    {
        await _service.DownloadAsync(_order.Id, Token);

        var payload = JsonNode.Parse(Assert.Single(_line.ReceivedPayloads))!;
        Assert.Equal(DownloadPayloadSchema.CurrentVersion, payload["schemaVersion"]!.GetValue<int>());
        Assert.Equal(Now, payload["generatedAt"]!.GetValue<DateTimeOffset>());
        Assert.Equal(_order.Id, payload["order"]!["id"]!.GetValue<Guid>());
        Assert.Equal(2, payload["boards"]!.AsArray().Count);

        // 500 × 12 + 200 × 3 = 6,600
        Assert.Equal(6_600L, payload["componentDemand"]![0]!["totalQuantity"]!.GetValue<long>());
    }

    [Fact]
    public async Task DownloadAsync_WhenLineRejects_KeepsOrderDraftAndReturnsReasons()
    {
        const string reason = "Board 'Controller board' is 160 mm long, the line supports at most 150 mm.";
        _line.RejectionReasons = [reason];

        var result = await _service.DownloadAsync(_order.Id, Token);

        Assert.True(result.Succeeded);
        Assert.False(result.Value.Accepted);
        Assert.Equal([reason], result.Value.Reasons);
        Assert.Null(result.Value.JobReference);

        var stored = await _orders.GetByIdAsync(_order.Id, Token);
        Assert.Equal(OrderStatus.Draft, stored!.Status);
        Assert.Null(stored.DownloadedAt);
    }

    [Fact]
    public async Task DownloadAsync_WhenLineUnavailable_ThrowsAndKeepsOrderDraft()
    {
        _line.IsUnavailable = true;

        await Assert.ThrowsAsync<SmtLineUnavailableException>(() => _service.DownloadAsync(_order.Id, Token));

        Assert.Equal(OrderStatus.Draft, (await _orders.GetByIdAsync(_order.Id, Token))!.Status);
    }

    [Fact]
    public async Task DownloadAsync_WithDownloadedOrder_DownloadsAgainAndRecordsNewTime()
    {
        var downloaded = Order.Create("Week 40", null, IssuedAt, _order.Lines);
        downloaded.MarkDownloaded(IssuedAt.AddHours(2));
        _orders.Seed(downloaded);

        var result = await _service.DownloadAsync(downloaded.Id, Token);

        Assert.True(result.Value.Accepted);
        Assert.Equal(FakeSmtLine.ReceivedAt, (await _orders.GetByIdAsync(downloaded.Id, Token))!.DownloadedAt);
    }

    [Fact]
    public async Task DownloadAsync_WithUnknownOrder_ReturnsViolationAndSendsNothing()
    {
        var unknownId = Guid.NewGuid();

        var result = await _service.DownloadAsync(unknownId, Token);

        Assert.Equal([new Violation($"Order {unknownId}", "Order not found.")], result.Violations);
        Assert.Empty(_line.ReceivedPayloads);
    }

    [Fact]
    public async Task DownloadAsync_WithMissingBoard_ReturnsViolationAndSendsNothing()
    {
        var missingBoardId = Guid.NewGuid();
        var order = Order.Create("Week 42", null, IssuedAt, [new OrderLine(missingBoardId, 10)]);
        _orders.Seed(order);

        var result = await _service.DownloadAsync(order.Id, Token);

        Assert.Equal(
            [new Violation("Order 'Week 42'", $"Board {missingBoardId} not found.")],
            result.Violations);
        Assert.Empty(_line.ReceivedPayloads);
        Assert.Equal(OrderStatus.Draft, (await _orders.GetByIdAsync(order.Id, Token))!.Status);
    }
}
