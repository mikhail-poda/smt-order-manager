using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Components;
using SmtOrderManager.Infrastructure.Persistence.Json;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Json;

/// <summary>
/// Runs the repository contract against JSON files and covers what only the JSON repositories
/// do, through the component repository: stored order, the data file and damaged data.
/// </summary>
public sealed class JsonComponentRepositoryTests : ComponentRepositoryContractTests, IDisposable
{
    private readonly JsonStorageTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SaveAsync_WithExistingId_KeepsStoredPositionAndAppendsNewOnes()
    {
        var repository = CreateRepository();
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        await repository.SaveAsync([resistor, capacitor], Token);
        var inductor = Component.Create("IND-10U-0805", null);

        resistor.Rename("RES-10K-0603");
        await repository.SaveAsync([inductor, resistor], Token);

        var all = await CreateRepository().SearchAsync(null, Token);
        Assert.Equal(["RES-10K-0603", "CAP-100N-0402", "IND-10U-0805"], all.Select(component => component.Name));
    }

    [Fact]
    public async Task SaveAsync_WithEmptyBatch_CreatesNoDataFile()
    {
        await CreateRepository().SaveAsync([], Token);

        Assert.False(Directory.Exists(_context.DataDirectory));
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

    protected override IComponentRepository CreateRepository() =>
        new JsonComponentRepository(_context.CreateStore<ComponentDocument>(JsonComponentRepository.FileName));
}
