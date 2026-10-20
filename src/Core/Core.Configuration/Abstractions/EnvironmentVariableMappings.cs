// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Configuration.Abstractions;

/// <summary>
/// Centralized registry of environment variable to configuration key mappings.
/// Ensures consistency between ConfigurationService and other consumers (e.g., CLI commands).
/// </summary>
public static class EnvironmentVariableMappings
{
    /// <summary>
    /// Gets all environment variable to configuration key mappings.
    /// Each tuple contains the environment variable name first and the configuration key second.
    /// Configuration keys use dot notation (e.g., "core.logging.level").
    /// </summary>
    public static IReadOnlyList<(string EnvironmentVariableName, string ConfigurationKey)> Mappings { get; } =
    [
        ("AZCLIPS_LOG_LEVEL", "core.logging.level"),
        ("PS_HTTP_MAX_RETRIES", "core.http.maxRetries"),
        ("AZURE_PS_HTTP_MAX_RETRIES", "core.http.maxRetries"),
        ("PS_HTTP_MAX_RETRIES_FOR_429", "core.http.maxRetriesFor429"),
        ("AZURE_PS_HTTP_MAX_RETRIES_FOR_429", "core.http.maxRetriesFor429"),
        ("AZCLIPS_NO_EXTENSION_CACHE", "assemblyLoading.bypassExtensionCache"),
        ("AZCLIPS_AGENT_RECOVERY", "agent.recovery"),
        ("AZCLIPS_AGENT_RECOVERY_STYLE", "agent.recovery.style"),
        ("AZCLIPS_AGENT_RECOVERY_HISTORY_SIZE", "agent.recovery.historySize"),
        // Internal-only startup-benchmark seam → BenchmarkOptions.ResolveOnly.
        ("AZCLIPS_BENCH_RESOLVE_ONLY", "benchmark.resolveOnly"),
    ];

    /// <summary>
    /// Gets the configuration key for a given environment variable name (case-insensitive).
    /// Returns null if no mapping exists.
    /// </summary>
    public static string? GetConfigurationKey(string environmentVariableName)
    {
        var normalized = environmentVariableName?.ToUpperInvariant() ?? string.Empty;
        return Mappings.FirstOrDefault(m => string.Equals(m.EnvironmentVariableName, normalized, StringComparison.Ordinal)).ConfigurationKey;
    }
}
