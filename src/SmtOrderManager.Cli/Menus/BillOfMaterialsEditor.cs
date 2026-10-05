using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Components;
using SmtOrderManager.Cli.Interaction;

namespace SmtOrderManager.Cli.Menus;

/// <summary>
/// Edits a bill of materials before it is submitted with a board.
/// </summary>
/// <remarks>
/// The editor only collects entries. Whether the result is valid, for example not empty and
/// with existing components, is checked by the board use cases when the board is saved.
/// </remarks>
internal sealed class BillOfMaterialsEditor(ComponentService components, ConsolePrompts prompts)
{
    /// <summary>
    /// Lets the user add, change and remove entries, starting from <paramref name="current"/>.
    /// </summary>
    public async Task<IReadOnlyList<BomEntryInput>> EditAsync(
        IReadOnlyList<BomEntryDetails> current,
        CancellationToken cancellationToken)
    {
        // Keeps the order of entry; a changed quantity keeps the entry's position.
        var entries = current.ToList();

        while (true)
        {
            prompts.WriteHeading("Bill of materials");

            if (entries.Count == 0)
            {
                prompts.WriteLine("  (no components yet)");
            }

            foreach (var entry in entries)
            {
                prompts.WriteLine($"  {entry.ComponentName}  x {entry.Quantity}");
            }

            var choice = prompts.ReadMenuChoice(
                "What next?",
                ["Add component or change quantity", "Remove component"],
                "Done");

            switch (choice)
            {
                case 1:
                    await AddOrChangeAsync(entries, cancellationToken);
                    break;
                case 2:
                    Remove(entries);
                    break;
                default:
                    return [.. entries.Select(entry => new BomEntryInput(entry.ComponentId, entry.Quantity))];
            }
        }
    }

    private async Task AddOrChangeAsync(List<BomEntryDetails> entries, CancellationToken cancellationToken)
    {
        var found = await components.SearchAsync(prompts.ReadText("Component search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No components found.");
            return;
        }

        var component = prompts.SelectOne(found, ComponentMenu.Describe, "Component");

        if (component is null)
        {
            return;
        }

        var index = entries.FindIndex(entry => entry.ComponentId == component.Id);
        int? currentQuantity = index >= 0 ? entries[index].Quantity : null;
        var entry = new BomEntryDetails(
            component.Id,
            component.Name,
            prompts.ReadInteger("Placements per board", currentQuantity));

        if (index >= 0)
        {
            entries[index] = entry;
        }
        else
        {
            entries.Add(entry);
        }
    }

    private void Remove(List<BomEntryDetails> entries)
    {
        if (entries.Count == 0)
        {
            prompts.WriteLine("The bill of materials is empty.");
            return;
        }

        var selected = prompts.SelectMany(entries, entry => entry.ComponentName, "Components to remove");
        entries.RemoveAll(selected.Contains);
    }
}
