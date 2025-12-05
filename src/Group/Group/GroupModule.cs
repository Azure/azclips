using Core.Abstractions;

namespace Group;

public class GroupModule : IModule
{
    public IEnumerable<ICommand> GetCommands() =>
    [
        new GroupGetCommand() // todo: don't want to instantiate all commands here
    ];
}
