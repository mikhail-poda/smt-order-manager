using System.Globalization;
using SmtOrderManager.Application.Common;

namespace SmtOrderManager.Cli.Interaction;

/// <summary>
/// Reads and writes everything the menus exchange with the user.
/// </summary>
/// <remarks>
/// <para>
/// Prompts only check the format of the input, such as whether a number can be parsed, and ask
/// again when it is wrong. Business rules, such as a blank name or a non-positive dimension, are
/// left to the domain, so they are enforced in one place and reported as violations per item.
/// </para>
/// <para>
/// Numbers are read with a dot or a comma as decimal separator and without thousands
/// separators, so both 100.5 and 100,5 are accepted.
/// </para>
/// </remarks>
internal sealed class ConsolePrompts(TextReader input, TextWriter output)
{
    /// <summary>
    /// Shows a numbered menu and returns the chosen number. 0 always means back or quit.
    /// </summary>
    public int ReadMenuChoice(string title, IReadOnlyList<string> options, string exitLabel)
    {
        WriteHeading(title);

        for (var index = 0; index < options.Count; index++)
        {
            output.WriteLine($"  {index + 1}  {options[index]}");
        }

        output.WriteLine($"  0  {exitLabel}");

        while (true)
        {
            if (TryParseInteger(ReadLine("Choice"), out var choice) && choice >= 0 && choice <= options.Count)
            {
                return choice;
            }

            WriteError($"Enter a number from 0 to {options.Count}.");
        }
    }

    /// <summary>
    /// Reads a text. With a current value, Enter keeps it.
    /// </summary>
    public string ReadText(string label, string? current = null)
    {
        var text = ReadLine(current is null ? label : $"{label} [{current}]");

        return text.Length == 0 && current is not null ? current : text;
    }

    /// <summary>
    /// Reads an optional text. Enter keeps the current value, a single dash clears it.
    /// </summary>
    public string? ReadOptionalText(string label, string? current = null)
    {
        if (string.IsNullOrEmpty(current))
        {
            var entered = ReadLine($"{label} (optional)");

            return entered.Length == 0 ? null : entered;
        }

        var text = ReadLine($"{label} [{current}] (- clears)");

        return text switch
        {
            "" => current,
            "-" => null,
            _ => text,
        };
    }

    /// <summary>
    /// Reads a decimal number. With a current value, Enter keeps it.
    /// </summary>
    public decimal ReadDecimal(string label, decimal? current = null) =>
        ReadValue(
            label,
            current,
            current is null ? null : Formatting.Number(current.Value),
            (string text, out decimal value) => decimal.TryParse(
                text.Replace(',', '.'),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value),
            "Enter a number, for example 100 or 80.5.");

    /// <summary>
    /// Reads a whole number. With a current value, Enter keeps it.
    /// </summary>
    public int ReadInteger(string label, int? current = null) =>
        ReadValue(
            label,
            current,
            current?.ToString(CultureInfo.InvariantCulture),
            TryParseInteger,
            "Enter a whole number, for example 12.");

    /// <summary>
    /// Reads a date with an optional time of day, in local time. With a current value, Enter
    /// keeps it unchanged.
    /// </summary>
    public DateTimeOffset ReadDateTime(string label, DateTimeOffset? current = null) =>
        ReadValue(
            $"{label} (yyyy-MM-dd HH:mm)",
            current,
            current is null ? null : Formatting.Timestamp(current.Value),
            TryParseLocalDateTime,
            "Enter a date like 2026-10-05 or a date and time like 2026-10-05 14:30.");

    /// <summary>
    /// Asks a yes or no question. Enter gives the default answer.
    /// </summary>
    public bool Confirm(string question, bool defaultAnswer)
    {
        var hint = defaultAnswer ? "Y/n" : "y/N";

        while (true)
        {
            switch (ReadLine($"{question} ({hint})").ToUpperInvariant())
            {
                case "":
                    return defaultAnswer;
                case "Y" or "YES":
                    return true;
                case "N" or "NO":
                    return false;
                default:
                    WriteError("Answer y or n.");
                    break;
            }
        }
    }

