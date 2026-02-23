using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Core.Runtime.Config;

public class PersistableJsonConfigurationSource : JsonConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        ResolveFileProvider();
        EnsureDefaults(builder);
        return new PersistableJsonConfigurationProvider(this);
    }
}

public class PersistableJsonConfigurationProvider(PersistableJsonConfigurationSource source) : JsonConfigurationProvider(source)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public override void Set(string key, string? value)
    {
        base.Set(key, value);
        if (!string.IsNullOrEmpty(Source.Path))
        {
            Save(Source.Path);
        }
    }

    private void Save(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var data = new Dictionary<string, string?>(Data);
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(filePath, json);
    }
}