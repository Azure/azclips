using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Extensions.Config.Commands;

public abstract class ConfigCommandBase : IProcessor
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    protected OutputStreamDelegate WriteOutput = null!;
    protected StartActivityDelegate StartActivity { get; set; } = null!;
    protected CancellationToken CancellationToken { get; private set; }

    public IProcessor Init(ProcessorContext context)
    {
        WriteOutput = context.OutputHandler.WriteOutput;
        //Todo: update this, the commaand processor should not start an activity directly
        StartActivity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartActivity;
        CancellationToken = context.Publisher.Token;
        return this;
    }

    public abstract Task ProcessAsync();

    protected static IConfigurationService GetConfigurationService() => CirrusServiceProvider.GetRequiredService<IConfigurationService>();

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

    protected static IConfigurationSchema GetConfigurationSchema() => GetConfigurationService().GetSchema();

    protected async Task<Dictionary<string, ConfigValueView>> GetResolvedEntriesAsync(bool includeDefaults)
    {
        var configurationService = GetConfigurationService();
        var fileValues = await configurationService.ListConfigurationAsync(includeDefaults: false, cancellationToken: CancellationToken).ConfigureAwait(false);
        var entries = new Dictionary<string, ConfigValueView>(StringComparer.OrdinalIgnoreCase);

        if (includeDefaults)
        {
            foreach (var entry in BuildDefaultEntries())
            {
                entries[entry.Key] = new ConfigValueView(entry.Key, entry.Value, "default");
            }
        }

        foreach (var entry in fileValues)
        {
            entries[entry.Key] = new ConfigValueView(entry.Key, entry.Value, "global config");
        }

        foreach (var entry in GetEnvironmentOverrides())
        {
            entries[entry.Key] = new ConfigValueView(entry.Key, entry.Value, "environment");
        }

        return entries;
    }

    protected static IDictionary<string, object?> GetEnvironmentOverrides()
    {
        var entries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (envVarName, configKey) in Core.Configuration.Abstractions.EnvironmentVariableMappings.Mappings)
        {
            AddEnvironment(entries, envVarName, configKey);
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

    protected static string Serialize(object value) => JsonSerializer.Serialize(value, s_jsonOptions);

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

    private static Dictionary<string, object?> BuildDefaultEntries()
    {
        var sectionDefaults = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["core"] = new CoreOptions(),
            ["assemblyLoading"] = new AssemblyLoadingOptions(),
            ["telemetry"] = new TelemetryOptions(),
            ["agent"] = new AgentOptions(),
            ["extensions"] = new ExtensionsOptions(),
            ["clients"] = new ClientsOptions(),
            ["defaults"] = new DefaultsOptions()
        };

        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadata in GetConfigurationSchema().GetAllKeys())
        {
            var segments = metadata.Key.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !sectionDefaults.TryGetValue(segments[0], out var sectionRoot))
            {
                continue;
            }

            defaults[metadata.Key] = GetNestedValue(sectionRoot, segments[1..]);
        }

        return defaults;
    }

    private static object? GetNestedValue(object root, IReadOnlyList<string> segments)
    {
        object? current = root;
        foreach (var segment in segments)
        {
            if (current is null)
            {
                return null;
            }

            var property = current.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate => string.Equals(Normalize(candidate.Name), Normalize(segment), StringComparison.OrdinalIgnoreCase));
            if (property is null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
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