    /// <summary>
    /// Shows a numbered list and lets the user select several entries, for example
    /// <c>1, 3-5</c>. Enter selects nothing.
    /// </summary>
    /// <returns>The selected items in the order they were entered, without duplicates.</returns>
    public IReadOnlyList<T> SelectMany<T>(IReadOnlyList<T> items, Func<T, string> describe, string label)
    {
        WriteNumbered(items, describe);

        while (true)
        {
            var text = ReadLine($"{label} (numbers or ranges like 1, 3-5; Enter cancels)");

            if (text.Length == 0)
            {
                return [];
            }

            if (TryParseSelection(text, items.Count, out var indexes))
            {
                return [.. indexes.Select(index => items[index])];
            }

            WriteError($"Enter numbers from 1 to {items.Count}, separated by commas or spaces.");
        }
    }

    /// <summary>
    /// Shows a numbered list and lets the user select one entry. Enter selects nothing.
    /// </summary>
    public T? SelectOne<T>(IReadOnlyList<T> items, Func<T, string> describe, string label)
        where T : class
    {
        WriteNumbered(items, describe);

        while (true)
        {
            var text = ReadLine($"{label} (Enter cancels)");

            if (text.Length == 0)
            {
                return null;
            }

            if (TryParseInteger(text, out var number) && number >= 1 && number <= items.Count)
            {
                return items[number - 1];
            }

            WriteError($"Enter a number from 1 to {items.Count}.");
        }
    }

    public void WriteHeading(string title)
    {
        output.WriteLine();
        output.WriteLine(title);
        output.WriteLine(new string('-', title.Length));
    }

    public void WriteLine(string text = "") => output.WriteLine(text);

    public void WriteSuccess(string message) => output.WriteLine($"OK: {message}");

    public void WriteError(string message) => output.WriteLine($"Error: {message}");

    /// <summary>
    /// Reports a rejected request with one line per violated rule.
    /// </summary>
    /// <param name="violations">The violated rules.</param>
    /// <param name="consequence">What did not happen because of them.</param>
    public void WriteViolations(IReadOnlyList<Violation> violations, string consequence = "Nothing was saved.")
    {
        output.WriteLine($"Error: {consequence} Correct the following and try again:");

        foreach (var violation in violations)
        {
            output.WriteLine($"  {violation.Target}: {violation.Message}");
        }
    }

    private delegate bool Parser<T>(string text, out T value);

    private T ReadValue<T>(string label, T? current, string? currentText, Parser<T> parse, string hint)
        where T : struct
    {
        while (true)
        {
            var text = ReadLine(currentText is null ? label : $"{label} [{currentText}]");

            if (text.Length == 0 && current is not null)
            {
                return current.Value;
            }

            if (parse(text, out var value))
            {
                return value;
            }

            WriteError(hint);
        }
    }

    private string ReadLine(string prompt)
    {
        output.Write($"{prompt}: ");

        var line = input.ReadLine() ?? throw new InputEndedException();

        return line.Trim();
    }

    private void WriteNumbered<T>(IReadOnlyList<T> items, Func<T, string> describe)
    {
        var width = items.Count.ToString(CultureInfo.InvariantCulture).Length;

        for (var index = 0; index < items.Count; index++)
        {
            var number = (index + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width);
            output.WriteLine($"  {number}  {describe(items[index])}");
        }
    }

    private static bool TryParseLocalDateTime(string text, out DateTimeOffset value)
    {
        if (DateTime.TryParseExact(
                text,
                ["yyyy-MM-dd HH:mm", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            value = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryParseInteger(string text, out int value) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    private static bool TryParseSelection(string text, int count, out List<int> indexes)
    {
        indexes = [];
        var tokens = text.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var bounds = token.Split('-');

            if (bounds.Length > 2
                || !TryParseInteger(bounds[0], out var first)
                || !TryParseInteger(bounds[^1], out var last)
                || first < 1
                || last > count
                || first > last)
            {
                return false;
            }

            for (var number = first; number <= last; number++)
            {
                if (!indexes.Contains(number - 1))
                {
                    indexes.Add(number - 1);
                }
            }
        }

        return indexes.Count > 0;
    }
}
