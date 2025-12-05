namespace Core.Abstractions;

/// <summary>
/// Represents a command engine that can execute commands.
/// </summary>
public interface ICommandEngine
{
    /// <summary>
    /// Gets the extracted command from the last execution (used by agent).
    /// </summary>
    string? ExtractedCommand { get; }

    /// <summary>
    /// Executes a command asynchronously.
    /// </summary>
    /// <param name="args">Command arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}
