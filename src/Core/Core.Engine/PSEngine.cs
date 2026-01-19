using Core.Abstractions;
using Core.Implementation;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Core.Runtime;
using System.Threading.Channels;

namespace Core.Engine
{
    /// <summary>
    /// PowerShell command engine for executing commands in PowerShell runspaces.
    /// <para><b>Thread Safety:</b> This engine is designed for concurrent command execution.
    /// Each <see cref="Process"/> call creates isolated context with per-invocation output channel.
    /// Extension indices are cached using thread-safe lazy initialization.</para>
    /// <para><b>Output Routing:</b> Each invocation gets its own Channel for worker-to-caller 
    /// communication. The worker thread writes to the channel, and the caller's thread reads 
    /// and dispatches to the provided output delegate (e.g., $PSCmdlet.WriteObject).</para>
    /// </summary>
    public class PSEngine : IEngine
    {
        // Extension indices are cached with Lazy<T> to ensure single initialization per extension
        // even under concurrent access. The factory runs exactly once per key.
        private readonly ConcurrentDictionary<string, Lazy<IExtensionIndex>> _extensionIndices = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;

        public ClientType ClientType = ClientType.PowerShell;

        public PSEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Processes a command invocation with isolated output routing.
        /// <para><b>Thread Safety:</b> Each call creates its own output channel. The worker thread
        /// writes output to the channel, while the caller's thread pumps the channel and invokes
        /// the provided output delegate. This ensures output is routed to the correct $PSCmdlet.</para>
        /// </summary>
        /// <param name="invocation">The command invocation containing extension name, command name, and parameters.</param>
        /// <param name="outputDelegate">The output delegate for this invocation (typically wired to $PSCmdlet.WriteObject).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public void Process(ICommandInvocation invocation, CommandOutputDelegate outputDelegate, CancellationToken cancellationToken = default)
        {
            // Create a per-invocation channel for worker-to-caller communication
            Channel<object> outputChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            // Create context with channel-based output handler
            ICommandContext commandContext = CreateCommandContext(outputChannel.Writer);

            // Start worker thread for command execution
            Task executeCommandTask = ExecuteCommandAsync(GetCommand(invocation).SetCommandContext(commandContext), outputChannel.Writer, cancellationToken);

            // Pump the channel on the caller's thread (runspace-affine)
            ChannelReader<object> reader = outputChannel.Reader;
            while (reader.WaitToReadAsync(cancellationToken).AsTask().GetAwaiter().GetResult())
            {
                while (reader.TryRead(out var output))
                {
                    outputDelegate(output, cancellationToken).GetAwaiter().GetResult();
                }
            }

            // Ensure worker completes (propagate any exceptions)
            executeCommandTask.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a new command context for this invocation with channel-based output.
        /// Each command execution gets its own isolated context.
        /// </summary>
        /// <param name="outputWriter">The channel writer for this invocation's output.</param>
        private ICommandContext CreateCommandContext(ChannelWriter<object> outputWriter)
        {
            IAuthService authService = _serviceProvider.GetRequiredService<IAuthService>();
            AuthContext authContext = authService.GetCurrentContext()!;
            CommandAuthHandler commandAuthHandler = new CommandAuthHandler
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

            // Create output handler that writes to the per-invocation channel
            CommandOutputHandler commandOutputHandler = new CommandOutputHandler
            {
                WriteOutput = async (value, ct) => await outputWriter.WriteAsync(value, ct)
            };

            IRuntimeHttpPipeline httpPipeline = new HttpPipeline(new HttpClientFactory(_httpClient));
            ICommandContext context = new CommandContext
            {
                CommandOutputHandler = commandOutputHandler,
                CommandAuthHandler = commandAuthHandler,
                HttpPipeline = httpPipeline.Prepend(commandAuthHandler.SendAsync)
            };
            return context;
        }

        /// <summary>
        /// Executes the command on a worker thread, writing output to the channel.
        /// </summary>
        private async Task ExecuteCommandAsync(IProcessor command, ChannelWriter<object> outputWriter, CancellationToken cancellationToken = default)
        {
            await Task.Run(async () =>
            {
                try
                {
                    await command.ProcessAsync();
                }
                finally
                {
                    // Signal completion to the reader
                    outputWriter.Complete();
                }
            }, cancellationToken);
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