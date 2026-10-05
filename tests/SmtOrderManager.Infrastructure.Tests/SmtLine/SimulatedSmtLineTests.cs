using System.Text.Json.Nodes;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Infrastructure.SmtLine;
using SmtOrderManager.Infrastructure.Tests.Fakes;

namespace SmtOrderManager.Infrastructure.Tests.SmtLine;

/// <summary>
/// Tests the simulated line as a consumer of the download contract.
/// </summary>
/// <remarks>
/// The payloads are built from the approved version 1 example of the application tests, linked
/// into this project. The application's contract test pins what is produced, these tests pin
/// that the line accepts it, so both sides are checked against the same file.
/// </remarks>
public sealed class SimulatedSmtLineTests : IDisposable
{
    private const string LineId = "SMT-TEST-01";

    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 6, 6, 0, 0, 123, TimeSpan.FromHours(2));

    private static readonly string ApprovedFile =
        Path.Combine(AppContext.BaseDirectory, "Contracts", "order-download.v1.json");

    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "smt-order-manager-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string InboxDirectory => Path.Combine(_rootDirectory, "inbox");

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WithApprovedVersion1Example_Accepts()
    {
        var line = CreateLine();

        var result = await line.DownloadAsync(await ReadApprovedPayloadAsync(), Token);

        Assert.True(result.Accepted);
        Assert.Equal(LineId, result.LineId);
        Assert.Equal(ReceivedAt, result.ReceivedAt);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public async Task DownloadAsync_WhenAccepted_WritesJobUnchangedToInboxFileNamedAfterOrderAndTime()
    {
        var payload = await ReadApprovedPayloadAsync();
        var orderId = (string)JsonNode.Parse(payload)!["order"]!["id"]!;

        var result = await CreateLine().DownloadAsync(payload, Token);

        var expectedPath = Path.Combine(InboxDirectory, $"{orderId}-20261006T040000123Z.json");
        Assert.Equal(expectedPath, result.JobReference);
        Assert.Equal(payload, await File.ReadAllTextAsync(expectedPath, Token));
        Assert.Equal([expectedPath], Directory.GetFiles(InboxDirectory));
    }

    [Fact]
    public async Task DownloadAsync_SameOrderTwiceAtSameTime_KeepsBothJobs()
    {
        var line = CreateLine();
        var payload = await ReadApprovedPayloadAsync();

        var first = await line.DownloadAsync(payload, Token);
        var second = await line.DownloadAsync(payload, Token);

        Assert.NotEqual(first.JobReference, second.JobReference);
        Assert.EndsWith("-2.json", second.JobReference);
        Assert.Equal(2, Directory.GetFiles(InboxDirectory).Length);
    }

    [Fact]
    public async Task DownloadAsync_WithUnknownFields_AcceptsTolerantly()
    {
        var payload = await ReadApprovedPayloadNodeAsync();
        payload["priority"] = "high";
        payload["order"]!["customerReference"] = "C-4711";
        payload["boards"]![0]!["solderPaste"] = new JsonObject { ["alloy"] = "SAC305" };

        var result = await CreateLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.True(result.Accepted, string.Join(" ", result.Reasons));
    }

    [Fact]
    public async Task DownloadAsync_WithUnsupportedSchemaVersion_RejectsNamingVersions()
    {
        var payload = await ReadApprovedPayloadNodeAsync();
        payload["schemaVersion"] = 2;

        var result = await CreateLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.False(result.Accepted);
        Assert.Equal(LineId, result.LineId);
        Assert.Equal(ReceivedAt, result.ReceivedAt);
        Assert.Equal(["Schema version 2 is not supported. Supported versions: 1."], result.Reasons);
        Assert.Null(result.JobReference);
        Assert.False(Directory.Exists(InboxDirectory));
    }

    [Fact]
    public async Task DownloadAsync_WithUnsupportedVersionAndOtherwiseUnreadableJob_RejectsForVersionOnly()
    {
        // The version is checked before anything else, so a future format is not misread.
        var result = await CreateLine().DownloadAsync("""{ "schemaVersion": 2, "job": "entirely different" }""", Token);

        Assert.Equal(["Schema version 2 is not supported. Supported versions: 1."], result.Reasons);
    }

    [Fact]
    public async Task DownloadAsync_WithSeveralSupportedVersions_ListsThemInReason()
    {
        var line = CreateLine(supportedVersions: [3, 1]);

        var result = await line.DownloadAsync("""{ "schemaVersion": 2 }""", Token);

        Assert.Equal(["Schema version 2 is not supported. Supported versions: 1, 3."], result.Reasons);
    }

    [Theory]
    [InlineData("""{ "order": {} }""")]
    [InlineData("""{ "schemaVersion": "1" }""")]
    [InlineData("""{ "schemaVersion": 1.5 }""")]
    [InlineData("""{ "schemaVersion": null }""")]
    [InlineData("""[1]""")]
    public async Task DownloadAsync_WithoutValidSchemaVersion_Rejects(string payload)
    {
        var result = await CreateLine().DownloadAsync(payload, Token);

        Assert.False(result.Accepted);
        Assert.Equal(["The job has no valid 'schemaVersion'."], result.Reasons);
    }

    [Fact]
    public async Task DownloadAsync_WithInvalidJson_Rejects()
    {
        var result = await CreateLine().DownloadAsync("""{ "schemaVersion": 1, """, Token);

        Assert.False(result.Accepted);
        Assert.Equal(["The job is not valid JSON."], result.Reasons);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("boards")]
    public async Task DownloadAsync_WithSupportedVersionButMissingField_Rejects(string field)
    {
        var payload = await ReadApprovedPayloadNodeAsync();
        payload.Remove(field);

        var result = await CreateLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.False(result.Accepted);
        Assert.StartsWith("The job does not match schema version 1", Assert.Single(result.Reasons));
        Assert.False(Directory.Exists(InboxDirectory));
    }

    [Fact]
    public async Task DownloadAsync_WithNullBoard_Rejects()
    {
        var payload = await ReadApprovedPayloadNodeAsync();
        payload["boards"]!.AsArray().Add(null);

        var result = await CreateLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task DownloadAsync_WhenInboxCannotBeCreated_ThrowsSmtLineUnavailableException()
    {
        // A file where the inbox directory should be makes the directory impossible to create.
        Directory.CreateDirectory(_rootDirectory);
        await File.WriteAllTextAsync(InboxDirectory, "not a directory", Token);

        var exception = await Assert.ThrowsAsync<SmtLineUnavailableException>(
            async () => await CreateLine().DownloadAsync(await ReadApprovedPayloadAsync(), Token));

        Assert.Contains(LineId, exception.Message);
    }

    [Fact]
    public async Task DownloadAsync_WithCancelledToken_ThrowsWithoutWritingToInbox()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var payload = await ReadApprovedPayloadAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateLine().DownloadAsync(payload, cancellation.Token));

        Assert.False(Directory.Exists(InboxDirectory));
    }

    [Fact]
    public void Constructor_WithoutSupportedVersions_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateLine(supportedVersions: []));
    }

    private SimulatedSmtLine CreateLine(int[]? supportedVersions = null) =>
        new(
            new SimulatedSmtLineOptions
            {
                LineId = LineId,
                InboxDirectory = InboxDirectory,
                SupportedSchemaVersions = supportedVersions ?? [1],
            },
            new FixedTimeProvider(ReceivedAt));

    private static Task<string> ReadApprovedPayloadAsync() => File.ReadAllTextAsync(ApprovedFile, Token);

    private static async Task<JsonObject> ReadApprovedPayloadNodeAsync() =>
        JsonNode.Parse(await ReadApprovedPayloadAsync())!.AsObject();
}
