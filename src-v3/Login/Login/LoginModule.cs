using Core.Abstractions;
using Login.Commands;

namespace Login;

public class LoginModule : IModule
{
    public IEnumerable<ICommand> GetCommands() => [
        new LoginCommand()
    ];
}
