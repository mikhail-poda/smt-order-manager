using Microsoft.Extensions.Logging;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// Downloads an order to the SMT line: loads the order with its boards and components,
/// calculates the total component demand, maps and serializes the payload, and sends it.
/// </summary>
/// <remarks>
/// There are three outcomes. An accepted download marks the order as downloaded. A rejection
/// is a normal answer of the line: the order stays a draft and the reasons are returned. An
/// unreachable line is a technical failure: <see cref="SmtLineUnavailableException"/> is logged
/// and passed on, and the order stays a draft. A downloaded order can be downloaded again.
/// </remarks>
public sealed partial class OrderDownloadService
{
    private readonly IOrderRepository _orders;
    private readonly IBoardRepository _boards;
    private readonly IComponentRepository _components;
    private readonly ISmtLine _line;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderDownloadService> _logger;

    public OrderDownloadService(
        IOrderRepository orders,
        IBoardRepository boards,
        IComponentRepository components,
        ISmtLine line,
        TimeProvider timeProvider,
        ILogger<OrderDownloadService> logger)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(boards);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _orders = orders;
        _boards = boards;
        _components = components;
        _line = line;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Downloads the order to the line.
    /// </summary>
    /// <returns>
    /// The line's answer, accepted or rejected; or violations when the order or data it
    /// references is missing, in which case nothing is sent.
    /// </returns>
    /// <exception cref="SmtLineUnavailableException">The line cannot be reached.</exception>
    public async Task<OperationResult<OrderDownloadResult>> DownloadAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var violations = new ViolationCollector();
        var order = await _orders.GetByIdAsync(orderId, cancellationToken);

        if (order is null)
        {
            violations.Add($"Order {orderId}", "Order not found.");
            return Reject(orderId, violations);
        }

        var target = $"Order '{order.Name}'";
        var boardIds = order.Lines.Select(line => line.BoardId).ToList();
        var boards = await _boards.GetByIdsAsync(boardIds, cancellationToken);
        ReportMissing(target, "Board", boardIds, boards.Select(board => board.Id), violations);

        var componentIds = boards
            .SelectMany(board => board.BillOfMaterials)
            .Select(entry => entry.ComponentId)
            .Distinct()
            .ToList();
        var components = await _components.GetByIdsAsync(componentIds, cancellationToken);
        ReportMissing(target, "Component", componentIds, components.Select(component => component.Id), violations);

        if (violations.HasViolations)
        {
            return Reject(orderId, violations);
        }

        var demand = ComponentDemandCalculator.Calculate(order, boards);
        var payload = OrderDownloadPayloadMapper.Map(order, boards, components, demand, _timeProvider.GetUtcNow());
        var payloadJson = DownloadPayloadSerializer.Serialize(payload);

        LineDownloadResult answer;

        try
        {
            answer = await _line.DownloadAsync(payloadJson, cancellationToken);
        }
        catch (SmtLineUnavailableException exception)
        {
            LogLineUnavailable(_logger, order.Name, order.Id, exception);
            throw;
        }

        if (answer.Accepted)
        {
            order.MarkDownloaded(answer.ReceivedAt);
            await _orders.SaveAsync([order], cancellationToken);
            LogAccepted(_logger, answer.LineId, order.Name, order.Id, answer.ReceivedAt);
        }
        else
        {
            LogRejectedByLine(_logger, answer.LineId, order.Name, order.Id, answer.Reasons);
        }

        return OperationResult.Success(new OrderDownloadResult(
            order.Id,
            order.Name,
            answer.Accepted,
            answer.LineId,
            answer.ReceivedAt,
            answer.Reasons,
            answer.JobReference));
    }

    private static void ReportMissing(
        string target,
        string kind,
        List<Guid> requested,
        IEnumerable<Guid> found,
        ViolationCollector violations)
    {
        var foundIds = found.ToHashSet();

        foreach (var id in requested.Where(id => !foundIds.Contains(id)))
        {
            violations.Add(target, $"{kind} {id} not found.");
        }
    }

    private OperationResult<OrderDownloadResult> Reject(Guid orderId, ViolationCollector violations)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            LogNotSent(_logger, orderId, violations.Count, violations.ToString());
        }

        return OperationResult.Failure<OrderDownloadResult>(violations.Violations);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SMT line {LineId} accepted order {OrderName} ({OrderId}) at {ReceivedAt}")]
    private static partial void LogAccepted(
        ILogger logger,
        string lineId,
        string orderName,
        Guid orderId,
        DateTimeOffset receivedAt);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SMT line {LineId} rejected order {OrderName} ({OrderId}): {Reasons}")]
    private static partial void LogRejectedByLine(
        ILogger logger,
        string lineId,
        string orderName,
        Guid orderId,
        IReadOnlyList<string> reasons);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "SMT line unavailable, order {OrderName} ({OrderId}) was not downloaded")]
    private static partial void LogLineUnavailable(ILogger logger, string orderName, Guid orderId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        SkipEnabledCheck = true,
        Message = "Order {OrderId} was not sent to the line, {ViolationCount} violations: {Violations}")]
    private static partial void LogNotSent(ILogger logger, Guid orderId, int violationCount, string violations);
}
