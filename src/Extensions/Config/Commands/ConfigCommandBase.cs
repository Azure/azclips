// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Config.Models;

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

    /// <summary>
    /// Calls the change handler registered for the key and, on delete, for every known key under it.
    /// A handler failure is reported as a warning; the configuration change itself has succeeded.
    /// </summary>
    protected async Task NotifyChangeHandlersAsync(string key, object? value, bool deleted)
    {
        var schema = GetConfigurationSchema();
        var normalizedKey = NormalizeKey(key);
        var canonicalKey = schema.GetKeyMetadata(normalizedKey)?.Key ?? normalizedKey;
        var keys = new List<string> { canonicalKey };
        if (deleted)
        {
            keys.AddRange(schema.GetAllKeys()
                .Select(metadata => metadata.Key)
                .Where(candidate => candidate.StartsWith(canonicalKey + ".", StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var changedKey in keys)
        {
            if (AzclipsServiceProvider.GetKeyedService<IConfigurationChangeHandler>(changedKey) is not { } handler)
                continue;
            try
            {
                await handler.OnChangedAsync(changedKey, deleted ? null : value, CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
                logger.Warn(ex.Message);
                FailureHistoryDiagnostics.EmitCauseChain(ex, logger);
            }
        }
    }

    protected async Task<Dictionary<string, ConfigEntry>> GetResolvedEntriesAsync(bool includeDefaults)
    {
        var entries = new Dictionary<string, ConfigEntry>(StringComparer.OrdinalIgnoreCase);

        var snapshot = await GetSourceAwareConfigurationService().ListConfigurationWithSourcesAsync(CancellationToken).ConfigureAwait(false);
        foreach (var entry in snapshot)
        {
            if (includeDefaults || entry.Value.Source != "default")
                entries[entry.Key] = new ConfigEntry(entry.Key, entry.Value.Value, entry.Value.Source);
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
}
