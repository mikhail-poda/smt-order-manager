using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Tests.Boards;

public sealed class BomEntryTests
{
    [Fact]
    public void Constructor_WithValidValues_SetsProperties()
    {
        var componentId = Guid.NewGuid();

        var entry = new BomEntry(componentId, 12);

        Assert.Equal(componentId, entry.ComponentId);
        Assert.Equal(12, entry.Quantity);
    }

    [Fact]
    public void Constructor_WithEmptyComponentId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => new BomEntry(Guid.Empty, 12));

        Assert.Equal("ComponentId must not be empty.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveQuantity_ThrowsDomainException(int quantity)
    {
        var exception = Assert.Throws<DomainException>(() => new BomEntry(Guid.NewGuid(), quantity));

        Assert.StartsWith("Quantity must be positive", exception.Message);
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var componentId = Guid.NewGuid();
        var first = new BomEntry(componentId, 12);
        var second = new BomEntry(componentId, 12);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentQuantity_ReturnsFalse()
    {
        var componentId = Guid.NewGuid();

        Assert.NotEqual(new BomEntry(componentId, 12), new BomEntry(componentId, 3));
    }

    [Fact]
    public void Equals_WithDifferentComponentId_ReturnsFalse()
    {
        Assert.NotEqual(new BomEntry(Guid.NewGuid(), 12), new BomEntry(Guid.NewGuid(), 12));
    }
}
