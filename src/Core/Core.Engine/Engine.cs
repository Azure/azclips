using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Core.Runtime;
using System.Threading.Channels;
using Core.Abstractions.Runtime.Handler;
using Core.Engine.Utils;
using System.Diagnostics;

namespace Core.Engine
{
    /// <summary>
    /// Mediator pattern, Engine orchestrates services and runtimes
    /// <para><b>Thread Safety:</b> This engine is designed for concurrent command execution.
    /// Each <see cref="Process"/> call creates isolated context with per-invocation output channel.
    /// Extension indices are cached using thread-safe lazy initialization.</para>
    /// <para><b>Output Routing:</b> Each invocation gets its own Channel for worker-to-caller
    /// communication. The worker thread writes to the channel, and the caller's thread reads
    /// and dispatches to the provided output delegate (e.g., $PSCmdlet.WriteObject).</para>
    /// </summary>
    public partial class Engine(
        ClientType clientType,
        HttpClient httpClient,
        IAuthContextService authContextService,
        ICommandHttpMessageHandler commandHttpMessageHandler,
        IAuthService authService,
        ITelemetryService telemetryService,
        ILoggingService loggingService,
        IAssemblyLoadingService assemblyLoadingService,
        IArgumentParsingService argumentParsingService) : IEngine
    {
        // Extension indices are cached with Lazy<T> to ensure single initialization per extension
        // even under concurrent access. The factory runs exactly once per key.
        //TODO(Yabo): tune the cache size based on expected number of extensions, get this key from config service in the future
        private const int cacheSize = 512;
        private LFUCache<Lazy<IExtensionIndex>>? _extensionIndices;
        private bool _disposed;

        public IInvocationSession InvokeSession(CommandInvocation invocation, OutputStreams outputStreams, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Create a per-invocation channel for worker-to-caller communication
            var session = new InvocationSession(clientType, invocation, outputStreams, cancellationToken);
            SubscribeAll(session);
            session.Signal(Events.SessionCreated, $"Session {session.Id} created").GetAwaiter().GetResult();
            // TODO(Yabo): get extension version from config service in the future
            session.GetExtensionVersion = () => string.Empty;
            session.LoadAssembly = assemblyLoadingService.LoadExtension;
            session.BindProcessor = argumentParsingService.BindArgumentsToProcessor;
            session.CreateProcessorContext = CreateProcessorContext;
            session.CacheGetOrAdd = CacheGetOrAdd;
            return session;
        }

        private IExtensionIndex CacheGetOrAdd(string name, string version, Func<IExtensionIndex> getIndex)
        {
            _extensionIndices ??= new(cacheSize);
            string key = string.IsNullOrEmpty(version) ? name : $"{name}-{version}";
            return _extensionIndices.GetOrAdd(
                    key,
                    _ => new Lazy<IExtensionIndex>(getIndex)
                ).Value;
        }

        /// <summary>
        /// Creates a new processor context for this invocation with channel-based output.
        /// Each processor execution gets its own isolated context.
        /// </summary>
        private ProcessorContext CreateProcessorContext(InvocationSession session)
        {
            // Sync-over-async by design: invoked on the PowerShell runspace thread
            // during Process(), which is intentionally synchronous.
            AuthContext authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()
                                    ?? new AuthContext(TenantId: "", SubscriptionId: "");
            IAuthHandler authHandler = new AuthHandler
            {
                AuthContext = authContext with { },
                AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };

            // Create output handler that writes to the per-invocation channel
            IOutputHandler outputHandler = new OutputHandler
            {
                WriteOutput = async (value, _) => await session.OutputChannel.Writer.WriteAsync(new OutputObject(value), session.Token)
            };

            IRuntimeHttpPipeline httpPipeline = new HttpPipeline(new HttpClientFactory(httpClient));
            ProcessorContext context = new ProcessorContext
            (
                HttpPipeline: httpPipeline.Prepend(authHandler.SendAsync),
                EventListener: session,
                AuthHandler: authHandler,
                OutputHandler: outputHandler
            );
            httpPipeline.Prepend(commandHttpMessageHandler.SendAsync);
            return context;
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
