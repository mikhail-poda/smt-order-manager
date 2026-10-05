using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Infrastructure.Tests.Persistence;

/// <summary>
/// The order mapping and the order-specific queries of the repository contract. Each
/// persistence provider derives a test class from it.
/// </summary>
public abstract class OrderRepositoryContractTests
{
    private static readonly Guid ControllerId = Guid.NewGuid();
    private static readonly Guid SensorId = Guid.NewGuid();
    private static readonly Guid BackplaneId = Guid.NewGuid();

    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 15, 123, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset DownloadedAt = new(2026, 10, 6, 14, 5, 0, TimeSpan.Zero);

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsyncAfterRestart_RestoresEveryFieldOfDraft()
    {
        var order = Order.Create(
            "Week 41",
            "Controllers and sensors",
            IssuedAt,
            [new OrderLine(ControllerId, 500), new OrderLine(SensorId, 200)]);
        await CreateRepository().SaveAsync([order], Token);

        var loaded = await CreateRepository().GetByIdAsync(order.Id, Token);

        Assert.NotNull(loaded);
        Assert.Equal(order.Id, loaded.Id);
        Assert.Equal("Week 41", loaded.Name);
        Assert.Equal("Controllers and sensors", loaded.Description);
        AssertSameInstantAndOffset(IssuedAt, loaded.OrderDate);
        Assert.Equal(new[] { new OrderLine(ControllerId, 500), new OrderLine(SensorId, 200) }, loaded.Lines);
        Assert.Equal(OrderStatus.Draft, loaded.Status);
        Assert.Null(loaded.DownloadedAt);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsyncAfterRestart_RestoresDownloadedStatus()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [new OrderLine(ControllerId, 500)]);
        order.MarkDownloaded(DownloadedAt);
        await CreateRepository().SaveAsync([order], Token);

        var loaded = await CreateRepository().GetByIdAsync(order.Id, Token);

        Assert.NotNull(loaded);
        Assert.Equal(OrderStatus.Downloaded, loaded.Status);
        Assert.False(loaded.IsEditable);
        Assert.NotNull(loaded.DownloadedAt);
        AssertSameInstantAndOffset(DownloadedAt, loaded.DownloadedAt.Value);
    }

    [Fact]
    public async Task FindOrdersUsingBoardsAsync_ReturnsOrdersContainingAnyBoard()
    {
        var repository = CreateRepository();
        var weekly = Order.Create("Week 41", null, IssuedAt, [new OrderLine(ControllerId, 500), new OrderLine(SensorId, 200)]);
        var sensors = Order.Create("Sensors", null, IssuedAt, [new OrderLine(SensorId, 50)]);
        var backplanes = Order.Create("Backplanes", null, IssuedAt, [new OrderLine(BackplaneId, 10)]);
        await repository.SaveAsync([weekly, sensors, backplanes], Token);

        var found = await repository.FindOrdersUsingBoardsAsync([SensorId], Token);

        RepositoryAssert.SameIds(new[] { weekly.Id, sensors.Id }, found);
    }

    /// <summary>
    /// Creates a new repository instance on the test's storage, as after an application restart.
    /// </summary>
    protected abstract IOrderRepository CreateRepository();

    private static void AssertSameInstantAndOffset(DateTimeOffset expected, DateTimeOffset actual)
    {
        // DateTimeOffset equality compares only the instant, so the offset is checked separately.
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Offset, actual.Offset);
    }
}
