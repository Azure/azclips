namespace Core.Abstractions;

public interface ICommand
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
