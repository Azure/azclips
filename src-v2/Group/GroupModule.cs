using Core.Abstractions;

namespace Group;

/// <summary>
/// Module that provides group-related commands.
/// </summary>
public class GroupModule : IModule
{
    public IEnumerable<ICommand> GetCommands()
    {
        return new ICommand[]
        {
            new GroupCreateCommand(),
            new GroupDeleteCommand(),
            new GroupShowCommand(),
            new GroupListCommand()
        };
    }
}
