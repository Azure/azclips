// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Configuration.Options;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Config.Commands;

public abstract class ConfigCommandBase : IProcessor
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected OutputStreamDelegate WriteOutput = null!;
    protected StartActivityDelegate StartActivity { get; set; } = null!;
    protected CancellationToken CancellationToken { get; private set; }

    public IProcessor Init(ProcessorContext context)
    {
        WriteOutput = context.OutputHandler.WriteOutput;
        StartActivity = GetTelemetryService().StartActivity;
        CancellationToken = context.Publisher.Token;
        return this;
    }

    public abstract Task ProcessAsync();

    protected static IConfigurationService GetConfigurationService() => AzclipsServiceProvider.GetRequiredService<IConfigurationService>();

    protected static ClientType GetClientType() => AzclipsServiceProvider.GetRequiredService<ClientType>();

    protected static ISourceAwareConfigurationService GetSourceAwareConfigurationService() =>
        AzclipsServiceProvider.GetRequiredService<ISourceAwareConfigurationService>();

    /// <summary>
    /// Normalizes a configuration key by replacing colons with dots and removing empty entries.
    /// Ensures consistent key handling across CLI and PowerShell inputs (dot vs colon notation).
    /// </summary>
    protected static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return string.Join(
            '.',
            key.Replace(':', '.')
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    protected static ITelemetryService GetTelemetryService() => AzclipsServiceProvider.GetRequiredService<ITelemetryService>();

    protected static IConfigurationSchema GetConfigurationSchema() => GetConfigurationService().GetSchema();

    protected async Task<Dictionary<string, ConfigValueView>> GetResolvedEntriesAsync(bool includeDefaults)
    {
        var entries = new Dictionary<string, ConfigValueView>(StringComparer.OrdinalIgnoreCase);

        var snapshot = await GetSourceAwareConfigurationService().ListConfigurationWithSourcesAsync(CancellationToken).ConfigureAwait(false);
        foreach (var entry in snapshot)
        {
            if (includeDefaults || entry.Value.Source != "default")
                entries[entry.Key] = new ConfigValueView(entry.Key, entry.Value.Value, entry.Value.Source);
        }

        return entries;
    }

    protected static object? ParseValue(string value)
    {
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var parsed = JsonNode.Parse(value);
            return parsed switch
            {
                JsonValue jsonValue => ConvertJsonValue(jsonValue),
                null => null,
                _ => parsed
            };
        }
        catch (JsonException)
        {
            return value;
        }
    }

    protected static SerializedJson SerializeAsJsonValue(object value) =>
        new(JsonSerializer.Serialize(value, s_jsonOptions));

    protected static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            JsonNode jsonNode => jsonNode.ToJsonString(s_jsonOptions),
            string stringValue => stringValue,
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            _ => Convert.ToString(value) ?? string.Empty
        };
    }

    protected readonly record struct ConfigValueView(string Key, object? Value, string Source);

    /// <summary>
    /// Reconstructs a nested JSON object from flat dot-notation keys.
    /// E.g., "core.logging.level"="debug", "core.logging.enabled"="true"
    /// becomes { "logging": { "level": "debug", "enabled": true } }
    /// </summary>
    protected static JsonObject ReconstructNestedObject(string sectionPrefix, List<KeyValuePair<string, ConfigValueView>> entries)
    {
        var obj = new JsonObject();

        foreach (var entry in entries)
        {
            var key = entry.Key;
            var value = entry.Value.Value;

            // Remove prefix: "core.logging.level" → "logging.level"
            var suffix = key.Length > sectionPrefix.Length
                ? key.Substring(sectionPrefix.Length + 1)
                : key;

            var segments = suffix.Split('.', StringSplitOptions.RemoveEmptyEntries);

            // Navigate/create nested structure
            var current = obj;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i];
                if (current[segment] is not JsonObject next)
                {
                    next = new JsonObject();
                    current[segment] = next;
                }

                current = next;
            }

            // Set leaf value
            if (segments.Length > 0)
            {
                var leaf = segments[segments.Length - 1];
                current[leaf] = ToJsonNode(value);
            }
        }

        return obj;
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode jsonNode => jsonNode.DeepClone(),
            _ => JsonValue.Create(value)
        };
    }

    /// <summary>
    /// Formats nested JSON object as a readable key=value table for CLI output.
    /// </summary>
    protected static string FormatNestedAsTable(JsonObject obj)
    {
        var lines = new List<string>();
        FormatNestedRecursive(obj, string.Empty, lines, string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    private static void FormatNestedRecursive(JsonObject obj, string keyPrefix, List<string> lines, string indent)
    {
        foreach (var (key, value) in obj)
        {
            var fullKey = string.IsNullOrEmpty(keyPrefix) ? key : $"{keyPrefix}.{key}";

            if (value is JsonObject nested && nested.Count > 0)
            {
                FormatNestedRecursive(nested, fullKey, lines, indent + "  ");
            }
            else
            {
                var formattedValue = value switch
                {
                    JsonValue jsonValue => FormatJsonValue(jsonValue),
                    _ => FormatValue(value)
                };
                lines.Add($"{indent}{fullKey} = {formattedValue}");
            }
        }
    }

    private static string FormatJsonValue(JsonValue jsonValue)
    {
        try
        {
            if (jsonValue.TryGetValue<bool>(out var boolValue))
                return boolValue.ToString().ToLowerInvariant();
            if (jsonValue.TryGetValue<int>(out var intValue))
                return intValue.ToString();
            if (jsonValue.TryGetValue<long>(out var longValue))
                return longValue.ToString();
            if (jsonValue.TryGetValue<double>(out var doubleValue))
                return doubleValue.ToString();
            if (jsonValue.TryGetValue<decimal>(out var decimalValue))
                return decimalValue.ToString();
            if (jsonValue.TryGetValue<string>(out var stringValue))
                return stringValue;
            return jsonValue.ToJsonString();
        }
        catch
        {
            return jsonValue.ToJsonString();
        }
    }


    private static object? ConvertJsonValue(JsonValue jsonValue)
    {
        if (jsonValue.TryGetValue<bool>(out var boolValue)) return boolValue;
        if (jsonValue.TryGetValue<int>(out var intValue)) return intValue;
        if (jsonValue.TryGetValue<long>(out var longValue)) return longValue;
        if (jsonValue.TryGetValue<double>(out var doubleValue)) return doubleValue;
        if (jsonValue.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
        if (jsonValue.TryGetValue<string>(out var stringValue)) return stringValue;
        return jsonValue.ToJsonString();
    }

    private static void AddEnvironment(IDictionary<string, object?> entries, string environmentName, string key)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        entries[key] = ParseValue(value);
    }

}
