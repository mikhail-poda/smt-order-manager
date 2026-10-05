using SmtOrderManager.Domain.Orders;
using SmtOrderManager.Infrastructure.Persistence.Json;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Json;

public sealed class JsonOrderRepositoryTests : IDisposable
{
    private static readonly Guid ControllerId = Guid.NewGuid();
    private static readonly Guid SensorId = Guid.NewGuid();
    private static readonly Guid BackplaneId = Guid.NewGuid();

    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 15, 123, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset DownloadedAt = new(2026, 10, 6, 14, 5, 0, TimeSpan.Zero);

    private readonly JsonStorageTestContext _context = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _context.Dispose();

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
        Assert.Equal([new OrderLine(ControllerId, 500), new OrderLine(SensorId, 200)], loaded.Lines);
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
    public async Task SaveAsync_WritesStatusAsName()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [new OrderLine(ControllerId, 500)]);
        order.MarkDownloaded(DownloadedAt);

        await CreateRepository().SaveAsync([order], Token);

        var json = await File.ReadAllTextAsync(Path.Combine(_context.DataDirectory, JsonOrderRepository.FileName), Token);
        Assert.Contains("\"status\": \"downloaded\"", json);
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

        Assert.Equal([weekly.Id, sensors.Id], found.Select(order => order.Id));
    }

    [Theory]
    [InlineData("draft", "\"2026-10-06T14:05:00+00:00\"")]
    [InlineData("downloaded", "null")]
    public async Task GetByIdAsync_WithStoredStatusContradictingDownloadTime_ThrowsInvalidDataException(
        string status,
        string downloadedAt)
    {
        var id = Guid.NewGuid();
        await WriteOrderAsync(id, $"\"{status}\"", downloadedAt);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateRepository().GetByIdAsync(id, Token));

        Assert.Contains($"order {id}", exception.Message);
    }

    [Theory]
    [InlineData("\"shipped\"")]
    [InlineData("1")]
    public async Task GetByIdAsync_WithUnknownOrNumericStoredStatus_ThrowsInvalidDataException(string status)
    {
        var id = Guid.NewGuid();
        await WriteOrderAsync(id, status, "null");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateRepository().GetByIdAsync(id, Token));
    }

    private static void AssertSameInstantAndOffset(DateTimeOffset expected, DateTimeOffset actual)
    {
        // DateTimeOffset equality compares only the instant, so the offset is checked separately.
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Offset, actual.Offset);
    }

    private Task WriteOrderAsync(Guid id, string status, string downloadedAt) =>
        _context.WriteFileAsync(
            JsonOrderRepository.FileName,
            $$"""
            [{
              "id": "{{id}}", "name": "Week 41", "description": "",
              "orderDate": "2026-10-05T08:30:00+02:00",
              "lines": [{ "boardId": "{{ControllerId}}", "quantity": 500 }],
              "status": {{status}}, "downloadedAt": {{downloadedAt}}
            }]
            """,
            Token);

    private JsonOrderRepository CreateRepository() =>
        new(_context.CreateStore<OrderDocument>(JsonOrderRepository.FileName));
}
