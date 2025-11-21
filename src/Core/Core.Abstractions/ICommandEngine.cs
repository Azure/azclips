namespace Core.Abstractions;

public interface ICommandEngine
{
    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the extracted command string if the engine produced an executable command
    /// that should be passed to another engine for execution. 
    /// Returns null if no command was extracted or user declined execution.
    /// </summary>
    string? ExtractedCommand => null;
}
