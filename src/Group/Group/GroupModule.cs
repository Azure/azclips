using Core.Abstractions;
using Group.Commands;

namespace Group;

public class GroupModule : IModule
{
    public IEnumerable<ICommand> GetCommands() =>
    [
        new GroupCreateCommand(),
        new GroupListCommand(),
        new GroupShowCommand(),
        new GroupDeleteCommand()
    ];
}
