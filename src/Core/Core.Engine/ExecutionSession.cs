// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Threading.Channels;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Core.Telemetry;

namespace Core.Engine;

internal class ExecutionSession : ISession
{
    private bool _disposed = false;
    private ClientType clientType;
    private OutputStreamDelegate sessionOutputWriter = null!;
    private IDisposable loggingScope;

    private readonly OutputStreamAdapter outputStreamAdapter;

    internal Channel<object> OutputChannel = null!;
    internal Func<string> GetExtensionVersion { get; set; } = null!;
    internal Func<string, string, Assembly> LoadAssembly { get; set; } = null!;
    internal Func<IProcessor, IDictionary<string, object>, IDictionary<string, ParameterMetadata>, IDictionary<string, string>?, Profile?, IProcessor> BindProcessor { get; set; } = null!;
    internal Func<ExecutionSession, Task<ProcessorContext>> CreateProcessorContext { get; set; } = null!;
    internal Func<string, string, Func<IExtensionIndex>, IExtensionIndex> CacheGetOrAdd { get; set; } = null!;
    internal Func<string?, Task<Profile?>> ResolveProfileAsync { get; set; } = null!;
    /// <summary>
    /// Resolves the ARM data boundary (EU/Global) for a resolved profile. Pure \u2014 does not
    /// write to <see cref="RoutingContext"/> itself; see <see cref="RoutingContext"/>'s remarks.
    /// </summary>
    internal Func<Profile, CancellationToken, Task<string?>> ResolveDataBoundaryAsync { get; set; } = null!;
    /// <summary>
    /// Set directly by <see cref="RunAsync"/>/<see cref="Run"/> (never from a nested awaited
    /// helper or event subscriber) \u2014 an AsyncLocal-backed write only remains visible to code
    /// that runs later in the SAME method body; it is discarded once a callee method returns.
    /// </summary>
    internal ITelemetryRoutingContext RoutingContext { get; set; } = null!;
    internal CoreOptions Option { get; set; } = null!;
    internal OutputStreamDelegate ProcessorOutputWriter = null!;
    internal Profile? Profile { get; set; } = null!;

    public string Id { get; }
    public IEventBus EventBus { get; private set; }
    public CommandInvocation Invocation { get; }

    public ExecutionSession(ClientType clientType, CommandInvocation invocation, OutputStreams outputStreams, CancellationToken cancellationToken = default)
    {
        this.clientType = clientType;
        Id = $"{GetType().Name}-{clientType}-{Guid.NewGuid().ToString()}";
        Invocation = invocation;
        EventBus = new RuntimeEventBus(cancellationToken);
        // wire output streams and logging service
        sessionOutputWriter = outputStreams.WriteOutput;
        outputStreamAdapter = new OutputStreamAdapter(outputStreams, EventBus.Token);
        loggingScope = ScopedLoggingService.UseScope(outputStreamAdapter);
    }

