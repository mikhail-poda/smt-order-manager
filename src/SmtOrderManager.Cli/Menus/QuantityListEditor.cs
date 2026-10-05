using SmtOrderManager.Cli.Interaction;

namespace SmtOrderManager.Cli.Menus;

/// <summary>
/// One entry of a list edited with <see cref="QuantityListEditor"/>: a referenced item and how
/// many of it.
/// </summary>
internal sealed record QuantityEntry(Guid Id, string Name, int Quantity);

/// <summary>
/// The texts that adapt <see cref="QuantityListEditor"/> to one kind of list.
/// </summary>
/// <param name="Title">The heading, for example <c>Bill of materials</c>.</param>
/// <param name="Items">The plural of the referenced items, for example <c>components</c>.</param>
/// <param name="QuantityLabel">The prompt for the quantity, for example <c>Placements per board</c>.</param>
internal sealed record QuantityListTexts(string Title, string Items, string QuantityLabel);

/// <summary>
/// Edits a list of referenced items with quantities: the bill of materials of a board and the
/// lines of an order.
/// </summary>
/// <remarks>
/// The editor only collects entries. Whether the result is valid, for example not empty and
/// with existing items, is checked by the use cases when the list is saved.
/// </remarks>
internal sealed class QuantityListEditor(ConsolePrompts prompts)
{
    /// <summary>
    /// Lets the user add, change and remove entries, starting from <paramref name="current"/>.
    /// A changed quantity keeps the entry's position.
    /// </summary>
    /// <param name="texts">The texts for this kind of list.</param>
    /// <param name="current">The entries to start with.</param>
    /// <param name="pickAsync">Lets the user pick an item to add; <see langword="null"/> when cancelled.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<IReadOnlyList<QuantityEntry>> EditAsync(
        QuantityListTexts texts,
        IReadOnlyList<QuantityEntry> current,
        Func<CancellationToken, Task<QuantityEntry?>> pickAsync,
        CancellationToken cancellationToken)
    {
        var entries = current.ToList();

        while (true)
        {
            prompts.WriteHeading(texts.Title);

            if (entries.Count == 0)
            {
                prompts.WriteLine($"  (no {texts.Items} yet)");
            }

            foreach (var entry in entries)
            {
                prompts.WriteLine($"  {entry.Name}  x {entry.Quantity}");
            }

            switch (prompts.ReadMenuChoice("What next?", ["Add or change quantity", "Remove"], "Done"))
            {
                case 1:
                    await AddOrChangeAsync(texts, entries, pickAsync, cancellationToken);
                    break;
                case 2:
                    Remove(texts, entries);
                    break;
                default:
                    return entries;
            }
        }
    }

    private async Task AddOrChangeAsync(
        QuantityListTexts texts,
        List<QuantityEntry> entries,
        Func<CancellationToken, Task<QuantityEntry?>> pickAsync,
        CancellationToken cancellationToken)
    {
        var picked = await pickAsync(cancellationToken);

        if (picked is null)
        {
            return;
        }

        var index = entries.FindIndex(entry => entry.Id == picked.Id);
        int? currentQuantity = index >= 0 ? entries[index].Quantity : null;
        var entry = picked with { Quantity = prompts.ReadInteger(texts.QuantityLabel, currentQuantity) };

        if (index >= 0)
        {
            entries[index] = entry;
        }
        else
        {
            entries.Add(entry);
        }
    }

    private void Remove(QuantityListTexts texts, List<QuantityEntry> entries)
    {
        if (entries.Count == 0)
        {
            prompts.WriteLine($"There are no {texts.Items} to remove.");
            return;
        }

        var selected = prompts.SelectMany(entries, entry => entry.Name, $"{char.ToUpperInvariant(texts.Items[0])}{texts.Items[1..]} to remove");
        entries.RemoveAll(selected.Contains);
    }
}
