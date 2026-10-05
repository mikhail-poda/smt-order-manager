using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// The serializer settings of the data files.
/// </summary>
/// <remarks>
/// The data files are a private storage format, not the download contract, so they have their
/// own settings and can evolve independently of it. Enums are written as names, so reordering
/// an enum, such as the order status, cannot change the meaning of stored data.
/// </remarks>
internal static class JsonStorageSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

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
