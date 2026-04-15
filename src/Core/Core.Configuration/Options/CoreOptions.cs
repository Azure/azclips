namespace Core.Configuration.Options;

public sealed class CoreOptions
{
    public LoggingOptions Logging { get; set; } = new();

    public EngineOptions Engine { get; set; } = new();

    public HttpOptions Http { get; set; } = new();
}

public sealed class LoggingOptions
{
    public string Level { get; set; } = "info";
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
