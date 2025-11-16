namespace Core.Abstractions;

public interface IModule
{
    IEnumerable<ICommand> GetCommands();
}
