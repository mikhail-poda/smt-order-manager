using System.Runtime.CompilerServices;

namespace SmtOrderManager.Application.Common;

/// <summary>
/// Checks shared by all batch use cases.
/// </summary>
internal static class Batch
{
    /// <summary>
    /// Ensures that a batch contains at least one item. An empty batch is a programming error
    /// of the caller, not a violated business rule, so it throws.
    /// </summary>
    public static void EnsureNotEmpty<T>(
        IReadOnlyCollection<T> items,
        [CallerArgumentExpression(nameof(items))] string paramName = "")
    {
        ArgumentNullException.ThrowIfNull(items, paramName);

        if (items.Count == 0)
        {
            throw new ArgumentException("A batch must contain at least one item.", paramName);
        }
    }

    /// <summary>
    /// Returns the keys that occur more than once.
    /// </summary>
    public static HashSet<TKey> FindDuplicates<T, TKey>(IEnumerable<T> items, Func<T, TKey> keySelector) =>
        items
            .GroupBy(keySelector)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
}
