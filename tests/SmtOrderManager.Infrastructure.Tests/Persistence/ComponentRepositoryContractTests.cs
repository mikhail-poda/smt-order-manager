using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Infrastructure.Tests.Persistence;

/// <summary>
/// The repository contract, checked through the component repository for the behaviour that
/// every repository shares. Each persistence provider derives a test class from it.
/// </summary>
public abstract class ComponentRepositoryContractTests
{
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsyncAfterRestart_RestoresEveryField()
    {
        var component = Component.Create("RES-10K-0402", "Resistor 10 kΩ, 1 %");
        await CreateRepository().SaveAsync([component], Token);

        var loaded = await CreateRepository().GetByIdAsync(component.Id, Token);

        Assert.NotNull(loaded);
        Assert.Equal(component.Id, loaded.Id);
        Assert.Equal("RES-10K-0402", loaded.Name);
        Assert.Equal("Resistor 10 kΩ, 1 %", loaded.Description);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var repository = CreateRepository();
        await repository.SaveAsync([Component.Create("RES-10K-0402", null)], Token);

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), Token));
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyStore_ReturnsNull()
    {
        Assert.Null(await CreateRepository().GetByIdAsync(Guid.NewGuid(), Token));
    }

    [Fact]
    public async Task GetByIdAsync_AfterChangingLoadedAggregateWithoutSaving_ReturnsStoredValues()
    {
        var repository = CreateRepository();
        var component = Component.Create("RES-10K-0402", null);
        await repository.SaveAsync([component], Token);

        var loaded = await repository.GetByIdAsync(component.Id, Token);
        loaded!.Rename("RES-10K-0603");

        Assert.Equal("RES-10K-0402", (await repository.GetByIdAsync(component.Id, Token))!.Name);
    }

    [Fact]
    public async Task GetByIdsAsync_SkipsUnknownIds()
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        await repository.SaveAsync([resistor, capacitor], Token);

        var loaded = await repository.GetByIdsAsync([capacitor.Id, Guid.NewGuid()], Token);

        RepositoryAssert.SameIds([capacitor.Id], loaded);
    }

    [Fact]
    public async Task SaveAsync_WithExistingId_ReplacesStoredAggregate()
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        await repository.SaveAsync([resistor, capacitor], Token);

        resistor.Rename("RES-10K-0603");
        await repository.SaveAsync([resistor], Token);

        var all = await CreateRepository().SearchAsync(null, Token);
        RepositoryAssert.SameIds([resistor.Id, capacitor.Id], all);
        Assert.Equal("RES-10K-0603", all.Single(component => component.Id == resistor.Id).Name);
    }

    [Fact]
    public async Task SaveAsync_WithNewAndExistingAggregates_ReplacesAndAppendsInOneBatch()
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        await repository.SaveAsync([resistor], Token);
        var capacitor = Component.Create("CAP-100N-0402", null);

        resistor.ChangeDescription("Resistor 10 kΩ");
        await repository.SaveAsync([capacitor, resistor], Token);

        var all = await CreateRepository().SearchAsync(null, Token);
        RepositoryAssert.SameIds([resistor.Id, capacitor.Id], all);
        Assert.Equal("Resistor 10 kΩ", all.Single(component => component.Id == resistor.Id).Description);
    }

    [Fact]
    public async Task SaveAsync_WithSameAggregateTwiceInBatch_KeepsLastVersion()
    {
        var repository = CreateRepository();
        var first = Component.Create("RES-10K-0402", null);
        var second = Component.Restore(first.Id, "RES-10K-0603", null);

        await repository.SaveAsync([first, second], Token);

        var all = await repository.SearchAsync(null, Token);
        Assert.Equal(["RES-10K-0603"], all.Select(component => component.Name));
    }

    [Fact]
    public async Task RemoveAsync_RemovesBatchAndIgnoresUnknownIds()
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        var inductor = Component.Create("IND-10U-0805", null);
        await repository.SaveAsync([resistor, capacitor, inductor], Token);

        await repository.RemoveAsync([resistor.Id, inductor.Id, Guid.NewGuid()], Token);

        var all = await CreateRepository().SearchAsync(null, Token);
        RepositoryAssert.SameIds([capacitor.Id], all);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WithoutText_ReturnsAll(string? text)
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        await repository.SaveAsync([resistor, capacitor], Token);

        var found = await repository.SearchAsync(text, Token);

        RepositoryAssert.SameIds([resistor.Id, capacitor.Id], found);
    }

    [Theory]
    [InlineData("res-10k")]
    [InlineData(" 0402 ")]
    [InlineData("CERAMIC")]
    [InlineData("FÜR")]
    public async Task SearchAsync_MatchesNameOrDescriptionIgnoringCaseAndSurroundingWhitespace(string text)
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0603", "Ceramic capacitor für Filter");
        await repository.SaveAsync([resistor, capacitor], Token);

        var found = await repository.SearchAsync(text, Token);

        Assert.Single(found);
    }

    /// <summary>
    /// Creates a new repository instance on the test's storage. Saving through one instance and
    /// loading through another therefore reads the stored data, as after an application restart.
    /// </summary>
    protected abstract IComponentRepository CreateRepository();
}
