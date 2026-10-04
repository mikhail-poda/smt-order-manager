using SmtOrderManager.Domain.Common;
using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Domain.Tests.Components;

public sealed class ComponentTests
{
    [Fact]
    public void Create_WithValidValues_SetsProperties()
    {
        var component = Component.Create("RES-10K-0402", "Resistor 10 kΩ, 0402");

        Assert.NotEqual(Guid.Empty, component.Id);
        Assert.Equal("RES-10K-0402", component.Name);
        Assert.Equal("Resistor 10 kΩ, 0402", component.Description);
    }

    [Fact]
    public void Create_CalledTwice_GeneratesDifferentIds()
    {
        var first = Component.Create("RES-10K-0402", null);
        var second = Component.Create("RES-10K-0402", null);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_TrimsNameAndDescription()
    {
        var component = Component.Create("  RES-10K-0402 ", " Resistor 10 kΩ ");

        Assert.Equal("RES-10K-0402", component.Name);
        Assert.Equal("Resistor 10 kΩ", component.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutDescription_UsesEmptyDescription(string? description)
    {
        var component = Component.Create("RES-10K-0402", description);

        Assert.Equal(string.Empty, component.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string? name)
    {
        // The null case simulates a caller that ignores the nullable annotations.
        var exception = Assert.Throws<DomainException>(() => Component.Create(name!, null));

        Assert.Equal("Name must not be blank.", exception.Message);
    }

    [Fact]
    public void Restore_WithExistingId_KeepsIdAndValues()
    {
        var id = Guid.NewGuid();

        var component = Component.Restore(id, "CAP-100N-0402", "Capacitor 100 nF, 0402");

        Assert.Equal(id, component.Id);
        Assert.Equal("CAP-100N-0402", component.Name);
        Assert.Equal("Capacitor 100 nF, 0402", component.Description);
    }

    [Fact]
    public void Restore_WithEmptyId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Component.Restore(Guid.Empty, "CAP-100N-0402", null));

        Assert.Equal("Id must not be empty.", exception.Message);
    }

    [Fact]
    public void Restore_WithBlankName_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Component.Restore(Guid.NewGuid(), " ", null));
    }

    [Fact]
    public void Rename_WithValidName_ChangesNameAndKeepsId()
    {
        var component = Component.Create("RES-10K-0402", null);
        var id = component.Id;

        component.Rename(" RES-10K-0603 ");

        Assert.Equal("RES-10K-0603", component.Name);
        Assert.Equal(id, component.Id);
    }

    [Fact]
    public void Rename_WithBlankName_ThrowsAndKeepsOriginalName()
    {
        var component = Component.Create("RES-10K-0402", null);

        Assert.Throws<DomainException>(() => component.Rename(" "));

        Assert.Equal("RES-10K-0402", component.Name);
    }

    [Fact]
    public void ChangeDescription_WithText_ChangesTrimmedDescription()
    {
        var component = Component.Create("RES-10K-0402", null);

        component.ChangeDescription(" Resistor 10 kΩ, 1 % ");

        Assert.Equal("Resistor 10 kΩ, 1 %", component.Description);
    }

    [Fact]
    public void ChangeDescription_WithNull_ClearsDescription()
    {
        var component = Component.Create("RES-10K-0402", "Resistor 10 kΩ");

        component.ChangeDescription(null);

        Assert.Equal(string.Empty, component.Description);
    }
}
