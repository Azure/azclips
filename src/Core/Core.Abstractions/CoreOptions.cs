// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public sealed class CoreOptions
{
    public LoggingOptions Logging { get; set; } = new();

    public EngineOptions Engine { get; set; } = new();

    public HttpOptions Http { get; set; } = new();

    public OutputOptions Output { get; set; } = new();
}

public sealed class LoggingOptions
{
    public string Level { get; set; } = "warn";
}

public sealed class EngineOptions
{
    public int ExtensionIndexCacheSize { get; set; } = 512;
}

public sealed class HttpOptions
{
    public int MaxRetries { get; set; } = 3;

    public int MaxRetriesFor429 { get; set; } = 5;
}

public sealed class OutputOptions
{
    public string Color { get; set; } = "auto";
}
