// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Configuration.Options;

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
    [
        ..ConfigurationSchemaBuilder.Build(typeof(CoreOptions)),
        ..ConfigurationSchemaBuilder.Build(typeof(AssemblyLoadingOptions)),
        ..ConfigurationSchemaBuilder.Build(typeof(TelemetryOptions)),
        ..ConfigurationSchemaBuilder.Build(typeof(AgentOptions)),
        ..ConfigurationSchemaBuilder.Build(typeof(CompletionOptions)),
        ..ConfigurationSchemaBuilder.Build(typeof(ExtensionsOptions)),
        ..ConfigurationSchemaBuilder.Build(typeof(ClientsOptions)),
        ..ConfigurationSchemaBuilder.Build(typeof(DefaultsOptions))
    ];
}
