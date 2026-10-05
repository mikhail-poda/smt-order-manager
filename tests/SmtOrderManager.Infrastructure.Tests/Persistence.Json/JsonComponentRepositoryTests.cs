using SmtOrderManager.Domain.Components;
using SmtOrderManager.Infrastructure.Persistence.Json;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Json;

/// <summary>
/// Covers the component mapping and, through the component repository, the behaviour that all
/// JSON repositories share.
/// </summary>
public sealed class JsonComponentRepositoryTests : IDisposable
{
    private readonly JsonStorageTestContext _context = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _context.Dispose();

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
    public async Task GetByIdAsync_WithoutDataFile_ReturnsNull()
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

        Assert.Equal([capacitor.Id], loaded.Select(component => component.Id));
    }

    [Fact]
    public async Task SaveAsync_WithExistingId_ReplacesStoredAggregateInPlace()
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        await repository.SaveAsync([resistor, capacitor], Token);

        resistor.Rename("RES-10K-0603");
        await repository.SaveAsync([resistor], Token);

        var all = await repository.SearchAsync(null, Token);
        Assert.Equal(["RES-10K-0603", "CAP-100N-0402"], all.Select(component => component.Name));
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
        Assert.Equal([resistor.Id, capacitor.Id], all.Select(component => component.Id));
        Assert.Equal("Resistor 10 kΩ", all[0].Description);
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
    public async Task SaveAsync_WithEmptyBatch_CreatesNoDataFile()
    {
        await CreateRepository().SaveAsync([], Token);

        Assert.False(Directory.Exists(_context.DataDirectory));
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
        Assert.Equal([capacitor.Id], all.Select(component => component.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WithoutText_ReturnsAllInStoredOrder(string? text)
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        await repository.SaveAsync([resistor, capacitor], Token);

        var found = await repository.SearchAsync(text, Token);

        Assert.Equal([resistor.Id, capacitor.Id], found.Select(component => component.Id));
    }

    [Theory]
    [InlineData("res-10k")]
    [InlineData(" 0402 ")]
    [InlineData("CERAMIC")]
    public async Task SearchAsync_MatchesNameOrDescriptionIgnoringCaseAndSurroundingWhitespace(string text)
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0603", "Ceramic capacitor");
        await repository.SaveAsync([resistor, capacitor], Token);

        var found = await repository.SearchAsync(text, Token);

        Assert.Single(found);
    }

    [Fact]
    public async Task GetByIdAsync_WithStoredDataBreakingDomainRule_ThrowsInvalidDataExceptionNamingAggregate()
    {
        var id = Guid.NewGuid();
        await _context.WriteFileAsync(
            JsonComponentRepository.FileName,
            $$"""[{ "id": "{{id}}", "name": "   ", "description": "" }]""",
            Token);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateRepository().GetByIdAsync(id, Token));

        Assert.Contains($"component {id}", exception.Message);
        Assert.Contains(JsonComponentRepository.FileName, exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingStoredField_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await _context.WriteFileAsync(
            JsonComponentRepository.FileName,
            $$"""[{ "id": "{{id}}", "description": "" }]""",
            Token);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateRepository().GetByIdAsync(id, Token));
    }

    private JsonComponentRepository CreateRepository() =>
        new(_context.CreateStore<ComponentDocument>(JsonComponentRepository.FileName));
}
