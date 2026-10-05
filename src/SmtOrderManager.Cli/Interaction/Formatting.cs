using System.Globalization;

namespace SmtOrderManager.Cli.Interaction;

/// <summary>
/// Formats values for display. Numbers use the invariant culture, matching how they are typed.
/// </summary>
internal static class Formatting
{
    public static string Number(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Dimensions(decimal length, decimal width) => $"{Number(length)} x {Number(width)} mm";
}
