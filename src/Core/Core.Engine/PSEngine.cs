using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Implementation;
using Core.Runtime;
using Core.Runtime.Handler;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Core.Engine.utils;

namespace Core.Engine
{
    /// <summary>
    /// PowerShell command engine for executing commands in PowerShell runspaces.
    /// Uses scoped DI to create isolated <see cref="ICommandContext"/> per invocation.
    /// <para><b>Thread Safety:</b> This engine is designed for concurrent command execution.
    /// Each <see cref="Process"/> call creates isolated context with per-invocation output channel.
    /// Extension indices are cached using thread-safe lazy initialization.</para>
    /// <para><b>Output Routing:</b> Each invocation gets its own Channel for worker-to-caller
    /// communication. The worker thread writes to the channel, and the caller's thread reads
    /// and dispatches to the provided output delegate (e.g., $PSCmdlet.WriteObject).</para>
    /// </summary>
    public class PSEngine : IEngine
    {
        private readonly IServiceProvider _serviceProvider;

        // Extension indices are cached with Lazy<T> to ensure single initialization per extension
        // even under concurrent access. The factory runs exactly once per key.
        private readonly LFUCache<Lazy<IExtensionIndex>> _extensionIndices = new(512);
        private bool _disposed;

        public ClientType ClientType = ClientType.PowerShell;

        public PSEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Processes a command invocation with isolated output routing via scoped DI.
        /// <para><b>Thread Safety:</b> Each call creates its own DI scope and output channel.
        /// The worker thread writes output to the channel, while the caller's thread pumps
        /// the channel and invokes the provided output delegate. This ensures output is
        /// routed to the correct $PSCmdlet.</para>
        /// </summary>
        public void Process(ICommandInvocation invocation, CommandOutputDelegate outputDelegate, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Create a per-invocation channel for worker-to-caller communication
            Channel<object> outputChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            // Create a scoped DI container for this invocation.
            // Scoped services get fresh instances per scope.
            using var scope = _serviceProvider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            // Build ICommandContext from scoped services + channel output
            ICommandContext commandContext = BuildCommandContext(scopedProvider, outputChannel.Writer);

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
        /// Builds an <see cref="ICommandContext"/> from scoped DI services and a channel writer.
        /// </summary>
        private static ICommandContext BuildCommandContext(IServiceProvider scopedProvider, ChannelWriter<object> outputWriter)
        {
            return new CommandContext
            {
                CommandAuthHandler = scopedProvider.GetRequiredService<ICommandAuthHandler>(),
                CommandTelemetryHandler = scopedProvider.GetRequiredService<ICommandTelemetryHandler>(),
                HttpPipeline = scopedProvider.GetRequiredService<IRuntimeHttpPipeline>(),
                CommandOutputHandler = new CommandOutputHandler
                {
                    WriteOutput = async (value, ct) => await outputWriter.WriteAsync(value, ct)
                }
            };
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

        /// <summary>
        /// Gets a command processor for the given invocation.
        /// Extension indices are lazily cached with guaranteed single initialization.
        /// </summary>
        public IProcessor GetCommand(ICommandInvocation invocation)
        {
            var loggingService = _serviceProvider.GetService<ILoggingService>();

            IExtensionIndex extensionIndex = _extensionIndices.GetOrAdd(
                invocation.ExtensionName,
                extensionName => new Lazy<IExtensionIndex>(() =>
                {
                    string assemblyName = extensionName.ResolveExtensionName().ResolveAssemblyName();
                    loggingService?.Verbose($"Resolved extension '{extensionName}' to assembly '{assemblyName}'. Loading extension index...");
                    Type? indexType = Type.GetType($"{assemblyName}.ExtensionIndex, {assemblyName}")
                        ?? throw new InvalidOperationException($"No IExtensionIndex implementation found in assembly {assemblyName}");
                    return (Activator.CreateInstance(indexType) as IExtensionIndex)!;
                })
            ).Value;

            ICommandMetadata commandMetadata = extensionIndex.GetCommandMetaData(invocation.CommandName, ClientType);
            loggingService?.Verbose($"Resolved command '{invocation.CommandName}' to type '{commandMetadata.CommandType.FullName}'. Creating instance...");
            IProcessor instance = commandMetadata.GetInstance();
            invocation.Parameters.ToList().ForEach(p => instance.SetProperty(p.Key, p.Value));
            return instance;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _extensionIndices.Clear();
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