using Core.Abstractions;
using Core.Implementation;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Core.Abstractions.Runtime;
using Core.Runtime.Provider;
using Microsoft.Identity.Client;
using Core.Runtime;

namespace Core.Engine
{
    /// <summary>
    /// PowerShell command engine for executing commands in PowerShell runspaces.
    /// <para><b>Thread Safety:</b> This engine is designed for concurrent command execution.
    /// Each <see cref="ExecuteCommandAsync"/> call creates isolated context and command instances.
    /// Extension indices are cached using thread-safe lazy initialization.</para>
    /// </summary>
    public class PSEngine : IEngine
    {
        // Extension indices are cached with Lazy<T> to ensure single initialization per extension
        // even under concurrent access. The factory runs exactly once per key.
        private readonly ConcurrentDictionary<string, Lazy<IExtensionIndex>> _extensionIndices = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;
        public ClientType ClientType = ClientType.PowerShell;

        public Action<string> Log { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action<string> Debug { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public PSEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _httpClient = new HttpClient();
        }

        public void Process(ICommandInvocation invocation) {
            ICommandContext commandContext = CreateCommandContext();
        }

        /// <summary>
        /// Creates a new command context for this invocation.
        /// Each command execution gets its own isolated context.
        /// </summary>
        private ICommandContext CreateCommandContext()
        {
            IAuthService authService = _serviceProvider.GetRequiredService<IAuthService>();
            AuthContext authContext = authService.GetCurrentContext()!;
            CommandAuthProvider commandAuthProvider = new CommandAuthProvider
            {
                AuthContext = new AuthContext
                {
                    SubscriptionId = authContext.SubscriptionId,
                    TenantId = authContext.TenantId,
                    IsAuthenticated = authContext.IsAuthenticated,
                    PrincipalId = authContext.PrincipalId
                },
                AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };

            IRuntimeHttpPipeline httpPipeline = new HttpPipeline(new HttpClientFactory(_httpClient));
            ICommandContext context = new CommandContext
            {
                CommandAuthProvider = commandAuthProvider,
                HttpPipeline = httpPipeline
            };
            return context;
        }
        

        public Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
        {
            // TODO: Implement command execution logic
            // var commandContext = CreateCommandContext(cancellationToken);
            // var invocation = ParseInvocation(args);
            // var command = GetCommand(invocation);
            // await command.ExecuteAsync(commandContext);
            throw new NotImplementedException();
        }



        /// <summary>
        /// Gets a command processor for the given invocation.
        /// <para><b>Thread Safety:</b> Extension indices are lazily cached with guaranteed single initialization.
        /// <see cref="IExtensionIndex.GetCommandMetaData"/> is thread-safe as implementations use
        /// read-only dictionaries populated during construction.</para>
        /// </summary>
        public IProcessor GetCommand(ICommandInvocation invocation)
        {
            // Lazy<T> ensures the factory runs exactly once per extension, even under contention.
            // Without Lazy, GetOrAdd's factory may execute multiple times (only one result stored).
            IExtensionIndex extensionIndex = _extensionIndices.GetOrAdd(
                invocation.ExtensionName,
                extensionName => new Lazy<IExtensionIndex>(() =>
                {
                    string assemblyName = extensionName.ResolveExtensionName().ResolveAssemblyName();
                    _serviceProvider
                        .GetRequiredService<ILoggingService>()
                        .Verbose($"Resolved extension '{extensionName}' to assembly '{assemblyName}'. Loading extension index...");
                    Type? indexType = Type.GetType($"{assemblyName}.ExtensionIndex, {assemblyName}")
                        ?? throw new InvalidOperationException($"No IExtensionIndex implementation found in assembly {assemblyName}");
                    return (Activator.CreateInstance(indexType) as IExtensionIndex)!;
                })
            ).Value;

            // Thread-safe: ExtensionIndex.GetCommandMetaData only reads from dictionaries
            // that are populated in constructor and never modified thereafter.
            ICommandMetadata commandMetadata = extensionIndex.GetCommandMetaData(invocation.CommandName, ClientType);
            
            // Each call creates a new command instance - no shared state
            IProcessor instance = commandMetadata.GetInstance();
            invocation.Parameters.ToList().ForEach(p => instance.SetProperty(p.Key, p.Value));
            return instance;
        }
    }
}
