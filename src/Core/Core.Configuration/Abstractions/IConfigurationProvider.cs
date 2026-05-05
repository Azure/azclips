using System.Text.Json.Nodes;

namespace Core.Configuration.Abstractions;

public interface IConfigurationProvider
{
    string GetConfigFilePath();

    Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default);
}
