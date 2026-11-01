// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using System.Globalization;
using Core.Engine.Utils;
using Core.Runtime;
using Core.Runtime.Handler;
using Core.Telemetry;
using OpenTelemetry;

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
        ClientMetadata clientMetadata,
        ISendAsyncTerminalFactory httpTransportFactory,
        IProfileService profileService,
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

        public ISession OpenExecutionSession(CommandInvocation invocation, OutputStreams outputStreams, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Create a per-invocation channel for worker-to-caller communication
            var session = new ExecutionSession(
                clientMetadata.ClientType, invocation, outputStreams, cancellationToken);
            SubscribeAll(session.EventBus);

            session.EventBus.Publish(Events.SessionCreated,
                new EventData
                {
                    Message = $"{session.Id} created",
                }).GetAwaiter().GetResult();

            // TODO(Yabo): get extension version from config service in the future
            session.GetExtensionVersion = () => invocation.ModuleVersion ?? string.Empty;
            session.LoadAssembly = assemblyLoadingService.LoadExtension;
            session.BindProcessor = argumentParsingService.BindArgumentsToProcessor; 
            session.Option = GetOption();
            session.ResolveProfileAsync = ResolveProfileAsync;
            session.CreateProcessorContext = CreateProcessorContext;
            session.CacheGetOrAdd = CacheGetOrAdd;
            session.TagDataBoundary = boundary => telemetryService.AddTag(
                TelemetryTags.DataBoundary, TelemetryInvocationContext.NormalizeDataBoundary(boundary));
            session.ResolveDataBoundaryAsync = ResolveDataBoundaryAsync;
            return session;
        }

        public ISession OpenDiscoverySession(IList<string> extensionNames, string? commandName, OutputStreams outputStreams, bool isTransient = false, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var session = new DiscoverySession(
                clientMetadata.ClientType,
                extensionNames,
                commandName,
                outputStreams,
                isTransient,
                cancellationToken);
            SubscribeAll(session.EventBus);
            session.EventBus.Publish(Events.SessionCreated, new EventData { Message = $"{session.Id} created" }).GetAwaiter().GetResult();
            // TODO(Yabo): get extension version from config service in the future
            session.GetExtensionVersion = (name) => string.Empty;
            session.LoadAssembly = assemblyLoadingService.LoadExtension;
            session.LoadAllAssemblies = () => assemblyLoadingService.LoadAllExtensions();
            session.CacheGetOrAdd = (name, version, getIndex) => CacheGetOrAdd(name, version, getIndex);
            return session;
        }

        private CoreOptions GetOption()
        {
            return configurationService.GetConfiguration<CoreOptions>();
        }

        internal static int GetPollingIntervalTemp()
        {
            // Temporary, unsupported test hook until polling intervals come from configuration.
            const string variable = "TEMP_AZCLIPS_PLAYBACK_TEST_POLLING_INTERVAL";
            string? value = Environment.GetEnvironmentVariable(variable);
            return value is null ? 15 : int.Parse(value, CultureInfo.InvariantCulture);
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
        /// <param name="session">The execution session.</param>
        /// <param name="profileNameOverride">
        /// The pre-bound global <c>--profile</c>/<c>-Profile</c> value, or null when not supplied.
        /// </param>
        private async Task<ProcessorContext> CreateProcessorContext(ExecutionSession session)
        {
            IAuthHandler authHandler = new AuthHandler
            {
                Profile = session.Profile,
                AcquireTokenAsyncDefault = authService.AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };

            // Create output handler that writes to the per-invocation channel
            IOutputHandler outputHandler = new OutputHandler
            {
                WriteOutput = session.ProcessorOutputWriter
            };

            IRuntimeHttpPipeline httpPipeline = new HttpPipeline(
                httpTransportFactory,
                session.Option.Http.MaxRetries,
                session.Option.Http.MaxRetriesFor429)
            {
                // TODO(Yabo): Get this from config service
                PollingInterval = GetPollingIntervalTemp()
            };
            ProcessorContext context = new ProcessorContext
            (
                HttpPipeline: httpPipeline.Prepend(authHandler.SendAsync),
                Publisher: session.EventBus,
                AuthHandler: authHandler,
                OutputHandler: outputHandler,
                Profile:session.Profile
            );
            // stamp a per-request ARM correlation ID before the request enters retry handling
            httpPipeline.Prepend(httpPipelineService.HttpAddClientRequestIdStep);
            // stamp User-Agent header on every outgoing request
            httpPipeline.Prepend(httpPipelineService.HttpAddUserAgentStep);
            // trace every http request message, record/playback request during test
            httpPipeline.Append(httpPipelineService.HttpRequestMessageTracingStep);
            // passthrough by default, terminate during client mocking
            httpPipeline.Append(httpPipelineService.HttpPipelineTerminatingStep);

            // Announce the created context; the ProcessorContextCreated subscriber enriches the
            // telemetry root span with profile-derived tags (subscription, tenant, auth type).
            await session.EventBus.Publish(Events.ProcessorContextCreated, new EventData { Payload = session.Profile });

            return context;
        }

        /// <summary>
        /// Resolves the profile for an invocation: the explicit <c>--profile</c> override if provided,
        /// otherwise the active profile, otherwise null. An explicit override that names a profile which
        /// does not exist is an error; a stale active pointer simply resolves to null.
        /// </summary>
        private async Task<Profile?> ResolveProfileAsync(string? profileNameOverride)
        {
            bool explicitOverride = !string.IsNullOrWhiteSpace(profileNameOverride);
            string? name = explicitOverride
                ? profileNameOverride?.Trim()
                : await profileService.GetActiveProfileNameAsync();

            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            Profile? profile = await profileService.GetProfileAsync(name);
            if (profile is null && explicitOverride)
            {
                throw AzclipsErrorException.ProfileNotFound($"Profile '{profileNameOverride?.Trim()}' does not exist.");
            }

            return profile;
        }

        /// <summary>
        /// Resolves the telemetry destination (EU/ROW) for a profile's tenant. Pure: returns the
        /// resolved value instead of tagging any Activity itself. Callers (ExecutionSession's own
        /// RunAsync/Run) must call <c>TagDataBoundary(...)</c> directly in their own method body —
        /// see <see cref="ExecutionSession.TagDataBoundary"/> remarks for why.
        /// </summary>
        private async Task<string?> ResolveDataBoundaryAsync(Profile profile, CancellationToken cancellationToken)
        {
            // Suppresses ALL instrumentation (both our own TelemetryService-created spans and
            // OpenTelemetry's HTTP auto-instrumentation) for the two network calls this makes
            // (token acquisition, then the ARM data-boundary query) — both would otherwise end
            // and export before the boundary they're used to discover is known, so
            // AriaActivityExporter would fall back to routing them to ROW even for an EU
            // tenant. SuppressInstrumentationScope is the OpenTelemetry SDK's own mechanism
            // (used e.g. by ZipkinExporter to avoid self-tracing) — AsyncLocal-backed, so this
            // is safe forward propagation into the awaited call below, same as elsewhere in
            // this codebase; it's checked by every ActivitySource's sampler, so it covers HTTP
            // client instrumentation too, unlike a custom check inside TelemetryService alone.
            using var _ = SuppressInstrumentationScope.Begin();
            return await DataBoundaryResolver.ResolveAndCacheAsync(
                profile.TenantId,
                async tokenCancellationToken =>
                {
                    var result = await authService.AcquireTokenSilentlyAsync(
                        scopes: null,
                        tenantId: profile.TenantId,
                        promptOnError: false,
                        cancellationToken: tokenCancellationToken,
                        profile: profile);
                    return result.AccessToken;
                },
                TelemetryRoutingResolver.DefaultTimeout,
                cancellationToken,
                loggingService);
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
