using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Configuration.Abstractions;
using Core.Configuration.Options;
using Microsoft.Extensions.Configuration;

namespace Core.Configuration.Implementation;

public sealed class ConfigurationService(
    Core.Configuration.Abstractions.IConfigurationProvider provider,
    IConfigurationSchema schema,
    IConfigurationValidator validator) : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<Type, string> SectionNames = new Dictionary<Type, string>
    {
        [typeof(CoreOptions)] = "core",
        [typeof(AssemblyLoadingOptions)] = "assemblyLoading",
        [typeof(TelemetryOptions)] = "telemetry",
        [typeof(AgentOptions)] = "agent",
        [typeof(ExtensionsOptions)] = "extensions",
        [typeof(ClientsOptions)] = "clients",
        [typeof(DefaultsOptions)] = "defaults"
    };

    private readonly Core.Configuration.Abstractions.IConfigurationProvider _provider = provider;
    private readonly IConfigurationSchema _schema = schema;
    private readonly IConfigurationValidator _validator = validator;

    public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var keyValidation = _validator.ValidateKey(key, force: true);
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

        builder.AddJsonFile(_provider.GetConfigFilePath(), optional: true, reloadOnChange: false);

        var environmentOverrides = BuildEnvironmentOverrides();
        if (environmentOverrides.Count > 0)
        {
            builder.AddInMemoryCollection(environmentOverrides);
        }

        var configuration = builder.Build();
        if (!string.IsNullOrWhiteSpace(key))
        {
            return configuration.GetSection(NormalizeConfigurationPath(key)).Get<T>() ?? defaults;
        }

        if (SectionNames.TryGetValue(typeof(T), out var sectionName))
        {
            return configuration.GetSection(sectionName).Get<T>() ?? defaults;
        }

        return configuration.Get<T>() ?? defaults;
    }

    public async Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var keyValidation = _validator.ValidateKey(normalizedKey, force);
        if (!keyValidation.IsValid)
        {
            throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
        }

        var valueValidation = _validator.ValidateValue(normalizedKey, value);
        if (!valueValidation.IsValid)
        {
            throw new ArgumentException(valueValidation.ErrorMessage, nameof(value));
        }

        await using var lockStream = await AcquireMutationLockAsync(cancellationToken).ConfigureAwait(false);
        var root = await _provider.ReadAsync(cancellationToken).ConfigureAwait(false);

        RemoveLegacyFlatKey(root, normalizedKey);
        SetValue(root, normalizedKey, JsonSerializer.SerializeToNode(value, JsonSerializerOptions));

        await _provider.WriteAsync(root, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);
        var keyValidation = _validator.ValidateKey(normalizedKey, force);
        if (!keyValidation.IsValid)
        {
            throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
        }

        await using var lockStream = await AcquireMutationLockAsync(cancellationToken).ConfigureAwait(false);
        var root = await _provider.ReadAsync(cancellationToken).ConfigureAwait(false);

        DeleteValue(root, normalizedKey);
        RemoveLegacyFlatKey(root, normalizedKey);

        await _provider.WriteAsync(root, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
    {
        var root = await _provider.ReadAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = NormalizeKey(name);
        var key = normalizedName.StartsWith("defaults.", StringComparison.OrdinalIgnoreCase)
            ? normalizedName
            : $"defaults.{normalizedName}";

        var entries = await ListConfigurationAsync(includeDefaults: false, cancellationToken).ConfigureAwait(false);
        return entries.TryGetValue(key, out var value) ? value : null;
    }

    public string GetConfigFilePath() => _provider.GetConfigFilePath();

    public IConfigurationSchema GetSchema() => _schema;

    private async Task<FileStream> AcquireMutationLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = $"{_provider.GetConfigFilePath()}.lock";
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

    private static Dictionary<string, string?> BuildDefaultEntries<T>(T defaults) where T : class
    {
        var results = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var node = JsonSerializer.SerializeToNode(defaults, JsonSerializerOptions) as JsonObject;
        if (node is null)
        {
            return results;
        }

        if (!SectionNames.TryGetValue(typeof(T), out var sectionName))
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
        AddEnvironmentMapping(mappings, "CIRRUS_LOG_LEVEL", "core:logging:level");
        AddEnvironmentMapping(mappings, "ZIPKIN_ENDPOINT", "telemetry:zipkinEndpoint");
        AddEnvironmentMapping(mappings, "PS_HTTP_MAX_RETRIES_FOR_429", "core:http:maxRetriesFor429");
        AddEnvironmentMapping(mappings, "AZURE_PS_HTTP_MAX_RETRIES_FOR_429", "core:http:maxRetriesFor429");
        AddEnvironmentMapping(mappings, "PS_HTTP_MAX_RETRIES", "core:http:maxRetries");
        AddEnvironmentMapping(mappings, "AZURE_PS_HTTP_MAX_RETRIES", "core:http:maxRetries");
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

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return string.Join('.', key.Replace(':', '.').Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeConfigurationPath(string key) => NormalizeKey(key).Replace('.', ':');

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
        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        AddDefaults(defaults, new CoreOptions(), "core");
        AddDefaults(defaults, new AssemblyLoadingOptions(), "assemblyLoading");
        AddDefaults(defaults, new TelemetryOptions(), "telemetry");
        AddDefaults(defaults, new AgentOptions(), "agent");
        AddDefaults(defaults, new ExtensionsOptions(), "extensions");
        AddDefaults(defaults, new ClientsOptions(), "clients");
        AddDefaults(defaults, new DefaultsOptions(), "defaults");
        return defaults;
    }

    private static void AddDefaults<T>(IDictionary<string, object?> target, T options, string sectionName) where T : class
    {
        var node = JsonSerializer.SerializeToNode(options, JsonSerializerOptions) as JsonObject;
        if (node is null)
        {
            return;
        }

        foreach (var entry in Flatten(node, sectionName))
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static Dictionary<string, object?> Flatten(JsonObject root, string? prefix = null)
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

    private static void FlattenNode(JsonObject root, string prefix, IDictionary<string, string?> destination)
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

    private static object? ConvertNodeToValue(JsonNode? node)
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

        return node.ToJsonString(JsonSerializerOptions);
    }

    private static string? ConvertNodeToString(JsonNode? node)
    {
        return ConvertNodeToValue(node) switch
        {
            null => null,
            bool boolValue => boolValue.ToString().ToLowerInvariant(),
            _ => Convert.ToString(ConvertNodeToValue(node))
        };
    }
}
