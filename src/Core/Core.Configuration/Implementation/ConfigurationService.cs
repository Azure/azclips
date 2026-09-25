// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Configuration.Abstractions;
using Core.Configuration.Options;
using Core.Configuration.Schema;
using Microsoft.Extensions.Configuration;

namespace Core.Configuration.Implementation;

public sealed class ConfigurationService(
    Core.Configuration.Abstractions.IConfigurationProvider provider,
    IConfigurationSchema schema,
    IConfigurationValidator validator,
    IRecoveryConfigurationCoordinator? recoveryCoordinator = null) : IConfigurationService, ISourceAwareConfigurationService, IConfigurationDiagnostics
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
                throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
            }
        }

        var defaults = defaultValue ?? new T();
        var builder = new ConfigurationBuilder();
        var defaultEntries = BuildDefaultEntries(defaults);
        if (defaultEntries.Count > 0)
        {
            builder.AddInMemoryCollection(defaultEntries);
        }

        // Read through the provider once. No cached options or deferred file reload can outlive
        // a history-lock policy check, and file/list/binding share the same public-key mapping.
        var root = ReadDocument();
        _ = ReadConfiguredValues(root);
        using var document = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(root, _jsonSerializerOptions));
        builder.AddJsonStream(document);

        var environmentOverrides = BuildEnvironmentOverrides();
        if (environmentOverrides.Count > 0)
        {
            builder.AddInMemoryCollection(environmentOverrides);
        }

        var configuration = builder.Build();

        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            return ValidateOptions(configuration.GetSection(NormalizeConfigurationPath(schema.GetStorageKey(normalizedKey))).Get<T>() ?? defaults);
        }

        if (typeof(T).GetCustomAttributes(typeof(ConfigurationSectionAttribute), inherit: true)
            .OfType<ConfigurationSectionAttribute>()
            .SingleOrDefault() is { } section)
        {
            return ValidateOptions(configuration.GetSection(section.Name).Get<T>() ?? defaults);
        }

        return ValidateOptions(configuration.Get<T>() ?? defaults);
    }

    public async Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
    {
        var normalizedKey = CanonicalizeKnownKey(key);
        var keyValidation = validator.ValidateKey(normalizedKey, force);
        if (!keyValidation.IsValid)
        {
            throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
        }

        var valueValidation = validator.ValidateValue(normalizedKey, value);
        if (!valueValidation.IsValid)
        {
            throw new ArgumentException(valueValidation.ErrorMessage, nameof(value));
        }

        await CommitMutationAsync(normalizedKey, Convert.ToString(value), async token =>
        {
            await using var lockStream = await AcquireMutationLockAsync(token).ConfigureAwait(false);
            var root = await provider.ReadAsync(token).ConfigureAwait(false);
            RecoveryConfigurationValidation.ValidateShape(root);
            var storageKey = schema.GetStorageKey(normalizedKey);
            RemoveLegacyFlatKey(root, storageKey);
            SetValue(root, storageKey, JsonSerializer.SerializeToNode(value, _jsonSerializerOptions));
            await provider.WriteAsync(root, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
    {
        var normalizedKey = CanonicalizeKnownKey(key);
        if (string.Equals(normalizedKey, "agent", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Delete individual public Agent configuration keys so recovery reset remains coordinated.", nameof(key));
        var keyValidation = validator.ValidateKey(normalizedKey, force);
        if (!keyValidation.IsValid)
        {
            throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
        }

        await CommitMutationAsync(normalizedKey, new RecoveryOptions().Trigger, async token =>
        {
            await using var lockStream = await AcquireMutationLockAsync(token).ConfigureAwait(false);
            var root = await provider.ReadAsync(token).ConfigureAwait(false);
            RecoveryConfigurationValidation.ValidateShape(root);
            var storageKey = schema.GetStorageKey(normalizedKey);
            DeleteValue(root, storageKey);
            RemoveLegacyFlatKey(root, storageKey);
            await provider.WriteAsync(root, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
    {
        var root = await provider.ReadAsync(cancellationToken).ConfigureAwait(false);
        var results = ReadConfiguredValues(root);

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
        var configured = ReadConfiguredValues(root);
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
            if (HasEnvironmentOverride(key, value))
            {
                var parsed = ParseEnvironmentValue(value!);
                RecoveryConfigurationValidation.ValidateValues([new(key, parsed)], schema, validator);
                result[key] = new ConfigurationValue(key, parsed, "environment");
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<ConfigurationDiagnostic>> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var root = await provider.ReadAsync(cancellationToken).ConfigureAwait(false);
        return Flatten(root).Keys.Where(RecoveryConfigurationValidation.IsLegacyKey)
            .Select(key => new ConfigurationDiagnostic("configuration_recovery_legacy", RecoveryConfigurationValidation.MigrationMessage(key)))
            .ToArray();
    }

    private Task CommitMutationAsync(string key, string? nextTrigger, Func<CancellationToken, Task> commit, CancellationToken cancellationToken)
    {
        if (!string.Equals(key, RecoveryConfigurationValidation.TriggerKey, StringComparison.OrdinalIgnoreCase))
            return commit(cancellationToken);
        if (recoveryCoordinator is null)
            throw new InvalidOperationException("Recovery configuration changes require the shared failure-history coordinator.");
        return recoveryCoordinator.CommitTriggerChangeAsync(nextTrigger!, ReadStoredTriggerAsync, commit, cancellationToken);
    }

    private async Task<string> ReadStoredTriggerAsync(CancellationToken cancellationToken)
    {
        var values = ReadConfiguredValues(await provider.ReadAsync(cancellationToken).ConfigureAwait(false));
        return values.TryGetValue(RecoveryConfigurationValidation.TriggerKey, out var value)
            ? (string)value! : new RecoveryOptions().Trigger;
    }

    private Dictionary<string, object?> ReadConfiguredValues(JsonObject root)
    {
        RecoveryConfigurationValidation.ValidateShape(root);
        var values = Flatten(root);
        RecoveryConfigurationValidation.ValidateValues(values, schema, validator);
        return values;
    }

    private JsonObject ReadDocument()
    {
        try { return provider.ReadAsync().ConfigureAwait(false).GetAwaiter().GetResult(); }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Configuration file '{provider.GetConfigFilePath()}' contains invalid JSON. Fix or remove the file and try again.", exception);
        }
    }

    private static T ValidateOptions<T>(T value) where T : class
    {
        if (value is AgentOptions agent)
        {
            if (agent.Recovery is null)
                throw new ArgumentException("Configuration 'agent.recovery' must be an object.");
            agent.Recovery.Validate();
        }
        if (value is RecoveryOptions recovery)
            recovery.Validate();
        return value;
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

    private Dictionary<string, string?> BuildEnvironmentOverrides()
    {
        var mappings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (envVarName, configKey) in Abstractions.EnvironmentVariableMappings.Mappings)
        {
            var value = Environment.GetEnvironmentVariable(envVarName);
            if (HasEnvironmentOverride(configKey, value))
            {
                var parsed = ParseEnvironmentValue(value!);
                RecoveryConfigurationValidation.ValidateValues([new(configKey, parsed)], schema, validator);
                mappings[NormalizeConfigurationPath(schema.GetStorageKey(configKey))] = FormatConfigurationValue(parsed);
            }
        }

        return mappings;
    }

    private static bool HasEnvironmentOverride(string key, string? value) =>
        value is not null && (RecoveryConfigurationValidation.IsRecoveryKey(key) || !string.IsNullOrWhiteSpace(value));

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

    private static void RemoveLegacyFlatKey(JsonObject root, string key)
    {
        foreach (var existing in root.Select(entry => entry.Key)
                     .Where(existing => string.Equals(NormalizeKey(existing), key, StringComparison.OrdinalIgnoreCase)).ToArray())
            root.Remove(existing);
    }

    private static void SetValue(JsonObject root, string key, JsonNode? value)
    {
        var segments = NormalizeKey(key).Split('.');
        JsonObject current = root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = FindPropertyName(current, segments[index]);
            if (current[segment] is not JsonObject next)
            {
                next = [];
                current[segment] = next;
            }

            current = next;
        }

        current[FindPropertyName(current, segments[^1])] = value;
    }

    private static void DeleteValue(JsonObject root, string key)
    {
        var segments = NormalizeKey(key).Split('.');
        DeleteValue(root, segments, 0);
    }

    private static bool DeleteValue(JsonObject current, IReadOnlyList<string> segments, int index)
    {
        var segment = FindPropertyName(current, segments[index]);
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
                    AddConfigurationValue(results, child.Key, child.Value);
                }

                continue;
            }

            AddConfigurationValue(results, schema.GetPublicKey(NormalizeKey(key)), ConvertNodeToValue(property.Value));
        }

        return results;
    }

    private static void AddConfigurationValue(IDictionary<string, object?> values, string key, object? value)
    {
        if (!values.TryAdd(key, value))
            throw new InvalidOperationException($"Configuration contains the duplicate key '{key}'. Remove the duplicate rather than relying on its order.");
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
        return FormatConfigurationValue(ConvertNodeToValue(node));
    }

    private static string? FormatConfigurationValue(object? value) => value switch
        {
            null => null,
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        };

    private static string FindPropertyName(JsonObject root, string name) =>
        root.Select(entry => entry.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) ?? name;
}
