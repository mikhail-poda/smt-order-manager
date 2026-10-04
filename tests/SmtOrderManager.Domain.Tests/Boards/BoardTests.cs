using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Tests.Boards;

public sealed class BoardTests
{
    public static TheoryData<decimal> NonPositiveDimensions => [0m, -0.1m, -160m];

    [Fact]
    public void Create_WithValidValues_SetsProperties()
    {
        var resistor = new BomEntry(Guid.NewGuid(), 12);
        var capacitor = new BomEntry(Guid.NewGuid(), 4);

        var board = Board.Create(" Controller board ", "Main controller", 160m, 100m, [resistor, capacitor]);

        Assert.NotEqual(Guid.Empty, board.Id);
        Assert.Equal("Controller board", board.Name);
        Assert.Equal("Main controller", board.Description);
        Assert.Equal(160m, board.Length);
        Assert.Equal(100m, board.Width);
        Assert.Equal(new[] { resistor, capacitor }, board.BillOfMaterials);
    }

    [Fact]
    public void Create_WithoutDescription_UsesEmptyDescription()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [Entry()]);

        Assert.Equal(string.Empty, board.Description);
    }

    [Fact]
    public void Create_WithBlankName_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Board.Create(" ", null, 160m, 100m, [Entry()]));

        Assert.Equal("Name must not be blank.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(NonPositiveDimensions))]
    public void Create_WithNonPositiveLength_ThrowsDomainException(decimal length)
    {
        var exception = Assert.Throws<DomainException>(
            () => Board.Create("Controller board", null, length, 100m, [Entry()]));

        Assert.StartsWith("Length must be positive", exception.Message);
    }

    [Theory]
    [MemberData(nameof(NonPositiveDimensions))]
    public void Create_WithNonPositiveWidth_ThrowsDomainException(decimal width)
    {
        var exception = Assert.Throws<DomainException>(
            () => Board.Create("Controller board", null, 160m, width, [Entry()]));

        Assert.StartsWith("Width must be positive", exception.Message);
    }

    [Fact]
    public void Create_WithEmptyBillOfMaterials_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Board.Create("Controller board", null, 160m, 100m, []));

        Assert.Equal("A board must contain at least one BOM entry.", exception.Message);
    }

    [Fact]
    public void Create_WithDuplicateComponent_ThrowsDomainException()
    {
        var componentId = Guid.NewGuid();

        var exception = Assert.Throws<DomainException>(() => Board.Create(
            "Controller board",
            null,
            160m,
            100m,
            [new BomEntry(componentId, 12), new BomEntry(componentId, 3)]));

        Assert.Contains(componentId.ToString(), exception.Message);
    }

    [Fact]
    public void Create_WhenSourceListChangesAfterwards_KeepsOriginalEntries()
    {
        var entry = Entry();
        var source = new List<BomEntry> { entry };

        var board = Board.Create("Controller board", null, 160m, 100m, source);
        source.Add(Entry());

        Assert.Equal(new[] { entry }, board.BillOfMaterials);
    }

    [Fact]
    public void BillOfMaterials_IsNotExposedAsMutableList()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [Entry()]);

        Assert.IsNotType<List<BomEntry>>(board.BillOfMaterials);
    }

    [Fact]
    public void Restore_WithExistingId_KeepsIdAndValues()
    {
        var id = Guid.NewGuid();
        var entry = Entry();

        var board = Board.Restore(id, "Sensor board", "Temperature sensor", 50m, 30m, [entry]);

        Assert.Equal(id, board.Id);
        Assert.Equal("Sensor board", board.Name);
        Assert.Equal(50m, board.Length);
        Assert.Equal(30m, board.Width);
        Assert.Equal(new[] { entry }, board.BillOfMaterials);
    }

    [Fact]
    public void Restore_WithEmptyBillOfMaterials_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Board.Restore(Guid.NewGuid(), "Sensor board", null, 50m, 30m, []));
    }

    [Fact]
    public void Rename_WithBlankName_ThrowsAndKeepsOriginalName()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [Entry()]);

        Assert.Throws<DomainException>(() => board.Rename(" "));

        Assert.Equal("Controller board", board.Name);
    }

    [Fact]
    public void ChangeDescription_WithText_ChangesTrimmedDescription()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [Entry()]);

        board.ChangeDescription(" Revision B ");

        Assert.Equal("Revision B", board.Description);
    }

    [Fact]
    public void ChangeDimensions_WithValidValues_ChangesBothDimensions()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [Entry()]);

        board.ChangeDimensions(233.35m, 160m);

        Assert.Equal(233.35m, board.Length);
        Assert.Equal(160m, board.Width);
    }

    [Fact]
    public void ChangeDimensions_WithInvalidWidth_ThrowsAndKeepsBothDimensions()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [Entry()]);

        Assert.Throws<DomainException>(() => board.ChangeDimensions(200m, 0m));

        Assert.Equal(160m, board.Length);
        Assert.Equal(100m, board.Width);
    }

    [Fact]
    public void SetBomEntry_WithNewComponent_AddsEntry()
    {
        var existing = Entry();
        var board = Board.Create("Controller board", null, 160m, 100m, [existing]);
        var componentId = Guid.NewGuid();

        board.SetBomEntry(componentId, 4);

        Assert.Equal(new[] { existing, new BomEntry(componentId, 4) }, board.BillOfMaterials);
    }

    [Fact]
    public void SetBomEntry_WithExistingComponent_ReplacesEntryInPlace()
    {
        var first = Entry(12);
        var second = Entry(4);
        var board = Board.Create("Controller board", null, 160m, 100m, [first, second]);

        board.SetBomEntry(first.ComponentId, 20);

        Assert.Equal(new[] { new BomEntry(first.ComponentId, 20), second }, board.BillOfMaterials);
    }

    [Fact]
    public void SetBomEntry_WithNonPositiveQuantity_ThrowsAndKeepsEntries()
    {
        var entry = Entry(12);
        var board = Board.Create("Controller board", null, 160m, 100m, [entry]);

        Assert.Throws<DomainException>(() => board.SetBomEntry(entry.ComponentId, 0));

        Assert.Equal(new[] { entry }, board.BillOfMaterials);
    }

    [Fact]
    public void RemoveBomEntry_WithExistingComponent_RemovesEntry()
    {
        var first = Entry();
        var second = Entry();
        var board = Board.Create("Controller board", null, 160m, 100m, [first, second]);

        board.RemoveBomEntry(first.ComponentId);

        Assert.Equal(new[] { second }, board.BillOfMaterials);
    }

    [Fact]
    public void RemoveBomEntry_WithLastEntry_ThrowsAndKeepsEntry()
    {
        var entry = Entry();
        var board = Board.Create("Controller board", null, 160m, 100m, [entry]);

        var exception = Assert.Throws<DomainException>(() => board.RemoveBomEntry(entry.ComponentId));

        Assert.Equal("The last BOM entry of board 'Controller board' cannot be removed.", exception.Message);
        Assert.Equal(new[] { entry }, board.BillOfMaterials);
    }

    [Fact]
    public void RemoveBomEntry_WithUnknownComponent_ThrowsDomainException()
    {
        var board = Board.Create("Controller board", null, 160m, 100m, [Entry(), Entry()]);

        Assert.Throws<DomainException>(() => board.RemoveBomEntry(Guid.NewGuid()));
    }

    private static BomEntry Entry(int quantity = 1) => new(Guid.NewGuid(), quantity);
}
