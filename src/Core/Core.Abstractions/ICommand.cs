namespace Core.Abstractions;

public interface ICommand
{
    Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="options">Parsed command options from --key value pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default);
}
