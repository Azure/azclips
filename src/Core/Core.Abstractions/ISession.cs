namespace Core.Abstractions;
using Core.Abstractions.Runtime;
public interface ISession : IDisposable
{
    string Id { get; }

    public IEventBus EventBus { get; }

    void Run();

    Task RunAsync();
}