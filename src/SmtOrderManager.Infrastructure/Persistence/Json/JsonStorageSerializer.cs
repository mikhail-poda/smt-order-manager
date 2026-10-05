using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// The serializer settings of the data files.
/// </summary>
/// <remarks>
/// <para>
/// The data files are a private storage format, not the download contract, so they have their
/// own settings and can evolve independently of it. Enums are written and read only as names,
/// so reordering an enum cannot change the meaning of stored data.
/// </para>
/// <para>
/// Reading is strict about shape: a missing constructor parameter or a <see langword="null"/>
/// in a non-nullable property fails as invalid JSON, instead of reaching the domain model as
/// a <see langword="null"/> it never expects.
/// </para>
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
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        };

        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
