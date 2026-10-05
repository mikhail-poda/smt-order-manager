using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Orders;
using SmtOrderManager.Cli.Interaction;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Cli.Menus;

/// <summary>
/// Creates, edits, searches and removes production orders and downloads them to the SMT line.
/// </summary>
internal sealed class OrderMenu(
    OrderService orders,
    OrderDownloadService downloads,
    BoardService boards,
    QuantityListEditor listEditor,
    ConsolePrompts prompts,
    TimeProvider timeProvider)
{
    private static readonly QuantityListTexts OrderLineTexts = new("Order lines", "boards", "Boards to produce");

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            switch (prompts.ReadMenuChoice("Orders", ["Create", "Edit", "Search", "Remove", "Download to SMT line"], "Back"))
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
                case 5:
                    await DownloadAsync(cancellationToken);
                    break;
                default:
                    return;
            }
        }
    }

    private static string Describe(OrderDetails order) =>
        $"{order.Name}  {Formatting.Timestamp(order.OrderDate)}  {DescribeStatus(order)}  {order.Lines.Count} board(s)";

    private static string DescribeStatus(OrderDetails order) =>
        order.Status == OrderStatus.Downloaded && order.DownloadedAt is { } downloadedAt
            ? $"downloaded {Formatting.Timestamp(downloadedAt)}"
            : "draft";

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Create orders");

        var commands = new List<CreateOrderCommand>();

        do
        {
            prompts.WriteLine($"Item {commands.Count + 1}");
            var name = prompts.ReadText("Name");
            var description = prompts.ReadOptionalText("Description");
            var orderDate = prompts.ReadDateTime("Order date", CurrentMinute());
            var lines = await EditLinesAsync([], cancellationToken);

            commands.Add(new CreateOrderCommand(name, description, orderDate, lines));
        }
        while (prompts.Confirm("Add another order?", defaultAnswer: false));

        if (!prompts.Confirm($"Save {commands.Count} order(s)?", defaultAnswer: true))
        {
            prompts.WriteLine("Nothing was saved.");
            return;
        }

        var result = await orders.CreateAsync(commands, cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Created {result.Value.Count} order(s).");
    }

    private async Task EditAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Edit orders");

        var selected = await SelectManyAsync("Orders to edit", cancellationToken);

        if (selected.Count == 0)
        {
            return;
        }

        // Downloaded orders can no longer be changed. Instead of asking for values that could not
        // be saved, they are submitted unchanged right away, so the user sees the rule as the
        // application reports it.
        var downloaded = selected.Where(order => order.Status == OrderStatus.Downloaded).ToList();

        if (downloaded.Count > 0)
        {
            var rejected = await orders.UpdateAsync([.. downloaded.Select(Unchanged)], cancellationToken);

            if (!rejected.Succeeded)
            {
                prompts.WriteViolations(rejected.Violations);
            }

            return;
        }

        var commands = new List<UpdateOrderCommand>();

        foreach (var order in selected)
        {
            prompts.WriteLine($"Editing '{order.Name}' (Enter keeps the current value)");
            var name = prompts.ReadText("Name", order.Name);
            var description = prompts.ReadOptionalText("Description", order.Description);
            var orderDate = prompts.ReadDateTime("Order date", order.OrderDate);
            IReadOnlyList<OrderLineInput> lines = prompts.Confirm("Edit the order lines?", defaultAnswer: false)
                ? await EditLinesAsync(order.Lines, cancellationToken)
                : [.. order.Lines.Select(line => new OrderLineInput(line.BoardId, line.Quantity))];

            commands.Add(new UpdateOrderCommand(order.Id, name, description, orderDate, lines));
        }

        var result = await orders.UpdateAsync(commands, cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Updated {result.Value.Count} order(s).");
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Search orders");

        var found = await orders.SearchAsync(prompts.ReadText("Search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No orders found.");
            return;
        }

        foreach (var order in found)
        {
            prompts.WriteLine($"  {Describe(order)}");

            if (order.Description.Length > 0)
            {
                prompts.WriteLine($"      {order.Description}");
            }

            foreach (var line in order.Lines)
            {
                prompts.WriteLine($"      {line.BoardName}  x {line.Quantity}");
            }
        }

        prompts.WriteLine($"{found.Count} order(s).");
    }

    private async Task RemoveAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Remove orders");

        var selected = await SelectManyAsync("Orders to remove", cancellationToken);

        if (selected.Count == 0
            || !prompts.Confirm($"Remove {selected.Count} order(s)?", defaultAnswer: false))
        {
            return;
        }

        var result = await orders.RemoveAsync([.. selected.Select(order => order.Id)], cancellationToken);

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations);
            return;
        }

        prompts.WriteSuccess($"Removed {result.Value} order(s).");
    }

    private async Task DownloadAsync(CancellationToken cancellationToken)
    {
        prompts.WriteHeading("Download order to SMT line");

        var found = await orders.SearchAsync(prompts.ReadText("Search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No orders found.");
            return;
        }

        var order = prompts.SelectOne(found, Describe, "Order to download");

        if (order is null)
        {
            return;
        }

        // Repeated downloads are allowed, for example when the line needs the job again.
        if (order.Status == OrderStatus.Downloaded
            && !prompts.Confirm($"'{order.Name}' was already {DescribeStatus(order)}. Download again?", defaultAnswer: false))
        {
            return;
        }

        OperationResult<OrderDownloadResult> result;

        try
        {
            result = await downloads.DownloadAsync(order.Id, cancellationToken);
        }
        catch (SmtLineUnavailableException exception)
        {
            prompts.WriteError($"The SMT line could not be reached: {exception.Message}");
            prompts.WriteLine("The order was not changed. Try again later.");
            return;
        }

        if (!result.Succeeded)
        {
            prompts.WriteViolations(result.Violations, "Nothing was sent to the line.");
            return;
        }

        var answer = result.Value;

        if (answer.Accepted)
        {
            prompts.WriteSuccess(
                $"Line {answer.LineId} accepted '{answer.OrderName}' at {Formatting.Timestamp(answer.ReceivedAt)}.");

            if (answer.JobReference is not null)
            {
                prompts.WriteLine($"Job file: {answer.JobReference}");
            }

            prompts.WriteLine("The order is now downloaded and can no longer be edited.");
            return;
        }

        prompts.WriteError($"Line {answer.LineId} rejected '{answer.OrderName}':");

        foreach (var reason in answer.Reasons)
        {
            prompts.WriteLine($"  {reason}");
        }

        prompts.WriteLine("The order was not changed. Correct it and download again.");
    }

    private async Task<IReadOnlyList<OrderDetails>> SelectManyAsync(string label, CancellationToken cancellationToken)
    {
        var found = await orders.SearchAsync(prompts.ReadText("Search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No orders found.");
            return [];
        }

        return prompts.SelectMany(found, Describe, label);
    }

    private async Task<IReadOnlyList<OrderLineInput>> EditLinesAsync(
        IReadOnlyList<OrderLineDetails> current,
        CancellationToken cancellationToken)
    {
        var entries = await listEditor.EditAsync(
            OrderLineTexts,
            [.. current.Select(line => new QuantityEntry(line.BoardId, line.BoardName, line.Quantity))],
            PickBoardAsync,
            cancellationToken);

        return [.. entries.Select(entry => new OrderLineInput(entry.Id, entry.Quantity))];
    }

    private async Task<QuantityEntry?> PickBoardAsync(CancellationToken cancellationToken)
    {
        var found = await boards.SearchAsync(prompts.ReadText("Board search text (Enter for all)"), cancellationToken);

        if (found.Count == 0)
        {
            prompts.WriteLine("No boards found.");
            return null;
        }

        var board = prompts.SelectOne(found, BoardMenu.Describe, "Board");

        return board is null ? null : new QuantityEntry(board.Id, board.Name, Quantity: 0);
    }

    private static UpdateOrderCommand Unchanged(OrderDetails order) =>
        new(
            order.Id,
            order.Name,
            order.Description,
            order.OrderDate,
            [.. order.Lines.Select(line => new OrderLineInput(line.BoardId, line.Quantity))]);

    private DateTimeOffset CurrentMinute()
    {
        var now = timeProvider.GetLocalNow();

        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMinute));
    }
}
