using Core.Abstractions;

namespace Login;

public class LoginCommand : ICommand
{
    public Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Login command is not yet implemented. This will authenticate with Azure.");
    }
}
