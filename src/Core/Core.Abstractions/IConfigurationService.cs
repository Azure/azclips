// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IConfigurationService
{
    T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new();

    Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default);

    Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default);

    async Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default)
    {
        var configured = await ListConfigurationAsync(includeDefaults: false, cancellationToken).ConfigureAwait(false);
        var all = await ListConfigurationAsync(includeDefaults: true, cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, ConfigurationValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in all)
        {
            result[entry.Key] = new ConfigurationValue(
                entry.Key,
                entry.Value,
                configured.ContainsKey(entry.Key) ? "global config" : "default");
        }

        return result;
    }

    Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default);

    string GetConfigFilePath();

    IConfigurationSchema GetSchema();
}
