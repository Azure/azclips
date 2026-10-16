// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IConfigurationService
{
    T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new();

    Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default);

    Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reads, mutates, and writes the stored home-config value of a list-valued key.
    /// </summary>
    Task UpdateListConfigurationAsync(string key, Func<string, string> mutate, CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default);

    Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default);

    string GetConfigFilePath();

    IConfigurationSchema GetSchema();
}
