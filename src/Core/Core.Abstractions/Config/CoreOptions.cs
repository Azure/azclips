// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

[ConfigurationSection("core")]
public sealed class CoreOptions
{
    public LoggingOptions Logging { get; set; } = new();

    public EngineOptions Engine { get; set; } = new();

    public HttpOptions Http { get; set; } = new();

    public OutputOptions Output { get; set; } = new();
}

public sealed class LoggingOptions
{
    [ConfigurationKey("level", Description = "Global log level.", AllowedValues = ["debug", "info", "warn", "error"])]
    public string Level { get; set; } = "warn";
}

public sealed class EngineOptions
{
    [ConfigurationKey("extensionIndexCacheSize", Description = "LFU cache size for extension index.", Minimum = 1, Maximum = 10000)]
    public int ExtensionIndexCacheSize { get; set; } = 512;
}

public sealed class HttpOptions
{
    [ConfigurationKey("maxRetries", Description = "Max retries for transient HTTP failures.", Minimum = 0, Maximum = 50)]
    public int MaxRetries { get; set; } = 3;

    [ConfigurationKey("maxRetriesFor429", Description = "Max retries for HTTP 429 responses.", Minimum = 0, Maximum = 50)]
    public int MaxRetriesFor429 { get; set; } = 5;
}

public sealed class OutputOptions
{
    [ConfigurationKey("color", Description = "Color mode for CLI output. 'auto' (default) respects env vars and redirection; 'always' forces color; 'never' suppresses color. NO_COLOR and FORCE_COLOR env vars still take precedence. This only permits color; it does not pick a format. 'json' and 'yaml' are never colored, use 'jsonc'/'yamlc' for colored results.", AllowedValues = ["auto", "always", "never"])]
    public string Color { get; set; } = "auto";
}
