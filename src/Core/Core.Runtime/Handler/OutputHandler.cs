namespace Core.Runtime.Handler
{
    using Core.Abstractions.Runtime;
    using Core.Abstractions.Runtime.Handler;

    /// <summary>
    /// Default implementation of IOutputHandler that writes output to the console.
    /// </summary>
    public class OutputHandler : IOutputHandler
    {
        public OutputStreamDelegate WriteOutput { get; set; } = null!;
    }
}