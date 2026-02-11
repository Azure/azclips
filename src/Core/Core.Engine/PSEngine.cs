using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime.Handler;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace Core.Engine
{
    /// <summary>
    /// PowerShell command engine — creates a scoped DI container per invocation
    /// and uses channel-based output routing.
    /// <para><b>Thread Safety:</b> This engine is designed for concurrent command execution.
    /// Each <see cref="Execute"/> call creates an isolated scope with per-invocation output channel.</para>
    /// <para><b>Output Routing:</b> Each invocation gets its own Channel for worker-to-caller 
    /// communication. The worker thread writes to the channel, and the caller's thread reads 
    /// and dispatches to the provided output delegate (e.g., $PSCmdlet.WriteObject).</para>
    /// </summary>
    public class PSEngine : IEngine
    {
        private readonly IServiceProvider _serviceProvider;
        private bool _disposed;

        public PSEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Executes a command with isolated output routing via scoped DI.
        /// </summary>
        public void Execute(Type commandType, IDictionary<string, object> parameters, CommandOutputDelegate output, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Create a per-invocation channel for worker-to-caller communication
            Channel<object> outputChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            // Create scoped DI container for this invocation
            using var scope = _serviceProvider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            // Create per-invocation output handler routing to the channel
            ICommandOutputHandler outputHandler = new CommandOutputHandler
            {
                WriteOutput = async (value, ct) => await outputChannel.Writer.WriteAsync(value, ct)
            };

            // Construct the processor with constructor injection;
            // outputHandler is passed as an extra parameter (not from DI)
            IProcessor command = (IProcessor)ActivatorUtilities.CreateInstance(scopedProvider, commandType, outputHandler);

            // Bind parameters
            BindParameters(command, parameters, scopedProvider.GetService<ILoggingService>());

            // Start worker thread for command execution
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
        /// Binds typed parameters to the processor's public properties by name.
        /// Uses case-insensitive matching.
        /// </summary>
        private static void BindParameters(IProcessor command, IDictionary<string, object> parameters, ILoggingService? logger)
        {
            var commandType = command.GetType();
            foreach (var (name, value) in parameters)
            {
                var prop = commandType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(command, value);
                    logger?.Verbose($"Bound parameter '{name}' on '{commandType.Name}'.");
                }
                else
                {
                    logger?.Verbose($"Skipping parameter '{name}' — no matching writable property on '{commandType.Name}'.");
                }
            }
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