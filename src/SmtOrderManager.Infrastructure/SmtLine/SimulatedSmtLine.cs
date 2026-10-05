using System.Globalization;
using System.Text.Json;
using SmtOrderManager.Application.Downloads;

namespace SmtOrderManager.Infrastructure.SmtLine;

/// <summary>
/// A stand-in for the handover interface of a real SMT line. It checks a job like an
/// independently developed line would and stores accepted jobs as files in its inbox. It does
/// not simulate production.
/// </summary>
/// <remarks>
/// <para>
/// The schema version is read first, on its own, so a job in an unsupported version is
/// rejected with a clear reason instead of being interpreted partially. Only a job in a
/// supported version is read into the line's own types, which ignore unknown fields.
/// </para>
/// <para>
/// A readable job is then checked against the line's capabilities. Every board must fit the
/// configured maximum length and width. All boards are checked, so one rejection lists every
/// board that does not fit. Boards are not rotated to fit: length is compared with the maximum
/// length and width with the maximum width, which keeps the rule simple and predictable.
/// </para>
/// <para>
/// A job the line cannot read or cannot handle is a rejection, because the line was reached
/// and answered. Only a failure to store an accepted job is a technical failure.
/// </para>
/// </remarks>
internal sealed class SimulatedSmtLine : ISmtLine
{
    private const string SchemaVersionProperty = "schemaVersion";
    private const int MaxFileNameAttempts = 100;

    private static readonly JsonSerializerOptions ReaderOptions = CreateReaderOptions();

    private readonly SimulatedSmtLineOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<int> _supportedVersions;
    private readonly string _inboxDirectory;

    public SimulatedSmtLine(SimulatedSmtLineOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.LineId, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.InboxDirectory, nameof(options));

        if (options.SupportedSchemaVersions is not { Length: > 0 })
        {
            throw new ArgumentException("At least one supported schema version is required.", nameof(options));
        }

        _options = options;
        _timeProvider = timeProvider;
        _supportedVersions = [.. options.SupportedSchemaVersions];
        _inboxDirectory = Path.GetFullPath(options.InboxDirectory);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxBoardLength, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxBoardWidth, nameof(options));
    }

    public async Task<LineDownloadResult> DownloadAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        cancellationToken.ThrowIfCancellationRequested();

        var receivedAt = _timeProvider.GetUtcNow();
        var read = Read(payloadJson);

        if (read.Job is null)
        {
            return LineDownloadResult.Reject(_options.LineId, receivedAt, [read.Reason!]);
        }

        var oversizedBoards = FindOversizedBoards(read.Job);

        if (oversizedBoards.Count > 0)
        {
            return LineDownloadResult.Reject(_options.LineId, receivedAt, oversizedBoards);
        }

        var jobReference = await StoreInInboxAsync(read.Job, payloadJson, receivedAt, cancellationToken);

        return LineDownloadResult.Accept(_options.LineId, receivedAt, jobReference);
    }

    private (LineJob? Job, string? Reason) Read(string payloadJson)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException)
        {
            return (null, "The job is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(SchemaVersionProperty, out var versionElement)
                || versionElement.ValueKind != JsonValueKind.Number
                || !versionElement.TryGetInt32(out var version))
            {
                return (null, $"The job has no valid '{SchemaVersionProperty}'.");
            }

            if (!_supportedVersions.Contains(version))
            {
                return (null, $"Schema version {version} is not supported. Supported versions: {SupportedVersionsText()}.");
            }

            LineJob? job;

            try
            {
                job = root.Deserialize<LineJob>(ReaderOptions);
            }
            catch (JsonException exception)
            {
                return (null, $"The job does not match schema version {version}: {exception.Message}");
            }

            // Nullable annotations are not enforced for collection elements, so null boards
            // are checked here.
            if (job is null || job.Boards.Any(board => board is null))
            {
                return (null, $"The job does not match schema version {version}.");
            }

            return (job, null);
        }
    }

    /// <summary>
    /// Returns one reason for each board that exceeds the maximum length or width. A board
    /// exactly at the limit fits.
    /// </summary>
    private List<string> FindOversizedBoards(LineJob job) =>
    [
        .. job.Boards
            .Where(board => board.LengthMm > _options.MaxBoardLength || board.WidthMm > _options.MaxBoardWidth)
            .Select(board => string.Create(
                CultureInfo.InvariantCulture,
                $"Board '{board.Name}' ({board.Id}) is {board.LengthMm:0.###} x {board.WidthMm:0.###} mm, "
                + $"which exceeds the line maximum of {_options.MaxBoardLength:0.###} x {_options.MaxBoardWidth:0.###} mm.")),
    ];

    /// <summary>
    /// Writes the job to the inbox exactly as received. The file is written under a temporary
    /// name and then moved, so anything watching the inbox never sees a half-written job.
    /// </summary>
    /// <returns>The full path of the inbox file.</returns>
    /// <exception cref="SmtLineUnavailableException">The job could not be stored.</exception>
    private async Task<string> StoreInInboxAsync(
        LineJob job,
        string payloadJson,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        var baseName = string.Create(
            CultureInfo.InvariantCulture,
            $"{job.Order.Id}-{receivedAt.UtcDateTime:yyyyMMdd'T'HHmmssfff'Z'}");
        var temporaryPath = Path.Combine(_inboxDirectory, $"{baseName}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(_inboxDirectory);
            await File.WriteAllTextAsync(temporaryPath, payloadJson, cancellationToken);

            return MoveToFreeName(temporaryPath, baseName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);

            throw new SmtLineUnavailableException(
                $"Line {_options.LineId} could not store the job in its inbox '{_inboxDirectory}'.",
                exception);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// Moves the file to <c>{baseName}.json</c>, or to <c>{baseName}-2.json</c> and so on when the
    /// same order was received twice within one millisecond. Existing jobs are never overwritten.
    /// </summary>
    private string MoveToFreeName(string temporaryPath, string baseName)
    {
        for (var attempt = 1; attempt <= MaxFileNameAttempts; attempt++)
        {
            var suffix = attempt == 1 ? string.Empty : $"-{attempt}";
            var path = Path.Combine(_inboxDirectory, $"{baseName}{suffix}.json");

            try
            {
                File.Move(temporaryPath, path, overwrite: false);

                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
                // Taken, try the next suffix.
            }
        }

        throw new IOException($"No free inbox file name found for '{baseName}'.");
    }

    private string SupportedVersionsText() =>
        string.Join(", ", _supportedVersions.Order());

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The original failure matters more than a leftover temporary file.
        }
    }

    private static JsonSerializerOptions CreateReaderOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            // Unknown fields are ignored (the default), but the fields the line relies on must
            // be present and must not be null.
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };

        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
