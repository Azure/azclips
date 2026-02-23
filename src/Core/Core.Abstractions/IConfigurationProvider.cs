namespace Core.Abstractions;

/// <summary>
/// Provides access to configuration values.
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value, or null if not found.</returns>
    string? Get(string key);
}
