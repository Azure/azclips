// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IConfigurationService
{
    T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new();

    Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default);

    Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default);

    Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default);

    string GetConfigFilePath();

    IConfigurationSchema GetSchema();
}
