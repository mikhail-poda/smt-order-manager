using System.Globalization;
using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Tests.Common;

public sealed class GuardTests
{
    public static TheoryData<decimal> NonPositiveDecimals => [0m, -0.01m, -100m];

    public static TheoryData<decimal> PositiveDecimals => [0.01m, 1m, 457.2m];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void NotBlank_WithBlankValue_ThrowsDomainException(string? value)
    {
        var exception = Assert.Throws<DomainException>(() => Guard.NotBlank(value));

        Assert.Equal("value must not be blank.", exception.Message);
    }

    [Fact]
    public void NotBlank_WithSurroundingWhitespace_ReturnsTrimmedValue()
    {
        var result = Guard.NotBlank("  RES-10K-0402 ");

        Assert.Equal("RES-10K-0402", result);
    }

    [Fact]
    public void NotBlank_WithExplicitName_UsesNameInMessage()
    {
        var exception = Assert.Throws<DomainException>(() => Guard.NotBlank(" ", "Name"));

        Assert.Equal("Name must not be blank.", exception.Message);
    }

    [Fact]
    public void NotEmpty_WithEmptyGuid_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => Guard.NotEmpty(Guid.Empty, "ComponentId"));

        Assert.Equal("ComponentId must not be empty.", exception.Message);
    }

    [Fact]
    public void NotEmpty_WithNonEmptyGuid_ReturnsValue()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, Guard.NotEmpty(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Positive_WithNonPositiveInt_ThrowsDomainException(int value)
    {
        Assert.Throws<DomainException>(() => Guard.Positive(value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Positive_WithPositiveInt_ReturnsValue(int value)
    {
        Assert.Equal(value, Guard.Positive(value));
    }

    [Theory]
    [MemberData(nameof(NonPositiveDecimals))]
    public void Positive_WithNonPositiveDecimal_ThrowsDomainException(decimal value)
    {
        Assert.Throws<DomainException>(() => Guard.Positive(value));
    }

    [Theory]
    [MemberData(nameof(PositiveDecimals))]
    public void Positive_WithPositiveDecimal_ReturnsValue(decimal value)
    {
        Assert.Equal(value, Guard.Positive(value));
    }

    [Fact]
    public void Positive_WithNonPositiveDecimal_FormatsMessageIndependentOfCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        try
        {
            var exception = Assert.Throws<DomainException>(() => Guard.Positive(-0.5m, "Length"));

            Assert.Equal("Length must be positive, but was -0.5.", exception.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
