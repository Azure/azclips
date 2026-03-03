using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Core.Runtime;
using System.Threading.Channels;
using Core.Abstractions.Runtime.Handler;
using Core.Engine.utils;

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
        //TODO: tune the cache size based on expected number of extensions
        private readonly LFUCache<Lazy<IExtensionIndex>> _extensionIndices = new(512);

        private readonly HttpClient _httpClient;
        private readonly Action<string> WriteVerbose;
        private readonly Action<string> WriteDebug;
        private readonly Func<AuthContext> GetAuthContext;
        private readonly AcquireTokenAsyncDelegate AcquireTokenSilentlyAsync;
        private readonly StartActivityDelegate StartActivity;
        private bool _disposed;

        public ClientType ClientType = ClientType.PowerShell;

        public PSEngine(IAuthContextService authContextService, IAuthService authService, ITelemetryService telemetryService, ILoggingService loggingService)
        {
            _httpClient = new HttpClient();
            WriteVerbose = loggingService.Verbose;
            WriteDebug = loggingService.Debug;
            GetAuthContext = () => authContextService.GetCurrentContextAsync().GetAwaiter().GetResult() ?? new AuthContext() { SubscriptionId = "", TenantId = "" };
            AcquireTokenSilentlyAsync = authService.AcquireTokenSilentlyAsync;
            StartActivity = telemetryService.StartActivity;
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
        /// <exception cref="ObjectDisposedException">Thrown if the engine has been disposed.</exception>
        public void Process(ICommandInvocation invocation, CommandOutputDelegate outputDelegate, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Create a per-invocation channel for worker-to-caller communication
            Channel<object> outputChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            // Create context with channel-based output handler
            ICommandContext commandContext = CreateCommandContext(outputChannel.Writer);

            // Start worker thread for command execution
            Task executeCommandTask = ExecuteCommandAsync(GetCommand(invocation).Init(commandContext), outputChannel.Writer, cancellationToken);

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
            AuthContext authContext = GetAuthContext();
            ICommandAuthHandler commandAuthHandler = new CommandAuthHandler
            {
                AuthContext = new AuthContext
                {
                    SubscriptionId = authContext.SubscriptionId,
                    TenantId = authContext.TenantId
                },
                AcquireTokenAsyncDefault = AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };

            // Create output handler that writes to the per-invocation channel
            ICommandOutputHandler commandOutputHandler = new CommandOutputHandler
            {
                WriteOutput = async (value, ct) => await outputWriter.WriteAsync(value, ct)
            };
            ICommandTelemetryHandler commandTelemetryHandler = new CommandTelemetryHandler
            {
                StartActivity = StartActivity
            };

            IRuntimeHttpPipeline httpPipeline = new HttpPipeline(new HttpClientFactory(_httpClient));
            ICommandContext context = new CommandContext
            {
                CommandOutputHandler = commandOutputHandler,
                CommandAuthHandler = commandAuthHandler,
                CommandTelemetryHandler = commandTelemetryHandler,
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
                    WriteVerbose($"Resolved extension '{extensionName}' to assembly '{assemblyName}'. Loading extension index...");
                    Type? indexType = Type.GetType($"{assemblyName}.ExtensionIndex, {assemblyName}")
                        ?? throw new InvalidOperationException($"No IExtensionIndex implementation found in assembly {assemblyName}");
                    return (Activator.CreateInstance(indexType) as IExtensionIndex)!;
                })
            ).Value;

            // Thread-safe: ExtensionIndex.GetCommandMetaData only reads from dictionaries
            // that are populated in constructor and never modified thereafter.
            ICommandMetadata commandMetadata = extensionIndex.GetCommandMetaData(invocation.CommandName, ClientType);
            WriteVerbose($"Resolved command '{invocation.CommandName}' to type '{commandMetadata.CommandType.FullName}'. Creating instance...");
            // Each call creates a new command instance - no shared state
            IProcessor instance = commandMetadata.GetInstance();
            invocation.Parameters.ToList().ForEach(p => instance.SetProperty(p.Key, p.Value));
            return instance;
        }

        /// <summary>
        /// Releases the unmanaged resources used by PSEngine and optionally releases managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                    _extensionIndices.Clear();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Releases all resources used by PSEngine.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}