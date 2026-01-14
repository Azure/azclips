namespace Core.Abstractions.Runtime
{
    /// <summary>
    /// The interface representing the context of a command execution.
    /// </summary>
    public interface ICommandContext
    {
        /// <summary>
        /// Gets the cancellation token for the command execution.
        /// </summary>
        CancellationToken CancellationToken { get; }
    }
}