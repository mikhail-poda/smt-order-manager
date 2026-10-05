using System.Globalization;

namespace SmtOrderManager.Cli.Interaction;

/// <summary>
/// Formats values for display. Numbers use the invariant culture, matching how they are typed.
/// </summary>
internal static class Formatting
{
    public static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a point in time in local time, to the minute.
    /// </summary>
    public static string Timestamp(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string Dimensions(decimal length, decimal width) => $"{Number(length)} x {Number(width)} mm";
}
