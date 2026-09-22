// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Core.Configuration.Options;

namespace Benchmark.CLI;

internal sealed class BenchmarkAssemblyConfigurationService : IConfigurationService
{
    private readonly AssemblyLoadingOptions _options;

    public BenchmarkAssemblyConfigurationService(string installedExtensionsRoot)
    {
        _options = new AssemblyLoadingOptions
        {
            InstalledExtensionsRoot = installedExtensionsRoot,
        };
    }

    public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
    {
        if (typeof(T) == typeof(AssemblyLoadingOptions))
        {
            return (T)(object)_options;
        }
        return defaultValue ?? new T();
    }

    public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public string GetConfigFilePath() => string.Empty;

    public IConfigurationSchema GetSchema() => throw new NotSupportedException();
}
