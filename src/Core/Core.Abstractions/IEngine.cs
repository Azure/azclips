using Core.Abstractions.Runtime;

namespace Core.Abstractions;

/// <summary>
/// Minimal execution engine. Creates a DI scope per invocation, constructs the
/// IProcessor via <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities"/>,
/// binds parameters, and runs it.
/// </summary>
public interface IEngine : IDisposable
{
    /// <summary>
    /// Executes a command. The engine creates a scoped DI container, constructs the
    /// processor with constructor injection, binds properties from <paramref name="parameters"/>,
    /// and calls <see cref="IProcessor.ProcessAsync"/>.
    /// </summary>
    /// <param name="commandType">The processor type to instantiate via ActivatorUtilities.</param>
    /// <param name="parameters">Typed parameters to set on the processor's public properties after construction.</param>
    /// <param name="output">The output delegate for this invocation (Console.WriteLine or $PSCmdlet.WriteObject).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    void Execute(Type commandType, IDictionary<string, object> parameters, CommandOutputDelegate output, CancellationToken cancellationToken = default);
}

public interface ICommandEngine
{
    /// <summary>
    /// Gets the last extracted PowerShell command (if applicable)
    /// </summary>
    string? ExtractedCommand { get; }

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}