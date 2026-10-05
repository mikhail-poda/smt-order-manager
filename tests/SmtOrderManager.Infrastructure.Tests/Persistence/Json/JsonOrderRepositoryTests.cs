using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;
using SmtOrderManager.Infrastructure.Persistence.Json;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Json;

/// <summary>
/// Runs the order repository contract against JSON files and covers the stored status format
/// and damaged order data.
/// </summary>
public sealed class JsonOrderRepositoryTests : OrderRepositoryContractTests, IDisposable
{
    private static readonly Guid ControllerId = Guid.NewGuid();

    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 15, 123, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset DownloadedAt = new(2026, 10, 6, 14, 5, 0, TimeSpan.Zero);

    private readonly JsonStorageTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SaveAsync_WritesStatusAsName()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [new OrderLine(ControllerId, 500)]);
        order.MarkDownloaded(DownloadedAt);

        await CreateRepository().SaveAsync([order], Token);

        var json = await File.ReadAllTextAsync(Path.Combine(_context.DataDirectory, JsonOrderRepository.FileName), Token);
        Assert.Contains("\"status\": \"downloaded\"", json);
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

    protected override IOrderRepository CreateRepository() =>
        new JsonOrderRepository(_context.CreateStore<OrderDocument>(JsonOrderRepository.FileName));

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
}
