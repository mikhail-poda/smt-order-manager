using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmtOrderManager.Application;
using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Components;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Orders;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Infrastructure.Persistence.Json;
using SmtOrderManager.Infrastructure.SmtLine;

namespace SmtOrderManager.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Checks the registrations the CLI composition root relies on: every use case resolves, and
/// invalid configuration fails at startup.
/// </summary>
/// <remarks>
/// The CLI has no test project of its own, and this project already references Application
/// and Infrastructure, the two layers whose registrations are combined here.
/// </remarks>
public sealed class ServiceRegistrationTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["JsonStorage:DataDirectory"] = "data",
        ["SimulatedSmtLine:LineId"] = "SMT-TEST-01",
        ["SimulatedSmtLine:InboxDirectory"] = "inbox",
        ["SimulatedSmtLine:SupportedSchemaVersions:0"] = "1",
        ["SimulatedSmtLine:MaxBoardLength"] = "300",
        ["SimulatedSmtLine:MaxBoardWidth"] = "200.5",
        ["SimulatedSmtLine:IsAvailable"] = "false",
    };

    [Fact]
    public void AddApplicationAndInfrastructure_ResolvesEveryUseCase()
    {
        using var provider = BuildProvider(ValidSettings);

        Assert.NotNull(provider.GetRequiredService<ComponentService>());
        Assert.NotNull(provider.GetRequiredService<BoardService>());
        Assert.NotNull(provider.GetRequiredService<OrderService>());
        Assert.NotNull(provider.GetRequiredService<OrderDownloadService>());
    }

    [Fact]
    public void AddInfrastructure_RegistersPortsAsSingletons()
    {
        using var provider = BuildProvider(ValidSettings);

        Assert.IsType<JsonComponentRepository>(provider.GetRequiredService<IComponentRepository>());
        Assert.IsType<JsonBoardRepository>(provider.GetRequiredService<IBoardRepository>());
        Assert.IsType<JsonOrderRepository>(provider.GetRequiredService<IOrderRepository>());
        Assert.IsType<SimulatedSmtLine>(provider.GetRequiredService<ISmtLine>());
        Assert.Same(
            provider.GetRequiredService<JsonFileStore<OrderDocument>>(),
            provider.GetRequiredService<JsonFileStore<OrderDocument>>());
    }

    [Fact]
    public void AddInfrastructure_BindsOptionsFromConfiguration()
    {
        using var provider = BuildProvider(ValidSettings);

        var storage = provider.GetRequiredService<IOptions<JsonStorageOptions>>().Value;
        var line = provider.GetRequiredService<IOptions<SimulatedSmtLineOptions>>().Value;

        Assert.Equal("data", storage.DataDirectory);
        Assert.Equal("SMT-TEST-01", line.LineId);
        Assert.Equal("inbox", line.InboxDirectory);
        Assert.Equal([1], line.SupportedSchemaVersions);
        Assert.Equal(300m, line.MaxBoardLength);
        Assert.Equal(200.5m, line.MaxBoardWidth);
        Assert.False(line.IsAvailable);
    }

    [Fact]
    public void ValidateOnStart_WithValidConfiguration_Passes()
    {
        using var provider = BuildProvider(ValidSettings);

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Theory]
    [InlineData("JsonStorage:DataDirectory", "")]
    [InlineData("SimulatedSmtLine:LineId", " ")]
    [InlineData("SimulatedSmtLine:InboxDirectory", "")]
    [InlineData("SimulatedSmtLine:MaxBoardLength", "0")]
    [InlineData("SimulatedSmtLine:MaxBoardWidth", "-5")]
    public void ValidateOnStart_WithInvalidValue_ThrowsNamingOption(string key, string value)
    {
        var settings = new Dictionary<string, string?>(ValidSettings) { [key] = value };
        using var provider = BuildProvider(settings);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(key.Split(':')[1], exception.Message);
    }

    [Fact]
    public void ValidateOnStart_WithoutSupportedSchemaVersions_Throws()
    {
        var settings = new Dictionary<string, string?>(ValidSettings);
        settings.Remove("SimulatedSmtLine:SupportedSchemaVersions:0");
        using var provider = BuildProvider(settings);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication().AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
