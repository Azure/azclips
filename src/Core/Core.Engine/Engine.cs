using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Core.Runtime;
using System.Threading.Channels;
using Core.Abstractions.Runtime.Handler;
using Core.Engine.Utils;
using Core.Configuration.Abstractions;
using Core.Configuration.Options;

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
    public class Engine(ClientType clientType, HttpClient httpClient, IAuthContextService authContextService, IAuthService authService, ITelemetryService telemetryService, ILoggingService loggingService, IAssemblyLoadingService assemblyLoadingService, IArgumentParsingService argumentParsingService, IConfigurationService configurationService) : IEngine
    {
        // Extension indices are cached with Lazy<T> to ensure single initialization per extension
        // even under concurrent access. The factory runs exactly once per key.
        private readonly CoreOptions _coreOptions = configurationService.GetConfiguration<CoreOptions>();
        private LFUCache<Lazy<IExtensionIndex>>? _extensionIndices;
        private bool _disposed;

        //TODO(Yabo): Consider implement ProcessAsync separately if we want to expose async processing in the future. For PowerShell, Process() must remain sync due to thread-affinity of cmdlet API, but other hosts could benefit from async.
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
        public void Process(CommandInvocation invocation, CommandOutputDelegate outputDelegate, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            //TODO(Yabo): replace with meaningful logging
            loggingService.Verbose("Engine starts processing...");

            // TODO(Yabo): Placeholder for extension versioning logic
            // Calculate from config service in the future
            string extensionVersion = string.Empty;
            telemetryService.ProcessInvocation(string.IsNullOrEmpty(invocation.ModuleVersion) ? invocation with { ModuleVersion = extensionVersion } : invocation);

            // Create a per-invocation channel for worker-to-caller communication
            Channel<object> outputChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            // Create context with channel-based output handler
            ICommandContext commandContext = CreateCommandContext(outputChannel.Writer);

            // create command instance, inject invocation and context
            IProcessor instance = GetCommand(invocation, commandContext, extensionVersion);

            // Start worker thread for command execution
            Task executeCommandTask = ExecuteCommandAsync(instance, outputChannel.Writer, cancellationToken);

            // Sync-over-async channel pump — intentional, not an anti-pattern here.
            //
            // WHY: PowerShell's $PSCmdlet.WriteObject (and the rest of the Cmdlet API)
            // has strict thread-affinity: it must execute on the thread that owns the
            // current runspace / pipeline. An async Process() would let continuations
            // resume on arbitrary thread-pool threads, causing PSInvalidOperationException.
            //
            // PATTERN: The worker task (ExecuteCommandAsync) runs on a thread-pool thread
            // and writes results into the channel. This loop blocks the *caller's* thread
            // (the runspace thread) to read from the channel and invoke the outputDelegate
            // in the correct synchronization context. This is the standard "sync shell
            // over async core" bridge used by PowerShell hosts.
            //
            // AsTask().GetAwaiter().GetResult() — bridges ValueTask to sync; safe here
            // because we are the sole consumer (SingleReader) and the channel is unbounded.
            // outputDelegate(...).GetAwaiter().GetResult() — ensures WriteObject runs on
            // the runspace thread before we read the next item.
            ChannelReader<object> reader = outputChannel.Reader;
            try
            {
                while (reader.WaitToReadAsync(cancellationToken).AsTask().GetAwaiter().GetResult())
                {
                    while (reader.TryRead(out var output))
                    {
                        outputDelegate(output, cancellationToken).GetAwaiter().GetResult();
                    }
                }
            }
            finally
            {
                // Always observe the worker task — even if the pump was interrupted by
                // cancellation or an exception. This prevents unobserved task exceptions
                // and ensures the worker's finally block (outputWriter.Complete()) runs
                // before we leave Process().
                executeCommandTask.GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Creates a new command context for this invocation with channel-based output.
        /// Each command execution gets its own isolated context.
        /// </summary>
        /// <param name="outputWriter">The channel writer for this invocation's output.</param>
        private ICommandContext CreateCommandContext(ChannelWriter<object> outputWriter)
        {
            // Sync-over-async by design: invoked on the PowerShell runspace thread
            // during Process(), which is intentionally synchronous.
            AuthContext authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult() 
                                    ?? new AuthContext(TenantId: "", SubscriptionId: "");
            ICommandAuthHandler commandAuthHandler = new CommandAuthHandler
            {
                AuthContext = authContext with { },
                AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };

            // Create output handler that writes to the per-invocation channel
            ICommandOutputHandler commandOutputHandler = new CommandOutputHandler
            {
                WriteOutput = async (value, ct) => await outputWriter.WriteAsync(value, ct)
            };
            ICommandTelemetryHandler commandTelemetryHandler = new CommandTelemetryHandler
            {
                StartActivity = telemetryService.StartActivity
            };

            IRuntimeHttpPipeline httpPipeline = new HttpPipeline(
                new HttpClientFactory(httpClient),
                _coreOptions.Http.MaxRetries,
                _coreOptions.Http.MaxRetriesFor429);
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
        /// Executes the command asynchronously, writing output to the channel.
        /// <para>No <see cref="Task.Run"/> wrapper — <c>ProcessAsync</c> is truly async,
        /// so the first real await yields back to the caller and continuations resume on
        /// the thread pool (no <see cref="SynchronizationContext"/> is present).
        /// The <c>finally</c> guarantees the channel is completed even on cancellation
        /// or exception, preventing the reader pump from hanging.</para>
        /// </summary>
        private async Task ExecuteCommandAsync(IProcessor command, ChannelWriter<object> outputWriter, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await command.ProcessAsync();
            }
            finally
            {
                // Signal completion to the reader — must run regardless of success,
                // failure, or cancellation to unblock the channel pump in Process().
                outputWriter.Complete();
            }
        }

        /// <summary>
        /// Gets a command processor for the given invocation.
        /// <para><b>Thread Safety:</b> Extension indices are lazily cached with guaranteed single initialization.
        /// <see cref="IExtensionIndex.GetCommandMetadata"/> is thread-safe as implementations use
        /// read-only dictionaries populated during construction.</para>
        /// </summary>
        public IProcessor GetCommand(CommandInvocation invocation, ICommandContext context, string extensionVersion = "")
        {
            //TODO(Yabo): come up with: intercept CLI global parameters elegantly if any
            CommandMetadata commandMetadata = GetExtensionIndex(invocation.ExtensionName, invocation.IsTransient, extensionVersion).GetCommandMetadata(invocation.CommandName, clientType);
            IDictionary<string, object> parsedParameters;
            try
            {
                parsedParameters = argumentParsingService.ParseArguments(invocation.Parameters, commandMetadata.Parameters, commandMetadata.ParameterAliases);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse arguments for command '{invocation.CommandName}': {ex.Message}", ex);
            }
            IList<string> validationErrors = argumentParsingService.ValidateArguments(parsedParameters, commandMetadata.Parameters.ToDictionary(kvp => kvp.Value.Name, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase));
            if (validationErrors != null && validationErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid arguments: {string.Join(", ", validationErrors)}");
            }
            return argumentParsingService.BindArgumentsToCommand(commandMetadata.GetInstance(), parsedParameters).Init(context);   
        }

        private IExtensionIndex GetExtensionIndex(string extensionName, bool isTransient, string extensionVersion)
        {
            if (!isTransient)
            {
                _extensionIndices ??= new(_coreOptions.Engine.ExtensionIndexCacheSize);
                // Lazy<T> ensures the factory runs exactly once per extension, even under contention.
                // Without Lazy, GetOrAdd's factory may execute multiple times (only one result stored).
                return _extensionIndices.GetOrAdd(
                    string.IsNullOrEmpty(extensionVersion) ? extensionName : $"{extensionName}-{extensionVersion}",
                    extensionName => new Lazy<IExtensionIndex>(() => assemblyLoadingService.LoadExtension(extensionName, extensionVersion).GetExtensionIndex())
                ).Value;
            }
            // For transient invocations, bypass the cache and load directly
            return assemblyLoadingService.LoadExtension(extensionName, extensionVersion).GetExtensionIndex();
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
                    _extensionIndices?.Clear();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Releases all resources used by Engine.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}