using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;

namespace Core.Engine;

internal class DiscoverySession : ISession
{
    private bool _disposed = false;
    private ClientType clientType;
    private OutputStreamDelegate sessionOutputWriter = null!;
    private IDisposable loggingScope;
    private bool isTransient;
    private string? commandName;
    private readonly OutputStreamAdapter outputStreamAdapter;

    public string Id { get; }
    IList<string> Names { get; }
    public IEventBus EventBus { get; private set; }

    internal Func<string, string, Assembly> LoadAssembly { get; set; } = null!;
    internal Func<string, string> GetExtensionVersion { get; set; } = null!;
    internal Func<IList<Tuple<string, string, Func<Assembly>>>?> LoadAllAssemblies { get; set; } = null!;
    internal Func<string, string, Func<IExtensionIndex>, IExtensionIndex> CacheGetOrAdd { get; set; } = null!;

    internal DiscoverySession(ClientType clientType, IList<string> extensionNames, string? commandName, OutputStreams outputStreams, bool isTransient, CancellationToken cancellationToken = default)
    {
        this.clientType = clientType;
        this.isTransient = isTransient;
        this.commandName = commandName;
        Id = $"{GetType().Name}-{clientType}-{Guid.NewGuid().ToString()}";
        Names = new List<string>(extensionNames);
        EventBus = new RuntimeEventBus(cancellationToken);
        sessionOutputWriter = outputStreams.WriteOutput;
        outputStreamAdapter = new OutputStreamAdapter(outputStreams, EventBus.Token);
        loggingScope = ScopedLoggingService.UseScope(outputStreamAdapter);
    }

    public void Run() => throw new NotImplementedException();
    public async Task RunAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Names == null || Names.Count == 0)
        {
            foreach (var (name, version, asmFunc) in LoadAllAssemblies() ?? [])
            {
                try
                {
                    IExtensionIndex index;
                    if (isTransient)
                        index = asmFunc.Invoke().GetExtensionIndex();
                    else
                        index = CacheGetOrAdd(name, version, () => asmFunc.Invoke().GetExtensionIndex());
                    foreach (CommandMetadata metadata in index.GetAllCommands(clientType))
                    {
                        await sessionOutputWriter(metadata, EventBus.Token);
                    }
                }
                catch (Exception ex)
                {
                    // Log the error and continue loading other extensions
                    Console.Error.WriteLine($"Failed to load extension in discovery session {Id}: {ex.Message}");
                }
            }
            return;
        }

        if (Names.Count > 1)
        {
            foreach (string name in Names)
            {
                try
                {
                    IExtensionIndex index;
                    if (isTransient)
                        index = LoadAssembly.Invoke(name, GetExtensionVersion(name)).GetExtensionIndex();
                    else
                        index = CacheGetOrAdd(name, GetExtensionVersion(name), () => LoadAssembly.Invoke(name, GetExtensionVersion(name)).GetExtensionIndex());
                    foreach (CommandMetadata metadata in index.GetAllCommands(clientType))
                    {
                        await sessionOutputWriter(metadata, EventBus.Token);
                    }
                }
                catch (Exception ex)
                {
                    // Log the error and continue loading other extensions
                    Console.Error.WriteLine($"Failed to load extension '{name}' in discovery session {Id}: {ex.Message}");
                }
            }
            return;
        }
        
        if (Names.Count == 1 && !string.IsNullOrEmpty(commandName))
        {
            try
            {
                IExtensionIndex index;
                if (isTransient)
                    index = LoadAssembly.Invoke(Names[0], GetExtensionVersion(Names[0])).GetExtensionIndex();
                else
                    index = CacheGetOrAdd(Names[0], GetExtensionVersion(Names[0]), () => LoadAssembly.Invoke(Names[0], GetExtensionVersion(Names[0])).GetExtensionIndex());
                await sessionOutputWriter(index.GetCommandMetadata(commandName, clientType), EventBus.Token);
            }
            catch (Exception ex)
            {
                // Log the error
                Console.Error.WriteLine($"Failed to load extension '{Names[0]}' in discovery session {Id}: {ex.Message}");
            }
            return;
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by Engine and optionally releases managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                EventBus.Publish(Events.SessionDisposed, $"Session {Id} disposed").GetAwaiter().GetResult();

                loggingScope.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}