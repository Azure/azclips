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
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                configuration = builder.Build();
                break;
            }
            catch (IOException ex) when (FileConfigurationProvider.ShouldRetryFileAccess(ex, stopwatch.Elapsed))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }
            catch (Exception ex) when (IsInvalidJsonConfigurationException(ex))
            {
                throw AzclipsErrorException.InvalidInput(
                    $"Configuration file '{configFilePath}' contains invalid JSON. Fix or remove the file and try again.",
                    innerException: ex);
            }
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

        DeleteValue(root, normalizedKey);
        SetValue(root, normalizedKey, JsonSerializer.SerializeToNode(value, _jsonSerializerOptions));

        await provider.WriteAsync(root, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateListConfigurationAsync(string key, Func<string, string> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        var normalizedKey = CanonicalizeKnownKey(key);
        var keyValidation = validator.ValidateKey(normalizedKey, force: false);
        if (!keyValidation.IsValid)
        {
            throw AzclipsErrorException.InvalidInput(keyValidation.ErrorMessage!);
        }

        if (schema.GetKeyMetadata(normalizedKey) is not { IsList: true })
        {
            throw AzclipsErrorException.InvalidInput($"'{normalizedKey}' is not a list-valued configuration key.");
        }

        await using var lockStream = await AcquireMutationLockAsync(cancellationToken).ConfigureAwait(false);
        var root = await provider.ReadAsync(cancellationToken).ConfigureAwait(false);

        var current = ReadStoredString(root, normalizedKey) ?? string.Empty;
        var updated = mutate(current);
        ArgumentNullException.ThrowIfNull(updated);

        DeleteValue(root, normalizedKey);
        SetValue(root, normalizedKey, JsonValue.Create(updated));

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

    private string? ReadStoredString(JsonObject root, string key)
    {
        var stored = Flatten(root);
        stored.TryGetValue(key, out var value);
        return value switch
        {
            null => null,
            string text => text,
            _ => throw AzclipsErrorException.InvalidInput($"'{key}' must be a string before it can be updated."),
        };
    }

    private static void SetValue(JsonObject root, string key, JsonNode? value)
    {
        var segments = NormalizeKey(key).Split('.');
        JsonObject current = root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = current.Select(property => property.Key)
                .FirstOrDefault(name => string.Equals(name, segments[index], StringComparison.OrdinalIgnoreCase))
                ?? segments[index];
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
        DeleteValue(root, NormalizeKey(key), string.Empty);
    }

    private static void DeleteValue(JsonObject current, string key, string prefix)
    {
        foreach (var property in current.ToArray())
        {
            var path = NormalizeKey(string.IsNullOrEmpty(prefix) ? property.Key : $"{prefix}.{property.Key}");
            if (string.Equals(path, key, StringComparison.OrdinalIgnoreCase))
            {
                current.Remove(property.Key);
            }
            else if (key.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase)
                && property.Value is JsonObject child)
            {
                DeleteValue(child, key, path);
                if (child.Count == 0)
                {
                    current.Remove(property.Key);
                }
            }
        }
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
            var key = CanonicalizeKnownKey(string.IsNullOrWhiteSpace(prefix) ? property.Key : $"{prefix}.{property.Key}");

            // List values must retain their JSON kind so malformed arrays/objects cannot
            // masquerade as comma-separated text after flattening.
            if (schema.GetKeyMetadata(key) is { IsList: true })
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    results[key] = text;
                }
                else
                {
                    results[key] = property.Value?.DeepClone();
                }
                continue;
            }

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
