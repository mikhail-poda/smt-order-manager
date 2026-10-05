using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Components;
using SmtOrderManager.Application.Orders;

namespace SmtOrderManager.Cli.Demo;

/// <summary>
/// Fills an empty store with data that shows the main features right away.
/// </summary>
/// <remarks>
/// <para>
/// The first order (500 controllers and 200 sensors) is accepted by the line with the default
/// limits of 510 x 460 mm. Its demand for <c>RES-10K-0402</c> is 500 x 12 + 200 x 3 = 6,600
/// placements. The second order contains a 600 x 400 mm backplane, which is longer than the
/// line allows, so the line rejects it and names the board.
/// </para>
/// <para>
/// The data is created through the use cases, so it passes the same rules as data entered by a
/// user. Seeding only happens when there are no components, boards and orders at all, so
/// existing data is never changed or mixed with demo data.
/// </para>
/// </remarks>
internal sealed partial class DemoDataSeeder(
    ComponentService components,
    BoardService boards,
    OrderService orders,
    TimeProvider timeProvider,
    IOptions<DemoDataOptions> options,
    ILogger<DemoDataSeeder> logger)
{
    /// <summary>
    /// Creates the demo data if it is enabled and the store is empty.
    /// </summary>
    /// <returns>Whether demo data was created.</returns>
    public async Task<bool> SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !await IsStoreEmptyAsync(cancellationToken))
        {
            return false;
        }

        var createdComponents = Expect(await components.CreateAsync(
            [
                new CreateComponentCommand("RES-10K-0402", "Resistor 10 kΩ, 1 %, 0402"),
                new CreateComponentCommand("CAP-100N-0402", "Ceramic capacitor 100 nF, X7R, 0402"),
            ],
            cancellationToken));
        // Looked up by name rather than position, so the result order of the use case does not matter.
        var componentIds = createdComponents.ToDictionary(component => component.Name, component => component.Id);
        var resistor = componentIds["RES-10K-0402"];
        var capacitor = componentIds["CAP-100N-0402"];

        var createdBoards = Expect(await boards.CreateAsync(
            [
                new CreateBoardCommand(
                    "Controller",
                    "Main controller board",
                    100m,
                    80m,
                    [new BomEntryInput(resistor, 12), new BomEntryInput(capacitor, 4)]),
                new CreateBoardCommand(
                    "Sensor",
                    "Temperature sensor board",
                    40m,
                    30m,
                    [new BomEntryInput(resistor, 3), new BomEntryInput(capacitor, 2)]),
                new CreateBoardCommand(
                    "Backplane",
                    "Rack backplane, larger than the SMT line allows",
                    600m,
                    400m,
                    [new BomEntryInput(resistor, 40), new BomEntryInput(capacitor, 20)]),
            ],
            cancellationToken));
        var boardIds = createdBoards.ToDictionary(board => board.Name, board => board.Id);
        var controller = boardIds["Controller"];
        var sensor = boardIds["Sensor"];
        var backplane = boardIds["Backplane"];

        var orderDate = timeProvider.GetLocalNow();

        Expect(await orders.CreateAsync(
            [
                new CreateOrderCommand(
                    "Controllers and sensors",
                    "Demo: accepted by the SMT line",
                    orderDate,
                    [new OrderLineInput(controller, 500), new OrderLineInput(sensor, 200)]),
                new CreateOrderCommand(
                    "Backplanes",
                    "Demo: rejected by the SMT line, the board is too long",
                    orderDate,
                    [new OrderLineInput(backplane, 50)]),
            ],
            cancellationToken));

        LogSeeded(logger);

        return true;
    }

    private async Task<bool> IsStoreEmptyAsync(CancellationToken cancellationToken) =>
        (await components.SearchAsync(null, cancellationToken)).Count == 0
        && (await boards.SearchAsync(null, cancellationToken)).Count == 0
        && (await orders.SearchAsync(null, cancellationToken)).Count == 0;

    /// <summary>
    /// Returns the value of a result that must succeed. Fixed demo data that breaks a rule is a
    /// programming error, so it throws.
    /// </summary>
    private static T Expect<T>(OperationResult<T> result) =>
        result.Succeeded
            ? result.Value
            : throw new InvalidOperationException(
                "The demo data is invalid: "
                + string.Join("; ", result.Violations.Select(violation => $"{violation.Target}: {violation.Message}")));

    [LoggerMessage(Level = LogLevel.Information, Message = "Created demo data: 2 components, 3 boards, 2 orders")]
    private static partial void LogSeeded(ILogger logger);
}
