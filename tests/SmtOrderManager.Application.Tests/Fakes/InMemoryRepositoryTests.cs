using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Components;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Tests.Fakes;

/// <summary>
/// Checks that the fakes honour the repository contract, because the use case tests rely on it.
/// </summary>
public sealed class InMemoryRepositoryTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_ReturnsCopyWithSameValues()
    {
        var repository = new InMemoryComponentRepository();
        var component = Component.Create("RES-10K-0402", "Resistor 10 kΩ");

        await repository.SaveAsync([component], Token);
        var loaded = await repository.GetByIdAsync(component.Id, Token);

        Assert.NotNull(loaded);
        Assert.NotSame(component, loaded);
        Assert.Equal(component.Id, loaded.Id);
        Assert.Equal("RES-10K-0402", loaded.Name);
        Assert.Equal("Resistor 10 kΩ", loaded.Description);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var repository = new InMemoryComponentRepository();

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), Token));
    }

    [Fact]
    public async Task GetByIdAsync_AfterChangingLoadedAggregateWithoutSaving_ReturnsStoredValues()
    {
        var repository = new InMemoryComponentRepository();
        var component = Component.Create("RES-10K-0402", null);
        repository.Seed(component);

        var loaded = await repository.GetByIdAsync(component.Id, Token);
        loaded!.Rename("RES-10K-0603");
        var reloaded = await repository.GetByIdAsync(component.Id, Token);

        Assert.Equal("RES-10K-0402", reloaded!.Name);
    }

    [Fact]
    public async Task SaveAsync_WithExistingId_ReplacesStoredAggregate()
    {
        var repository = new InMemoryComponentRepository();
        var component = Component.Create("RES-10K-0402", null);
        repository.Seed(component);

        component.Rename("RES-10K-0603");
        await repository.SaveAsync([component], Token);

        Assert.Equal(1, repository.Count);
        Assert.Equal("RES-10K-0603", (await repository.GetByIdAsync(component.Id, Token))!.Name);
    }

    [Fact]
    public async Task GetByIdsAsync_WithUnknownIds_ReturnsOnlyStoredAggregates()
    {
        var repository = new InMemoryComponentRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        repository.Seed(resistor, capacitor);

        var loaded = await repository.GetByIdsAsync([capacitor.Id, Guid.NewGuid(), resistor.Id], Token);

        Assert.Equal([capacitor.Id, resistor.Id], loaded.Select(component => component.Id));
    }

    [Fact]
    public async Task RemoveAsync_WithStoredAndUnknownIds_RemovesStoredAggregates()
    {
        var repository = new InMemoryComponentRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        repository.Seed(resistor, capacitor);

        await repository.RemoveAsync([resistor.Id, Guid.NewGuid()], Token);

        Assert.Null(await repository.GetByIdAsync(resistor.Id, Token));
        Assert.NotNull(await repository.GetByIdAsync(capacitor.Id, Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WithoutText_ReturnsAllInInsertionOrder(string? text)
    {
        var repository = new InMemoryComponentRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        repository.Seed(resistor, capacitor);

        var result = await repository.SearchAsync(text, Token);

        Assert.Equal([resistor.Id, capacitor.Id], result.Select(component => component.Id));
    }

    [Theory]
    [InlineData("res", "RES-10K-0402")]
    [InlineData(" red led ", "LED-RED-0603")]
    public async Task SearchAsync_WithText_MatchesNameOrDescriptionIgnoringCase(string text, string expectedName)
    {
        var repository = new InMemoryComponentRepository();
        repository.Seed(
            Component.Create("RES-10K-0402", "Resistor 10 kΩ"),
            Component.Create("CAP-100N-0402", "Capacitor 100 nF"),
            Component.Create("LED-RED-0603", "Red LED"));

        var result = await repository.SearchAsync(text, Token);

        Assert.Equal([expectedName], result.Select(component => component.Name));
    }

    [Fact]
    public async Task FindBoardsUsingComponentsAsync_ReturnsBoardsContainingAnyComponent()
    {
        var resistor = Guid.NewGuid();
        var capacitor = Guid.NewGuid();
        var led = Guid.NewGuid();
        var controller = Board.Create("Controller board", null, 160m, 100m, [new BomEntry(resistor, 12)]);
        var sensor = Board.Create("Sensor board", null, 50m, 30m, [new BomEntry(capacitor, 4)]);
        var display = Board.Create("Display board", null, 80m, 40m, [new BomEntry(led, 8)]);
        var repository = new InMemoryBoardRepository();
        repository.Seed(controller, sensor, display);

        var result = await repository.FindBoardsUsingComponentsAsync([resistor, capacitor], Token);

        Assert.Equal([controller.Id, sensor.Id], result.Select(board => board.Id));
        Assert.Equal([new BomEntry(resistor, 12)], result[0].BillOfMaterials);
    }

    [Fact]
    public async Task FindOrdersUsingBoardsAsync_ReturnsOrdersContainingAnyBoard()
    {
        var controller = Guid.NewGuid();
        var sensor = Guid.NewGuid();
        var week41 = Order.Create("Week 41", null, IssuedAt, [new OrderLine(controller, 500)]);
        var week42 = Order.Create("Week 42", null, IssuedAt, [new OrderLine(sensor, 200)]);
        var repository = new InMemoryOrderRepository();
        repository.Seed(week41, week42);

        var result = await repository.FindOrdersUsingBoardsAsync([controller], Token);

        Assert.Equal([week41.Id], result.Select(order => order.Id));
    }

    [Fact]
    public async Task GetByIdAsync_WithDownloadedOrder_KeepsStatusAndDownloadTime()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [new OrderLine(Guid.NewGuid(), 500)]);
        order.MarkDownloaded(IssuedAt.AddHours(2));
        var repository = new InMemoryOrderRepository();
        repository.Seed(order);

        var loaded = await repository.GetByIdAsync(order.Id, Token);

        Assert.Equal(OrderStatus.Downloaded, loaded!.Status);
        Assert.Equal(IssuedAt.AddHours(2), loaded.DownloadedAt);
    }
}
