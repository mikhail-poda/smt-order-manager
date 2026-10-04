using SmtOrderManager.Domain.Common;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Domain.Tests.Orders;

public sealed class OrderTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Create_WithValidValues_SetsProperties()
    {
        var controllers = new OrderLine(Guid.NewGuid(), 500);
        var sensors = new OrderLine(Guid.NewGuid(), 200);

        var order = Order.Create(" Week 41 ", "Controller and sensor boards", IssuedAt, [controllers, sensors]);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal("Week 41", order.Name);
        Assert.Equal("Controller and sensor boards", order.Description);
        Assert.Equal(IssuedAt, order.OrderDate);
        Assert.Equal([controllers, sensors], order.Lines);
    }

    [Fact]
    public void Create_WithOffset_KeepsOffset()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        Assert.Equal(TimeSpan.FromHours(2), order.OrderDate.Offset);
    }

    [Fact]
    public void Create_WithoutDescription_UsesEmptyDescription()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        Assert.Equal(string.Empty, order.Description);
    }

    [Fact]
    public void Create_WithBlankName_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Order.Create(" ", null, IssuedAt, [Line()]));

        Assert.Equal("Name must not be blank.", exception.Message);
    }

    [Fact]
    public void Create_WithDefaultOrderDate_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Order.Create("Week 41", null, default, [Line()]));

        Assert.Equal("OrderDate must be set.", exception.Message);
    }

    [Fact]
    public void Create_WithoutLines_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Order.Create("Week 41", null, IssuedAt, []));

        Assert.Equal("An order must contain at least one order line.", exception.Message);
    }

    [Fact]
    public void Create_WithDuplicateBoard_ThrowsDomainException()
    {
        var boardId = Guid.NewGuid();

        var exception = Assert.Throws<DomainException>(() => Order.Create(
            "Week 41",
            null,
            IssuedAt,
            [new OrderLine(boardId, 500), new OrderLine(boardId, 200)]));

        Assert.Contains(boardId.ToString(), exception.Message);
    }

    [Fact]
    public void Create_WhenSourceListChangesAfterwards_KeepsOriginalLines()
    {
        var line = Line();
        var source = new List<OrderLine> { line };

        var order = Order.Create("Week 41", null, IssuedAt, source);
        source.Add(Line());

        Assert.Equal([line], order.Lines);
    }

    [Fact]
    public void Lines_IsNotExposedAsMutableList()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        Assert.IsNotType<List<OrderLine>>(order.Lines);
    }

    [Fact]
    public void Restore_WithExistingId_KeepsIdAndValues()
    {
        var id = Guid.NewGuid();
        var line = Line();

        var order = Order.Restore(id, "Week 41", "Restored", IssuedAt, [line], OrderStatus.Draft, null);

        Assert.Equal(id, order.Id);
        Assert.Equal("Week 41", order.Name);
        Assert.Equal(IssuedAt, order.OrderDate);
        Assert.Equal([line], order.Lines);
    }

    [Fact]
    public void Restore_WithoutLines_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(
            () => Order.Restore(Guid.NewGuid(), "Week 41", null, IssuedAt, [], OrderStatus.Draft, null));
    }

    [Fact]
    public void Rename_WithBlankName_ThrowsAndKeepsOriginalName()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        Assert.Throws<DomainException>(() => order.Rename(" "));

        Assert.Equal("Week 41", order.Name);
    }

    [Fact]
    public void ChangeDescription_WithText_ChangesTrimmedDescription()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        order.ChangeDescription(" Rush order ");

        Assert.Equal("Rush order", order.Description);
    }

    [Fact]
    public void ChangeOrderDate_WithValidDate_ChangesOrderDate()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);
        var newDate = IssuedAt.AddDays(1);

        order.ChangeOrderDate(newDate);

        Assert.Equal(newDate, order.OrderDate);
    }

    [Fact]
    public void ChangeOrderDate_WithDefaultDate_ThrowsAndKeepsOrderDate()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        Assert.Throws<DomainException>(() => order.ChangeOrderDate(default));

        Assert.Equal(IssuedAt, order.OrderDate);
    }

    [Fact]
    public void SetLine_WithNewBoard_AddsLine()
    {
        var existing = Line();
        var order = Order.Create("Week 41", null, IssuedAt, [existing]);
        var boardId = Guid.NewGuid();

        order.SetLine(boardId, 200);

        Assert.Equal([existing, new OrderLine(boardId, 200)], order.Lines);
    }

    [Fact]
    public void SetLine_WithExistingBoard_ReplacesLineInPlace()
    {
        var first = Line(500);
        var second = Line(200);
        var order = Order.Create("Week 41", null, IssuedAt, [first, second]);

        order.SetLine(first.BoardId, 750);

        Assert.Equal([new OrderLine(first.BoardId, 750), second], order.Lines);
    }

    [Fact]
    public void SetLine_WithNonPositiveQuantity_ThrowsAndKeepsLines()
    {
        var line = Line(500);
        var order = Order.Create("Week 41", null, IssuedAt, [line]);

        Assert.Throws<DomainException>(() => order.SetLine(line.BoardId, 0));

        Assert.Equal([line], order.Lines);
    }

    [Fact]
    public void RemoveLine_WithExistingBoard_RemovesLine()
    {
        var first = Line();
        var second = Line();
        var order = Order.Create("Week 41", null, IssuedAt, [first, second]);

        order.RemoveLine(first.BoardId);

        Assert.Equal([second], order.Lines);
    }

    [Fact]
    public void RemoveLine_WithLastLine_ThrowsAndKeepsLine()
    {
        var line = Line();
        var order = Order.Create("Week 41", null, IssuedAt, [line]);

        var exception = Assert.Throws<DomainException>(() => order.RemoveLine(line.BoardId));

        Assert.Equal("The last order line of order 'Week 41' cannot be removed.", exception.Message);
        Assert.Equal([line], order.Lines);
    }

    [Fact]
    public void RemoveLine_WithUnknownBoard_ThrowsDomainException()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line(), Line()]);

        Assert.Throws<DomainException>(() => order.RemoveLine(Guid.NewGuid()));
    }

    [Fact]
    public void Create_SetsStatusToDraft()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Null(order.DownloadedAt);
        Assert.True(order.IsEditable);
    }

    [Fact]
    public void MarkDownloaded_OnDraftOrder_SetsStatusAndDownloadTime()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);
        var downloadedAt = IssuedAt.AddHours(2);

        order.MarkDownloaded(downloadedAt);

        Assert.Equal(OrderStatus.Downloaded, order.Status);
        Assert.Equal(downloadedAt, order.DownloadedAt);
        Assert.False(order.IsEditable);
    }

    [Fact]
    public void MarkDownloaded_OnDownloadedOrder_RecordsMostRecentDownloadTime()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);
        order.MarkDownloaded(IssuedAt.AddHours(2));
        var secondDownload = IssuedAt.AddDays(1);

        order.MarkDownloaded(secondDownload);

        Assert.Equal(OrderStatus.Downloaded, order.Status);
        Assert.Equal(secondDownload, order.DownloadedAt);
    }

    [Fact]
    public void MarkDownloaded_WithDefaultTime_ThrowsAndKeepsDraft()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [Line()]);

        var exception = Assert.Throws<DomainException>(() => order.MarkDownloaded(default));

        Assert.Equal("DownloadedAt must be set.", exception.Message);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Null(order.DownloadedAt);
    }

    [Theory]
    [InlineData(nameof(Order.Rename))]
    [InlineData(nameof(Order.ChangeDescription))]
    [InlineData(nameof(Order.ChangeOrderDate))]
    [InlineData(nameof(Order.SetLine))]
    [InlineData(nameof(Order.RemoveLine))]
    public void EditingMethod_OnDownloadedOrder_ThrowsDomainException(string method)
    {
        var first = Line();
        var order = Order.Create("Week 41", null, IssuedAt, [first, Line()]);
        order.MarkDownloaded(IssuedAt.AddHours(2));

        var exception = Assert.Throws<DomainException>(() => Edit(order, method, first.BoardId));

        Assert.Equal("Order 'Week 41' has been downloaded and can no longer be changed.", exception.Message);
    }

    [Fact]
    public void SetLine_OnDownloadedOrder_KeepsLines()
    {
        var line = Line(500);
        var order = Order.Create("Week 41", null, IssuedAt, [line]);
        order.MarkDownloaded(IssuedAt.AddHours(2));

        Assert.Throws<DomainException>(() => order.SetLine(line.BoardId, 750));

        Assert.Equal([line], order.Lines);
    }

    [Fact]
    public void Restore_WithDownloadedStatus_KeepsStatusAndBlocksEdits()
    {
        var downloadedAt = IssuedAt.AddHours(2);

        var order = Order.Restore(
            Guid.NewGuid(), "Week 41", null, IssuedAt, [Line()], OrderStatus.Downloaded, downloadedAt);

        Assert.Equal(OrderStatus.Downloaded, order.Status);
        Assert.Equal(downloadedAt, order.DownloadedAt);
        Assert.Throws<DomainException>(() => order.Rename("Week 42"));
    }

    [Fact]
    public void Restore_WithDownloadedStatusWithoutDownloadTime_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Order.Restore(
            Guid.NewGuid(), "Week 41", null, IssuedAt, [Line()], OrderStatus.Downloaded, null));

        Assert.Equal("A downloaded order must have a download time.", exception.Message);
    }

    [Fact]
    public void Restore_WithDraftStatusAndDownloadTime_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Order.Restore(
            Guid.NewGuid(), "Week 41", null, IssuedAt, [Line()], OrderStatus.Draft, IssuedAt.AddHours(2)));

        Assert.Equal("A draft order must not have a download time.", exception.Message);
    }

    [Fact]
    public void Restore_WithUndefinedStatus_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Order.Restore(
            Guid.NewGuid(), "Week 41", null, IssuedAt, [Line()], (OrderStatus)99, null));
    }

    private static void Edit(Order order, string method, Guid existingBoardId)
    {
        switch (method)
        {
            case nameof(Order.Rename):
                order.Rename("Week 42");
                break;
            case nameof(Order.ChangeDescription):
                order.ChangeDescription("Changed");
                break;
            case nameof(Order.ChangeOrderDate):
                order.ChangeOrderDate(IssuedAt.AddDays(1));
                break;
            case nameof(Order.SetLine):
                order.SetLine(existingBoardId, 750);
                break;
            case nameof(Order.RemoveLine):
                order.RemoveLine(existingBoardId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown editing method.");
        }
    }

    private static OrderLine Line(int quantity = 1) => new(Guid.NewGuid(), quantity);
}