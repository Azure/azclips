namespace Core.Abstractions;

public interface ICommand
{
    Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
