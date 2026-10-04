using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Components;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Tests.Downloads;

public sealed class OrderDownloadPayloadMapperTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset GeneratedAt = new(2026, 10, 6, 6, 0, 0, TimeSpan.FromHours(2));

    private readonly Component _resistor = Component.Create("RES-10K-0402", "Resistor 10 kΩ");
    private readonly Component _capacitor = Component.Create("CAP-100N-0402", null);
    private readonly Component _unrelatedComponent = Component.Create("LED-RED-0603", null);
    private readonly Board _controller;
    private readonly Board _sensor;
    private readonly Board _unrelatedBoard;
    private readonly Order _order;

    public OrderDownloadPayloadMapperTests()
    {
        _controller = Board.Create(
            "Controller board",
            "Main controller",
            160m,
            100m,
            [new BomEntry(_resistor.Id, 12), new BomEntry(_capacitor.Id, 4)]);
        _sensor = Board.Create("Sensor board", null, 50m, 30m, [new BomEntry(_resistor.Id, 3)]);
        _unrelatedBoard = Board.Create("Display board", null, 80m, 40m, [new BomEntry(_unrelatedComponent.Id, 8)]);
        _order = Order.Create(
            "Week 41",
            "Controller and sensor boards",
            IssuedAt,
            [new OrderLine(_sensor.Id, 200), new OrderLine(_controller.Id, 500)]);
    }

    [Fact]
    public void Map_WithOrder_SetsSchemaVersionAndGenerationTime()
    {
        var payload = Map();

        Assert.Equal(DownloadPayloadSchema.CurrentVersion, payload.SchemaVersion);
        Assert.Equal(1, payload.SchemaVersion);
        Assert.Equal(GeneratedAt, payload.GeneratedAt);
    }

    [Fact]
    public void Map_WithOrder_MapsOrderFieldsAndLines()
    {
        var order = Map().Order;

        Assert.Equal(_order.Id, order.Id);
        Assert.Equal("Week 41", order.Name);
        Assert.Equal("Controller and sensor boards", order.Description);
        Assert.Equal(IssuedAt, order.OrderDate);
        Assert.Equal(
            [new OrderLinePayload(_sensor.Id, 200), new OrderLinePayload(_controller.Id, 500)],
            order.Lines);
    }

    [Fact]
    public void Map_WithAdditionalBoards_IncludesReferencedBoardsInLineOrder()
    {
        var boards = Map().Boards;

        Assert.Equal([_sensor.Id, _controller.Id], boards.Select(board => board.Id));

        var controller = boards[1];
        Assert.Equal("Controller board", controller.Name);
        Assert.Equal("Main controller", controller.Description);
        Assert.Equal(160m, controller.LengthMm);
        Assert.Equal(100m, controller.WidthMm);
        Assert.Equal(
            [
                new BomEntryPayload(_resistor.Id, "RES-10K-0402", 12),
                new BomEntryPayload(_capacitor.Id, "CAP-100N-0402", 4)
            ],
            controller.BillOfMaterials);
    }

    [Fact]
    public void Map_WithDemand_IncludesComponentNames()
    {
        var payload = Map();

        // 200 × 3 + 500 × 12 = 6,600 resistors; 500 × 4 = 2,000 capacitors
        Assert.Equal(
            [
                new ComponentDemandPayload(_resistor.Id, "RES-10K-0402", 6_600),
                new ComponentDemandPayload(_capacitor.Id, "CAP-100N-0402", 2_000)
            ],
            payload.ComponentDemand);
    }

    [Fact]
    public void Map_WithMissingBoard_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => OrderDownloadPayloadMapper.Map(
            _order,
            [_controller],
            [_resistor, _capacitor],
            [],
            GeneratedAt));

        Assert.Contains(_sensor.Id.ToString(), exception.Message);
    }

    [Fact]
    public void Map_WithMissingComponent_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => OrderDownloadPayloadMapper.Map(
            _order,
            [_controller, _sensor],
            [_resistor],
            [],
            GeneratedAt));

        Assert.Contains(_capacitor.Id.ToString(), exception.Message);
    }

    private OrderDownloadPayload Map()
    {
        Board[] boards = [_controller, _sensor, _unrelatedBoard];

        return OrderDownloadPayloadMapper.Map(
            _order,
            boards,
            [_resistor, _capacitor, _unrelatedComponent],
            ComponentDemandCalculator.Calculate(_order, boards),
            GeneratedAt);
    }
}
