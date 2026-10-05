using SmtOrderManager.Api.Authentication;

namespace SmtOrderManager.Api.Tests.Authentication;

public sealed class PasswordHashingTests
{
    [Fact]
    public void Verify_WithHashOfSamePassword_ReturnsTrue()
    {
        var hash = PasswordHashing.Hash("smt-demo");

        Assert.True(PasswordHashing.Verify(hash, "smt-demo"));
    }

    [Fact]
    public void Verify_WithOtherPassword_ReturnsFalse()
    {
        var hash = PasswordHashing.Hash("smt-demo");

        Assert.False(PasswordHashing.Verify(hash, "smt-demo "));
    }

    [Fact]
    public void Hash_SamePasswordTwice_UsesDifferentSalts()
    {
        Assert.NotEqual(PasswordHashing.Hash("smt-demo"), PasswordHashing.Hash("smt-demo"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("smt-demo")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    public void IsWellFormed_WithoutHashFormat_ReturnsFalse(string text)
    {
        Assert.False(PasswordHashing.IsWellFormed(text));
        Assert.False(PasswordHashing.Verify(text, "smt-demo"));
    }
}
