using System.Text.Json.Nodes;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Components;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Tests.Downloads;

/// <summary>
/// Pins the download payload format to the approved example in
/// <c>Contracts/order-download.v1.json</c>, which also documents the format for consumers.
/// </summary>
/// <remarks>
/// <para>
/// The fixture goes through the same steps as a real download: domain model, demand
/// calculation, mapping and serialization. Any change to a field name, type or value fails this
/// test. Whitespace and property order do not matter.
/// </para>
/// <para>
/// When this test fails, decide whether the change is intended. An additive change keeps
/// version 1: update the approved file. Any other change is breaking: increase
/// <see cref="DownloadPayloadSchema.CurrentVersion"/> and add a new approved file.
/// </para>
/// <para>
/// The identifiers are fixed and readable: <c>0a…</c> is the order, <c>0b…</c> are boards and
/// <c>0c…</c> are components.
/// </para>
/// </remarks>
public sealed class DownloadPayloadContractTests
{
    private static readonly string ApprovedFile =
        Path.Combine(AppContext.BaseDirectory, "Contracts", "order-download.v1.json");

    [Fact]
    public async Task Serialize_WithFixture_MatchesApprovedVersion1Example()
    {
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(ApprovedFile, TestContext.Current.CancellationToken));

        var json = DownloadPayloadSerializer.Serialize(CreateFixture());

        Assert.True(
            JsonNode.DeepEquals(expected, JsonNode.Parse(json)),
            $"The download payload no longer matches {ApprovedFile}. Actual payload:\n{json}");
    }

    private static OrderDownloadPayload CreateFixture()
    {
        var resistor = Component.Restore(Id("0c", 1), "RES-10K-0402", "Resistor 10 kΩ, 0402");
        var capacitor = Component.Restore(Id("0c", 2), "CAP-100N-0402", null);
        var led = Component.Restore(Id("0c", 3), "LED-RED-0603", null);

        var controller = Board.Restore(
            Id("0b", 1),
            "Controller board",
            "Main controller",
            160m,
            100m,
            [new BomEntry(resistor.Id, 12), new BomEntry(capacitor.Id, 4)]);
        var sensor = Board.Restore(
            Id("0b", 2),
            "Sensor board",
            null,
            50.8m,
            30.5m,
            [new BomEntry(resistor.Id, 3), new BomEntry(led.Id, 2)]);

        var order = Order.Restore(
            Id("0a", 1),
            "Week 41",
            "Controller and sensor boards",
            new DateTimeOffset(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2)),
            [new OrderLine(controller.Id, 500), new OrderLine(sensor.Id, 200)],
            OrderStatus.Draft,
            downloadedAt: null);

        Board[] boards = [controller, sensor];

        return OrderDownloadPayloadMapper.Map(
            order,
            boards,
            [resistor, capacitor, led],
            ComponentDemandCalculator.Calculate(order, boards),
            new DateTimeOffset(2026, 10, 6, 6, 0, 0, TimeSpan.FromHours(2)));
    }

    private static Guid Id(string prefix, int number) => Guid.Parse($"{prefix}000000-0000-0000-0000-{number:D12}");
}
