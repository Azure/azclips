using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Engine.Utils;
using Core.Runtime;
using Core.Runtime.Handler;

namespace Core.Engine
{
    /// <summary>
    /// Mediator pattern, Engine orchestrates services and runtimes
    /// <para><b>Thread Safety:</b> This engine is designed for concurrent command execution.
    /// Each <see cref="InvokeSession"/> call creates isolated context with per-invocation output channel.
    /// Extension indices are cached using thread-safe lazy initialization.</para>
    /// <para><b>Output Routing:</b> Each invocation gets its own Channel for worker-to-caller
    /// communication. The worker thread writes to the channel, and the caller's thread reads
    /// and dispatches to the provided output delegate (e.g., $PSCmdlet.WriteObject).</para>
    /// </summary>
    public partial class Engine(
        ClientType clientType,
        HttpClient httpClient,
        IAuthContextService authContextService,
        IAuthService authService,
        ITelemetryService telemetryService,
        ILoggingService loggingService,
        IAssemblyLoadingService assemblyLoadingService,
        IArgumentParsingService argumentParsingService,
        IConfigurationService configurationService,
        IHttpPipelineService httpPipelineService) : IEngine
    {
        // Extension indices are cached with Lazy<T> to ensure single initialization per extension
        // even under concurrent access. The factory runs exactly once per key.
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
            session.Option = GetOption();
            session.CreateProcessorContext = s => CreateProcessorContext(s);
            session.CacheGetOrAdd = (name, version, getIndex) => CacheGetOrAdd(name, version, getIndex);
            return session;
        }

        private CoreOptions GetOption()
        {
            return configurationService.GetConfiguration<CoreOptions>();
        }

        private IExtensionIndex CacheGetOrAdd(string name, string version, Func<IExtensionIndex> getIndex)
        {
            _extensionIndices ??= new(configurationService.GetConfiguration<CoreOptions>().Engine.ExtensionIndexCacheSize);
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
        private async Task<ProcessorContext> CreateProcessorContext(InvocationSession session)
        {
            AuthContext authContext = await authContextService.GetCurrentContextAsync() ?? new AuthContext(TenantId: "", SubscriptionId: "");
            IAuthHandler authHandler = new AuthHandler
            {
                AuthContext = authContext with { },
                AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };

            // Create output handler that writes to the per-invocation channel
            IOutputHandler outputHandler = new OutputHandler
            {
                WriteOutput = session.ProcessorOutputWriter
            };

            IRuntimeHttpPipeline httpPipeline = new HttpPipeline(
                new HttpClientFactory(httpClient),
                session.Option.Http.MaxRetries,
                session.Option.Http.MaxRetriesFor429);
            ProcessorContext context = new ProcessorContext
            (
                HttpPipeline: httpPipeline.Prepend(authHandler.SendAsync),
                EventListener: session,
                AuthHandler: authHandler,
                OutputHandler: outputHandler
            );
            // passthrough by default, terminate during client mocking
            httpPipeline.Prepend(httpPipelineService.HttpPipelineTerminatingStep);
            // trace every http request message, record/playback request during test
            httpPipeline.Append(httpPipelineService.HttpRequestMessageTracingStep);
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
