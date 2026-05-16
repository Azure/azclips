using Core.Abstractions.Runtime;

namespace Core.Abstractions;

public interface IEngine : IDisposable
{
    ISession OpenExecutionSession(CommandInvocation invocation, OutputStreams outputStreams, CancellationToken cancellationToken = default);
}

public interface ICommandEngine
{
    /// <summary>
    /// Gets the last extracted PowerShell command (if applicable)
    /// </summary>
    string? ExtractedCommand { get; }

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}