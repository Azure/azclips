using Core.Abstractions;

namespace Login;

public class LoginModule : IModule
{
    public IEnumerable<ICommand> GetCommands() => [
        new LoginCommand()
    ];
}
