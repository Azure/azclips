namespace Core.Runtime.Handler
{
    using Core.Abstractions.Runtime;
    using Core.Abstractions.Runtime.Handler;

    /// <summary>
    /// Default implementation of ICommandOutputHandler that writes output to the console.
    /// </summary>
    public class CommandOutputHandler : ICommandOutputHandler
    {
        public OutputStreamDelegate WriteOutput { get; set; } = null!;
    }
}