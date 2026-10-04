using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// Serializes the download payload. The JSON representation is part of the contract, so the
/// serializer settings live here, next to the payload types, and nowhere else.
/// </summary>
internal static class DownloadPayloadSerializer
{
    /// <summary>
    /// The settings of the contract: camelCase names, enums as camelCase strings, indented with
    /// line feeds on every platform, and non-ASCII characters written as they are, so names
    /// stay readable in the files on the line.
    /// </summary>
    /// <remarks>
    /// The payload has no enum yet. Writing enums as names is set now, so a future enum cannot
    /// be published as a number, which would break consumers when its values are reordered.
    /// </remarks>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(OrderDownloadPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return JsonSerializer.Serialize(payload, Options);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
