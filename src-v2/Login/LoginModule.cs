using Core.Abstractions;

namespace Login;

/// <summary>
/// Module that provides login-related commands.
/// </summary>
public class LoginModule : IModule
{
    public IEnumerable<ICommand> GetCommands()
    {
        return new ICommand[]
        {
            new LoginCommand()
        };
    }
}
