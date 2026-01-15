using Core.Abstractions.Runtime;

namespace Core.Abstractions;

public interface IEngine
{
    Action<String> Log { get; set; }
    Action<String> Debug { get; set; }

    void Process(ICommandInvocation invocation);

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}

public interface ICommandEngine
{
    /// <summary>
    /// Gets the last extracted PowerShell command (if applicable)
    /// </summary>
    string? ExtractedCommand { get; }

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}