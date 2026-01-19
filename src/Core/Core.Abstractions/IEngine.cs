using Core.Abstractions.Runtime;

namespace Core.Abstractions;

public interface IEngine
{
    /// <summary>
    /// Processes a command invocation synchronously on the caller's thread.
    /// Use this when the output delegate must run on the caller thread (e.g., PowerShell runspace).
    /// </summary>
    /// <param name="invocation">The command invocation to process.</param>
    /// <param name="outputDelegate">The output delegate for this specific invocation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    void Process(ICommandInvocation invocation, CommandOutputDelegate outputDelegate, CancellationToken cancellationToken = default);
}

public interface ICommandEngine
{
    /// <summary>
    /// Gets the last extracted PowerShell command (if applicable)
    /// </summary>
    string? ExtractedCommand { get; }

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}