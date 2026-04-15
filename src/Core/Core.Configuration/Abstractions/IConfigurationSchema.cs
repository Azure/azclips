namespace Core.Configuration.Abstractions;

public interface IConfigurationSchema
{
    IReadOnlyList<ConfigurationKeyMetadata> GetAllKeys();

    ConfigurationKeyMetadata? GetKeyMetadata(string key);

    bool IsKnownKey(string key);
}
