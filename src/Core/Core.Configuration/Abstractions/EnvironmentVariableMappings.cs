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
        ("CIRRUS_LOG_LEVEL", "core.logging.level"),
        ("ZIPKIN_ENDPOINT", "telemetry.zipkinEndpoint"),
        ("PS_HTTP_MAX_RETRIES", "core.http.maxRetries"),
        ("AZURE_PS_HTTP_MAX_RETRIES", "core.http.maxRetries"),
        ("PS_HTTP_MAX_RETRIES_FOR_429", "core.http.maxRetriesFor429"),
        ("AZURE_PS_HTTP_MAX_RETRIES_FOR_429", "core.http.maxRetriesFor429"),
        ("CIRRUS_NO_EXTENSION_CACHE", "assemblyLoading.bypassExtensionCache"),
        // Internal-only startup-benchmark seam → BenchmarkOptions.ResolveOnly. Deliberately NOT in
        // ConfigurationSchema, so it stays hidden from `config info` / `config list --all` and is
        // rejected by `config set` without --force. For benchmarking use only.
        ("CIRRUS_BENCH_RESOLVE_ONLY", "benchmark.resolveOnly"),
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
