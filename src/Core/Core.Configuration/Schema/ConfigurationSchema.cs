// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Configuration.Schema;

public sealed class ConfigurationSchema : IConfigurationSchema
{
    private static readonly IReadOnlyList<ConfigurationKeyMetadata> Keys =
    [
        new() { Key = "core.logging.level", Description = "Global log level.", DefaultValue = "warn", ValueType = typeof(string), AllowedValues = ["debug", "info", "warn", "error"] },
        new() { Key = "core.output.color", Description = "Color rendering mode for CLI diagnostic output. 'auto' (default) respects env vars and redirection; 'always' forces color; 'never' suppresses color. NO_COLOR and FORCE_COLOR env vars still take precedence.", DefaultValue = "auto", ValueType = typeof(string), AllowedValues = ["auto", "always", "never"] },
        new() { Key = "core.engine.extensionIndexCacheSize", Description = "LFU cache size for extension index.", DefaultValue = "512", ValueType = typeof(int), Minimum = 1, Maximum = 10000 },
        new() { Key = "core.http.maxRetries", Description = "Max retries for transient HTTP failures.", DefaultValue = "3", ValueType = typeof(int), Minimum = 0, Maximum = 50 },
        new() { Key = "core.http.maxRetriesFor429", Description = "Max retries for HTTP 429 responses.", DefaultValue = "5", ValueType = typeof(int), Minimum = 0, Maximum = 50 },
        new() { Key = "assemblyLoading.installedExtensionsRoot", Description = "Root directory for extensions installed via `az extension add` (typically ~/.azclips/extensions).", ValueType = typeof(string) },
        new() { Key = "assemblyLoading.extensionFolderPrefix", Description = "Assembly discovery prefix for extensions.", ValueType = typeof(string) },
        new() { Key = "assemblyLoading.bypassExtensionCache", Description = "Skip the per-process ALC cache and reload extensions on every dispatch (benchmark/test hook).", DefaultValue = "false", ValueType = typeof(bool) },
        new() { Key = "telemetry.enabled", Description = "Enable or disable telemetry.", DefaultValue = "true", ValueType = typeof(bool) },
        new() { Key = "agent.enabled", Description = "Enable or disable agent features. Off by default; set true to opt in.", DefaultValue = "false", ValueType = typeof(bool) },
        new() { Key = "agent.sessionStyle", Description = "Agent interaction style: oneshot (propose a fix and exit) | interactive (stay in a chat loop until you exit).", DefaultValue = "oneshot", ValueType = typeof(string), AllowedValues = ["oneshot", "interactive"] },
        new() { Key = "agent.interactive.verbose", Description = "When true, interactive agent sessions also show internal tool details (get_command / find_* args and output), not just shell commands. Off by default to keep the session clean.", DefaultValue = "false", ValueType = typeof(bool) },
        new() { Key = "agent.trace.enabled", Description = "When true, every agent session writes a JSONL trace (one file per session) under agent.trace.path. Off by default. Local debug/evaluation only.", DefaultValue = "false", ValueType = typeof(bool) },
        new() { Key = "agent.trace.path", Description = "Directory for agent JSONL trace files (used when agent.trace.enabled=true). Created on first write if absent.", DefaultValue = HomePaths.GetDefaultTraceConfigValue(), ValueType = typeof(string) },
        new() { Key = "agent.provider", Description = "Agent model provider: builtin (default, Microsoft's AOAI deployment via APIM, requires 'az login') | github (user's Copilot subscription, requires Copilot login).", DefaultValue = "builtin", ValueType = typeof(string), AllowedValues = ["builtin", "github"] },
        new() { Key = "agent.builtin.endpoint", Description = "Endpoint URL for the builtin provider (APIM in front of AOAI).", DefaultValue = "https://azclis-copilot-apim-beta-eus.azure-api.net/azclips/openai/", ValueType = typeof(string), IsUri = true },
        new() { Key = "agent.builtin.modelId", Description = "Model deployment name used by the builtin provider.", DefaultValue = "gpt-5.4", ValueType = typeof(string) },
        new() { Key = "agent.builtin.apiVersion", Description = "Azure OpenAI API version used by the builtin provider.", DefaultValue = "2025-04-01-preview", ValueType = typeof(string) },
        new() { Key = "completion.enabled", Description = "Enable or disable az shell tab-completion. On by default; set false to disable suggestions even when the completion script is already sourced.", DefaultValue = "true", ValueType = typeof(bool) },
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
