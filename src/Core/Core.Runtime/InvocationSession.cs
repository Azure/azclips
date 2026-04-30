using System.Reflection;
using System.Threading.Channels;
using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Core.Runtime;

internal partial class InvocationSession : IInvocationSession
{
    private bool _disposed = false;
    private ClientType clientType;
    private OutputStreamDelegate sessionOutputWriter = null!;
    private IDisposable loggingScope;
    private CancellationToken cancellationToken;
    private readonly OutputStreamAdapter outputStreamAdapter;

    internal Channel<object> OutputChannel = null!;
    internal Func<string> GetExtensionVersion { get; set; } = null!;
    internal Func<string, string, Assembly> LoadAssembly { get; set; } = null!;
    internal Func<IProcessor, IDictionary<string, object>, IDictionary<string, ParameterMetadata>, IDictionary<string, string>?, IProcessor> BindProcessor { get; set; } = null!;
    internal Func<InvocationSession, Task<ProcessorContext>> CreateProcessorContext { get; set; } = null!;
    internal Func<string, string, Func<IExtensionIndex>, IExtensionIndex> CacheGetOrAdd { get; set; } = null!;
    internal CoreOptions Option { get; set; } = null!;
    internal OutputStreamDelegate ProcessorOutputWriter = null!;

    public string Id { get; }
    public CommandInvocation Invocation { get; }
    public CancellationToken Token => cancellationToken;
    public Action Cancel { get; private set; } = null!;

    public InvocationSession(ClientType clientType, CommandInvocation invocation, OutputStreams outputStreams, CancellationToken cancellationToken = default)
    {
        this.clientType = clientType;
        Id = $"{clientType}-{Guid.NewGuid().ToString()}";
        Invocation = invocation;
        this.cancellationToken = cancellationToken;
        // wire output streams and logging service
        sessionOutputWriter = outputStreams.WriteOutput;
        outputStreamAdapter = new OutputStreamAdapter(outputStreams, cancellationToken);
        loggingScope = ScopedLoggingService.UseScope(outputStreamAdapter);
        subscribers = new Dictionary<string, Event>();
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

    public async Task ProcessAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        ProcessorOutputWriter = sessionOutputWriter;
        await (await GetProcessor()).ProcessAsync();
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
    public void Process()
    {
        IDisposable? previousScope = loggingScope;
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            OutputChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            OutputStreamAdapter channelAdapter = new OutputStreamAdapter(GetChannelStreams(), cancellationToken);
            loggingScope = ScopedLoggingService.UseScope(channelAdapter);
            ProcessorOutputWriter = async (value, _) => await OutputChannel.Writer.WriteAsync(new OutputObject(value), Token);

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
                    cancellationToken.ThrowIfCancellationRequested();
                    await (await GetProcessor()).ProcessAsync();
                }
                finally
                {
                    // Signal completion to the reader — must run regardless of success,
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
                while (reader.WaitToReadAsync(cancellationToken).AsTask().GetAwaiter().GetResult())
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
        finally
        {
            // Always restore the session logger back to the original output adapter.
            // Dispose() signals SessionDisposed, and that signal must not write into the
            // closed channel-backed adapter left behind by Process().
            loggingScope.Dispose();
            loggingScope = previousScope ?? ScopedLoggingService.UseScope(outputStreamAdapter);
        }
    }

    private void WriteChannelOutput(object? output, OutputStreamAdapter outputStreamAdapter)
    {
        switch (output)
        {
            case OutputObject obj:
                sessionOutputWriter(obj.Value, cancellationToken).GetAwaiter().GetResult();
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
        // Create context with channel-based output handler
        ProcessorContext processorContext = await CreateProcessorContext(this);

        CommandMetadata commandMetadata = GetExtensionIndex().GetCommandMetadata(Invocation.CommandName, clientType);
        IProcessor processor = commandMetadata.GetInstance().Init(processorContext);
        try
        {
            processor = BindProcessor(processor, Invocation.Parameters, commandMetadata.Parameters, commandMetadata.ParameterAliases);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse arguments for command '{Invocation.CommandName}': {ex.Message}", ex);
        }
        return processor;
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
                this.Signal(Events.SessionDisposed, CancellationToken.None, $"Session {Id} disposed").GetAwaiter().GetResult();

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