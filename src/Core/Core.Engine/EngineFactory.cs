using Core.Abstractions;

namespace Core.Engine;

/// <summary>
/// Factory for creating ICommandEngine instances based on the client type.
/// Thread-safe and caches one engine instance per client type.
/// </summary>
public class EngineFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();
    private readonly Dictionary<ClientType, IEngine> _engines = new();

    public EngineFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets or creates an ICommandEngine instance for the specified client type.
    /// The engine is cached and reused for subsequent calls with the same client type.
    /// </summary>
    /// <param name="clientType">The type of client (CLI or PowerShell).</param>
    /// <returns>An IEngine instance configured for the specified client type.</returns>
    public IEngine GetEngine(ClientType clientType)
    {
        if (_engines.TryGetValue(clientType, out var engine))
        {
            return engine;
        }

        lock (_lock)
        {
            if (_engines.TryGetValue(clientType, out engine))
            {
                return engine;
            }
            switch (clientType)
            {
                case ClientType.PowerShell:
                    engine = new PSEngine(_serviceProvider);
                    break;
                case ClientType.CLI:
                    engine = new CLIEngine(_serviceProvider, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cirrus", "extensions"));
                    break;
                default:
                    throw new NotSupportedException($"Client type '{clientType}' is not supported.");
            }
            _engines[clientType] = engine;
            return engine;
        }
    }
}
