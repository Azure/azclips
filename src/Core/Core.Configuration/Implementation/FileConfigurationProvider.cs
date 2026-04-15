using System.Text.Json;
using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

public sealed class FileConfigurationProvider : IConfigurationProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public string GetConfigFilePath()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homePath, ".cirrus", "config.json");
    }

    public async Task<Dictionary<string, object?>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var path = GetConfigFilePath();
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var result = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, SerializerOptions);
        return result ?? [];
    }

    public async Task WriteAsync(Dictionary<string, object?> configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var path = GetConfigFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(configuration, SerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }
}
