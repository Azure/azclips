namespace Core.Configuration.Abstractions;

public interface IConfigurationProvider
{
    string GetConfigFilePath();

    Task<Dictionary<string, object?>> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(Dictionary<string, object?> configuration, CancellationToken cancellationToken = default);
}
