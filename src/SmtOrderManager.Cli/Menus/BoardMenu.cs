using SmtOrderManager.Application.Boards;
using SmtOrderManager.Cli.Interaction;

namespace SmtOrderManager.Cli.Menus;

/// <summary>
/// Creates, edits, searches and removes boards with their bills of materials.
/// </summary>
internal sealed class BoardMenu(BoardService boards, BillOfMaterialsEditor bomEditor, ConsolePrompts prompts)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            switch (prompts.ReadMenuChoice("Boards", ["Create", "Edit", "Search", "Remove"], "Back"))
            {
                case 1:
                    await CreateAsync(cancellationToken);
                    break;
                case 2:
                    await EditAsync(cancellationToken);
                    break;
                case 3:
                    await SearchAsync(cancellationToken);
                    break;
                case 4:
                    await RemoveAsync(cancellationToken);
                    break;
                default:
                    return;
            }
        }
    }

    /// <summary>
    /// Formats a board for lists, also used when picking boards for an order.
    /// </summary>
    public static string Describe(BoardDetails board) =>
        $"{board.Name}  {Formatting.Dimensions(board.Length, board.Width)}  {board.BillOfMaterials.Count} component(s)";

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Create boards");

        var commands = new List<CreateBoardCommand>();

        do
        {
            prompts.WriteLine($"Item {commands.Count + 1}");
            var name = prompts.ReadText("Name");
            var description = prompts.ReadOptionalText("Description");
            var length = prompts.ReadDecimal("Length in mm");
            var width = prompts.ReadDecimal("Width in mm");
            var billOfMaterials = await bomEditor.EditAsync([], cancellationToken);

            commands.Add(new CreateBoardCommand(name, description, length, width, billOfMaterials));
        }
        while (prompts.Confirm("Add another board?", defaultAnswer: false));

        if (!prompts.Confirm($"Save {commands.Count} board(s)?", defaultAnswer: true))
        {
            prompts.WriteLine("Nothing was saved.");
            return;
        }

        var result = await boards.CreateAsync(commands, cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Created {result.Value.Count} board(s).");
    }

    private async Task EditAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Edit boards");

        var selected = await SelectAsync("Boards to edit", cancellationToken);

        if (selected.Count == 0)
        {
            return;
        }

        var commands = new List<UpdateBoardCommand>();

        foreach (var board in selected)
        {
            prompts.WriteLine($"Editing '{board.Name}' (Enter keeps the current value)");
            var name = prompts.ReadText("Name", board.Name);
            var description = prompts.ReadOptionalText("Description", board.Description);
            var length = prompts.ReadDecimal("Length in mm", board.Length);
            var width = prompts.ReadDecimal("Width in mm", board.Width);
            IReadOnlyList<BomEntryInput> billOfMaterials = prompts.Confirm("Edit the bill of materials?", defaultAnswer: false)
                ? await bomEditor.EditAsync(board.BillOfMaterials, cancellationToken)
                : [.. board.BillOfMaterials.Select(entry => new BomEntryInput(entry.ComponentId, entry.Quantity))];

            commands.Add(new UpdateBoardCommand(board.Id, name, description, length, width, billOfMaterials));
        }

        var result = await boards.UpdateAsync(commands, cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Updated {result.Value.Count} board(s).");
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Search boards");

        var found = await boards.SearchAsync(prompts.ReadText("Search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No boards found.");
            return;
        }

        foreach (var board in found)
        {
            prompts.WriteLine($"  {Describe(board)}");

            if (board.Description.Length > 0)
            {
                prompts.WriteLine($"      {board.Description}");
            }

            foreach (var entry in board.BillOfMaterials)
            {
                prompts.WriteLine($"      {entry.ComponentName}  x {entry.Quantity}");
            }
        }

        prompts.WriteLine($"{found.Count} board(s).");
    }

    private async Task RemoveAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Remove boards");

        var selected = await SelectAsync("Boards to remove", cancellationToken);

        if (selected.Count == 0
            || !prompts.Confirm($"Remove {selected.Count} board(s)?", defaultAnswer: false))
        {
            return;
        }

        var result = await boards.RemoveAsync([.. selected.Select(board => board.Id)], cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Removed {result.Value} board(s).");
    }

    private async Task<IReadOnlyList<BoardDetails>> SelectAsync(string label, CancellationToken cancellationToken)
    {
        var found = await boards.SearchAsync(prompts.ReadText("Search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No boards found.");
            return [];
        }

        return prompts.SelectMany(found, Describe, label);
    }
}
