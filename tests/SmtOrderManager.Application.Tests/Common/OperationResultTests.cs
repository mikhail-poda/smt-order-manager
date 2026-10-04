using SmtOrderManager.Application.Common;

namespace SmtOrderManager.Application.Tests.Common;

public sealed class OperationResultTests
{
    [Fact]
    public void Success_WithValue_SucceedsWithoutViolations()
    {
        var result = OperationResult.Success(42);

        Assert.True(result.Succeeded);
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Failure_WithViolations_FailsAndThrowsOnValue()
    {
        var violation = new Violation("Item 1", "Name must not be blank.");

        var result = OperationResult.Failure<int>([violation]);

        Assert.False(result.Succeeded);
        Assert.Equal(new[] { violation }, result.Violations);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_WithoutViolations_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => OperationResult.Failure<int>([]));
    }
}
