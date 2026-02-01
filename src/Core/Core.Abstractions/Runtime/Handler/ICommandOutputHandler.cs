namespace Core.Abstractions.Runtime.Handler
{
    /// <summary>
    /// Marker interface for command output handlers.
    /// </summary>
    public interface ICommandOutputHandler
    {
        CommandOutputDelegate WriteOutput { get; set; }
    }
}