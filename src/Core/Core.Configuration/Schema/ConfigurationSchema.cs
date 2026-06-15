using Core.Abstractions;

namespace Core.Configuration.Schema;

public sealed class ConfigurationSchema : IConfigurationSchema
{
    private static readonly IReadOnlyList<ConfigurationKeyMetadata> Keys =
    [
        new() { Key = "core.logging.level", Description = "Global log level.", DefaultValue = "info", ValueType = typeof(string), AllowedValues = ["debug", "info", "warn", "error"] },
        new() { Key = "core.engine.extensionIndexCacheSize", Description = "LFU cache size for extension index.", DefaultValue = "512", ValueType = typeof(int), Minimum = 1, Maximum = 10000 },
        new() { Key = "core.http.maxRetries", Description = "Max retries for transient HTTP failures.", DefaultValue = "3", ValueType = typeof(int), Minimum = 0, Maximum = 50 },
        new() { Key = "core.http.maxRetriesFor429", Description = "Max retries for HTTP 429 responses.", DefaultValue = "5", ValueType = typeof(int), Minimum = 0, Maximum = 50 },
        new() { Key = "assemblyLoading.builtinInstallationDir", Description = "Built-in extensions installation directory.", ValueType = typeof(string) },
        new() { Key = "assemblyLoading.versionedInstallationDir", Description = "Versioned extensions installation directory.", ValueType = typeof(string) },
        new() { Key = "assemblyLoading.extensionFolderPrefix", Description = "Assembly discovery prefix for extensions.", ValueType = typeof(string) },
        new() { Key = "assemblyLoading.bypassExtensionCache", Description = "Skip the per-process ALC cache and reload extensions on every dispatch (benchmark/test hook).", DefaultValue = "false", ValueType = typeof(bool) },
        new() { Key = "telemetry.enabled", Description = "Enable or disable telemetry.", DefaultValue = "true", ValueType = typeof(bool) },
        new() { Key = "telemetry.zipkinEndpoint", Description = "Zipkin endpoint URI.", ValueType = typeof(string), IsUri = true },
        new() { Key = "telemetry.appInsightsInstrumentationKey", Description = "Application Insights instrumentation key.", ValueType = typeof(string) },
        new() { Key = "agent.enabled", Description = "Enable or disable agent features.", DefaultValue = "true", ValueType = typeof(bool) },
        new() { Key = "extensions.resources.arm.apiVersionOverride", Description = "Override ARM API version.", ValueType = typeof(string) },
        new() { Key = "clients.cli.output.format", Description = "Default CLI output format.", DefaultValue = "table", ValueType = typeof(string), AllowedValues = ["json", "table", "tsv", "yaml"] },
        new() { Key = "clients.powershell.output.format", Description = "Default PowerShell output format.", DefaultValue = "object", ValueType = typeof(string), AllowedValues = ["object", "json", "table"] },
        new() { Key = "defaults.group", Description = "Default resource group for commands that support it.", ValueType = typeof(string) },
        new() { Key = "defaults.location", Description = "Default location for commands that support it.", ValueType = typeof(string) },
        new() { Key = "defaults.web", Description = "Default web app name for commands that support it.", ValueType = typeof(string) },
        new() { Key = "defaults.vm", Description = "Default VM name for commands that support it.", ValueType = typeof(string) },
        new() { Key = "defaults.vmss", Description = "Default VMSS name for commands that support it.", ValueType = typeof(string) },
        new() { Key = "defaults.acr", Description = "Default ACR name for commands that support it.", ValueType = typeof(string) }
    ];

    public IReadOnlyList<ConfigurationKeyMetadata> GetAllKeys() => Keys;

    public ConfigurationKeyMetadata? GetKeyMetadata(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Keys.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsKnownKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Keys.Any(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
