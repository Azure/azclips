namespace Core.Abstractions;

/// <summary>
/// Represents a command that can be executed.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="options">Parsed command options from --key value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default);
}
