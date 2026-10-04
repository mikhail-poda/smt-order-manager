using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Tests.Common;

public sealed class AggregateRootTests
{
    [Fact]
    public void Constructor_WithEmptyId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => new TestAggregate(Guid.Empty));

        Assert.Equal("Id must not be empty.", exception.Message);
    }

    [Fact]
    public void Equals_WithSameTypeAndId_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var first = new TestAggregate(id);
        var second = new TestAggregate(id);

        Assert.True(first.Equals(second));
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        var first = new TestAggregate(Guid.NewGuid());
        var second = new TestAggregate(Guid.NewGuid());

        Assert.False(first.Equals(second));
        Assert.True(first != second);
    }

    [Fact]
    public void Equals_WithDifferentTypeAndSameId_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        AggregateRoot first = new TestAggregate(id);
        AggregateRoot second = new OtherTestAggregate(id);

        Assert.False(first.Equals(second));
        Assert.False(first == second);
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        Assert.False(aggregate.Equals(null));
        Assert.False(aggregate == null);
        Assert.False(null == aggregate);
    }

    [Fact]
    public void HashSet_WithTwoInstancesOfSameIdentity_ContainsOneElement()
    {
        var id = Guid.NewGuid();
        var set = new HashSet<AggregateRoot> { new TestAggregate(id), new TestAggregate(id) };

        Assert.Single(set);
    }

    private sealed class TestAggregate(Guid id) : AggregateRoot(id);

    private sealed class OtherTestAggregate(Guid id) : AggregateRoot(id);
}
