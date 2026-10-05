using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmtOrderManager.Api.Tests.Infrastructure;

/// <summary>
/// The JSON settings a client of the API uses: web defaults and enums as camelCase names.
/// </summary>
internal static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
