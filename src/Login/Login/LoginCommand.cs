using Core.Abstractions;

namespace Login;

public class LoginCommand : ICommand
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Login: Not implemented yet.");
        Console.ForegroundColor = previousColor;
        return Task.CompletedTask;
    }
}
