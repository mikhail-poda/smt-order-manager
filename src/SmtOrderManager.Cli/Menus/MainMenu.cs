using SmtOrderManager.Cli.Interaction;

namespace SmtOrderManager.Cli.Menus;

/// <summary>
/// The top-level menu.
/// </summary>
internal sealed class MainMenu(ComponentMenu componentMenu, BoardMenu boardMenu, ConsolePrompts prompts)
{
    /// <summary>
    /// Runs until the user quits.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            switch (prompts.ReadMenuChoice("Main menu", ["Components", "Boards"], "Quit"))
            {
                case 1:
                    await componentMenu.RunAsync(cancellationToken);
                    break;
                case 2:
                    await boardMenu.RunAsync(cancellationToken);
                    break;
                default:
                    return;
            }
        }
    }
}
