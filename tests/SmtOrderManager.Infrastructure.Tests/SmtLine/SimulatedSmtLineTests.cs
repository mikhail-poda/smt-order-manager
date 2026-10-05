using System.Globalization;
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
    private const decimal MaxLength = 300m;
    private const decimal MaxWidth = 200m;

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
    public async Task DownloadAsync_WithAllBoardsWithinLimits_Accepts()
    {
        var payload = await PayloadWithBoardSizesAsync((250m, 150m), (100m, 80m));

        var result = await CreateLimitedLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.True(result.Accepted, string.Join(" ", result.Reasons));
    }

    [Fact]
    public async Task DownloadAsync_WithBoardExactlyAtLimits_Accepts()
    {
        var payload = await PayloadWithBoardSizesAsync((MaxLength, MaxWidth), (100m, 80m));

        var result = await CreateLimitedLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.True(result.Accepted, string.Join(" ", result.Reasons));
    }

    [Theory]
    [InlineData("300.01", "200")]
    [InlineData("300", "200.01")]
    [InlineData("450", "250")]
    public async Task DownloadAsync_WithBoardAboveLimit_RejectsNamingBoardAndSizes(string length, string width)
    {
        var lengthMm = decimal.Parse(length, CultureInfo.InvariantCulture);
        var widthMm = decimal.Parse(width, CultureInfo.InvariantCulture);
        var payload = await PayloadWithBoardSizesAsync((lengthMm, widthMm), (100m, 80m));
        var board = payload["boards"]![0]!;

        var result = await CreateLimitedLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.False(result.Accepted);
        Assert.Equal(
            [
                $"Board '{(string)board["name"]!}' ({(string)board["id"]!}) is {length} x {width} mm, "
                + "which exceeds the line maximum of 300 x 200 mm.",
            ],
            result.Reasons);
        Assert.Null(result.JobReference);
        Assert.False(Directory.Exists(InboxDirectory));
    }

    [Fact]
    public async Task DownloadAsync_WithSeveralBoardsAboveLimit_ListsEveryBoard()
    {
        var payload = await PayloadWithBoardSizesAsync((400m, 100m), (100m, 250m));
        var boardIds = payload["boards"]!.AsArray().Select(board => (string)board!["id"]!).ToList();

        var result = await CreateLimitedLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.False(result.Accepted);
        Assert.Collection(
            result.Reasons,
            reason => Assert.Contains(boardIds[0], reason),
            reason => Assert.Contains(boardIds[1], reason));
    }

    [Fact]
    public async Task DownloadAsync_WithBoardThatWouldFitOnlyRotated_Rejects()
    {
        // Boards are not rotated: 150 x 300 fits a 300 x 200 line only when turned by 90 degrees.
        var payload = await PayloadWithBoardSizesAsync((150m, 300m), (100m, 80m));

        var result = await CreateLimitedLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.False(result.Accepted);
        Assert.Single(result.Reasons);
    }

    [Fact]
    public async Task DownloadAsync_WithUnsupportedVersionAndOversizedBoard_RejectsForVersionOnly()
    {
        var payload = await PayloadWithBoardSizesAsync((400m, 100m), (100m, 80m));
        payload["schemaVersion"] = 2;

        var result = await CreateLimitedLine().DownloadAsync(payload.ToJsonString(), Token);

        Assert.Equal(["Schema version 2 is not supported. Supported versions: 1."], result.Reasons);
    }

    [Fact]
    public async Task DownloadAsync_WhenUnavailable_ThrowsSmtLineUnavailableExceptionNamingLine()
    {
        var payload = await ReadApprovedPayloadAsync();

        var exception = await Assert.ThrowsAsync<SmtLineUnavailableException>(
            () => CreateLine(isAvailable: false).DownloadAsync(payload, Token));

        Assert.Contains(LineId, exception.Message);
        Assert.False(Directory.Exists(InboxDirectory));
    }

    [Fact]
    public async Task DownloadAsync_WhenUnavailable_ThrowsEvenForUnreadableJob()
    {
        // The line is not reached, so it cannot answer with a rejection.
        await Assert.ThrowsAsync<SmtLineUnavailableException>(
            () => CreateLine(isAvailable: false).DownloadAsync("not json", Token));
    }

    [Fact]
    public void Options_ByDefault_AreAvailable()
    {
        Assert.True(new SimulatedSmtLineOptions().IsAvailable);
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(300, -1)]
    public void Constructor_WithNonPositiveLimit_Throws(int maxLength, int maxWidth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateLine(maxBoardLength: maxLength, maxBoardWidth: maxWidth));
    }

    [Fact]
    public void Constructor_WithoutSupportedVersions_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateLine(supportedVersions: []));
    }

    /// <summary>
    /// Creates the line under test. Without limits, they are far above any board in the approved
    /// example, so tests that are not about dimensions are not affected by them.
    /// </summary>
    private SimulatedSmtLine CreateLine(
        int[]? supportedVersions = null,
        decimal maxBoardLength = 10_000m,
        decimal maxBoardWidth = 10_000m,
        bool isAvailable = true) =>
        new(
            new SimulatedSmtLineOptions
            {
                LineId = LineId,
                InboxDirectory = InboxDirectory,
                SupportedSchemaVersions = supportedVersions ?? [1],
                MaxBoardLength = maxBoardLength,
                MaxBoardWidth = maxBoardWidth,
                IsAvailable = isAvailable,
            },
            new FixedTimeProvider(ReceivedAt));

    /// <summary>
    /// Creates a line that handles boards up to <see cref="MaxLength"/> x <see cref="MaxWidth"/> mm.
    /// </summary>
    private SimulatedSmtLine CreateLimitedLine() =>
        CreateLine(maxBoardLength: MaxLength, maxBoardWidth: MaxWidth);

    /// <summary>
    /// Returns the approved example with the given dimensions, one pair per board in the
    /// example, in order.
    /// </summary>
    private static async Task<JsonObject> PayloadWithBoardSizesAsync(params (decimal Length, decimal Width)[] sizes)
    {
        var payload = await ReadApprovedPayloadNodeAsync();
        var boards = payload["boards"]!.AsArray();
        Assert.Equal(boards.Count, sizes.Length);

        for (var index = 0; index < sizes.Length; index++)
        {
            boards[index]!["lengthMm"] = sizes[index].Length;
            boards[index]!["widthMm"] = sizes[index].Width;
        }

        return payload;
    }

    private static Task<string> ReadApprovedPayloadAsync() => File.ReadAllTextAsync(ApprovedFile, Token);

    private static async Task<JsonObject> ReadApprovedPayloadNodeAsync() =>
        JsonNode.Parse(await ReadApprovedPayloadAsync())!.AsObject();
}