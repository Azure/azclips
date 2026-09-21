// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Configuration.Schema;

public sealed class ConfigurationSchema : IConfigurationSchema
{
    private static readonly IReadOnlyList<ConfigurationKeyMetadata> GeneratedKeys = BuildKeys();

    public IReadOnlyList<ConfigurationKeyMetadata> GetAllKeys() => GeneratedKeys;

    public ConfigurationKeyMetadata? GetKeyMetadata(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return GeneratedKeys.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsKnownKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return GeneratedKeys.Any(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<ConfigurationKeyMetadata> BuildKeys() =>
        ConfigurationSchemaBuilder.BuildAll(typeof(ConfigurationSectionAttribute).Assembly);
}
