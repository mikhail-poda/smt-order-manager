using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Infrastructure.Tests.Persistence;

/// <summary>
/// Assertions for repository results, which come back in no particular order.
/// </summary>
internal static class RepositoryAssert
{
    /// <summary>
    /// Checks that the aggregates have exactly the expected identifiers, in any order.
    /// </summary>
    public static void SameIds(IEnumerable<Guid> expected, IEnumerable<AggregateRoot> actual) =>
        Assert.Equal(expected.Order().ToArray(), actual.Select(aggregate => aggregate.Id).Order().ToArray());
}
