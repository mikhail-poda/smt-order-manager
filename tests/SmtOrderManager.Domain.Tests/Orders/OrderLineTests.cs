using SmtOrderManager.Domain.Common;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Domain.Tests.Orders;

public sealed class OrderLineTests
{
    [Fact]
    public void Constructor_WithValidValues_SetsProperties()
    {
        var boardId = Guid.NewGuid();

        var line = new OrderLine(boardId, 500);

        Assert.Equal(boardId, line.BoardId);
        Assert.Equal(500, line.Quantity);
    }

    [Fact]
    public void Constructor_WithEmptyBoardId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => new OrderLine(Guid.Empty, 500));

        Assert.Equal("BoardId must not be empty.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveQuantity_ThrowsDomainException(int quantity)
    {
        var exception = Assert.Throws<DomainException>(() => new OrderLine(Guid.NewGuid(), quantity));

        Assert.StartsWith("Quantity must be positive", exception.Message);
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var boardId = Guid.NewGuid();
        var first = new OrderLine(boardId, 500);
        var second = new OrderLine(boardId, 500);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentQuantity_ReturnsFalse()
    {
        var boardId = Guid.NewGuid();

        Assert.NotEqual(new OrderLine(boardId, 500), new OrderLine(boardId, 200));
    }

    [Fact]
    public void Equals_WithDifferentBoardId_ReturnsFalse()
    {
        Assert.NotEqual(new OrderLine(Guid.NewGuid(), 500), new OrderLine(Guid.NewGuid(), 500));
    }
}