    public IExtensionIndex GetExtensionIndex()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Invocation.IsTransient)
        {
            return LoadAssembly(Invocation.ExtensionName, GetExtensionVersion()).GetExtensionIndex();
        }
        return CacheGetOrAdd(Invocation.ExtensionName, GetExtensionVersion(), () => LoadAssembly(Invocation.ExtensionName, GetExtensionVersion()).GetExtensionIndex());
    }

    public async Task RunAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EventBus.Token.ThrowIfCancellationRequested();
        ProcessorOutputWriter = sessionOutputWriter;
        var processor = await GetProcessor();

        // Resolved and set directly here, in RunAsync's own body \u2014 not in a nested awaited
        // helper \u2014 so the write is visible to processor.ProcessAsync() below. See
        // RoutingContext's remarks for why this can't be extracted into a shared async helper.
        if (Profile is not null)
        {
            RoutingContext.Set(Profile.TenantId, null);
            var boundary = await ResolveDataBoundaryAsync(Profile, EventBus.Token);
            RoutingContext.Set(Profile.TenantId, boundary);
        }
        else
        {
            RoutingContext.Set(null, null);
        }

        await EventBus.Publish(Events.ProcessorAcquired);
        await processor.ProcessAsync();
    }

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
    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EventBus.Token.ThrowIfCancellationRequested();

        OutputChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        OutputStreamAdapter channelAdapter = new OutputStreamAdapter(GetChannelStreams(), EventBus.Token);
        using var channelScope = ScopedLoggingService.UseScope(channelAdapter);
        ProcessorOutputWriter = async (value, _) => await OutputChannel.Writer.WriteAsync(new OutputObject(value), EventBus.Token);

        /// <summary>
        /// Executes the processor asynchronously, writing output to the channel.
        /// <para>No <see cref="Task.Run"/> wrapper — <c>ProcessAsync</c> is truly async,
        /// so the first real await yields back to the caller and continuations resume on
        /// the thread pool (no <see cref="SynchronizationContext"/> is present).
        /// The <c>finally</c> guarantees the channel is completed even on cancellation
        /// or exception, preventing the reader pump from hanging.</para>
        /// </summary>
        async Task ExecuteProcessorAsync()
        {
            try
            {
                EventBus.Token.ThrowIfCancellationRequested();
                var processor = await GetProcessor();

                // See RunAsync's identical block: must stay inline in this local function's own
                // body so the write is visible to processor.ProcessAsync() below.
                if (Profile is not null)
                {
                    RoutingContext.Set(Profile.TenantId, null);
                    var boundary = await ResolveDataBoundaryAsync(Profile, EventBus.Token);
                    RoutingContext.Set(Profile.TenantId, boundary);
                }
                else
                {
                    RoutingContext.Set(null, null);
                }

                await EventBus.Publish(Events.ProcessorAcquired);
                await processor.ProcessAsync();
            }
            finally
            {
                // Publish completion to the reader — must run regardless of success,
                // failure, or cancellation to unblock the channel pump in Process().
                OutputChannel.Writer.Complete();
            }
        }
        Task executeProcessorTask = ExecuteProcessorAsync();

        // Sync-over-async channel pump — intentional, not an anti-pattern here.
        //
        // WHY: PowerShell's $PSCmdlet.WriteObject (and the rest of the Cmdlet API)
        // has strict thread-affinity: it must execute on the thread that owns the
        // current runspace / pipeline. An async Process() would let continuations
        // resume on arbitrary thread-pool threads, causing PSInvalidOperationException.
        //
        // PATTERN: The worker task (ExecuteProcessorAsync) runs on a thread-pool thread
        // and writes results into the channel. This loop blocks the *caller's* thread
        // (the runspace thread) to read from the channel and invoke the outputDelegate
        // in the correct synchronization context. This is the standard "sync shell
        // over async core" bridge used by PowerShell hosts.
        //
        // AsTask().GetAwaiter().GetResult() — bridges ValueTask to sync; safe here
        // because we are the sole consumer (SingleReader) and the channel is unbounded.
        // outputStreamDelegate(...).GetAwaiter().GetResult() — ensures WriteObject runs on
        // the runspace thread before we read the next item.
        ChannelReader<object> reader = OutputChannel.Reader;
        try
        {
            while (reader.WaitToReadAsync(EventBus.Token).AsTask().GetAwaiter().GetResult())
            {
                while (reader.TryRead(out var output))
                {
                    WriteChannelOutput(output, outputStreamAdapter);
                }
            }
        }
        finally
        {
            // Always observe the worker task — even if the pump was interrupted by
            // cancellation or an exception. This prevents unobserved task exceptions
            // and ensures the worker's finally block (OutputChannel.Writer.Complete()) runs
            // before we leave Process().
            executeProcessorTask.GetAwaiter().GetResult();
        }
    }

    private void WriteChannelOutput(object? output, OutputStreamAdapter outputStreamAdapter)
    {
        switch (output)
        {
            case OutputObject obj:
                sessionOutputWriter(obj.Value, EventBus.Token).GetAwaiter().GetResult();
                break;
            case VerboseMessage verbose:
                outputStreamAdapter.Verbose(verbose.Message);
                break;
            case DebugMessage debug:
                outputStreamAdapter.Debug(debug.Message);
                break;
            case WarningMessage warning:
                outputStreamAdapter.Warn(warning.Message);
                break;
            case InformationMessage info:
                outputStreamAdapter.Info(info.Message);
                break;
            case ErrorMessage error:
                outputStreamAdapter.Error(error.Message);
                break;
            default:
                throw new InvalidOperationException($"Unrecognized output type: {(output == null ? "null" : output.GetType().FullName)}");
        }
    }

    private OutputStreams GetChannelStreams() => new OutputStreams
        (
            WriteOutput: (value, ct) =>
            {
                OutputChannel.Writer.WriteAsync(new OutputObject(value), ct).GetAwaiter().GetResult();
                return Task.CompletedTask;
            },
            WriteVerbose: (value, ct) =>
            {
                OutputChannel.Writer.WriteAsync(new VerboseMessage((string)value), ct).GetAwaiter().GetResult();
                return Task.CompletedTask;
            },
            WriteDebug: (value, ct) =>
            {
                OutputChannel.Writer.WriteAsync(new DebugMessage((string)value), ct).GetAwaiter().GetResult();
                return Task.CompletedTask;
            },
            WriteWarning: (value, ct) =>
            {
                OutputChannel.Writer.WriteAsync(new WarningMessage((string)value), ct).GetAwaiter().GetResult();
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                OutputChannel.Writer.WriteAsync(new InformationMessage((string)value), ct).GetAwaiter().GetResult();
                return Task.CompletedTask;
            },
            WriteError: (value, ct) =>
            {
                OutputChannel.Writer.WriteAsync(new ErrorMessage((string)value), ct).GetAwaiter().GetResult();
                return Task.CompletedTask;
            }
        );

    private async Task<IProcessor> GetProcessor()
    {
        CommandMetadata commandMetadata = GetExtensionIndex().GetCommandMetadata(Invocation.CommandName, clientType);

        // Global-parameter pre-binding pass: resolve and normalize the global --profile/-Profile
        // value from the raw invocation parameters BEFORE the processor context is created, because
        // the context (which carries the resolved Profile?) is injected via Init() ahead of the
        // command-specific argument binding below.
        string? profileName = ExtractGlobalProfileParameter(commandMetadata);
        Profile = await ResolveProfileAsync(profileName);

        // Create context with channel-based output handler
        ProcessorContext processorContext = await CreateProcessorContext(this);

        IProcessor processor = commandMetadata.GetInstance().Init(processorContext);
        try
        {
            processor = BindProcessor(processor, Invocation.Parameters, commandMetadata.Parameters, commandMetadata.ParameterAliases, Profile);
        }
        catch (AzclipsErrorException)
        {
            // Structured error (e.g. a missing required argument): let it propagate as-is so the
            // host renders it directly instead of falling through to AI-fallback recovery.
            throw;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse arguments for command '{Invocation.CommandName}': {ex.Message}", ex);
        }
        return processor;
    }

    /// <summary>
    /// Extracts the global <c>--profile</c>/<c>-Profile</c> value from the raw invocation parameters,
    /// normalizing surface aliases/casing via the command metadata. Returns null when the command does
    /// not declare a profile parameter or none was supplied.
    /// </summary>
    private string? ExtractGlobalProfileParameter(CommandMetadata commandMetadata)
    {
        const string canonicalName = "Profile";
        foreach (var (key, value) in Invocation.Parameters)
        {
            string resolvedKey = key;
            if (!commandMetadata.Parameters.ContainsKey(resolvedKey)
                && commandMetadata.ParameterAliases != null
                && commandMetadata.ParameterAliases.TryGetValue(resolvedKey, out var canonicalKey))
            {
                resolvedKey = canonicalKey;
            }

            bool isProfile =
                (commandMetadata.Parameters.TryGetValue(resolvedKey, out var metadata)
                    && string.Equals(metadata.Name, canonicalName, StringComparison.OrdinalIgnoreCase))
                || string.Equals(key, canonicalName, StringComparison.OrdinalIgnoreCase);

            if (isProfile)
            {
                return CoerceToString(value);
            }
        }
        return null;
    }

    /// <summary>Flattens a raw parameter value (scalar or CLI list-of-lists) to a single string.</summary>
    private static string? CoerceToString(object? value) => value switch
    {
        null => null,
        string s => s,
        List<List<string>> listOfLists => listOfLists.SelectMany(l => l).FirstOrDefault(),
        List<string> list => list.FirstOrDefault(),
        IEnumerable<string> enumerable => enumerable.FirstOrDefault(),
        _ => value.ToString()
    };

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
