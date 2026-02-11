using Core.Abstractions.Runtime;

namespace Core.Abstractions;

/// <summary>
/// Minimal execution engine. Accepts a fully-configured IProcessor (properties set)
/// and runs it. The engine handles context creation and execution only.
/// No parameter binding, no extension loading — that's the caller's job.
/// </summary>
public interface IEngine : IDisposable
{
    /// <summary>
    /// Executes a command processor. The engine creates the command context,
    /// initializes the processor via <see cref="IProcessor.Init"/>, and runs it.
    /// </summary>
    /// <param name="command">A fully-configured IProcessor with all properties set.</param>
    /// <param name="output">The output delegate for this invocation (Console.WriteLine or $PSCmdlet.WriteObject).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    void Execute(IProcessor command, CommandOutputDelegate output, CancellationToken cancellationToken = default);
}

public interface ICommandEngine
{
    /// <summary>
    /// Gets the last extracted PowerShell command (if applicable)
    /// </summary>
    string? ExtractedCommand { get; }

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}