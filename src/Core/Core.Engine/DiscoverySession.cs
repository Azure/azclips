// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    private IList<string> extensionNames;
    private readonly OutputStreamAdapter outputStreamAdapter;

    public string Id { get; }
    public IEventBus EventBus { get; private set; }

    internal Func<string, string, Assembly> LoadAssembly { get; set; } = null!;
    internal Func<string, string> GetExtensionVersion { get; set; } = null!;
    internal Func<IList<(InstalledExtensionRef Info, Func<Assembly> Load)>?> LoadAllAssemblies { get; set; } = null!;
    internal Func<string, string, Func<IExtensionIndex>, IExtensionIndex> CacheGetOrAdd { get; set; } = null!;

    internal DiscoverySession(ClientType clientType, IList<string> extensionNames, string? commandName, OutputStreams outputStreams, bool isTransient, CancellationToken cancellationToken = default)
    {
        this.clientType = clientType;
        this.isTransient = isTransient;
        this.commandName = commandName;
        this.extensionNames = [.. extensionNames];
        Id = $"{GetType().Name}-{clientType}-{Guid.NewGuid().ToString()}";
        EventBus = new RuntimeEventBus(cancellationToken);
        sessionOutputWriter = outputStreams.WriteOutput;
        outputStreamAdapter = new OutputStreamAdapter(outputStreams, EventBus.Token);
        loggingScope = ScopedLoggingService.UseScope(outputStreamAdapter);
    }

    public void Run() => throw new NotImplementedException();

    /// <summary>
    /// Discovers commands from the requested extension(s) and writes a
    /// <see cref="IList{DiscoveredExtensionInfo}"/> to the output stream. Each entry
    /// carries the extension package id, resolved version, contributed verbs, and full
    /// command table keyed by <see cref="CommandMetadata.InvocationName"/>.
    /// </summary>
    public async Task RunAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var extensionInfos = new List<DiscoveredExtensionInfo>();

        if (extensionNames == null || extensionNames.Count == 0)
        {
            // All installed extensions: enumerate every on-disk installation.
            foreach (var (info, _) in LoadAllAssemblies() ?? [])
            {
                try
                {
                    var discoveredVersion = info.Version;
                    var configuredVersion = GetExtensionVersion(info.Name);
                    var versionToLoad = string.IsNullOrWhiteSpace(configuredVersion)
                        ? discoveredVersion
                        : configuredVersion;

                    var loaded = LoadDiscoveredIndex(info.Name, versionToLoad);
                    IExtensionIndex index = loaded.Index;
                    var reportedVersion = ResolveReportedVersion(discoveredVersion, versionToLoad, loaded.EffectiveVersion);

                    var commands = index.GetAllCommands(clientType)
                        .ToDictionary(c => c.InvocationName);
                    var packageId = ResolvePackageId(index, commands, info.Name);

                    extensionInfos.Add(new DiscoveredExtensionInfo(
                        PackageId: packageId,
                        ResolvedVersion: reportedVersion,
                        Verbs: ExtensionVerbDeclarations.GetDeclaredVerbs(index),
                        Commands: commands,
                        Location: info.Location));
                }
                catch (Exception ex)
                {
                    outputStreamAdapter.Warn($"[{nameof(DiscoverySession)}] Session {Id} failed to load extension '{info.Name}': {ex.Message}");
                }
            }
        }
        else if (extensionNames.Count > 0 && string.IsNullOrEmpty(commandName))
        {
            // Named extensions, all commands.
            foreach (string name in extensionNames)
            {
                try
                {
                    var version = GetExtensionVersion(name);
                    IExtensionIndex index = isTransient
                        ? LoadAssembly.Invoke(name, version).GetExtensionIndex()
                        : CacheGetOrAdd(name, version, () => LoadAssembly.Invoke(name, version).GetExtensionIndex());

                    var commands = index.GetAllCommands(clientType)
                        .ToDictionary(c => c.InvocationName);
                    var packageId = ResolvePackageId(index, commands, name);

                    extensionInfos.Add(new DiscoveredExtensionInfo(
                        PackageId: packageId,
                        ResolvedVersion: version,
                        Verbs: ExtensionVerbDeclarations.GetDeclaredVerbs(index),
                        Commands: commands,
                        Location: null));
                }
                catch (Exception ex)
                {
                    outputStreamAdapter.Warn($"[{nameof(DiscoverySession)}] Session {Id} failed to load extension '{name}': {ex.Message}");
                }
            }
        }
        else if (extensionNames.Count == 1 && !string.IsNullOrEmpty(commandName))
        {
            // Single extension, single command lookup.
            try
            {
                var name = extensionNames[0];
                var version = GetExtensionVersion(name);
                IExtensionIndex index = isTransient
                    ? LoadAssembly.Invoke(name, version).GetExtensionIndex()
                    : CacheGetOrAdd(name, version, () => LoadAssembly.Invoke(name, version).GetExtensionIndex());

                var metadata = index.GetCommandMetadata(commandName, clientType);
                extensionInfos.Add(new DiscoveredExtensionInfo(
                    PackageId: metadata.AssemblyName,
                    ResolvedVersion: version,
                    Verbs: ExtensionVerbDeclarations.GetDeclaredVerbs(index),
                    Commands: new Dictionary<string, CommandMetadata> { [metadata.InvocationName] = metadata },
                    Location: null));
            }
            catch (Exception ex)
            {
                outputStreamAdapter.Warn($"[{nameof(DiscoverySession)}] Session {Id} failed to load extension '{extensionNames[0]}': {ex.Message}");
            }
        }

        await sessionOutputWriter(extensionInfos, EventBus.Token);
    }

    private LoadedDiscoveredIndex LoadDiscoveredIndex(string extensionName, string versionToLoad)
    {
        if (isTransient)
        {
            var loadedAssembly = LoadAssemblyWithFallback(extensionName, versionToLoad);
            return new LoadedDiscoveredIndex(loadedAssembly.Assembly.GetExtensionIndex(), loadedAssembly.EffectiveVersion);
        }

        if (string.IsNullOrWhiteSpace(versionToLoad))
        {
            return new LoadedDiscoveredIndex(
                CacheGetOrAdd(
                    extensionName,
                    string.Empty,
                    () => LoadAssembly.Invoke(extensionName, string.Empty).GetExtensionIndex()),
                string.Empty);
        }

        try
        {
            return new LoadedDiscoveredIndex(
                CacheGetOrAdd(
                    extensionName,
                    versionToLoad,
                    () => LoadAssembly.Invoke(extensionName, versionToLoad).GetExtensionIndex()),
                versionToLoad);
        }
        catch (InvalidOperationException ex) when (IsVersionedLoadMiss(ex))
        {
            return new LoadedDiscoveredIndex(
                CacheGetOrAdd(
                    extensionName,
                    string.Empty,
                    () => LoadAssembly.Invoke(extensionName, string.Empty).GetExtensionIndex()),
                string.Empty);
        }
    }

    private LoadedAssembly LoadAssemblyWithFallback(string extensionName, string versionToLoad)
    {
        try
        {
            return new LoadedAssembly(LoadAssembly.Invoke(extensionName, versionToLoad), versionToLoad);
        }
        catch (InvalidOperationException ex) when (
            !string.IsNullOrWhiteSpace(versionToLoad) &&
            IsVersionedLoadMiss(ex))
        {
            // Some loaders cannot resolve explicit versions; retry unversioned to keep discovery resilient.
            return new LoadedAssembly(LoadAssembly.Invoke(extensionName, string.Empty), string.Empty);
        }
    }

    private static bool IsVersionedLoadMiss(InvalidOperationException ex)
    {
        return ex.Message.Contains("could not be found by any extension loader", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReportedVersion(string discoveredVersion, string requestedVersion, string effectiveVersion)
    {
        if (!string.IsNullOrWhiteSpace(effectiveVersion))
        {
            return effectiveVersion;
        }

        if (!string.IsNullOrWhiteSpace(discoveredVersion))
        {
            return discoveredVersion;
        }

        return requestedVersion;
    }

    private sealed record LoadedAssembly(Assembly Assembly, string EffectiveVersion);

    private sealed record LoadedDiscoveredIndex(IExtensionIndex Index, string EffectiveVersion);

    private static string ResolvePackageId(IExtensionIndex index, IReadOnlyDictionary<string, CommandMetadata> commands, string fallbackIdentifier)
    {
        var fromCommands = commands.Values.FirstOrDefault()?.AssemblyName;
        if (!string.IsNullOrWhiteSpace(fromCommands))
        {
            return fromCommands;
        }

        var fromAssembly = index.GetType().Assembly.GetName().Name;
        if (!string.IsNullOrWhiteSpace(fromAssembly))
        {
            return fromAssembly;
        }

        return fallbackIdentifier;
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
