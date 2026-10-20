// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using System.Text.Json;
using Core.Abstractions;
using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

internal sealed class SourceAwareConfigurationServiceAdapter(IConfigurationService inner) : ISourceAwareConfigurationService
{
    public async Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default)
    {
        var configured = await inner.ListConfigurationAsync(includeDefaults: false, cancellationToken).ConfigureAwait(false);
        var all = await inner.ListConfigurationAsync(includeDefaults: true, cancellationToken).ConfigureAwait(false);
        var schema = inner.GetSchema();
        var configuredKeys = configured.Keys.Select(schema.GetPublicKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, ConfigurationValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in all)
        {
            var key = schema.GetPublicKey(entry.Key);
            result[key] = new ConfigurationValue(
                key,
                entry.Value,
                configuredKeys.Contains(key) ? "global config" : "default");
        }

        foreach (var (environmentVariable, key) in EnvironmentVariableMappings.Mappings)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            if (value is not null && (RecoveryConfigurationValidation.IsRecoveryKey(key) || !string.IsNullOrWhiteSpace(value)))
            {
                var parsed = ParseEnvironmentValue(value);
                RecoveryConfigurationValidation.ValidateValues([new(key, parsed)], schema, new DefaultConfigurationValidator(schema));
                result[key] = new ConfigurationValue(key, parsed, "environment");
            }
        }

        return result;
    }

    private static object? ParseEnvironmentValue(string value)
    {
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var parsed = JsonNode.Parse(value);
            return parsed is JsonValue jsonValue ? ConvertJsonValue(jsonValue) : parsed;
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static object? ConvertJsonValue(JsonValue value)
    {
        if (value.TryGetValue<bool>(out var boolean)) return boolean;
        if (value.TryGetValue<int>(out var integer)) return integer;
        if (value.TryGetValue<long>(out var longValue)) return longValue;
        if (value.TryGetValue<double>(out var doubleValue)) return doubleValue;
        if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
        if (value.TryGetValue<string>(out var text)) return text;
        return value.ToJsonString();
    }
}
