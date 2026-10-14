// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;
using Core.Configuration.Options;
using Core.Configuration.Schema;
using Microsoft.Extensions.Configuration;

namespace Core.Configuration.Implementation;

public sealed class ConfigurationService(
    Core.Configuration.Abstractions.IConfigurationProvider provider,
    IConfigurationSchema schema,
    IConfigurationValidator validator) : IConfigurationService, ISourceAwareConfigurationService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? null : CanonicalizeKnownKey(key);

        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            var keyValidation = validator.ValidateKey(normalizedKey, force: true);
            if (!keyValidation.IsValid)
            {
                throw AzclipsErrorException.InvalidInput(keyValidation.ErrorMessage!);
            }
        }

        var defaults = defaultValue ?? new T();
        var builder = new ConfigurationBuilder();
        var configFilePath = provider.GetConfigFilePath();

        var defaultEntries = BuildDefaultEntries(defaults);
        if (defaultEntries.Count > 0)
        {
            builder.AddInMemoryCollection(defaultEntries);
        }

        builder.AddJsonFile(configFilePath, optional: true, reloadOnChange: false);

        var environmentOverrides = BuildEnvironmentOverrides();
        if (environmentOverrides.Count > 0)
        {
            builder.AddInMemoryCollection(environmentOverrides);
        }

        IConfigurationRoot configuration;
        try
        {
            configuration = builder.Build();
        }
        catch (Exception ex) when (IsInvalidJsonConfigurationException(ex))
        {
            throw AzclipsErrorException.InvalidInput(
                $"Configuration file '{configFilePath}' contains invalid JSON. Fix or remove the file and try again.",
                innerException: ex);
        }

        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            return configuration.GetSection(NormalizeConfigurationPath(normalizedKey)).Get<T>() ?? defaults;
        }

        if (typeof(T).GetCustomAttributes(typeof(ConfigurationSectionAttribute), inherit: true)
            .OfType<ConfigurationSectionAttribute>()
            .SingleOrDefault() is { } section)
        {
            return configuration.GetSection(section.Name).Get<T>() ?? defaults;
        }

        return configuration.Get<T>() ?? defaults;
    }

    public async Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
    {
        var normalizedKey = CanonicalizeKnownKey(key);
        var keyValidation = validator.ValidateKey(normalizedKey, force);
        if (!keyValidation.IsValid)
        {
            throw AzclipsErrorException.InvalidInput(keyValidation.ErrorMessage!);
        }

        var valueValidation = validator.ValidateValue(normalizedKey, value);
        if (!valueValidation.IsValid)
        {
            throw AzclipsErrorException.InvalidInput(valueValidation.ErrorMessage!);
        }

        await using var lockStream = await AcquireMutationLockAsync(cancellationToken).ConfigureAwait(false);
        var root = await provider.ReadAsync(cancellationToken).ConfigureAwait(false);

        RemoveLegacyFlatKey(root, normalizedKey);
        SetValue(root, normalizedKey, JsonSerializer.SerializeToNode(value, _jsonSerializerOptions));

        await provider.WriteAsync(root, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
    {
        var normalizedKey = CanonicalizeKnownKey(key);
        var keyValidation = validator.ValidateKey(normalizedKey, force);
        if (!keyValidation.IsValid)
        {
            throw AzclipsErrorException.InvalidInput(keyValidation.ErrorMessage!);
        }

        await using var lockStream = await AcquireMutationLockAsync(cancellationToken).ConfigureAwait(false);
        var root = await provider.ReadAsync(cancellationToken).ConfigureAwait(false);

        DeleteValue(root, normalizedKey);
        RemoveLegacyFlatKey(root, normalizedKey);

        await provider.WriteAsync(root, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
    {
        var root = await provider.ReadAsync(cancellationToken).ConfigureAwait(false);
        var results = Flatten(root);

        if (!includeDefaults)
        {
            return results;
        }

        foreach (var entry in BuildDefaultKeyValues())
        {
            results.TryAdd(entry.Key, entry.Value);
        }

        return results;
    }

    public async Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default)
    {
        var root = await provider.ReadAsync(cancellationToken).ConfigureAwait(false);
        var configured = Flatten(root);
        var result = new Dictionary<string, ConfigurationValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in BuildDefaultKeyValues())
        {
            result[entry.Key] = new ConfigurationValue(entry.Key, entry.Value, "default");
        }

        foreach (var entry in configured)
        {
            result[entry.Key] = new ConfigurationValue(entry.Key, entry.Value, "global config");
        }

        foreach (var (environmentVariable, key) in Core.Configuration.Abstractions.EnvironmentVariableMappings.Mappings)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[key] = new ConfigurationValue(key, ParseEnvironmentValue(value), "environment");
            }
        }

        return result;
    }

    private object? ParseEnvironmentValue(string value)
    {
        try
        {
            var parsed = JsonNode.Parse(value);
            return parsed switch
            {
                JsonValue jsonValue => ConvertNodeToValue(jsonValue),
                null => null,
                _ => parsed
            };
        }
        catch (JsonException)
        {
            return value;
        }
    }

    public async Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = CanonicalizeKnownKey(name);
        var key = normalizedName.StartsWith("defaults.", StringComparison.OrdinalIgnoreCase)
            ? normalizedName
            : $"defaults.{normalizedName}";

        var entries = await ListConfigurationAsync(includeDefaults: false, cancellationToken).ConfigureAwait(false);
        return entries.TryGetValue(key, out var value) ? value : null;
    }

    public string GetConfigFilePath() => provider.GetConfigFilePath();

    public IConfigurationSchema GetSchema() => schema;

    private async Task<FileStream> AcquireMutationLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = $"{provider.GetConfigFilePath()}.lock";
        var directory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private Dictionary<string, string?> BuildDefaultEntries<T>(T defaults) where T : class
    {
        var results = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var node = JsonSerializer.SerializeToNode(defaults, _jsonSerializerOptions) as JsonObject;
        if (node is null)
        {
            return results;
        }

        var sectionName = typeof(T).GetCustomAttributes(typeof(ConfigurationSectionAttribute), inherit: true)
            .OfType<ConfigurationSectionAttribute>()
            .SingleOrDefault()?.Name;
        if (sectionName is null)
        {
            FlattenNode(node, string.Empty, results);
            return results;
        }

        FlattenNode(node, sectionName, results);
        return results;
    }

    private static Dictionary<string, string?> BuildEnvironmentOverrides()
    {
        var mappings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (envVarName, configKey) in Abstractions.EnvironmentVariableMappings.Mappings)
        {
            AddEnvironmentMapping(mappings, envVarName, NormalizeConfigurationPath(configKey));
        }

        return mappings;
    }

    private static void AddEnvironmentMapping(IDictionary<string, string?> mappings, string environmentName, string key)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            mappings[key] = value;
        }
    }

    private string CanonicalizeKnownKey(string key)
    {
        var normalizedKey = NormalizeKey(key);
        return schema.GetKeyMetadata(normalizedKey)?.Key ?? normalizedKey;
    }

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return string.Join(
            '.',
            key.Replace(':', '.')
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeConfigurationPath(string key) => NormalizeKey(key).Replace('.', ':');

    private static bool IsInvalidJsonConfigurationException(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is FormatException or InvalidDataException or JsonException)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveLegacyFlatKey(JsonObject root, string key)
    {
        root.Remove(key);
        root.Remove(key.Replace('.', ':'));
    }

    private static void SetValue(JsonObject root, string key, JsonNode? value)
    {
        var segments = NormalizeKey(key).Split('.');
        JsonObject current = root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            if (current[segment] is not JsonObject next)
            {
                next = [];
                current[segment] = next;
            }

            current = next;
        }

        current[segments[^1]] = value;
    }

    private static void DeleteValue(JsonObject root, string key)
    {
        var segments = NormalizeKey(key).Split('.');
        DeleteValue(root, segments, 0);
    }

    private static bool DeleteValue(JsonObject current, IReadOnlyList<string> segments, int index)
    {
        var segment = segments[index];
        if (index == segments.Count - 1)
        {
            current.Remove(segment);
            return current.Count == 0;
        }

        if (current[segment] is not JsonObject child)
        {
            return current.Count == 0;
        }

        var shouldRemoveChild = DeleteValue(child, segments, index + 1);
        if (shouldRemoveChild)
        {
            current.Remove(segment);
        }

        return current.Count == 0;
    }

    private Dictionary<string, object?> BuildDefaultKeyValues()
    {
        return ConfigurationSchemaBuilder.BuildDefaultValues(typeof(ConfigurationSectionAttribute).Assembly);
    }

    private Dictionary<string, object?> Flatten(JsonObject root, string? prefix = null)
    {
        var results = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in root)
        {
            var key = string.IsNullOrWhiteSpace(prefix) ? property.Key : $"{prefix}.{property.Key}";

            if (property.Value is JsonObject childObject)
            {
                foreach (var child in Flatten(childObject, key))
                {
                    results[child.Key] = child.Value;
                }

                continue;
            }

            results[key] = ConvertNodeToValue(property.Value);
        }

        return results;
    }

    private void FlattenNode(JsonObject root, string prefix, IDictionary<string, string?> destination)
    {
        foreach (var property in root)
        {
            var key = string.IsNullOrWhiteSpace(prefix) ? property.Key : $"{prefix}:{property.Key}";
            if (property.Value is JsonObject childObject)
            {
                FlattenNode(childObject, key, destination);
                continue;
            }

            destination[key] = ConvertNodeToString(property.Value);
        }
    }

    private object? ConvertNodeToValue(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue)) return boolValue;
            if (value.TryGetValue<int>(out var intValue)) return intValue;
            if (value.TryGetValue<long>(out var longValue)) return longValue;
            if (value.TryGetValue<double>(out var doubleValue)) return doubleValue;
            if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
            if (value.TryGetValue<string>(out var stringValue)) return stringValue;
        }

        return node.ToJsonString(_jsonSerializerOptions);
    }

    private string? ConvertNodeToString(JsonNode? node)
    {
        return ConvertNodeToValue(node) switch
        {
            null => null,
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            _ => Convert.ToString(ConvertNodeToValue(node))
        };
    }
}
