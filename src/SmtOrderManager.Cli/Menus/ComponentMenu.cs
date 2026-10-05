using SmtOrderManager.Application.Components;
using SmtOrderManager.Cli.Interaction;

namespace SmtOrderManager.Cli.Menus;

/// <summary>
/// Creates, edits, searches and removes components of the component library.
/// </summary>
internal sealed class ComponentMenu(ComponentService components, ConsolePrompts prompts)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            switch (prompts.ReadMenuChoice("Components", ["Create", "Edit", "Search", "Remove"], "Back"))
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
    /// Formats a component for lists, also used when picking components for a bill of materials.
    /// </summary>
    public static string Describe(ComponentDetails component) =>
        component.Description.Length == 0 ? component.Name : $"{component.Name}  ({component.Description})";

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Create components");

        var commands = new List<CreateComponentCommand>();

        do
        {
            prompts.WriteLine($"Item {commands.Count + 1}");
            commands.Add(new CreateComponentCommand(prompts.ReadText("Name"), prompts.ReadOptionalText("Description")));
        }
        while (prompts.Confirm("Add another component?", defaultAnswer: false));

        if (!prompts.Confirm($"Save {commands.Count} component(s)?", defaultAnswer: true))
        {
            prompts.WriteLine("Nothing was saved.");
            return;
        }

        var result = await components.CreateAsync(commands, cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Created {result.Value.Count} component(s).");
    }

    private async Task EditAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Edit components");

        var selected = await SelectAsync("Components to edit", cancellationToken);

        if (selected.Count == 0)
        {
            return;
        }

        var commands = new List<UpdateComponentCommand>();

        foreach (var component in selected)
        {
            prompts.WriteLine($"Editing '{component.Name}' (Enter keeps the current value)");
            commands.Add(new UpdateComponentCommand(
                component.Id,
                prompts.ReadText("Name", component.Name),
                prompts.ReadOptionalText("Description", component.Description)));
        }

        var result = await components.UpdateAsync(commands, cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Updated {result.Value.Count} component(s).");
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Search components");

        var found = await components.SearchAsync(prompts.ReadText("Search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No components found.");
            return;
        }

        foreach (var component in found)
        {
            prompts.WriteLine($"  {Describe(component)}");
        }

        prompts.WriteLine($"{found.Count} component(s).");
    }

    private async Task RemoveAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Remove components");

        var selected = await SelectAsync("Components to remove", cancellationToken);

        if (selected.Count == 0
            || !prompts.Confirm($"Remove {selected.Count} component(s)?", defaultAnswer: false))
        {
            return;
        }

        var result = await components.RemoveAsync([.. selected.Select(component => component.Id)], cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Removed {result.Value} component(s).");
    }

    private async Task<IReadOnlyList<ComponentDetails>> SelectAsync(string label, CancellationToken cancellationToken)
    {
        var found = await components.SearchAsync(prompts.ReadText("Search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No components found.");
            return [];
        }

        return prompts.SelectMany(found, Describe, label);
    }
}
