using SmtOrderManager.Domain.Boards;

namespace SmtOrderManager.Application.Persistence;

/// <summary>
/// Repository for boards and their bills of materials.
/// </summary>
public interface IBoardRepository : IRepository<Board>
{
    /// <summary>
    /// Finds the boards whose bill of materials contains at least one of the components. Used
    /// to enforce restricted deletion of components.
    /// </summary>
    Task<IReadOnlyList<Board>> FindBoardsUsingComponentsAsync(
        IReadOnlyCollection<Guid> componentIds,
        CancellationToken cancellationToken = default);
}
