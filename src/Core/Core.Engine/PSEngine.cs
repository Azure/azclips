using Core.Abstractions;
using Core.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;
using Core.Runtime;
using System.Threading.Channels;
using Core.Abstractions.Runtime.Handler;

namespace Core.Engine
{
    /// <summary>
    /// PowerShell command engine — handles context creation and channel-based execution.
    /// Extension loading and command resolution are handled by <see cref="CommandHost"/>.
    /// <para><b>Thread Safety:</b> This engine is designed for concurrent command execution.
    /// Each <see cref="Execute"/> call creates isolated context with per-invocation output channel.</para>
    /// <para><b>Output Routing:</b> Each invocation gets its own Channel for worker-to-caller 
    /// communication. The worker thread writes to the channel, and the caller's thread reads 
    /// and dispatches to the provided output delegate (e.g., $PSCmdlet.WriteObject).</para>
    /// </summary>
    public class PSEngine : IEngine
    {
        private readonly HttpClient _httpClient;
        private readonly Func<AuthContext> GetAuthContext;
        private readonly AcquireTokenAsyncDelegate AcquireTokenSilentlyAsync;
        private readonly StartActivityDelegate StartActivity;
        private bool _disposed;

        public PSEngine(IServiceProvider serviceProvider)
        {
            _httpClient = new HttpClient();
            GetAuthContext = () => serviceProvider.GetRequiredService<IAuthContextService>().GetCurrentContextAsync().GetAwaiter().GetResult() ?? new AuthContext() { SubscriptionId = "", TenantId = "" };
            AcquireTokenSilentlyAsync = serviceProvider.GetRequiredService<IAuthService>().AcquireTokenSilentlyAsync;
            StartActivity = serviceProvider.GetRequiredService<ITelemetryService>().StartActivity;
        }

        /// <summary>
        /// Executes a fully-configured IProcessor with isolated output routing.
        /// <para><b>Thread Safety:</b> Each call creates its own output channel. The worker thread
        /// writes output to the channel, while the caller's thread pumps the channel and invokes
        /// the provided output delegate. This ensures output is routed to the correct $PSCmdlet.</para>
        /// </summary>
        /// <param name="command">A fully-configured IProcessor with all properties set.</param>
        /// <param name="output">The output delegate for this invocation (typically wired to $PSCmdlet.WriteObject).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the engine has been disposed.</exception>
        public void Execute(IProcessor command, CommandOutputDelegate output, CancellationToken cancellationToken = default)
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

            // Initialize and start worker thread for command execution
            command.Init(commandContext);
            Task executeCommandTask = ExecuteCommandAsync(command, outputChannel.Writer, cancellationToken);

            // Pump the channel on the caller's thread (runspace-affine)
            ChannelReader<object> reader = outputChannel.Reader;
            while (reader.WaitToReadAsync(cancellationToken).AsTask().GetAwaiter().GetResult())
            {
                while (reader.TryRead(out var outputItem))
                {
                    output(outputItem, cancellationToken).GetAwaiter().GetResult();
                }
            }

            // Ensure worker completes (propagate any exceptions)
            executeCommandTask.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a new command context for this invocation with channel-based output.
        /// Each command execution gets its own isolated context.
        /// </summary>
        private ICommandContext CreateCommandContext(ChannelWriter<object> outputWriter)
        {
            AuthContext authContext = GetAuthContext();
            CommandAuthHandler commandAuthHandler = new CommandAuthHandler
            {
                AuthContext = new AuthContext
                {
                    SubscriptionId = authContext.SubscriptionId,
                    TenantId = authContext.TenantId
                },
                AcquireTokenAsyncDefault = AcquireTokenSilentlyAsync,
                ApplyOverride = false
            };

            CommandOutputHandler commandOutputHandler = new CommandOutputHandler
            {
                WriteOutput = async (value, ct) => await outputWriter.WriteAsync(value, ct)
            };
            CommandTelemetryHandler commandTelemetryHandler = new CommandTelemetryHandler
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
                    outputWriter.Complete();
                }
            }, cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
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
}