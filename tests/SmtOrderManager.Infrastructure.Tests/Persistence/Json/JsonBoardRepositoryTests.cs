using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Infrastructure.Persistence.Json;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Json;

/// <summary>
/// Runs the board repository contract against JSON files and covers damaged board data.
/// </summary>
public sealed class JsonBoardRepositoryTests : BoardRepositoryContractTests, IDisposable
{
    private readonly JsonStorageTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetByIdAsync_WithStoredEmptyBillOfMaterials_ThrowsInvalidDataException()
    {
        var id = Guid.NewGuid();
        await _context.WriteFileAsync(
            JsonBoardRepository.FileName,
            $$"""
            [{
              "id": "{{id}}", "name": "Controller", "description": "",
              "length": 100, "width": 80, "billOfMaterials": []
            }]
            """,
            Token);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateRepository().GetByIdAsync(id, Token));

        Assert.Contains($"board {id}", exception.Message);
    }

    protected override IBoardRepository CreateRepository() =>
        new JsonBoardRepository(_context.CreateStore<BoardDocument>(JsonBoardRepository.FileName));
}
