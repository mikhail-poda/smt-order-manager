using System.Text.Json.Nodes;
using SmtOrderManager.Application.Downloads;

namespace SmtOrderManager.Application.Tests.Downloads;

public sealed class DownloadPayloadSerializerTests
{
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly Guid ComponentId = Guid.NewGuid();

    [Fact]
    public void Serialize_WithPayload_UsesCamelCasePropertyNames()
    {
        var json = JsonNode.Parse(DownloadPayloadSerializer.Serialize(CreatePayload("Week 41")))!;

        Assert.Equal(1, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Week 41", json["order"]!["name"]!.GetValue<string>());
        Assert.Equal(160m, json["boards"]![0]!["lengthMm"]!.GetValue<decimal>());
        Assert.Equal(6_600L, json["componentDemand"]![0]!["totalQuantity"]!.GetValue<long>());
    }

    [Fact]
    public void Serialize_WithNonAsciiText_WritesCharactersUnescaped()
    {
        var json = DownloadPayloadSerializer.Serialize(CreatePayload("Prüfauftrag Größe 2"));

        Assert.Contains("Prüfauftrag Größe 2", json);
    }

    [Fact]
    public void Serialize_WithPayload_UsesLineFeedsOnEveryPlatform()
    {
        var json = DownloadPayloadSerializer.Serialize(CreatePayload("Week 41"));

        Assert.Contains("\n", json);
        Assert.DoesNotContain("\r", json);
    }

    private static OrderDownloadPayload CreatePayload(string orderName) =>
        new(
            DownloadPayloadSchema.CurrentVersion,
            new DateTimeOffset(2026, 10, 6, 6, 0, 0, TimeSpan.FromHours(2)),
            new OrderPayload(
                Guid.NewGuid(),
                orderName,
                string.Empty,
                new DateTimeOffset(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2)),
                [new OrderLinePayload(BoardId, 500)]),
            [new BoardPayload(BoardId, "Controller board", string.Empty, 160m, 100m, [new BomEntryPayload(ComponentId, "RES-10K-0402", 12)])],
            [new ComponentDemandPayload(ComponentId, "RES-10K-0402", 6_600)]);
}
