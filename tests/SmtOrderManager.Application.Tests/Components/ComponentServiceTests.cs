using Microsoft.Extensions.Logging.Abstractions;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Components;
using SmtOrderManager.Application.Tests.Fakes;
using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Application.Tests.Components;

public sealed class ComponentServiceTests
{
    private readonly InMemoryComponentRepository _repository = new();
    private readonly InMemoryBoardRepository _boards = new();
    private readonly ComponentService _service;

    public ComponentServiceTests()
    {
        _service = new ComponentService(_repository, _boards, NullLogger<ComponentService>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAsync_WithValidBatch_SavesAllAndReturnsDetails()
    {
        var result = await _service.CreateAsync(
            [new CreateComponentCommand("RES-10K-0402", "Resistor 10 kΩ"), new CreateComponentCommand("CAP-100N-0402", null)],
            Token);

        Assert.True(result.Succeeded);
        Assert.Equal(["RES-10K-0402", "CAP-100N-0402"], result.Value.Select(details => details.Name));
        Assert.Equal(2, _repository.Count);

        var stored = await _repository.GetByIdAsync(result.Value[0].Id, Token);
        Assert.Equal("Resistor 10 kΩ", stored!.Description);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidItems_SavesNothingAndReportsEachItem()
    {
        var result = await _service.CreateAsync(
            [new CreateComponentCommand("RES-10K-0402", null), new CreateComponentCommand(" ", null), new CreateComponentCommand("", null)],
            Token);

        Assert.False(result.Succeeded);
        Assert.Equal(
            [
                new Violation("Item 2", "Name must not be blank."),
                new Violation("Item 3", "Name must not be blank.")
            ],
            result.Violations);
        Assert.Equal(0, _repository.Count);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyBatch_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync([], Token));
    }

    [Fact]
    public async Task UpdateAsync_WithValidBatch_SavesChanges()
    {
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        _repository.Seed(resistor, capacitor);

        var result = await _service.UpdateAsync(
            [new UpdateComponentCommand(resistor.Id, "RES-10K-0603", "Resistor 10 kΩ"), new UpdateComponentCommand(capacitor.Id, "CAP-100N-0402", "Capacitor")],
            Token);

        Assert.True(result.Succeeded);
        var storedResistor = await _repository.GetByIdAsync(resistor.Id, Token);
        Assert.Equal("RES-10K-0603", storedResistor!.Name);
        Assert.Equal("Resistor 10 kΩ", storedResistor.Description);
        Assert.Equal("Capacitor", (await _repository.GetByIdAsync(capacitor.Id, Token))!.Description);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownComponent_SavesNothingAndReportsIt()
    {
        var resistor = Component.Create("RES-10K-0402", null);
        _repository.Seed(resistor);
        var unknownId = Guid.NewGuid();

        var result = await _service.UpdateAsync(
            [new UpdateComponentCommand(resistor.Id, "RES-10K-0603", null), new UpdateComponentCommand(unknownId, "CAP-100N-0402", null)],
            Token);

        Assert.Equal([new Violation($"Component {unknownId}", "Component not found.")], result.Violations);
        Assert.Equal("RES-10K-0402", (await _repository.GetByIdAsync(resistor.Id, Token))!.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithBlankName_SavesNothingAndKeepsStoredValues()
    {
        var resistor = Component.Create("RES-10K-0402", "Resistor");
        _repository.Seed(resistor);

        var result = await _service.UpdateAsync([new UpdateComponentCommand(resistor.Id, " ", "Changed")], Token);

        Assert.Equal(
            [new Violation("Component 'RES-10K-0402'", "Name must not be blank.")],
            result.Violations);
        var stored = await _repository.GetByIdAsync(resistor.Id, Token);
        Assert.Equal("RES-10K-0402", stored!.Name);
        Assert.Equal("Resistor", stored.Description);
    }

    [Fact]
    public async Task UpdateAsync_WithSameComponentTwice_SavesNothingAndReportsDuplicate()
    {
        var resistor = Component.Create("RES-10K-0402", null);
        _repository.Seed(resistor);

        var result = await _service.UpdateAsync(
            [new UpdateComponentCommand(resistor.Id, "RES-10K-0603", null), new UpdateComponentCommand(resistor.Id, "RES-10K-0805", null)],
            Token);

        Assert.Equal(
            [new Violation("Component 'RES-10K-0402'", "Component appears more than once in the batch.")],
            result.Violations);
        Assert.Equal("RES-10K-0402", (await _repository.GetByIdAsync(resistor.Id, Token))!.Name);
    }

    [Theory]
    [InlineData(null, new[] { "CAP-100N-0402", "led-red-0603", "RES-10K-0402" })]
    [InlineData("0402", new[] { "CAP-100N-0402", "RES-10K-0402" })]
    public async Task SearchAsync_WithText_ReturnsMatchesSortedByName(string? text, string[] expectedNames)
    {
        _repository.Seed(
            Component.Create("RES-10K-0402", null),
            Component.Create("led-red-0603", null),
            Component.Create("CAP-100N-0402", null));

        var result = await _service.SearchAsync(text, Token);

        Assert.Equal(expectedNames, result.Select(details => details.Name));
    }

    [Fact]
    public async Task RemoveAsync_WithExistingComponents_RemovesThemAndReturnsCount()
    {
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        var led = Component.Create("LED-RED-0603", null);
        _repository.Seed(resistor, capacitor, led);

        var result = await _service.RemoveAsync([resistor.Id, capacitor.Id, resistor.Id], Token);

        Assert.Equal(2, result.Value);
        Assert.Equal(1, _repository.Count);
        Assert.NotNull(await _repository.GetByIdAsync(led.Id, Token));
    }

    [Fact]
    public async Task RemoveAsync_WithUnknownComponent_RemovesNothingAndReportsIt()
    {
        var resistor = Component.Create("RES-10K-0402", null);
        _repository.Seed(resistor);
        var unknownId = Guid.NewGuid();

        var result = await _service.RemoveAsync([resistor.Id, unknownId], Token);

        Assert.Equal([new Violation($"Component {unknownId}", "Component not found.")], result.Violations);
        Assert.Equal(1, _repository.Count);
    }

    [Fact]
    public async Task RemoveAsync_WithComponentUsedByBoards_RemovesNothingAndReportsEveryBoard()
    {
        var resistor = Component.Create("RES-10K-0402", null);
        var capacitor = Component.Create("CAP-100N-0402", null);
        var unused = Component.Create("LED-RED-0603", null);
        _repository.Seed(resistor, capacitor, unused);
        _boards.Seed(
            Board.Create("Sensor board", null, 50m, 30m, [new BomEntry(resistor.Id, 3)]),
            Board.Create("Controller board", null, 160m, 100m, [new BomEntry(resistor.Id, 12), new BomEntry(capacitor.Id, 4)]));

        var result = await _service.RemoveAsync([unused.Id, resistor.Id, capacitor.Id], Token);

        Assert.Equal(
            [
                new Violation("Component 'RES-10K-0402'", "Component is used by board 'Controller board'."),
                new Violation("Component 'RES-10K-0402'", "Component is used by board 'Sensor board'."),
                new Violation("Component 'CAP-100N-0402'", "Component is used by board 'Controller board'.")
            ],
            result.Violations);
        Assert.Equal(3, _repository.Count);
    }
}