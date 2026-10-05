namespace SmtOrderManager.Infrastructure.SmtLine;

// The line's own view of the download payload. It declares only the fields the line uses and
// ignores everything else, as an independently developed consumer would (tolerant reader).
// It never uses the application's payload types.

/// <summary>
/// A job received by the simulated line.
/// </summary>
internal sealed record LineJob(int SchemaVersion, LineJobOrder Order, IReadOnlyList<LineJobBoard> Boards);

/// <summary>
/// The order a job belongs to.
/// </summary>
internal sealed record LineJobOrder(Guid Id, string Name);

/// <summary>
/// A board to produce in a job, with its dimensions in millimetres.
/// </summary>
internal sealed record LineJobBoard(Guid Id, string Name, decimal LengthMm, decimal WidthMm);
