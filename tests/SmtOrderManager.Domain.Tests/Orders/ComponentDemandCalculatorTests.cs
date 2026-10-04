using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Common;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Domain.Tests.Orders;

public sealed class ComponentDemandCalculatorTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));

    private readonly Guid _resistor = Guid.NewGuid();
    private readonly Guid _capacitor = Guid.NewGuid();
    private readonly Guid _led = Guid.NewGuid();

    [Fact]
    public void Calculate_WithExampleFromDomainModel_SumsResistorDemandAcrossBoards()
    {
        // 500 controller boards with 12 resistors each, 200 sensor boards with 3 resistors each.
        var controller = CreateBoard("Controller board", new BomEntry(_resistor, 12));
        var sensor = CreateBoard("Sensor board", new BomEntry(_resistor, 3));
        var order = CreateOrder(new OrderLine(controller.Id, 500), new OrderLine(sensor.Id, 200));

        var demand = ComponentDemandCalculator.Calculate(order, [controller, sensor]);

        // 500 × 12 + 200 × 3 = 6,600
        var resistorDemand = Assert.Single(demand);
        Assert.Equal(new ComponentDemand(_resistor, 6_600), resistorDemand);
    }

    [Fact]
    public void Calculate_WithDifferentComponents_KeepsThemSeparateInOrderOfFirstAppearance()
    {
        var controller = CreateBoard(
            "Controller board",
            new BomEntry(_resistor, 12),
            new BomEntry(_capacitor, 4));
        var sensor = CreateBoard(
            "Sensor board",
            new BomEntry(_resistor, 3),
            new BomEntry(_led, 2));
        var order = CreateOrder(new OrderLine(controller.Id, 500), new OrderLine(sensor.Id, 200));

        var demand = ComponentDemandCalculator.Calculate(order, [controller, sensor]);

        Assert.Equal(
            [
                new ComponentDemand(_resistor, 6_600),
                new ComponentDemand(_capacitor, 2_000),
                new ComponentDemand(_led, 400),
            ],
            demand);
    }

    [Fact]
    public void Calculate_WithAdditionalBoardsNotInOrder_IgnoresThem()
    {
        var controller = CreateBoard("Controller board", new BomEntry(_resistor, 12));
        var unrelated = CreateBoard("Unrelated board", new BomEntry(_capacitor, 100));
        var order = CreateOrder(new OrderLine(controller.Id, 500));

        var demand = ComponentDemandCalculator.Calculate(order, [controller, unrelated]);

        Assert.Equal([new ComponentDemand(_resistor, 6_000)], demand);
    }

    [Fact]
    public void Calculate_WithMissingBoard_ThrowsDomainException()
    {
        var controller = CreateBoard("Controller board", new BomEntry(_resistor, 12));
        var missingBoardId = Guid.NewGuid();
        var order = CreateOrder(new OrderLine(controller.Id, 500), new OrderLine(missingBoardId, 200));

        var exception = Assert.Throws<DomainException>(
            () => ComponentDemandCalculator.Calculate(order, [controller]));

        Assert.Contains(missingBoardId.ToString(), exception.Message);
    }

    [Fact]
    public void Calculate_WithMaximumQuantities_DoesNotOverflow()
    {
        var board = CreateBoard("Large board", new BomEntry(_resistor, int.MaxValue));
        var order = CreateOrder(new OrderLine(board.Id, int.MaxValue));

        var demand = ComponentDemandCalculator.Calculate(order, [board]);

        Assert.Equal((long)int.MaxValue * int.MaxValue, Assert.Single(demand).TotalQuantity);
    }

    private static Board CreateBoard(string name, params BomEntry[] billOfMaterials) =>
        Board.Create(name, null, 100m, 80m, billOfMaterials);

    private static Order CreateOrder(params OrderLine[] lines) =>
        Order.Create("Week 41", null, IssuedAt, lines);
}
