using System.Text.Json.Nodes;
using Core.Configuration.Abstractions;

namespace Scenario.Tests;

/// <summary>
/// Keeps configuration in memory so scenario runs never read or write the developer's
/// <c>~/.azclips/config.json</c>.
/// </summary>
internal sealed class InMemoryConfigurationProvider : IConfigurationProvider
{
    private JsonObject _configuration = [];

    public string GetConfigFilePath() => "<in-memory>";

    public Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((JsonObject)_configuration.DeepClone());

    public Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default)
    {
        _configuration = configuration;
        return Task.CompletedTask;
    }
}
