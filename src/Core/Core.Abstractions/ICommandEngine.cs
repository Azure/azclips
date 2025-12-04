namespace Core.Abstractions;

public interface ICommandEngine
{
    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}
